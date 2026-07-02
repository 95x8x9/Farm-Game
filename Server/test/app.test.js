const assert = require('node:assert/strict');
const { after, before, test } = require('node:test');
const app = require('../src/app');
const farmService = require('../src/services/farmService');
const {
  CROPS,
  WATER_SUCCESS_REDUCTION_SECONDS,
  WATER_FAIL_REDUCTION_SECONDS,
} = require('../src/config/cropConfig');

let server;
let baseUrl;

before(async () => {
  await new Promise((resolve) => {
    server = app.listen(0, '127.0.0.1', () => {
      const { port } = server.address();
      baseUrl = `http://127.0.0.1:${port}`;
      resolve();
    });
  });
});

after(async () => {
  await new Promise((resolve, reject) => {
    server.close((err) => (err ? reject(err) : resolve()));
  });
});

test('빈 회원가입 요청은 400으로 응답한다', async () => {
  const response = await fetch(`${baseUrl}/api/auth/register`, { method: 'POST' });
  assert.equal(response.status, 400);
});

test('빈 로그인 요청은 400으로 응답한다', async () => {
  const response = await fetch(`${baseUrl}/api/auth/login`, { method: 'POST' });
  assert.equal(response.status, 400);
});

test('GitHub Pages 출처의 preflight 요청을 허용한다', async () => {
  const response = await fetch(`${baseUrl}/api/farm`, {
    method: 'OPTIONS',
    headers: {
      Origin: 'https://95x8x9.github.io',
      'Access-Control-Request-Method': 'GET',
    },
  });

  assert.equal(response.status, 204);
  assert.equal(response.headers.get('access-control-allow-origin'), 'https://95x8x9.github.io');
  assert.equal(response.headers.get('access-control-allow-credentials'), 'true');
});

test('3×3 범위를 벗어난 밭 번호를 거부한다', async () => {
  await assert.rejects(
    farmService.buyPlot(1, 9),
    (err) => err instanceof farmService.ApiError && err.status === 400
  );
});

test('물주기 성공 여부는 boolean이어야 한다', async () => {
  await assert.rejects(
    farmService.waterCrop(1, 0, 'yes'),
    (err) => err instanceof farmService.ApiError && err.status === 400
  );
});

test('물주기 규칙은 성공 60초, 실패 30초이며 작물 분당 1회이다', () => {
  assert.equal(WATER_SUCCESS_REDUCTION_SECONDS, 60);
  assert.equal(WATER_FAIL_REDUCTION_SECONDS, 30);

  for (const crop of Object.values(CROPS)) {
    assert.equal(crop.maxWaterCount, Math.ceil(crop.growSeconds / 60));
  }
});
