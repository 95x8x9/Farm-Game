const pool = require('../config/db');
const {
  PLOT_PRICE,
  PLOT_COUNT,
  WATER_SUCCESS_REDUCTION_SECONDS,
  WATER_FAIL_REDUCTION_SECONDS,
  BATCH_UNLOCK_HARVEST_COUNT,
  DEFAULT_CONCURRENT_LIMIT,
  UNLOCKED_CONCURRENT_LIMIT,
  getCropConfig,
} = require('../config/cropConfig');

// login.js / register.js 등과 동일하게 { error: 'code', message: '...' } 포맷을 쓰기 위한 커스텀 에러
class ApiError extends Error {
  constructor(status, errorCode, message) {
    super(message);
    this.status = status;
    this.errorCode = errorCode;
  }
}

/**
 * 시간이 지나 수확 가능해진(growing -> ready) 밭들을 최신 상태로 동기화한다.
 * GET /api/farm 을 호출할 때마다, 그리고 water/harvest 직전에 한 번씩 불러서
 * "밭 상태"가 항상 실제 시간 기준으로 정확하도록 맞춘다.
 */
async function syncPlotStates(conn, userId) {
  await conn.execute(
    `UPDATE plots
     SET state = 'ready'
     WHERE user_id = ?
       AND state = 'growing'
       AND ready_at IS NOT NULL
       AND ready_at <= NOW()`,
    [userId]
  );
}

async function getOrCreatePlayerState(conn, userId, lockForUpdate = false) {
  const lockClause = lockForUpdate ? ' FOR UPDATE' : '';
  const [rows] = await conn.execute(
    `SELECT * FROM player_state WHERE user_id = ?${lockClause}`,
    [userId]
  );
  if (rows.length > 0) return rows[0];

  // register.js에서 이미 만들어주지만, 방어적으로 없으면 여기서도 생성한다.
  await conn.execute(
    'INSERT INTO player_state (user_id, money, level, wheat_harvest_count, batch_unlocked) VALUES (?, 500, 1, 0, 0)',
    [userId]
  );
  const [created] = await conn.execute(
    `SELECT * FROM player_state WHERE user_id = ?${lockClause}`,
    [userId]
  );
  return created[0];
}

function validatePlotIndex(plotIndex) {
  if (!Number.isInteger(plotIndex) || plotIndex < 0 || plotIndex >= PLOT_COUNT) {
    throw new ApiError(400, 'invalid_input', `plotIndex는 0~${PLOT_COUNT - 1} 사이의 정수여야 합니다.`);
  }
}

/** 1) 내 농장 데이터 불러오기 (GET /api/farm) */
async function getFarmData(userId) {
  const conn = await pool.getConnection();
  try {
    await conn.beginTransaction();

    const playerState = await getOrCreatePlayerState(conn, userId);
    await syncPlotStates(conn, userId);

    const [plots] = await conn.execute(
      'SELECT plot_index, unlocked, crop_type, planted_at, water_count, ready_at, state FROM plots WHERE user_id = ? ORDER BY plot_index ASC',
      [userId]
    );
    const [inventory] = await conn.execute(
      'SELECT item_type, quantity FROM inventory WHERE user_id = ?',
      [userId]
    );

    await conn.commit();

    return {
      money: playerState.money,
      level: playerState.level,
      wheat_harvest_count: playerState.wheat_harvest_count,
      batch_unlocked: !!playerState.batch_unlocked,
      plots,
      inventory,
    };
  } catch (err) {
    await conn.rollback();
    throw err;
  } finally {
    conn.release();
  }
}

/** 2) 밭 구매 (POST /api/plots/buy) body: { plotIndex } */
async function buyPlot(userId, plotIndex) {
  validatePlotIndex(plotIndex);

  const conn = await pool.getConnection();
  try {
    await conn.beginTransaction();

    const playerState = await getOrCreatePlayerState(conn, userId, true);

    const [existing] = await conn.execute(
      'SELECT * FROM plots WHERE user_id = ? AND plot_index = ? FOR UPDATE',
      [userId, plotIndex]
    );
    if (existing.length > 0 && existing[0].unlocked) {
      throw new ApiError(409, 'plot_already_owned', '이미 구매한 밭입니다.');
    }

    if (playerState.money < PLOT_PRICE) {
      throw new ApiError(400, 'not_enough_money', '돈이 부족합니다.');
    }

    await conn.execute(
      'UPDATE player_state SET money = money - ? WHERE user_id = ?',
      [PLOT_PRICE, userId]
    );

    if (existing.length > 0) {
      await conn.execute(
        "UPDATE plots SET unlocked = TRUE, state = 'empty' WHERE user_id = ? AND plot_index = ?",
        [userId, plotIndex]
      );
    } else {
      await conn.execute(
        "INSERT INTO plots (user_id, plot_index, unlocked, state) VALUES (?, ?, TRUE, 'empty')",
        [userId, plotIndex]
      );
    }

    await conn.commit();
    return { plotIndex, spent: PLOT_PRICE, money: playerState.money - PLOT_PRICE };
  } catch (err) {
    await conn.rollback();
    throw err;
  } finally {
    conn.release();
  }
}

