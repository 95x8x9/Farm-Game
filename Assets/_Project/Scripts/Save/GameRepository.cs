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

        public bool TryLoad(out PlayerSaveData data)
        {
            data = null;
            if (!PlayerPrefs.HasKey(SaveKey))
            {
                return false;
            }

            string json = PlayerPrefs.GetString(SaveKey, string.Empty);
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
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        public void Delete()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
        }
    }
}
