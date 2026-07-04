using System;
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
        // Server/src/config/cropConfig.js의 DEFAULT/UNLOCKED_CONCURRENT_LIMIT과 동일해야 한다.
        private const int ServerConcurrentPlotLimit = 9;

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
        private FarmApiClient api;
        private bool isServerBacked;
        private bool serverRequestInFlight;
        private bool serverBatchUnlocked;
        private readonly Dictionary<string, int> serverInventory = new();

        public bool IsMinigameActive => wateringMinigame != null && wateringMinigame.IsPlaying;
        public bool IsPlacingPlot => isPlacingPlot;
        public bool IsPlantingCrop => selectedCropForPlanting != null;
        public bool IsRemovingPlot => isRemovingPlot;
        public bool IsInputBlocked => IsMinigameActive
            || serverRequestInFlight
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
            api = FarmApiClient.Ensure();
            EnsureCropCatalog();
            FarmBackdrop.Ensure();
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

            if (isServerBacked)
            {
                BuyAndPlaceServerPlot(placementCell, worldPosition);
                return;
            }

            saveData.money -= PlotPrice;
            placementCell.purchased = true;
            placementCell.hasWorldPosition = true;
            placementCell.worldX = worldPosition.x;
            placementCell.worldY = worldPosition.y;
            isPlacingPlot = false;
            ClearPlacementPreview();
            Commit($"선택한 위치에 밭을 설치했습니다! 다시 누르면 밀 씨앗을 심습니다. (-{PlotPrice}원)");
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
            if (isServerBacked)
            {
                hud.SetMessage("로그인 계정의 농장은 서버에 저장되어 초기화할 수 없습니다.");
                return;
            }

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
            isServerBacked = true;
            LoadFarmFromServer();
        }

        private void HandleSessionEnded()
        {
            isServerBacked = false;
            serverRequestInFlight = false;
            serverInventory.Clear();
            serverBatchUnlocked = false;
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

            isRemovingPlot = false;

            if (isServerBacked)
            {
                DeleteServerPlot(cell);
                return;
            }

            cell.purchased = false;
            cell.hasWorldPosition = false;
            cell.ClearCrop();
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

            if (isServerBacked)
            {
                int activePlotCount = saveData.cells.Count(candidate => !string.IsNullOrEmpty(candidate.cropId));
                if (activePlotCount >= ServerConcurrentPlotLimit)
                {
                    hud.SetMessage($"동시에 작업 가능한 밭은 {ServerConcurrentPlotLimit}칸입니다. 먼저 자란 작물을 수확하세요.");
                    return;
                }

                BuySeedAndPlantOnServer(cell, crop);
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

            if (isServerBacked)
            {
                WaterServerPlot(cell, succeeded);
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
        }

        private void ShowRemainingTime(FarmCellState cell)
        {
            long seconds = System.Math.Max(0, cell.readyAtUtc - timeProvider.UtcNowSeconds);
            hud.SetMessage($"밀 성장 중: 약 {seconds}초 남았습니다. 게임을 꺼도 계속 자랍니다.");
        }

        private void Harvest(FarmCellState cell)
        {
            CropDefinition crop = GetCropDefinition(cell.cropId) ?? wheat;
            if (isServerBacked)
            {
                HarvestServerPlot(cell, crop);
                return;
            }

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
        }

        private void LoadFarmFromServer()
        {
            if (!isServerBacked || api == null)
            {
                return;
            }

            serverRequestInFlight = true;
            hud.SetMessage("서버에서 농장 정보를 불러오는 중입니다...");
            api.GetFarm((success, message, farm) =>
            {
                serverRequestInFlight = false;
                if (!success)
                {
                    hud.SetMessage(message);
                    return;
                }

                ApplyServerFarm(farm);
                Commit("서버에 저장된 농장을 불러왔습니다.");
            });
        }

        private void ApplyServerFarm(FarmApiClient.FarmDataResponse farm)
        {
            if (farm == null)
            {
                return;
            }

            RepairCellCollection();
            saveData.money = farm.money;
            saveData.totalWheatHarvested = farm.wheat_harvest_count;
            serverBatchUnlocked = farm.batch_unlocked;
            serverInventory.Clear();

            if (farm.inventory != null)
            {
                foreach (FarmApiClient.InventoryDto item in farm.inventory)
                {
                    if (item != null && !string.IsNullOrEmpty(item.item_type))
                    {
                        serverInventory[item.item_type] = item.quantity;
                    }
                }
            }

            foreach (FarmCellState cell in saveData.cells)
            {
                cell.purchased = false;
                cell.hasWorldPosition = false;
                cell.ClearCrop();
            }

            if (farm.plots == null)
            {
                return;
            }

            foreach (FarmApiClient.PlotDto plot in farm.plots)
            {
                if (plot == null)
                {
                    continue;
                }

                FarmCellState cell = FindCellByPlotIndex(plot.plot_index);
                if (cell == null || plot.unlocked == 0)
                {
                    continue;
                }

                cell.purchased = true;
                if (plot.has_position != 0)
                {
                    cell.hasWorldPosition = true;
                    cell.worldX = plot.world_x;
                    cell.worldY = plot.world_y;
                }
                else
                {
                    EnsureServerPlotPosition(cell);
                }

                if (string.IsNullOrEmpty(plot.crop_type) || plot.state == "empty")
                {
                    cell.ClearCrop();
                    continue;
                }

                cell.cropId = plot.crop_type;
                cell.waterCount = plot.water_count;
                cell.plantedAtUtc = ParseServerTime(plot.planted_at, timeProvider.UtcNowSeconds);
                cell.growthStartedAtUtc = cell.plantedAtUtc;
                cell.readyAtUtc = ParseServerTime(plot.ready_at, timeProvider.UtcNowSeconds);
                if (plot.state == "ready")
                {
                    cell.readyAtUtc = Math.Min(cell.readyAtUtc, timeProvider.UtcNowSeconds);
                }
            }
        }

        private void BuyAndPlaceServerPlot(FarmCellState cell, Vector2 worldPosition)
        {
            serverRequestInFlight = true;
            hud.SetMessage("밭 구매를 서버에 저장하는 중입니다...");
            api.BuyPlot(ToPlotIndex(cell), worldPosition.x, worldPosition.y, (success, message, response) =>
            {
                serverRequestInFlight = false;
                if (!success)
                {
                    HandleServerMutationFailure(message);
                    return;
                }

                saveData.money = response.money;
                cell.purchased = true;
                cell.hasWorldPosition = true;
                cell.worldX = worldPosition.x;
                cell.worldY = worldPosition.y;
                isPlacingPlot = false;
                ClearPlacementPreview();
                Commit($"선택한 위치에 밭을 설치했습니다! (-{response.spent}원)");
                shopPanel?.Open();
            });
        }

        private void BuySeedAndPlantOnServer(FarmCellState cell, CropDefinition crop)
        {
            string seedType = crop.CropId + "_seed";
            serverRequestInFlight = true;
            hud.SetMessage($"{crop.DisplayName} 심기를 서버에 저장하는 중입니다...");

            if (GetServerInventoryQuantity(seedType) > 0)
            {
                PlantServerSeed(cell, crop, seedType);
                return;
            }

            api.BuySeed(seedType, 1, (success, message, response) =>
            {
                if (!success)
                {
                    serverRequestInFlight = false;
                    HandleServerMutationFailure(message);
                    return;
                }

                saveData.money = response.money;
                serverInventory[seedType] = GetServerInventoryQuantity(seedType) + response.quantity;
                PlantServerSeed(cell, crop, seedType);
            });
        }

        private void PlantServerSeed(FarmCellState cell, CropDefinition crop, string seedType)
        {
            api.PlantCrop(ToPlotIndex(cell), seedType, (success, message, response) =>
            {
                serverRequestInFlight = false;
                if (!success)
                {
                    // 구매는 성공하고 심기만 실패했을 수 있다. 인벤토리 수량은 유지해 다음 시도에 재사용한다.
                    Save();
                    RefreshAll();
                    hud.SetMessage(message);
                    return;
                }

                serverInventory[seedType] = Math.Max(0, GetServerInventoryQuantity(seedType) - 1);
                long now = timeProvider.UtcNowSeconds;
                cell.cropId = response.cropType;
                cell.waterCount = 0;
                cell.plantedAtUtc = now;
                cell.growthStartedAtUtc = now;
                cell.readyAtUtc = now + response.growSeconds;
                Commit($"{crop.DisplayName}을(를) 심었습니다. (-{crop.SeedPrice}원)");
            });
        }

        private void WaterServerPlot(FarmCellState cell, bool succeeded)
        {
            serverRequestInFlight = true;
            api.WaterCrop(ToPlotIndex(cell), succeeded, (success, message, response) =>
            {
                serverRequestInFlight = false;
                if (!success)
                {
                    HandleServerMutationFailure(message);
                    return;
                }

                cell.waterCount = response.waterCount;
                cell.readyAtUtc = ParseServerTime(response.readyAt, timeProvider.UtcNowSeconds);
                if (response.state == "ready")
                {
                    cell.readyAtUtc = timeProvider.UtcNowSeconds;
                }

                FarmCellView wateredView = FindView(cell.x, cell.y);
                if (wateredView != null)
                {
                    WateringEffect.Play(wateredView.transform.position, succeeded);
                }

                string result = succeeded ? "성공" : "실패";
                Commit($"물주기 {result}! 서버에 반영했습니다. ({response.waterCount}/{response.maxWaterCount})");
            });
        }

        private void HarvestServerPlot(FarmCellState cell, CropDefinition crop)
        {
            serverRequestInFlight = true;
            hud.SetMessage("수확 결과를 서버에 저장하는 중입니다...");
            api.HarvestCrop(ToPlotIndex(cell), (success, message, response) =>
            {
                serverRequestInFlight = false;
                if (!success)
                {
                    HandleServerMutationFailure(message);
                    return;
                }

                saveData.money = response.money;
                saveData.totalWheatHarvested = response.wheat_harvest_count;
                serverBatchUnlocked = response.batch_unlocked;
                cell.ClearCrop();
                string unlockHint = response.batch_unlocked && response.wheat_harvest_count == 5
                    ? "  밀 5회 수확 달성! 2×2 작업이 해금되었습니다."
                    : string.Empty;
                Commit($"{crop.DisplayName}을(를) 수확해 {response.earned}원을 벌었습니다!{unlockHint}");
            });
        }

        private void DeleteServerPlot(FarmCellState cell)
        {
            serverRequestInFlight = true;
            hud.SetMessage("밭 삭제를 서버에 반영하는 중입니다...");
            api.DeletePlot(ToPlotIndex(cell), (success, message, response) =>
            {
                serverRequestInFlight = false;
                if (!success)
                {
                    HandleServerMutationFailure(message);
                    return;
                }

                cell.purchased = false;
                cell.hasWorldPosition = false;
                cell.ClearCrop();
                Commit("밭을 삭제했습니다. (환급 0원)");
            });
        }

        private void HandleServerMutationFailure(string message)
        {
            RefreshAll();
            hud.SetMessage(message + " 서버 상태를 다시 확인해주세요.");
        }

        private int GetServerInventoryQuantity(string itemType)
        {
            return serverInventory.TryGetValue(itemType, out int quantity) ? quantity : 0;
        }

        private static int ToPlotIndex(FarmCellState cell)
        {
            return cell.y * 3 + cell.x;
        }

        private FarmCellState FindCellByPlotIndex(int plotIndex)
        {
            return plotIndex < 0 || plotIndex >= 9 ? null : FindCell(plotIndex % 3, plotIndex / 3);
        }

        private void EnsureServerPlotPosition(FarmCellState cell)
        {
            if (cell.hasWorldPosition)
            {
                return;
            }

            FarmCellView view = FindView(cell.x, cell.y);
            if (view == null)
            {
                return;
            }

            Vector3 position = view.transform.position;
            cell.hasWorldPosition = true;
            cell.worldX = position.x;
            cell.worldY = position.y;
        }

        private static long ParseServerTime(string value, long fallback)
        {
            return DateTimeOffset.TryParse(value, out DateTimeOffset parsed)
                ? parsed.ToUniversalTime().ToUnixTimeSeconds()
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
                // Server/src/config/cropConfig.js와 동일한 값이어야 UI 가격/시간이 서버 처리와 일치한다.
                potato = CropDefinition.CreateRuntime("potato", "감자", 30, 60, 180, 3);
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
