-- 백로그 9번 "DB 설계"를 기반으로 하되,
-- 감자의 "물 줄 때마다 남은 시간 60초 감소" 로직을 서버에서 계산하려면
-- 수확 가능 시각을 저장해둘 컬럼(ready_at)이 필요해서 plots 테이블에 추가했다.

CREATE TABLE IF NOT EXISTS users (
  id            INT AUTO_INCREMENT PRIMARY KEY,
  username      VARCHAR(50) NOT NULL UNIQUE,
  password_hash VARCHAR(255) NOT NULL,
  created_at    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS player_state (
  user_id              INT PRIMARY KEY,
  money                INT NOT NULL DEFAULT 500,   -- 초기 자금 500원
  level                INT NOT NULL DEFAULT 1,
  wheat_harvest_count  INT NOT NULL DEFAULT 0,
  batch_unlocked       BOOLEAN NOT NULL DEFAULT FALSE,
  FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS plots (
  id          INT AUTO_INCREMENT PRIMARY KEY,
  user_id     INT NOT NULL,
  plot_index  INT NOT NULL,
  unlocked    BOOLEAN NOT NULL DEFAULT FALSE,
  crop_type   VARCHAR(20) NULL,          -- 'wheat' | 'potato' | NULL
  planted_at  DATETIME NULL,
  water_count INT NOT NULL DEFAULT 0,
  ready_at    DATETIME NULL,             -- 추가 컬럼: 수확 가능 예정 시각
  state       ENUM('empty', 'growing', 'ready') NOT NULL DEFAULT 'empty',
  UNIQUE KEY uniq_user_plot (user_id, plot_index),
  FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS inventory (
  id         INT AUTO_INCREMENT PRIMARY KEY,
  user_id    INT NOT NULL,
  item_type  VARCHAR(30) NOT NULL,       -- 'wheat_seed', 'potato_seed' 등
  quantity   INT NOT NULL DEFAULT 0,
  UNIQUE KEY uniq_user_item (user_id, item_type),
  FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
);
