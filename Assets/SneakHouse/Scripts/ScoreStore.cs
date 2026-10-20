using System;
using UnityEngine;

namespace SneakHouse
{
    public static class ScoreStore
    {
        public const string Key = "SneakHouse.Scores.v2";
        public const string LegacyPrefix = "SneakHouse.Score.Campaign.v1.Level.";

        public static string LegacyKey(int level) => LegacyPrefix + level;

        public static ScoreProfile Load()
        {
            ScoreProfile profile = null;
            string json = PlayerPrefs.GetString(Key, "");
            if (!string.IsNullOrEmpty(json))
            {
                try { profile = JsonUtility.FromJson<ScoreProfile>(json); }
                catch (ArgumentException) { profile = null; }
            }
            if (profile == null) profile = new ScoreProfile();
            profile.Normalize();
            for (int i = 1; i <= CampaignLevels.Count; i++)
            {
                int legacy = PlayerPrefs.GetInt(LegacyKey(i), 0);
                if (legacy > 0) profile.Level(i).MigrateLegacy(legacy);
            }
            return profile;
        }

        public static void Save(ScoreProfile profile)
        {
            if (profile == null) return;
            profile.Normalize();
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(profile));
        }

        public static void Delete()
        {
            PlayerPrefs.DeleteKey(Key);
            for (int i = 1; i <= CampaignLevels.Count; i++) PlayerPrefs.DeleteKey(LegacyKey(i));
        }
    }
}
