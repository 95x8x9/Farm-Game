const app = require('./app');
const { ensurePlotPositionColumns } = require('./config/migrate');

const PORT = process.env.PORT || 3000;

// 마이그레이션이 실패해도 서버는 뜨게 해서 /health로 원인을 파악할 수 있게 한다.
ensurePlotPositionColumns()
  .catch((err) => {
    console.error('DB migration failed (continuing):', err.message);
  })
  .finally(() => {
    app.listen(PORT, () => {
      console.log(`farm API server listening on port ${PORT}`);
    });
  });
