const express = require('express');
const router = express.Router();
const { requireAuth } = require('../../middleware/auth');
const farmService = require('../../services/farmService');

function handleError(res, err) {
  if (err instanceof farmService.ApiError) {
    return res.status(err.status).json({ error: err.errorCode, message: err.message });
  }
  console.error(err);
  return res.status(500).json({ error: 'internal_server_error', message: '서버 오류가 발생했습니다.' });
}

// GET /api/farm - 내 농장 데이터 불러오기
router.get('/api/farm', requireAuth, async (req, res) => {
  try {
    const farm = await farmService.getFarmData(req.user.id);
    return res.status(200).json(farm);
  } catch (err) {
    return handleError(res, err);
  }
});

// POST /api/plots/buy - 밭 구매 { plotIndex }
router.post('/api/plots/buy', requireAuth, async (req, res) => {
  try {
    const { plotIndex } = req.body || {};
    const result = await farmService.buyPlot(req.user.id, plotIndex);
    return res.status(200).json({ message: '밭을 구매했습니다.', ...result });
  } catch (err) {
    return handleError(res, err);
  }
});

// POST /api/seeds/buy - 씨앗 구매 { seedType, quantity }
router.post('/api/seeds/buy', requireAuth, async (req, res) => {
  try {
    const { seedType, quantity } = req.body || {};
    const result = await farmService.buySeed(req.user.id, seedType, quantity);
    return res.status(200).json({ message: '씨앗을 구매했습니다.', ...result });
  } catch (err) {
    return handleError(res, err);
  }
});

// POST /api/crops/plant - 작물 심기 { plotIndex, seedType }
router.post('/api/crops/plant', requireAuth, async (req, res) => {
  try {
    const { plotIndex, seedType } = req.body || {};
    const result = await farmService.plantCrop(req.user.id, plotIndex, seedType);
    return res.status(200).json({ message: '작물을 심었습니다.', ...result });
  } catch (err) {
    return handleError(res, err);
  }
});

// POST /api/crops/water - 물주기 { plotIndex, succeeded }
router.post('/api/crops/water', requireAuth, async (req, res) => {
  try {
    const { plotIndex, succeeded } = req.body || {};
    const result = await farmService.waterCrop(req.user.id, plotIndex, succeeded);
    return res.status(200).json({ message: '물을 줬습니다.', ...result });
  } catch (err) {
    return handleError(res, err);
  }
});

// POST /api/crops/harvest - 수확 { plotIndex }
router.post('/api/crops/harvest', requireAuth, async (req, res) => {
  try {
    const { plotIndex } = req.body || {};
    const result = await farmService.harvestCrop(req.user.id, plotIndex);
    return res.status(200).json({ message: '수확했습니다.', ...result });
  } catch (err) {
    return handleError(res, err);
  }
});

module.exports = router;
