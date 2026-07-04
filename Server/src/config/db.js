require('./env');

const mysql = require('mysql2/promise');

/**
 * 백로그 4번 구조: web-server -> Cloud DB for MySQL (3306)
 * login.js / register.js / health.js / me.js 등 모든 팀원 파일이
 * `../../config/db` 로 이 pool을 그대로 가져다 쓴다.
 */
// NCP Cloud DB for MySQL은 기본적으로 VPC 내부 통신이라 SSL이 필수는 아니지만,
// 콘솔에서 SSL 접속을 강제하도록 설정했다면 DB_SSL=true로 켤 수 있게 해둔다.
const useSSL = process.env.DB_SSL === 'true';

const pool = mysql.createPool({
  host: process.env.DB_HOST || '127.0.0.1',
  port: Number(process.env.DB_PORT || 3306),
  user: process.env.DB_USER || 'farm',
  password: process.env.DB_PASSWORD || '',
  database: process.env.DB_NAME || 'farmdb',
  waitForConnections: true,
  connectionLimit: Number(process.env.DB_CONNECTION_LIMIT || 10),
  connectTimeout: Number(process.env.DB_CONNECT_TIMEOUT_MS || 10000),
  ssl: useSSL ? { rejectUnauthorized: process.env.DB_SSL_REJECT_UNAUTHORIZED !== 'false' } : undefined,
});

module.exports = pool;
