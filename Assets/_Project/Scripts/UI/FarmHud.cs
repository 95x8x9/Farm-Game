using UnityEngine;
using UnityEngine.UI;

namespace FarmGame.UI
{
    public sealed class FarmHud : MonoBehaviour
    {
        [SerializeField] private Text moneyText;
        [SerializeField] private Text harvestText;
        [SerializeField] private Text messageText;

        public void Configure(Text money, Text harvest, Text message)
        {
            moneyText = money;
            harvestText = harvest;
            messageText = message;
        }

        public void Refresh(int money, int harvested)
        {
            moneyText.text = $"보유금  {money:N0}원";
            harvestText.text = $"밀 수확  {harvested}회";
        }

        public void SetMessage(string message)
        {
            messageText.text = message;
        }
    }
}
