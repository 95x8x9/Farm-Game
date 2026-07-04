using System.Collections.Generic;
using System.Linq;
using FarmGame.Data;
using FarmGame.Farm;
using FarmGame.Net;
using FarmGame.Save;
using FarmGame.UI;
using UnityEngine;

namespace FarmGame.Core
{
    public sealed class FarmGameManager : MonoBehaviour
    {
        private const int InitialMoney = 510;
        private const int LegacyInitialMoney = 500;
        private const int PlotPrice = 100;
        private const int WaterSuccessReductionSeconds = 60;
        private const int WaterFailReductionSeconds = 30;

        [SerializeField] private CropDefinition wheat;
        [SerializeField] private CropDefinition[] crops;
        [SerializeField] private FarmCellView[] cellViews;
        [SerializeField] private FarmHud hud;
        [SerializeField] private WateringMinigame wateringMinigame;
        [SerializeField] private FarmShopPanel shopPanel;
        [SerializeField] private FirstPlotTutorialPopup firstPlotTutorialPopup;
        [SerializeField] private LoginPanel loginPanel;
        // 픽셀 배경(bg_farm_pixel)의 흙밭 스트립(x -4.0~3.8)에서 밭 절반 크기만큼 안쪽으로 들어온 영역.
        private static readonly Rect PixelFieldPlacementBounds = new(-3.3f, -2.6f, 6.4f, 5.55f);

        [SerializeField] private Rect plotPlacementBounds = new(-3.3f, -2.6f, 6.4f, 5.55f);
        [SerializeField] private Vector2 plotFootprint = new(1.36f, 1.44f);
        [SerializeField] private LayerMask placementBlockingLayers = ~0;

        private IGameRepository repository;
        private ITimeProvider timeProvider;
        private PlayerSaveData saveData;
        private float nextRefreshTime;
        private int inputBlockThroughFrame = -1;
        private bool isPlacingPlot;
        private bool hasPlacementPreviewPosition;
        private bool canPlaceAtPreviewPosition;
        private Vector2 placementPreviewPosition;
        private FarmCellState placementCell;
        private FarmCellView placementPreviewView;
        private CropDefinition selectedCropForPlanting;
        private CropDefinition potato;
        private bool isRemovingPlot;
        private string activeSaveOwner = string.Empty;
        private FarmApiClient apiClient;

        // 로그인 상태에서만 게임 액션을 서버 DB에 반영한다 (게스트는 로컬 저장만).
        private bool IsServerSyncEnabled => apiClient != null && apiClient.IsLoggedIn;

        public bool IsMinigameActive => wateringMinigame != null && wateringMinigame.IsPlaying;
        public bool IsPlacingPlot => isPlacingPlot;
        public bool IsPlantingCrop => selectedCropForPlanting != null;
        public bool IsRemovingPlot => isRemovingPlot;
        public bool IsInputBlocked => IsMinigameActive
            || (shopPanel != null && shopPanel.IsOpen)
            || (firstPlotTutorialPopup != null && firstPlotTutorialPopup.IsVisible)
            || (loginPanel != null && loginPanel.IsVisible)
            || Time.frameCount <= inputBlockThroughFrame;

        public void Configure(
            CropDefinition wheatDefinition,
            FarmCellView[] views,
            FarmHud hudReference,
            WateringMinigame minigame,
            FarmShopPanel shop,
            FirstPlotTutorialPopup tutorialPopup)
        {
            wheat = wheatDefinition;
            crops = wheatDefinition == null ? new CropDefinition[0] : new[] { wheatDefinition };
            cellViews = views;
            hud = hudReference;
            wateringMinigame = minigame;
            shopPanel = shop;
            firstPlotTutorialPopup = tutorialPopup;
        }

