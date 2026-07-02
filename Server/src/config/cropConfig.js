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
    waterRequired: 1,       // 물 필요 횟수
    waterReduceSeconds: 0,  // 밀은 물을 줘도 시간이 단축되지 않음
  },
  potato_seed: {
    cropType: 'potato',
    seedPrice: 30,
    sellPrice: 60,
    growSeconds: 180,
    waterRequired: 3,
    waterReduceSeconds: 60, // 감자는 물 줄 때마다 남은 시간 60초 감소
  },
};

// 밭 구매 관련 상수 (9~10번 문서 기준)
const PLOT_PRICE = 100;

// 누적 수확 관련 상수
const BATCH_UNLOCK_HARVEST_COUNT = 5; // 밀 5번 수확 시 4칸 동시 작업 해금
const DEFAULT_CONCURRENT_LIMIT = 1;   // 해금 전에는 동시에 1칸만 작업 가능
const UNLOCKED_CONCURRENT_LIMIT = 4;  // 해금 후에는 4칸까지 동시 작업 가능

function getCropConfig(seedType) {
  return CROPS[seedType] || null;
}

module.exports = {
  CROPS,
  PLOT_PRICE,
  BATCH_UNLOCK_HARVEST_COUNT,
  DEFAULT_CONCURRENT_LIMIT,
  UNLOCKED_CONCURRENT_LIMIT,
  getCropConfig,
};
