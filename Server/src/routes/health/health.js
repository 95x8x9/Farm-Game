const express = require('express');
const router = express.Router();
const pool = require('../../config/db');

// GET /health
// ALB의 Health Check 대상 경로.
// 어떤 web-server가 응답했는지 함께 내려줘서, 새로고침 시
// farm-web-1 / farm-web-2가 번갈아 나오는 걸 보여줄 수 있습니다.
router.get('/', async (req, res) => {
  const serverName = process.env.SERVER_NAME || 'unknown-server';

  try {
    await pool.query('SELECT 1');
    return res.status(200).json({
      status: 'ok',
      server: serverName,
      db: 'connected',
    });
  } catch (err) {
    console.error('health check DB error:', err);
    return res.status(503).json({
      status: 'error',
      server: serverName,
      db: 'disconnected',
    });
  }
});

module.exports = router;
