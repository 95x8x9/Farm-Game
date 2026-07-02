const express = require('express');
const bcrypt = require('bcrypt');
const router = express.Router();
const pool = require('../../config/db');
const { signToken } = require('../../utils/jwt');

const COOKIE_OPTIONS = {
  httpOnly: true,
  sameSite: 'lax',
  // ALB HTTPS 리스너 뒤에서 서비스하므로 운영 환경에서는 secure: true 권장
  secure: process.env.NODE_ENV === 'production',
  maxAge: 7 * 24 * 60 * 60 * 1000, // 7일
};

// POST /api/auth/login
router.post('/', async (req, res) => {
  const { username, password } = req.body;

  if (!username || !password) {
    return res.status(400).json({ error: 'invalid_input', message: 'username, password를 모두 입력해주세요.' });
  }

  try {
    const [rows] = await pool.query(
      'SELECT id, username, password_hash FROM users WHERE username = ?',
      [username]
    );
    if (rows.length === 0) {
      return res.status(401).json({ error: 'invalid_credentials', message: '아이디 또는 비밀번호가 올바르지 않습니다.' });
    }

    const user = rows[0];
    const passwordMatches = await bcrypt.compare(password, user.password_hash);
    if (!passwordMatches) {
      return res.status(401).json({ error: 'invalid_credentials', message: '아이디 또는 비밀번호가 올바르지 않습니다.' });
    }

    const token = signToken({ userId: user.id, username: user.username });

    // 웹 게임(Unity WebGL)에서 바로 쓸 수 있도록 쿠키와 응답 바디 둘 다 내려줌
    res.cookie('farm_token', token, COOKIE_OPTIONS);

    return res.status(200).json({
      message: '로그인 성공',
      token,
      user: { id: user.id, username: user.username },
    });
  } catch (err) {
    console.error('login error:', err);
    return res.status(500).json({ error: 'internal_server_error', message: '로그인 중 오류가 발생했습니다.' });
  }
});

module.exports = router;
