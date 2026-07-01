using UnityEngine;
using UnityEngine.UI;

namespace FarmGame.UI
{
    public sealed class RuntimeFontInstaller : MonoBehaviour
    {
        private static Font runtimeFont;
        private const string EmbeddedKoreanFontPath = "Fonts/NotoSansKR-VF";

        private void Awake()
        {
            runtimeFont ??= Resources.Load<Font>(EmbeddedKoreanFontPath);
            runtimeFont ??= Font.CreateDynamicFontFromOSFont(
                new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Arial Unicode MS", "Arial" },
                28);

            if (runtimeFont == null)
            {
                return;
            }

            foreach (Text text in GetComponentsInChildren<Text>(true))
            {
                text.font = runtimeFont;
            }
        }
    }
}
