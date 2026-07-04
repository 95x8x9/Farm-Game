/**
 * 개발 백로그 10번 "게임 로직 설계" 표를 그대로 옮긴 설정값.
 * 나중에 작물이 추가되면 이 객체에만 항목을 추가하면 된다.
 */
const CROPS = {
  wheat_seed: {
    cropType: 'wheat',
    seedPrice: 10,          // 씨앗 가격
    sellPrice: 20,          // 판매 가격
    growSeconds: 60,        // 성장 시간
    maxWaterCount: 1,       // 성장 시간 1분 = 물주기 최대 1회
  },
  potato_seed: {
    cropType: 'potato',
    seedPrice: 30,
    sellPrice: 60,
    growSeconds: 180,
    maxWaterCount: 3,       // 성장 시간 3분 = 물주기 최대 3회
  },
};

// 밭 구매 관련 상수 (9~10번 문서 기준)
const PLOT_PRICE = 100;
const PLOT_COUNT = 9;
const WATER_SUCCESS_REDUCTION_SECONDS = 60;
const WATER_FAIL_REDUCTION_SECONDS = 30;

// 누적 수확 관련 상수
const BATCH_UNLOCK_HARVEST_COUNT = 5; // 밀 5번 수확 시 해금 (통계용으로 유지)
// 클라이언트에 동시 재배 제한 UI가 없어 시연 편의를 위해 전체 밭 수만큼 허용한다.
// 해금 기반 제한 규칙은 2단계에서 클라이언트 UI와 함께 재도입 예정.
const DEFAULT_CONCURRENT_LIMIT = 9;
const UNLOCKED_CONCURRENT_LIMIT = 9;

function getCropConfig(seedType) {
  return CROPS[seedType] || null;
}

module.exports = {
  CROPS,
  PLOT_PRICE,
  PLOT_COUNT,
  WATER_SUCCESS_REDUCTION_SECONDS,
  WATER_FAIL_REDUCTION_SECONDS,
  BATCH_UNLOCK_HARVEST_COUNT,
  DEFAULT_CONCURRENT_LIMIT,
  UNLOCKED_CONCURRENT_LIMIT,
  getCropConfig,
};
