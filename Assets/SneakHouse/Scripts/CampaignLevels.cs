using System;
using System.Collections.Generic;

namespace SneakHouse
{
    public sealed class CampaignLevel
    {
        public int Number { get; }
        public string Name { get; }
        public string MapName { get; }
        public int ShellIndex { get; }
        public int Guards { get; }
        public IReadOnlyList<int> TargetIndices { get; }
        public int TargetCount => TargetIndices.Count;
        public int Cans { get; }
        public float VisionDistance { get; }
        public float FieldOfView { get; }
        public float SearchSeconds { get; }
        public string TacticalHint { get; }
        public int LayoutSeed => 2401 + Number * 137;
        public int ReferenceSeconds => 60 + 45 * TargetCount + 30 * Guards +
            45 * ShellIndex + (ShellIndex == 3 ? 60 : 0);
        // A design index, not a measured probability of player failure.
        public float DifficultyScore => 100 * (.30f * Guards / 4 + .25f * TargetCount / 3 +
            .20f * (ShellIndex + 1) / 4 + .15f * (VisionDistance / 9) * (FieldOfView / 120) +
            .10f * (4 - Cans) / 3);

        internal CampaignLevel(int number, string name, string mapName, int shellIndex,
            int guards, int[] targets, int cans, float vision, float fieldOfView,
            float searchSeconds, string hint)
        {
            Number = number; Name = name; MapName = mapName; ShellIndex = shellIndex;
            Guards = guards; TargetIndices = Array.AsReadOnly((int[])targets.Clone());
            Cans = cans; VisionDistance = vision; FieldOfView = fieldOfView;
            SearchSeconds = searchSeconds; TacticalHint = hint;
        }
    }

    public static class CampaignLevels
    {
        private static readonly CampaignLevel[] levels =
        {
            new CampaignLevel(1, "First Steps", "Collector's House", 0, 1, new[] { 2 }, 4, 6, 90, 4,
                "Watch the living-room patrol. Take the console and return to the entrance."),
            new CampaignLevel(2, "Office Job", "Collector's House", 0, 2, new[] { 2, 0 }, 3, 7, 100, 5,
                "Cross into the office for the drive. The desk gives you a place to hide."),
            new CampaignLevel(3, "Full Collection", "Collector's House", 0, 3, new[] { 2, 0, 1 }, 3, 7.5f, 105, 6,
                "All three rooms are guarded. Choose your pickup order and keep an escape route."),
            new CampaignLevel(4, "Library Detour", "Library Villa", 1, 3, new[] { 2, 0, 1 }, 3, 8, 110, 7,
                "The office sits deep in the library wing. Use corners to break line of sight."),
            new CampaignLevel(5, "Across the Courtyard", "Courtyard Estate", 2, 4, new[] { 2, 0, 1 }, 2, 8.5f, 115, 8,
                "Two wings and a hall patrol. Compare the indoor route with courtyard side doors."),
            new CampaignLevel(6, "The Upstairs Job", "Loft Villa", 3, 4, new[] { 2, 0, 1 }, 1, 9, 120, 9,
                "The drive is upstairs. Check the stairs before climbing and plan the return trip.")
        };
        public static int Count => levels.Length;
        public static CampaignLevel Get(int number)
        {
            if (number < 1 || number > Count) throw new ArgumentOutOfRangeException(nameof(number));
            return levels[number - 1];
        }
    }

    public sealed class CampaignProgress
    {
        public int HighestCompleted { get; }
        public int HighestUnlocked => Math.Min(CampaignLevels.Count, HighestCompleted + 1);
        public CampaignProgress(int highestCompleted)
        {
            HighestCompleted = Math.Max(0, Math.Min(CampaignLevels.Count, highestCompleted));
        }
        public bool IsUnlocked(int number) => number >= 1 && number <= HighestUnlocked;
        public bool IsCompleted(int number) => number >= 1 && number <= HighestCompleted;
        public CampaignProgress Complete(int number) => IsUnlocked(number)
            ? new CampaignProgress(Math.Max(HighestCompleted, number)) : this;
    }
}
