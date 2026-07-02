const express = require('express');
const router = express.Router();
const pool = require('../../config/db');
const { requireAuth } = require('../../middleware/auth');

// GET /api/me
router.get('/', requireAuth, async (req, res) => {
  try {
    const [rows] = await pool.query(
      `SELECT u.id, u.username, u.created_at,
              p.money, p.level, p.wheat_harvest_count, p.batch_unlocked
       FROM users u
       JOIN player_state p ON p.user_id = u.id
       WHERE u.id = ?`,
      [req.user.id]
    );

    if (rows.length === 0) {
      return res.status(404).json({ error: 'user_not_found', message: '사용자 정보를 찾을 수 없습니다.' });
    }

    const row = rows[0];
    return res.status(200).json({
      user: {
        id: row.id,
        username: row.username,
        created_at: row.created_at,
      },
      farm: {
        money: row.money,
        level: row.level,
        wheat_harvest_count: row.wheat_harvest_count,
        batch_unlocked: !!row.batch_unlocked,
      },
    });
  } catch (err) {
    console.error('me error:', err);
    return res.status(500).json({ error: 'internal_server_error', message: '정보 조회 중 오류가 발생했습니다.' });
  }
});

module.exports = router;
