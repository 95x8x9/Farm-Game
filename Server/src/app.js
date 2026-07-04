require('./config/env');

const express = require('express');
const cookieParser = require('cookie-parser');
const cors = require('cors');

const registerRouter = require('./routes/auth/register');
const loginRouter = require('./routes/auth/login');
const logoutRouter = require('./routes/auth/logout');
const meRouter = require('./routes/api/me');
const healthRouter = require('./routes/health/health');
const farmRoutes = require('./routes/farm/farmRoutes'); // 내 담당 6개 API

const app = express();

const allowedOrigins = (process.env.CORS_ORIGINS || 'https://95x8x9.github.io,http://localhost:8000')
  .split(',')
  .map((origin) => origin.trim())
  .filter(Boolean);

// NCP 구조: 사용자 -> ALB(HTTPS) -> Nginx -> Node(이 앱) 순서로 프록시를 거친다.
// trust proxy를 켜야 req.ip, req.protocol이 프록시 뒤에서도 올바르게 계산된다.
app.set('trust proxy', 1);

app.use(cors({
  origin(origin, callback) {
    callback(null, !origin || allowedOrigins.includes(origin));
  },
  credentials: true,
}));
app.use(express.json());
app.use(cookieParser()); // login.js가 쿠키(farm_token)를 쓰므로 필요

// 백로그 11번 API 설계표 기준 라우팅
app.use('/api/auth/register', registerRouter);
app.use('/api/auth/login', loginRouter);
app.use('/api/auth/logout', logoutRouter);
app.use('/api/me', meRouter);
app.use('/health', healthRouter);

// 내가 맡은 6개 API (farmRoutes.js 내부에서 전체 경로를 직접 선언함)
// GET  /api/farm
// POST /api/plots/buy
// POST /api/seeds/buy
// POST /api/crops/plant
// POST /api/crops/water
// POST /api/crops/harvest
app.use(farmRoutes);

module.exports = app;
