const pool = require('../config/db');

const REQUIRED_USERS_COLUMNS = ['id', 'username', 'password_hash'];

async function main() {
  const [[dbInfo]] = await pool.query(
    'SELECT DATABASE() AS databaseName, CURRENT_USER() AS currentUser, @@hostname AS serverHost'
  );

  const [columns] = await pool.query(
    `SELECT COLUMN_NAME AS columnName
     FROM INFORMATION_SCHEMA.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE()
       AND TABLE_NAME = 'users'
       AND COLUMN_NAME IN (?, ?, ?)`,
    REQUIRED_USERS_COLUMNS
  );

  const existingColumns = new Set(columns.map((column) => column.columnName));
  const missingColumns = REQUIRED_USERS_COLUMNS.filter((column) => !existingColumns.has(column));
  if (missingColumns.length > 0) {
    throw new Error(`users table is missing required columns: ${missingColumns.join(', ')}`);
  }

  const [[userCount]] = await pool.query('SELECT COUNT(*) AS count FROM users');

  console.log('DB connection OK');
  console.log(`database=${dbInfo.databaseName}`);
  console.log(`current_user=${dbInfo.currentUser}`);
  console.log(`server_host=${dbInfo.serverHost}`);
  console.log(`users=${userCount.count}`);
}

main()
  .catch((err) => {
    console.error('DB connection failed');
    console.error(`message=${err.message}`);
    if (err.code) console.error(`code=${err.code}`);
    process.exitCode = 1;
  })
  .finally(async () => {
    await pool.end();
  });
