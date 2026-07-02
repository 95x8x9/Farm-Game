const app = require('./app');

const PORT = process.env.PORT || 3000;
app.listen(PORT, () => {
  console.log(`farm API server listening on port ${PORT}`);
});
