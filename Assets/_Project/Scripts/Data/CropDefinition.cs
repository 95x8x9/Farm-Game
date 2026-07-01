using UnityEngine;

namespace FarmGame.Data
{
    [CreateAssetMenu(fileName = "CropDefinition", menuName = "Farm Game/Crop Definition")]
    public sealed class CropDefinition : ScriptableObject
    {
        [SerializeField] private string cropId = "wheat";
        [SerializeField] private string displayName = "밀";
        [SerializeField, Min(0)] private int seedPrice = 10;
        [SerializeField, Min(0)] private int sellPrice = 20;
        [SerializeField, Min(1)] private int growthSeconds = 60;
        [SerializeField, Min(1)] private int requiredWaterCount = 1;

        public string CropId => cropId;
        public string DisplayName => displayName;
        public int SeedPrice => seedPrice;
        public int SellPrice => sellPrice;
        public int GrowthSeconds => growthSeconds;
        public int RequiredWaterCount => requiredWaterCount;

#if UNITY_EDITOR
        public void Configure(
            string id,
            string cropDisplayName,
            int buyPrice,
            int harvestPrice,
            int seconds,
            int waterCount)
        {
            cropId = id;
            displayName = cropDisplayName;
            seedPrice = buyPrice;
            sellPrice = harvestPrice;
            growthSeconds = seconds;
            requiredWaterCount = waterCount;
        }
#endif
    }
}
