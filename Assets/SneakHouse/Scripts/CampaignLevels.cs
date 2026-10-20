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
        public float SuspicionRate { get; }
        public float ChaseSpeed { get; }
        public float HearingSuspicion { get; }
        public float LoseSightSeconds { get; }
        public string TacticalHint { get; }
        public int LayoutSeed => 2401 + Number * 137;
        public int ReferenceSeconds => 60 + 45 * TargetCount + 30 * Guards +
            45 * ShellIndex + (ShellIndex == 3 ? 60 : 0);
        // A design index, not a measured probability of player failure.
        public float DifficultyScore => Math.Min(100f, 100 * (.30f * Guards / 4 + .25f * TargetCount / 3 +
            .20f * (ShellIndex + 1) / 4 + .15f * (VisionDistance / 9) * (FieldOfView / 120) +
            .10f * (4 - Cans) / 3));

        internal CampaignLevel(int number, string name, string mapName, int shellIndex,
            int guards, int[] targets, int cans, float vision, float fieldOfView,
            float searchSeconds, float suspicionRate, float chaseSpeed,
            float hearingSuspicion, float loseSightSeconds, string hint)
        {
            Number = number; Name = name; MapName = mapName; ShellIndex = shellIndex;
            Guards = guards; TargetIndices = Array.AsReadOnly((int[])targets.Clone());
            Cans = cans; VisionDistance = vision; FieldOfView = fieldOfView;
            SearchSeconds = searchSeconds; SuspicionRate = suspicionRate;
            ChaseSpeed = chaseSpeed; HearingSuspicion = hearingSuspicion;
            LoseSightSeconds = loseSightSeconds; TacticalHint = hint;
        }
    }

    public static class CampaignLevels
    {
        private static readonly CampaignLevel[] levels =
        {
            new CampaignLevel(1, "First Steps", "Collector's House", 0, 1, new[] { 2 }, 5, 4.5f, 70, 3,
                .55f, 2.35f, .18f, 1.10f,
                "Stay crouched. The guard is slow to notice movement. Take the console and return to the entrance."),
            new CampaignLevel(2, "Office Job", "Collector's House", 0, 2, new[] { 2, 0 }, 4, 5, 80, 3.5f,
                .62f, 2.45f, .20f, 1.00f,
                "The office is watched too. Wait for the patrol to turn, take the drive, and use the desk to hide."),
            new CampaignLevel(3, "Full Collection", "Collector's House", 0, 3, new[] { 2, 0, 1 }, 4, 5.5f, 85, 4.5f,
                .70f, 2.55f, .23f, .95f,
                "All three rooms are guarded. Pick a safe order, then collect the console, drive and watch."),
            new CampaignLevel(4, "Library Detour", "Library Villa", 1, 3, new[] { 2, 0, 1 }, 3, 6.5f, 95, 5.5f,
                .82f, 2.75f, .27f, .85f,
                "The office sits deep in the library wing. Use corners to break line of sight and keep a return route."),
            new CampaignLevel(5, "Across the Courtyard", "Courtyard Estate", 2, 4, new[] { 2, 0, 1 }, 3, 7.5f, 105, 7,
                .93f, 2.90f, .31f, .78f,
                "Two wings and a hall patrol. Compare the indoor route with the courtyard side doors."),
            new CampaignLevel(6, "The Upstairs Job", "Loft Villa", 3, 4, new[] { 2, 0, 1 }, 1, 9, 120, 9,
                1.05f, 3.05f, .35f, .70f,
                "The drive is upstairs. Check the stairs before climbing, use hiding spots, and plan the return trip.")
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