        private void Awake()
        {
            repository = new PlayerPrefsGameRepository();
            timeProvider = new SystemTimeProvider();
            EnsureCropCatalog();
            FarmBackdrop.Ensure();
            apiClient = FarmApiClient.Ensure();
            // 씬에 옛 배치 범위가 직렬화되어 있을 수 있으므로 흙밭 전체 범위로 덮어쓴다.
            plotPlacementBounds = PixelFieldPlacementBounds;
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (shopPanel == null && canvas != null)
            {
                shopPanel = FarmShopPanel.Create(canvas.transform);
            }

            shopPanel?.Initialize(BeginPlotPlacement, BeginCropPlanting, BeginPlotRemoval, HandleShopVisibilityChanged);
            if (firstPlotTutorialPopup == null)
            {
                if (canvas != null)
                {
                    firstPlotTutorialPopup = FirstPlotTutorialPopup.Create(canvas.transform);
                }
            }

            bool loaded = repository.TryLoad(out saveData);
            if (!loaded)
            {
                saveData = CreateNewSave();
            }
            else
            {
                ApplyStartingMoneyMigration();
            }

            if (loginPanel == null && canvas != null)
            {
                loginPanel = LoginPanel.Create(canvas.transform);
            }

            loginPanel?.Initialize(
                message => hud.SetMessage(message),
                HandleSessionStarted,
                HandleSessionEnded);

            RepairCellCollection();
            RefreshAll();
            hud.SetMessage(loaded
                ? "저장을 불러왔습니다. 자란 밀은 노란색으로 표시됩니다."
                : "상점에서 밭을 고른 뒤 원하는 위치에 배치하세요.  R: 저장 초기화");

            if (loaded)
            {
                firstPlotTutorialPopup?.Hide();
            }
            else
            {
                firstPlotTutorialPopup?.Show(HandleFirstPlotTutorialDismissed);
            }
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

            if (isRemovingPlot)
            {
                RemovePlot(cell);
                return;
            }

            CropDefinition crop = GetCropDefinition(cell.cropId) ?? wheat;
            FarmCellStatus status = cell.GetStatus(crop, timeProvider.UtcNowSeconds);
            if (selectedCropForPlanting != null && status != FarmCellStatus.Empty)
            {
                hud.SetMessage("작물을 심을 빈 밭을 선택하세요.");
                return;
            }

            switch (status)
            {
                case FarmCellStatus.Locked:
                    hud.SetMessage("밭은 상점 탭에서 구매할 수 있습니다.");
                    break;
                case FarmCellStatus.Empty:
                    if (selectedCropForPlanting == null)
                    {
                        hud.SetMessage("상점에서 심을 작물을 먼저 선택하세요.");
                    }
                    else
                    {
                        PlantCrop(cell, selectedCropForPlanting);
                    }
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

        public void UpdatePlotPlacementPreview(Vector2 worldPosition)
        {
            if (!isPlacingPlot || placementPreviewView == null)
            {
                return;
            }

            placementPreviewPosition = worldPosition;
            hasPlacementPreviewPosition = true;
            canPlaceAtPreviewPosition = CanPlacePlotAt(worldPosition);
            placementPreviewView.ShowPlacementPreview(worldPosition, canPlaceAtPreviewPosition);
        }

        public void TryPlacePlotAt(Vector2 worldPosition)
        {
            if (!isPlacingPlot || placementCell == null || placementPreviewView == null)
            {
                return;
            }

            UpdatePlotPlacementPreview(worldPosition);
            if (!canPlaceAtPreviewPosition)
            {
                hud.SetMessage("이 위치에는 설치할 수 없습니다. 농장 안의 겹치지 않는 공간을 선택하세요.");
                return;
            }

            if (saveData.money < PlotPrice)
            {
                CancelPlotPlacement();
                hud.SetMessage($"밭 구매에는 {PlotPrice}원이 필요합니다.");
                return;
            }

            saveData.money -= PlotPrice;
            placementCell.purchased = true;
            placementCell.hasWorldPosition = true;
            placementCell.worldX = worldPosition.x;
            placementCell.worldY = worldPosition.y;
            FarmCellState purchasedCell = placementCell;
            isPlacingPlot = false;
            ClearPlacementPreview();
            Commit($"선택한 위치에 밭을 설치했습니다! 다시 누르면 밀 씨앗을 심습니다. (-{PlotPrice}원)");
            SyncBuyPlot(purchasedCell);
            shopPanel?.Open();
        }

        public void CancelPlotPlacement()
        {
            if (!isPlacingPlot)
            {
                return;
            }

            isPlacingPlot = false;
            ClearPlacementPreview();
            RefreshAll();
            hud.SetMessage("밭 배치를 취소했습니다. 아직 돈은 사용되지 않았습니다.");
        }

        public void ResetProgress()
        {
            firstPlotTutorialPopup?.Hide();
            shopPanel?.Close();
            isPlacingPlot = false;
            selectedCropForPlanting = null;
            isRemovingPlot = false;
            ClearPlacementPreview();
            repository.Delete();
            saveData = CreateNewSave();
            Save();
            RefreshAll();
            hud.SetMessage("새 농장으로 초기화했습니다. 상점에서 밭을 골라 시작하세요.");
        }

        public void CancelCropPlanting()
        {
            if (selectedCropForPlanting == null)
            {
                return;
            }

            selectedCropForPlanting = null;
            hud.SetMessage("작물 심기를 취소했습니다.");
        }

        private void HandleSessionStarted(string username)
        {
            LoadSaveForOwner(username);
            FetchServerFarm();
        }

        private void HandleSessionEnded()
        {
            LoadSaveForOwner(null);
        }

        private void LoadSaveForOwner(string ownerKey)
        {
            Save();

            string nextOwner = string.IsNullOrWhiteSpace(ownerKey) ? string.Empty : ownerKey.Trim();
            if (nextOwner == activeSaveOwner)
            {
                return;
            }

            activeSaveOwner = nextOwner;
            repository = new PlayerPrefsGameRepository(activeSaveOwner);
            bool loaded = repository.TryLoad(out saveData);
            if (!loaded)
            {
                saveData = CreateNewSave();
            }
            else
            {
                ApplyStartingMoneyMigration();
            }

            isPlacingPlot = false;
            selectedCropForPlanting = null;
            isRemovingPlot = false;
            ClearPlacementPreview();
            shopPanel?.Close();
            RepairCellCollection();
            RefreshAll();

            if (loaded)
            {
                firstPlotTutorialPopup?.Hide();
            }
            else
            {
                firstPlotTutorialPopup?.Show(HandleFirstPlotTutorialDismissed);
            }
        }

        public void CancelPlotRemoval()
        {
            if (!isRemovingPlot)
            {
                return;
            }

            isRemovingPlot = false;
            hud.SetMessage("밭 삭제를 취소했습니다.");
        }

        private void HandleFirstPlotTutorialDismissed(bool purchaseRequested)
        {
            inputBlockThroughFrame = Time.frameCount;
            if (!purchaseRequested)
            {
                Save();
                return;
            }

            OpenShop();
        }

        private void OpenShop()
        {
            isPlacingPlot = false;
            isRemovingPlot = false;
            selectedCropForPlanting = null;
            ClearPlacementPreview();
            shopPanel?.Open();
            RefreshAll();
            hud.SetMessage("상점에서 밭 상품의 '배치' 버튼을 누르세요.");
        }

        private void HandleShopVisibilityChanged()
        {
            inputBlockThroughFrame = Time.frameCount;
        }

        private void BeginPlotRemoval()
        {
            selectedCropForPlanting = null;
            isPlacingPlot = false;
            ClearPlacementPreview();
            if (!saveData.cells.Any(cell => cell.purchased && string.IsNullOrEmpty(cell.cropId)))
            {
                hud.SetMessage("삭제할 수 있는 빈 밭이 없습니다. 작물이 있는 밭은 수확 후 삭제하세요.");
                return;
            }

            shopPanel?.Close();
            inputBlockThroughFrame = Time.frameCount;
            isRemovingPlot = true;
            RefreshAll();
            hud.SetMessage("삭제할 빈 밭을 클릭하세요. Esc 또는 우클릭으로 취소합니다.");
        }

        private void RemovePlot(FarmCellState cell)
        {
            if (!cell.purchased)
            {
                hud.SetMessage("구매한 밭만 삭제할 수 있습니다.");
                return;
            }

            if (!string.IsNullOrEmpty(cell.cropId))
            {
                hud.SetMessage("작물이 자라는 밭은 삭제할 수 없습니다. 수확 후 삭제하세요.");
                return;
            }

            cell.purchased = false;
            cell.hasWorldPosition = false;
            cell.ClearCrop();
            isRemovingPlot = false;
            Commit("밭을 삭제했습니다. (환급 0원)");
        }

        private void BeginPlotPlacement()
        {
            selectedCropForPlanting = null;
            isRemovingPlot = false;
            if (saveData.money < PlotPrice)
            {
                hud.SetMessage($"밭 구매에는 {PlotPrice}원이 필요합니다.");
                return;
            }

            placementCell = saveData.cells.FirstOrDefault(cell => !cell.purchased);
            placementPreviewView = placementCell == null ? null : FindView(placementCell.x, placementCell.y);
            if (placementCell == null || placementPreviewView == null)
            {
                hud.SetMessage("더 이상 밭을 배치할 공간이 없습니다.");
                ClearPlacementPreview();
                return;
            }

            shopPanel?.Close();
            inputBlockThroughFrame = Time.frameCount;
            isPlacingPlot = true;
            hasPlacementPreviewPosition = false;
            canPlaceAtPreviewPosition = false;
            RefreshAll();
            hud.SetMessage("빈 공간으로 밭을 옮긴 뒤 클릭해 설치하세요. 빨간색은 설치 불가 위치입니다.");
        }

        private void BeginCropPlanting(string cropId)
        {
            CropDefinition crop = GetCropDefinition(cropId);
            if (crop == null)
            {
                hud.SetMessage("아직 판매 준비 중인 작물입니다.");
                return;
            }

            if (saveData.money < crop.SeedPrice)
            {
                hud.SetMessage($"{crop.DisplayName} 씨앗에는 {crop.SeedPrice}원이 필요합니다.");
                return;
            }

            isPlacingPlot = false;
            isRemovingPlot = false;
            ClearPlacementPreview();
            selectedCropForPlanting = crop;
            shopPanel?.Close();
            inputBlockThroughFrame = Time.frameCount;
            hud.SetMessage($"빈 밭을 클릭해 {crop.DisplayName}을(를) 심으세요. 우클릭 또는 Esc로 취소할 수 있습니다.");
        }

        private bool CanPlacePlotAt(Vector2 worldPosition)
        {
            if (!plotPlacementBounds.Contains(worldPosition))
            {
                return false;
            }

            Collider2D[] overlaps = Physics2D.OverlapBoxAll(worldPosition, plotFootprint, 0f, placementBlockingLayers);
            foreach (Collider2D overlap in overlaps)
            {
                if (overlap == null || !overlap.enabled)
                {
                    continue;
                }

                FarmCellView cellView = overlap.GetComponentInParent<FarmCellView>();
                if (cellView == placementPreviewView)
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private void ClearPlacementPreview()
        {
            hasPlacementPreviewPosition = false;
            canPlaceAtPreviewPosition = false;
            placementCell = null;
            placementPreviewView = null;
        }

        private void PlantCrop(FarmCellState cell, CropDefinition crop)
        {
            if (crop == null || saveData.money < crop.SeedPrice)
            {
                selectedCropForPlanting = null;
                hud.SetMessage($"씨앗에는 {crop?.SeedPrice ?? 0}원이 필요합니다.");
                return;
            }

            saveData.money -= crop.SeedPrice;
            long now = timeProvider.UtcNowSeconds;
            cell.cropId = crop.CropId;
            cell.waterCount = 0;
            cell.plantedAtUtc = now;
            cell.growthStartedAtUtc = now;
            cell.readyAtUtc = now + crop.GrowthSeconds;
            Commit($"{crop.DisplayName}을(를) 심었습니다. 계속 빈 밭을 클릭해 심을 수 있습니다. 우클릭 또는 Esc로 종료하세요. (-{crop.SeedPrice}원)");
            SyncPlantCrop(cell, crop);
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

            CropDefinition crop = GetCropDefinition(cell.cropId) ?? wheat;
            int maxWaterCount = GetMaxWaterCount(crop);
            if (cell.waterCount >= maxWaterCount)
            {
                ShowRemainingTime(cell);
                return;
            }

            cell.waterCount++;
            int reductionSeconds = succeeded ? WaterSuccessReductionSeconds : WaterFailReductionSeconds;
            cell.readyAtUtc = System.Math.Max(now, cell.readyAtUtc - reductionSeconds);

            FarmCellView wateredView = FindView(cell.x, cell.y);
            if (wateredView != null)
            {
                WateringEffect.Play(wateredView.transform.position, succeeded);
            }

            string result = succeeded ? "성공" : "실패";
            long remainingSeconds = System.Math.Max(0, cell.readyAtUtc - now);
            Commit($"물주기 {result}! 성장 시간이 {reductionSeconds}초 줄었습니다. ({cell.waterCount}/{maxWaterCount}, 남은 시간 약 {remainingSeconds}초)");
            SyncWaterCrop(cell, succeeded);
        }

        private void ShowRemainingTime(FarmCellState cell)
        {
            long seconds = System.Math.Max(0, cell.readyAtUtc - timeProvider.UtcNowSeconds);
            hud.SetMessage($"밀 성장 중: 약 {seconds}초 남았습니다. 게임을 꺼도 계속 자랍니다.");
        }

        private void Harvest(FarmCellState cell)
        {
            CropDefinition crop = GetCropDefinition(cell.cropId) ?? wheat;
            saveData.money += crop.SellPrice;
            if (crop.CropId == wheat.CropId)
            {
                saveData.totalWheatHarvested++;
            }
            cell.ClearCrop();

            string unlockHint = saveData.totalWheatHarvested == 5
                ? "  밀 5회 수확 달성! 다음 단계에서 2×2 작업을 해금할 수 있습니다."
                : string.Empty;
            Commit($"{crop.DisplayName}을(를) 수확해 {crop.SellPrice}원을 벌었습니다!{unlockHint}");
            SyncHarvestCrop(cell);
        }

        // ===== 서버 동기화 (로그인 상태에서 게임 액션을 DB에 반영) =====

        private static int ToPlotIndex(FarmCellState cell)
        {
            return cell.y * 3 + cell.x;
        }

        private void SyncBuyPlot(FarmCellState cell)
        {
            if (!IsServerSyncEnabled || cell == null)
            {
                return;
            }

            apiClient.BuyPlot(ToPlotIndex(cell), (ok, message) => ReportSyncFailure(ok, "밭 구매", message));
        }

        private void SyncPlantCrop(FarmCellState cell, CropDefinition crop)
        {
            if (!IsServerSyncEnabled || cell == null || crop == null)
            {
                return;
            }

            apiClient.PlantCrop(ToPlotIndex(cell), crop.CropId + "_seed", (ok, message) => ReportSyncFailure(ok, "심기", message));
        }

        private void SyncWaterCrop(FarmCellState cell, bool succeeded)
        {
            if (!IsServerSyncEnabled || cell == null)
            {
                return;
            }

            apiClient.WaterCrop(ToPlotIndex(cell), succeeded, (ok, message) => ReportSyncFailure(ok, "물주기", message));
        }

        private void SyncHarvestCrop(FarmCellState cell)
        {
            if (!IsServerSyncEnabled || cell == null)
            {
                return;
            }

            apiClient.HarvestCrop(ToPlotIndex(cell), (ok, message) => ReportSyncFailure(ok, "수확", message));
        }

        private void ReportSyncFailure(bool ok, string action, string message)
        {
            if (ok)
            {
                return;
            }

            Debug.LogWarning($"[FarmSync] {action} 서버 반영 실패: {message}");
            hud.SetMessage($"서버 반영 실패({action}): {message}");
        }

        /// <summary>로그인 직후 서버 DB의 농장 상태로 로컬을 맞춘다 (서버가 원본).</summary>
        private void FetchServerFarm()
        {
            if (!IsServerSyncEnabled)
            {
                return;
            }

            apiClient.GetFarm((ok, snapshot, message) =>
            {
                if (!ok || snapshot == null)
                {
                    Debug.LogWarning($"[FarmSync] 농장 불러오기 실패: {message}");
                    return;
                }

                ApplyServerFarm(snapshot);
            });
        }

        private void ApplyServerFarm(FarmSnapshot snapshot)
        {
            if (saveData == null)
            {
                return;
            }

            saveData.money = snapshot.money;
            saveData.totalWheatHarvested = snapshot.wheat_harvest_count;
            long now = timeProvider.UtcNowSeconds;

            foreach (FarmCellState cell in saveData.cells)
            {
                cell.purchased = false;
                cell.hasWorldPosition = false;
                cell.ClearCrop();
            }

            if (snapshot.plots != null)
            {
                foreach (FarmPlotDto plot in snapshot.plots)
                {
                    FarmCellState cell = FindCell(plot.plot_index % 3, plot.plot_index / 3);
                    if (cell == null || plot.unlocked == 0)
                    {
                        continue;
                    }

                    cell.purchased = true;
                    if (string.IsNullOrEmpty(plot.crop_type) || plot.state == "empty")
                    {
                        continue;
                    }

                    cell.cropId = plot.crop_type;
                    cell.waterCount = plot.water_count;
                    long readyAt = ParseServerTime(plot.ready_at, now);
                    // 서버-클라이언트 시간대 차이에 대비해 상태값(state)을 우선 신뢰하고 시각은 보정한다.
                    cell.readyAtUtc = plot.state == "ready"
                        ? now
                        : System.Math.Clamp(readyAt, now + 1, now + 3600);
                    long plantedAt = ParseServerTime(plot.planted_at, now);
                    cell.plantedAtUtc = System.Math.Min(plantedAt, cell.readyAtUtc - 1);
                    cell.growthStartedAtUtc = cell.plantedAtUtc;
                }
            }

            RepairCellCollection();
            Save();
            RefreshAll();
            hud.SetMessage("서버에서 농장을 불러왔습니다. 이제 모든 진행이 DB에 저장됩니다.");
        }

        private static long ParseServerTime(string value, long fallback)
        {
            if (string.IsNullOrEmpty(value))
            {
                return fallback;
            }

            return System.DateTimeOffset.TryParse(
                value,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out System.DateTimeOffset parsed)
                ? parsed.ToUnixTimeSeconds()
                : fallback;
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
                if (state == null || !state.purchased)
                {
                    if (!(isPlacingPlot && view == placementPreviewView && hasPlacementPreviewPosition))
                    {
                        view.Hide();
                    }

                    continue;
                }

                if (state.hasWorldPosition)
                {
                    view.SetWorldPosition(state.worldX, state.worldY);
                }

                view.Refresh(state, GetCropDefinition(state.cropId) ?? wheat, now);
            }

            if (isPlacingPlot && placementPreviewView != null && hasPlacementPreviewPosition)
            {
                placementPreviewView.ShowPlacementPreview(placementPreviewPosition, canPlaceAtPreviewPosition);
            }

            hud.Refresh(saveData.money, saveData.totalWheatHarvested);
            shopPanel?.Refresh(
                PlotPrice,
                wheat.SeedPrice,
                wheat.SellPrice,
                wheat.GrowthSeconds,
                saveData.money,
                saveData.cells.Count(cell => !cell.purchased));
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

        private void ApplyStartingMoneyMigration()
        {
            if (saveData == null || saveData.money != LegacyInitialMoney || saveData.totalWheatHarvested != 0)
            {
                return;
            }

            bool hasProgress = saveData.cells != null && saveData.cells.Any(cell =>
                cell.purchased ||
                !string.IsNullOrEmpty(cell.cropId) ||
                cell.waterCount > 0 ||
                cell.plantedAtUtc > 0 ||
                cell.readyAtUtc > 0);

            if (hasProgress)
            {
                return;
            }

            saveData.money = InitialMoney;
            Save();
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
                if (cell.purchased && !cell.hasWorldPosition)
                {
                    FarmCellView view = FindView(cell.x, cell.y);
                    if (view != null)
                    {
                        cell.hasWorldPosition = true;
                        cell.worldX = view.transform.position.x;
                        cell.worldY = view.transform.position.y;
                    }
                }

                if (string.IsNullOrEmpty(cell.cropId) || cell.readyAtUtc > 0)
                {
                    continue;
                }

                long startedAt = cell.plantedAtUtc > 0 ? cell.plantedAtUtc : now;
                cell.plantedAtUtc = startedAt;
                cell.growthStartedAtUtc = startedAt;
                CropDefinition crop = GetCropDefinition(cell.cropId) ?? wheat;
                cell.readyAtUtc = startedAt + crop.GrowthSeconds;
            }

            saveData.schemaVersion = 2;
        }

        private static int GetMaxWaterCount(CropDefinition crop)
        {
            return FarmCellState.GetMaxWaterCount(crop);
        }

        private CropDefinition GetCropDefinition(string cropId)
        {
            if (string.IsNullOrEmpty(cropId))
            {
                return null;
            }

            CropDefinition crop = crops?.FirstOrDefault(candidate => candidate != null && candidate.CropId == cropId);
            return crop ?? (wheat != null && wheat.CropId == cropId ? wheat : null);
        }

        private void EnsureCropCatalog()
        {
            List<CropDefinition> catalog = crops?
                .Where(candidate => candidate != null)
                .ToList() ?? new List<CropDefinition>();

            if (wheat != null && catalog.All(candidate => candidate.CropId != wheat.CropId))
            {
                catalog.Add(wheat);
            }

            potato = catalog.FirstOrDefault(candidate => candidate.CropId == "potato");
            if (potato == null)
            {
                potato = CropDefinition.CreateRuntime("potato", "감자", 20, 35, 90, 2);
                catalog.Add(potato);
            }

            crops = catalog.ToArray();
        }

        private FarmCellState FindCell(int x, int y)
        {
            return saveData.cells.FirstOrDefault(cell => cell.x == x && cell.y == y);
        }

        private FarmCellView FindView(int x, int y)
        {
            return cellViews.FirstOrDefault(view => view.X == x && view.Y == y);
        }
    }
}