/** 3) 씨앗 구매 (POST /api/seeds/buy) body: { seedType, quantity } */
async function buySeed(userId, seedType, quantity) {
  const crop = getCropConfig(seedType);
  if (!crop) throw new ApiError(400, 'invalid_seed_type', '존재하지 않는 씨앗입니다.');
  const qty = Number.isInteger(quantity) && quantity > 0 ? quantity : 1;

  const conn = await pool.getConnection();
  try {
    await conn.beginTransaction();

    const playerState = await getOrCreatePlayerState(conn, userId, true);
    const totalPrice = crop.seedPrice * qty;

    if (playerState.money < totalPrice) {
      throw new ApiError(400, 'not_enough_money', '돈이 부족합니다.');
    }

    await conn.execute(
      'UPDATE player_state SET money = money - ? WHERE user_id = ?',
      [totalPrice, userId]
    );

    await conn.execute(
      `INSERT INTO inventory (user_id, item_type, quantity)
       VALUES (?, ?, ?)
       ON DUPLICATE KEY UPDATE quantity = quantity + ?`,
      [userId, seedType, qty, qty]
    );

    await conn.commit();
    return { seedType, quantity: qty, spent: totalPrice, money: playerState.money - totalPrice };
  } catch (err) {
    await conn.rollback();
    throw err;
  } finally {
    conn.release();
  }
}

/** 4) 작물 심기 (POST /api/crops/plant) body: { plotIndex, seedType } */
async function plantCrop(userId, plotIndex, seedType) {
  validatePlotIndex(plotIndex);
  const crop = getCropConfig(seedType);
  if (!crop) throw new ApiError(400, 'invalid_seed_type', '존재하지 않는 씨앗입니다.');

  const conn = await pool.getConnection();
  try {
    await conn.beginTransaction();

    const playerState = await getOrCreatePlayerState(conn, userId, true);

    const [plotRows] = await conn.execute(
      'SELECT * FROM plots WHERE user_id = ? AND plot_index = ? FOR UPDATE',
      [userId, plotIndex]
    );
    const plot = plotRows[0];
    if (!plot || !plot.unlocked) {
      throw new ApiError(404, 'plot_not_found', '구매하지 않은 밭입니다.');
    }
    if (plot.state !== 'empty') {
      throw new ApiError(409, 'plot_not_empty', '이미 작물이 심어진 밭입니다.');
    }

    // 동시 작업 가능 칸 수 제한 (batch_unlocked 전에는 1칸만 동시 진행)
    const limit = playerState.batch_unlocked
      ? UNLOCKED_CONCURRENT_LIMIT
      : DEFAULT_CONCURRENT_LIMIT;
    const [[{ growingCount }]] = await conn.query(
      "SELECT COUNT(*) AS growingCount FROM plots WHERE user_id = ? AND state IN ('growing','ready')",
      [userId]
    );
    if (growingCount >= limit) {
      throw new ApiError(
        409,
        'concurrent_limit_reached',
        `동시에 작업 가능한 밭 수(${limit}칸)를 초과했습니다.`
      );
    }

    const [invRows] = await conn.execute(
      'SELECT quantity FROM inventory WHERE user_id = ? AND item_type = ? FOR UPDATE',
      [userId, seedType]
    );
    if (!invRows.length || invRows[0].quantity < 1) {
      throw new ApiError(400, 'seed_not_owned', '보유한 씨앗이 없습니다.');
    }

    await conn.execute(
      'UPDATE inventory SET quantity = quantity - 1 WHERE user_id = ? AND item_type = ?',
      [userId, seedType]
    );

    await conn.execute(
      `UPDATE plots
       SET crop_type = ?,
           planted_at = NOW(),
           water_count = 0,
           ready_at = DATE_ADD(NOW(), INTERVAL ? SECOND),
           state = 'growing'
       WHERE user_id = ? AND plot_index = ?`,
      [crop.cropType, crop.growSeconds, userId, plotIndex]
    );

    await conn.commit();
    return { plotIndex, cropType: crop.cropType, growSeconds: crop.growSeconds };
  } catch (err) {
    await conn.rollback();
    throw err;
  } finally {
    conn.release();
  }
}

