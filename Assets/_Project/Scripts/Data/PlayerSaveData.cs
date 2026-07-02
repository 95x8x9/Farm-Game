using System;
using System.Collections.Generic;

namespace FarmGame.Data
{
    public enum FarmCellStatus
    {
        Locked,
        Empty,
        NeedsWater,
        Growing,
        Ready
    }

    [Serializable]
    public sealed class FarmCellState
    {
        public int x;
        public int y;
        public bool purchased;
        public bool hasWorldPosition;
        public float worldX;
        public float worldY;
        public string cropId;
        public int waterCount;
        public long plantedAtUtc;
        public long growthStartedAtUtc;
        public long readyAtUtc;

        public FarmCellStatus GetStatus(CropDefinition crop, long nowUtc)
        {
            if (!purchased)
            {
                return FarmCellStatus.Locked;
            }

            if (string.IsNullOrEmpty(cropId))
            {
                return FarmCellStatus.Empty;
            }

            if (nowUtc >= readyAtUtc)
            {
                return FarmCellStatus.Ready;
            }

            return waterCount < GetMaxWaterCount(crop) ? FarmCellStatus.NeedsWater : FarmCellStatus.Growing;
        }

        public static int GetMaxWaterCount(CropDefinition crop)
        {
            return Math.Max(1, (int)Math.Ceiling(crop.GrowthSeconds / 60.0));
        }

        public void ClearCrop()
        {
            cropId = string.Empty;
            waterCount = 0;
            plantedAtUtc = 0;
            growthStartedAtUtc = 0;
            readyAtUtc = 0;
        }
    }

    [Serializable]
    public sealed class PlayerSaveData
    {
        public int schemaVersion = 2;
        public int money = 500;
        public int totalWheatHarvested;
        public List<FarmCellState> cells = new();
        public long lastSavedAtUtc;
    }
}
