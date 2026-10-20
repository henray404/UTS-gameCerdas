using System;
using System.Collections.Generic;

namespace SneakHouse
{
    [Serializable]
    public class ScoreEntry
    {
        public const string LegacyDate = "legacy";
        public int score;
        public float time;
        public int alerts;
        public string date = "";

        public bool Timed => time > 0 && date != LegacyDate;

        public ScoreEntry() { }
        public ScoreEntry(int score, float time, int alerts, string date)
        {
            this.score = Math.Max(0, score); this.time = Math.Max(0, time);
            this.alerts = Math.Max(0, alerts); this.date = date ?? "";
        }
        public static ScoreEntry Legacy(int score) => new ScoreEntry(score, 0, 0, LegacyDate);
    }

    public struct RecordResult
    {
        public bool NewBestScore;
        public bool NewBestTime;
        public int ScoreRank;
        public int TimeRank;
    }

    [Serializable]
    public class LevelRecord
    {
        public const int Keep = 5;
        public int level;
        public int bestScore;
        public float bestTime;
        public List<ScoreEntry> scores = new List<ScoreEntry>();
        public List<ScoreEntry> times = new List<ScoreEntry>();

        public bool HasTime => bestTime > 0;

        public static int CompareScore(ScoreEntry a, ScoreEntry b)
        {
            int order = b.score.CompareTo(a.score);
            if (order != 0) return order;
            if (a.Timed != b.Timed) return a.Timed ? -1 : 1;
            order = a.time.CompareTo(b.time);
            return order != 0 ? order : string.CompareOrdinal(a.date, b.date);
        }
        public static int CompareTime(ScoreEntry a, ScoreEntry b)
        {
            int order = a.time.CompareTo(b.time);
            if (order != 0) return order;
            order = b.score.CompareTo(a.score);
            return order != 0 ? order : string.CompareOrdinal(a.date, b.date);
        }

        public RecordResult Record(ScoreEntry entry)
        {
            Normalize();
            RecordResult result = new RecordResult { ScoreRank = -1, TimeRank = -1 };
            if (entry == null) return result;
            result.NewBestScore = entry.score > bestScore;
            result.NewBestTime = entry.Timed && (!HasTime || entry.time < bestTime);
            if (result.NewBestScore) bestScore = entry.score;
            if (result.NewBestTime) bestTime = entry.time;
            result.ScoreRank = Insert(scores, entry, CompareScore);
            if (entry.Timed) result.TimeRank = Insert(times, entry, CompareTime);
            return result;
        }

        public bool MigrateLegacy(int legacyScore)
        {
            Normalize();
            if (legacyScore <= bestScore) return false;
            Record(ScoreEntry.Legacy(legacyScore));
            return true;
        }

        public void Normalize()
        {
            if (scores == null) scores = new List<ScoreEntry>();
            if (times == null) times = new List<ScoreEntry>();
            scores.RemoveAll(e => e == null);
            times.RemoveAll(e => e == null || !e.Timed);
            scores.Sort(CompareScore);
            times.Sort(CompareTime);
            if (scores.Count > Keep) scores.RemoveRange(Keep, scores.Count - Keep);
            if (times.Count > Keep) times.RemoveRange(Keep, times.Count - Keep);
            bestScore = Math.Max(Math.Max(0, bestScore), scores.Count > 0 ? scores[0].score : 0);
            if (bestTime < 0) bestTime = 0;
            if (times.Count > 0 && (!HasTime || times[0].time < bestTime)) bestTime = times[0].time;
        }

        private static int Insert(List<ScoreEntry> list, ScoreEntry entry, Comparison<ScoreEntry> compare)
        {
            int index = 0;
            while (index < list.Count && compare(list[index], entry) <= 0) index++;
            if (index >= Keep) return -1;
            list.Insert(index, entry);
            if (list.Count > Keep) list.RemoveRange(Keep, list.Count - Keep);
            return index;
        }
    }

    [Serializable]
    public class ScoreProfile
    {
        public const string DefaultPlayer = "Agent";
        public int version = 2;
        public string player = DefaultPlayer;
        public List<LevelRecord> levels = new List<LevelRecord>();

        public string PlayerName => string.IsNullOrWhiteSpace(player) ? DefaultPlayer : player.Trim();

        public LevelRecord Level(int number)
        {
            if (levels == null) levels = new List<LevelRecord>();
            foreach (LevelRecord record in levels)
                if (record != null && record.level == number) return record;
            LevelRecord created = new LevelRecord { level = number };
            levels.Add(created);
            return created;
        }
        public LevelRecord Find(int number)
        {
            if (levels == null) return null;
            foreach (LevelRecord record in levels)
                if (record != null && record.level == number) return record;
            return null;
        }
        public int BestScore(int number) { LevelRecord record = Find(number); return record == null ? 0 : record.bestScore; }
        public float BestTime(int number) { LevelRecord record = Find(number); return record == null ? 0 : record.bestTime; }
        public RecordResult Record(int number, ScoreEntry entry) => Level(number).Record(entry);

        public int TotalBestScore(int levelCount)
        {
            int total = 0;
            for (int i = 1; i <= levelCount; i++) total += BestScore(i);
            return total;
        }
        public float TotalBestTime(int levelCount)
        {
            float total = 0;
            for (int i = 1; i <= levelCount; i++) if (BestTime(i) > 0) total += BestTime(i);
            return total;
        }
        public int TimedLevels(int levelCount)
        {
            int count = 0;
            for (int i = 1; i <= levelCount; i++) if (BestTime(i) > 0) count++;
            return count;
        }

        public void Normalize()
        {
            if (levels == null) levels = new List<LevelRecord>();
            levels.RemoveAll(r => r == null || r.level < 1);
            List<LevelRecord> merged = new List<LevelRecord>();
            foreach (LevelRecord record in levels)
            {
                record.Normalize();
                LevelRecord existing = merged.Find(r => r.level == record.level);
                if (existing == null) { merged.Add(record); continue; }
                existing.scores.AddRange(record.scores);
                existing.times.AddRange(record.times);
                existing.bestScore = Math.Max(existing.bestScore, record.bestScore);
                if (record.HasTime && (!existing.HasTime || record.bestTime < existing.bestTime)) existing.bestTime = record.bestTime;
                existing.Normalize();
            }
            merged.Sort((a, b) => a.level.CompareTo(b.level));
            levels = merged;
            version = 2;
        }
    }
}