/** 5) 물주기 (POST /api/crops/water) body: { plotIndex, succeeded } */
async function waterCrop(userId, plotIndex, succeeded) {
  validatePlotIndex(plotIndex);
  if (typeof succeeded !== 'boolean') {
    throw new ApiError(400, 'invalid_input', 'succeeded는 boolean 값이어야 합니다.');
  }

  const conn = await pool.getConnection();
  try {
    await conn.beginTransaction();

    await syncPlotStates(conn, userId);

    const [plotRows] = await conn.execute(
      'SELECT * FROM plots WHERE user_id = ? AND plot_index = ? FOR UPDATE',
      [userId, plotIndex]
    );
    const plot = plotRows[0];
    if (!plot || !plot.unlocked) {
      throw new ApiError(404, 'plot_not_found', '구매하지 않은 밭입니다.');
    }
    if (plot.state !== 'growing') {
      throw new ApiError(409, 'plot_not_growing', '지금은 물을 줄 수 없는 상태입니다.');
    }

    const crop = getCropConfig(`${plot.crop_type}_seed`);
    if (!crop) throw new ApiError(500, 'unknown_crop', '알 수 없는 작물입니다.');

    if (plot.water_count >= crop.maxWaterCount) {
      throw new ApiError(409, 'water_attempts_exhausted', '사용 가능한 물주기 횟수를 모두 사용했습니다.');
    }

    const newWaterCount = plot.water_count + 1;
    const reductionSeconds = succeeded
      ? WATER_SUCCESS_REDUCTION_SECONDS
      : WATER_FAIL_REDUCTION_SECONDS;

    // 성공은 60초, 실패는 30초를 줄이고 완료 시각이 현재보다 과거로 내려가지 않게 한다.
    await conn.execute(
      `UPDATE plots
       SET water_count = ?,
           ready_at = GREATEST(NOW(), DATE_SUB(ready_at, INTERVAL ? SECOND))
       WHERE user_id = ? AND plot_index = ?`,
      [newWaterCount, reductionSeconds, userId, plotIndex]
    );

    await syncPlotStates(conn, userId);

    const [updated] = await conn.execute(
      'SELECT water_count, ready_at, state FROM plots WHERE user_id = ? AND plot_index = ?',
      [userId, plotIndex]
    );

    await conn.commit();
    return {
      plotIndex,
      succeeded,
      reducedSeconds: reductionSeconds,
      waterCount: updated[0].water_count,
      maxWaterCount: crop.maxWaterCount,
      readyAt: updated[0].ready_at,
      state: updated[0].state,
    };
  } catch (err) {
    await conn.rollback();
    throw err;
  } finally {
    conn.release();
  }
}

/** 6) 수확 (POST /api/crops/harvest) body: { plotIndex } */
async function harvestCrop(userId, plotIndex) {
  validatePlotIndex(plotIndex);
  const conn = await pool.getConnection();
  try {
    await conn.beginTransaction();

    const playerState = await getOrCreatePlayerState(conn, userId, true);
    await syncPlotStates(conn, userId);

    const [plotRows] = await conn.execute(
      'SELECT * FROM plots WHERE user_id = ? AND plot_index = ? FOR UPDATE',
      [userId, plotIndex]
    );
    const plot = plotRows[0];
    if (!plot || !plot.unlocked) {
      throw new ApiError(404, 'plot_not_found', '구매하지 않은 밭입니다.');
    }

    const crop = getCropConfig(`${plot.crop_type}_seed`);
    if (!crop) throw new ApiError(500, 'unknown_crop', '알 수 없는 작물입니다.');

    if (plot.state !== 'ready') {
      throw new ApiError(409, 'not_ready', '아직 수확할 수 없습니다.');
    }

    await conn.execute(
      'UPDATE player_state SET money = money + ? WHERE user_id = ?',
      [crop.sellPrice, userId]
    );

    let wheatHarvestCount = playerState.wheat_harvest_count;
    let batchUnlocked = !!playerState.batch_unlocked;

    if (crop.cropType === 'wheat') {
      wheatHarvestCount += 1;
      if (!batchUnlocked && wheatHarvestCount >= BATCH_UNLOCK_HARVEST_COUNT) {
        batchUnlocked = true;
      }
      await conn.execute(
        'UPDATE player_state SET wheat_harvest_count = ?, batch_unlocked = ? WHERE user_id = ?',
        [wheatHarvestCount, batchUnlocked ? 1 : 0, userId]
      );
    }

    await conn.execute(
      `UPDATE plots
       SET crop_type = NULL, planted_at = NULL, water_count = 0, ready_at = NULL, state = 'empty'
       WHERE user_id = ? AND plot_index = ?`,
      [userId, plotIndex]
    );

    await conn.commit();
    return {
      plotIndex,
      cropType: crop.cropType,
      earned: crop.sellPrice,
      money: playerState.money + crop.sellPrice,
      wheat_harvest_count: wheatHarvestCount,
      batch_unlocked: batchUnlocked,
    };
  } catch (err) {
    await conn.rollback();
    throw err;
  } finally {
    conn.release();
  }
}

module.exports = {
  ApiError,
  getFarmData,
  buyPlot,
  buySeed,
  plantCrop,
  waterCrop,
  harvestCrop,
};
