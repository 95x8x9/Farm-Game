const express = require('express');
const router = express.Router();

// POST /api/auth/logout
// JWT는 서버가 상태를 들고 있지 않으므로(stateless)
// 로그아웃은 클라이언트가 들고 있는 쿠키를 지우는 방식으로 처리합니다.
router.post('/', (req, res) => {
  res.clearCookie('farm_token', {
    httpOnly: true,
    sameSite: 'lax',
    secure: process.env.NODE_ENV === 'production',
  });
  return res.status(200).json({ message: '로그아웃 되었습니다.' });
});

module.exports = router;
