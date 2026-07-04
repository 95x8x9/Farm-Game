const pool = require('./db');

/**
 * 이미 운영 중인 DB에 새로 추가된 컬럼을 안전하게 반영한다.
 * schema.sql은 CREATE TABLE IF NOT EXISTS라 기존 테이블을 바꾸지 못하므로
 * 서버 시작 시 한 번 검사해서 없는 컬럼만 ALTER로 추가한다.
 */
async function ensurePlotPositionColumns() {
  const [rows] = await pool.query(
    `SELECT COLUMN_NAME FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'plots'
       AND COLUMN_NAME IN ('world_x', 'world_y')`
  );
  const existing = new Set(rows.map((row) => row.COLUMN_NAME));

  if (!existing.has('world_x')) {
    await pool.query('ALTER TABLE plots ADD COLUMN world_x FLOAT NULL');
    console.log('migrate: plots.world_x column added');
  }
  if (!existing.has('world_y')) {
    await pool.query('ALTER TABLE plots ADD COLUMN world_y FLOAT NULL');
    console.log('migrate: plots.world_y column added');
  }
}

module.exports = { ensurePlotPositionColumns };
