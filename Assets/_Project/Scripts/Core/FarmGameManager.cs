using System.Collections.Generic;
using System.Linq;
using FarmGame.Data;
using FarmGame.Farm;
using FarmGame.Save;
using FarmGame.UI;
using UnityEngine;

namespace FarmGame.Core
{
    public sealed class FarmGameManager : MonoBehaviour
    {
        private const int InitialMoney = 500;
        private const int PlotPrice = 100;
        private const int WaterSuccessReductionSeconds = 60;
        private const int WaterFailReductionSeconds = 30;

        [SerializeField] private CropDefinition wheat;
        [SerializeField] private FarmCellView[] cellViews;
        [SerializeField] private FarmHud hud;
        [SerializeField] private WateringMinigame wateringMinigame;

        private IGameRepository repository;
        private ITimeProvider timeProvider;
        private PlayerSaveData saveData;
        private float nextRefreshTime;

        public bool IsMinigameActive => wateringMinigame != null && wateringMinigame.IsPlaying;

        public void Configure(
            CropDefinition wheatDefinition,
            FarmCellView[] views,
            FarmHud hudReference,
            WateringMinigame minigame)
        {
            wheat = wheatDefinition;
            cellViews = views;
            hud = hudReference;
            wateringMinigame = minigame;
        }

        private void Awake()
        {
            repository = new PlayerPrefsGameRepository();
            timeProvider = new SystemTimeProvider();

            bool loaded = repository.TryLoad(out saveData);
            if (!loaded)
            {
                saveData = CreateNewSave();
            }

            RepairCellCollection();
            RefreshAll();
            hud.SetMessage(loaded
                ? "저장을 불러왔습니다. 자란 밀은 노란색으로 표시됩니다."
                : "회색 밭을 눌러 100원에 구매하세요.  R: 저장 초기화");
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.unscaledTime + 0.2f;
            RefreshAll();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                Save();
            }
        }

        private void OnApplicationQuit()
        {
            Save();
        }

        public void Interact(FarmCellView view)
        {
            FarmCellState cell = FindCell(view.X, view.Y);
            if (cell == null)
            {
                return;
            }

            switch (cell.GetStatus(wheat, timeProvider.UtcNowSeconds))
            {
                case FarmCellStatus.Locked:
                    PurchasePlot(cell);
                    break;
                case FarmCellStatus.Empty:
                    PlantWheat(cell);
                    break;
                case FarmCellStatus.NeedsWater:
                    BeginWatering(cell);
                    break;
                case FarmCellStatus.Growing:
                    ShowRemainingTime(cell);
                    break;
                case FarmCellStatus.Ready:
                    Harvest(cell);
                    break;
            }
        }

        public void ResetProgress()
        {
            repository.Delete();
            saveData = CreateNewSave();
            Save();
            RefreshAll();
            hud.SetMessage("새 농장으로 초기화했습니다. 회색 밭을 눌러 시작하세요.");
        }

        private void PurchasePlot(FarmCellState cell)
        {
            if (saveData.money < PlotPrice)
            {
                hud.SetMessage($"밭 구매에는 {PlotPrice}원이 필요합니다.");
                return;
            }

            saveData.money -= PlotPrice;
            cell.purchased = true;
            Commit($"밭을 구매했습니다! 빈 밭을 다시 누르면 밀 씨앗을 심습니다. (-{PlotPrice}원)");
        }

        private void PlantWheat(FarmCellState cell)
        {
            if (saveData.money < wheat.SeedPrice)
            {
                hud.SetMessage($"밀 씨앗에는 {wheat.SeedPrice}원이 필요합니다.");
                return;
            }

            saveData.money -= wheat.SeedPrice;
            long now = timeProvider.UtcNowSeconds;
            cell.cropId = wheat.CropId;
            cell.waterCount = 0;
            cell.plantedAtUtc = now;
            cell.growthStartedAtUtc = now;
            cell.readyAtUtc = now + wheat.GrowthSeconds;
            Commit($"밀을 심었습니다. 물을 안 줘도 자라며, 물주기는 최대 {GetMaxWaterCount()}번 가능합니다. (-{wheat.SeedPrice}원)");
        }

