const { verifyToken } = require('../utils/jwt');

/**
 * login.js가 Unity WebGL 클라이언트를 위해 토큰을 응답 바디로도 내려주고,
 * 쿠키(farm_token)로도 내려주기 때문에 두 방식 모두 지원한다.
 *   1) Authorization: Bearer <token>
 *   2) Cookie: farm_token=<token>   (app.js에 cookie-parser 필요)
 */
function requireAuth(req, res, next) {
  const header = req.headers.authorization || '';
  const [scheme, headerToken] = header.split(' ');
  const cookieToken = req.cookies && req.cookies.farm_token;

  const token = scheme === 'Bearer' && headerToken ? headerToken : cookieToken;

  if (!token) {
    return res.status(401).json({ error: 'unauthorized', message: '인증 토큰이 필요합니다.' });
  }

  try {
    const payload = verifyToken(token);
    req.user = { id: payload.userId, username: payload.username };
    next();
  } catch (err) {
    return res.status(401).json({ error: 'invalid_token', message: '토큰이 유효하지 않거나 만료되었습니다.' });
  }
}

module.exports = { requireAuth };
