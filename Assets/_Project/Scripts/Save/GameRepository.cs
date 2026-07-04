using System.Text;
using FarmGame.Data;
using UnityEngine;

namespace FarmGame.Save
{
    public interface IGameRepository
    {
        bool TryLoad(out PlayerSaveData data);
        void Save(PlayerSaveData data);
        void Delete();
    }

    public sealed class PlayerPrefsGameRepository : IGameRepository
    {
        private const string SaveKey = "farm_game.prototype.save.v1";
        private readonly string scopedSaveKey;

        public PlayerPrefsGameRepository(string ownerKey = null)
        {
            scopedSaveKey = BuildSaveKey(ownerKey);
        }

        public bool TryLoad(out PlayerSaveData data)
        {
            data = null;
            if (!PlayerPrefs.HasKey(scopedSaveKey))
            {
                return false;
            }

            string json = PlayerPrefs.GetString(scopedSaveKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                data = JsonUtility.FromJson<PlayerSaveData>(json);
                return data != null && data.schemaVersion is >= 1 and <= 2;
            }
            catch
            {
                data = null;
                return false;
            }
        }

        public void Save(PlayerSaveData data)
        {
            PlayerPrefs.SetString(scopedSaveKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        public void Delete()
        {
            PlayerPrefs.DeleteKey(scopedSaveKey);
            PlayerPrefs.Save();
        }

        private static string BuildSaveKey(string ownerKey)
        {
            if (string.IsNullOrWhiteSpace(ownerKey))
            {
                return SaveKey;
            }

            StringBuilder builder = new($"{SaveKey}.user.");
            foreach (char character in ownerKey.Trim().ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(character) || character is '-' or '_' or '.')
                {
                    builder.Append(character);
                }
                else
                {
                    builder.Append('_');
                }
            }

            return builder.ToString();
        }
    }
}
