const express = require('express');
const bcrypt = require('bcrypt');
const router = express.Router();
const pool = require('../../config/db');

const SALT_ROUNDS = 10;
const STARTING_MONEY = 500; // 문서 3단계: 초기 자금 500원

// POST /api/auth/register
router.post('/', async (req, res) => {
  const { username, password } = req.body || {};

  if (!username || !password) {
    return res.status(400).json({ error: 'invalid_input', message: 'username, password를 모두 입력해주세요.' });
  }
  if (username.length < 3 || username.length > 50) {
    return res.status(400).json({ error: 'invalid_username', message: 'username은 3~50자여야 합니다.' });
  }
  if (password.length < 4) {
    return res.status(400).json({ error: 'invalid_password', message: '비밀번호는 4자 이상이어야 합니다.' });
  }

  const conn = await pool.getConnection();
  try {
    const [existing] = await conn.query('SELECT id FROM users WHERE username = ?', [username]);
    if (existing.length > 0) {
      return res.status(409).json({ error: 'username_taken', message: '이미 사용 중인 username입니다.' });
    }

    const passwordHash = await bcrypt.hash(password, SALT_ROUNDS);

    await conn.beginTransaction();
    const [result] = await conn.query(
      'INSERT INTO users (username, password_hash) VALUES (?, ?)',
      [username, passwordHash]
    );
    const userId = result.insertId;

    // 계정 생성과 동시에 초기 농장 상태 생성 (초기 자금 500원)
    await conn.query(
      'INSERT INTO player_state (user_id, money, level, wheat_harvest_count, batch_unlocked) VALUES (?, ?, 1, 0, 0)',
      [userId, STARTING_MONEY]
    );
    await conn.commit();

    return res.status(201).json({
      message: '회원가입이 완료되었습니다.',
      user: { id: userId, username },
    });
  } catch (err) {
    await conn.rollback();
    console.error('register error:', err);
    return res.status(500).json({ error: 'internal_server_error', message: '회원가입 중 오류가 발생했습니다.' });
  } finally {
    conn.release();
  }
});

module.exports = router;