        private void BeginWatering(FarmCellState cell)
        {
            hud.SetMessage($"타이밍 게임 진행 중… 성공 -{WaterSuccessReductionSeconds}초, 실패 -{WaterFailReductionSeconds}초");
            wateringMinigame.Begin(succeeded => ApplyWateringResult(cell, succeeded));
        }

        private void ApplyWateringResult(FarmCellState cell, bool succeeded)
        {
            long now = timeProvider.UtcNowSeconds;
            if (string.IsNullOrEmpty(cell.cropId) || now >= cell.readyAtUtc)
            {
                RefreshAll();
                return;
            }

            int maxWaterCount = GetMaxWaterCount();
            if (cell.waterCount >= maxWaterCount)
            {
                ShowRemainingTime(cell);
                return;
            }

            cell.waterCount++;
            int reductionSeconds = succeeded ? WaterSuccessReductionSeconds : WaterFailReductionSeconds;
            cell.readyAtUtc = System.Math.Max(now, cell.readyAtUtc - reductionSeconds);

            string result = succeeded ? "성공" : "실패";
            long remainingSeconds = System.Math.Max(0, cell.readyAtUtc - now);
            Commit($"물주기 {result}! 성장 시간이 {reductionSeconds}초 줄었습니다. ({cell.waterCount}/{maxWaterCount}, 남은 시간 약 {remainingSeconds}초)");
        }

        private void ShowRemainingTime(FarmCellState cell)
        {
            long seconds = System.Math.Max(0, cell.readyAtUtc - timeProvider.UtcNowSeconds);
            hud.SetMessage($"밀 성장 중: 약 {seconds}초 남았습니다. 게임을 꺼도 계속 자랍니다.");
        }

        private void Harvest(FarmCellState cell)
        {
            saveData.money += wheat.SellPrice;
            saveData.totalWheatHarvested++;
            cell.ClearCrop();

            string unlockHint = saveData.totalWheatHarvested == 5
                ? "  밀 5회 수확 달성! 다음 단계에서 2×2 작업을 해금할 수 있습니다."
                : string.Empty;
            Commit($"밀을 수확해 {wheat.SellPrice}원을 벌었습니다!{unlockHint}");
        }

        private void Commit(string message)
        {
            Save();
            RefreshAll();
            hud.SetMessage(message);
        }

        private void Save()
        {
            if (saveData == null || repository == null || timeProvider == null)
            {
                return;
            }

            saveData.lastSavedAtUtc = timeProvider.UtcNowSeconds;
            repository.Save(saveData);
        }

        private void RefreshAll()
        {
            if (saveData == null || wheat == null || cellViews == null)
            {
                return;
            }

            long now = timeProvider.UtcNowSeconds;
            foreach (FarmCellView view in cellViews)
            {
                FarmCellState state = FindCell(view.X, view.Y);
                if (state != null)
                {
                    view.Refresh(state, wheat, now);
                }
            }

            hud.Refresh(saveData.money, saveData.totalWheatHarvested);
        }

        private PlayerSaveData CreateNewSave()
        {
            PlayerSaveData data = new() { money = InitialMoney };
            if (cellViews != null)
            {
                foreach (FarmCellView view in cellViews)
                {
                    data.cells.Add(new FarmCellState { x = view.X, y = view.Y });
                }
            }

            return data;
        }

        private void RepairCellCollection()
        {
            saveData.cells ??= new List<FarmCellState>();
            foreach (FarmCellView view in cellViews)
            {
                if (FindCell(view.X, view.Y) == null)
                {
                    saveData.cells.Add(new FarmCellState { x = view.X, y = view.Y });
                }
            }

            long now = timeProvider.UtcNowSeconds;
            foreach (FarmCellState cell in saveData.cells)
            {
                if (string.IsNullOrEmpty(cell.cropId) || cell.readyAtUtc > 0)
                {
                    continue;
                }

                long startedAt = cell.plantedAtUtc > 0 ? cell.plantedAtUtc : now;
                cell.plantedAtUtc = startedAt;
                cell.growthStartedAtUtc = startedAt;
                cell.readyAtUtc = startedAt + wheat.GrowthSeconds;
            }
        }

        private int GetMaxWaterCount()
        {
            return FarmCellState.GetMaxWaterCount(wheat);
        }

        private FarmCellState FindCell(int x, int y)
        {
            return saveData.cells.FirstOrDefault(cell => cell.x == x && cell.y == y);
        }
    }
}
