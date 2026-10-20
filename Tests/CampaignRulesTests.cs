using System;
using SneakHouse;

public static class CampaignRulesTests
{
    private static int checks;
    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }
    public static int Main()
    {
        var fresh = new CampaignProgress(0);
        Check(fresh.HighestUnlocked == 1, "New profile must unlock only level 1");
        Check(!fresh.IsUnlocked(0) && !fresh.IsUnlocked(7), "Invalid level must be locked");
        Check(!fresh.IsUnlocked(2), "Cannot skip first level");
        Check(fresh.Complete(2).HighestCompleted == 0, "Locked level cannot advance progress");
        Check(fresh.Complete(0).HighestCompleted == 0, "Invalid completion must be ignored");
        Check(fresh.HighestCompleted == 0, "Progress must remain immutable");
        var progress = fresh;
        float previous = 0, previousVision = 0, previousFieldOfView = 0;
        float previousSuspicion = 0, previousChase = 0;
        float previousLoseSight = 0, previousIdleMax = 99, previousIdleChance = 99, previousHearing = 0;
        float previousError = 99, previousAlert = 0, previousWalkSeconds = 99, previousSneakSeconds = 99;
        for (int i = 1; i <= CampaignLevels.Count; i++)
        {
            var level = CampaignLevels.Get(i);
            Check(level.Number == i, "Catalog identity");
            Check(level.ShellIndex == (i <= 3 ? 0 : i - 3), "Map progression");
            Check(level.TargetCount == Math.Min(i, 3), "Objective progression");
            Check(level.Guards == new[] { 1, 2, 3, 3, 4, 4 }[i - 1], "Guard progression");
            Check(level.TargetIndices.Count == level.TargetCount, "Target contract");
            Check(level.DifficultyScore > previous, "Difficulty must rise strictly");
            Check(level.DifficultyScore <= 100, "Difficulty must be bounded");
            Check(level.VisionDistance > previousVision, "Sight range must rise");
            Check(level.FieldOfView > previousFieldOfView, "Field of view must rise");
            Check(level.SuspicionRate > previousSuspicion, "Detection speed must rise");
            Check(level.ChaseSpeed > previousChase, "Chase speed must rise");
            Check(level.Cans >= 2 && level.Cans <= 5, "Can supply must stay bounded");
            Check(level.LoseSightSeconds > previousLoseSight, "Chasers must persist longer on harder levels");
            Check(level.IdleMin > 0 && level.IdleMin < level.IdleMax, "Idle range must be positive");
            Check(level.IdleMax < previousIdleMax, "Idle posts must shorten as levels rise");
            Check(level.IdleChance > 0 && level.IdleChance <= previousIdleChance, "Idle chance must not rise");
            Check(level.HearingScale >= previousHearing && level.HearingScale >= .5f && level.HearingScale <= 1, "Hearing must sharpen");
            Check(level.HearingError <= previousError, "Investigation error must shrink");
            Check(level.AlertRadius >= previousAlert, "Alerts must carry further");
            float walk = level.SecondsToDetect(3, false), sneak = level.SecondsToDetect(3, true);
            Check(walk < previousWalkSeconds && sneak < previousSneakSeconds, "Time to detect must fall every level");
            Check(sneak > walk, "Sneaking must delay detection");
            Check(walk >= 1f, "Never detect faster than one second at 3 m");
            Check(level.ReferenceSeconds == 60 + 45 * level.TargetCount + 30 * level.Guards +
                45 * level.ShellIndex + (level.ShellIndex == 3 ? 60 : 0), "Time calculation");
            Check(progress.IsUnlocked(i), "Previous win unlocks next");
            var won = progress.Complete(i);
            Check(won.IsCompleted(i), "Win marks level completed");
            Check(progress.HighestCompleted == i - 1, "Completion returns new state");
            Check(won.Complete(1).HighestCompleted == i, "Replay never lowers progress");
            Check(new CampaignProgress(won.HighestCompleted).HighestCompleted == i, "Save reload");
            previous = level.DifficultyScore; previousVision = level.VisionDistance;
            previousFieldOfView = level.FieldOfView; previousSuspicion = level.SuspicionRate;
            previousChase = level.ChaseSpeed; previousLoseSight = level.LoseSightSeconds;
            previousIdleMax = level.IdleMax; previousIdleChance = level.IdleChance;
            previousHearing = level.HearingScale; previousError = level.HearingError;
            previousAlert = level.AlertRadius; previousWalkSeconds = walk; previousSneakSeconds = sneak;
            progress = won;
        }
        Check(CampaignLevels.Get(1).VisionDistance <= 5 && CampaignLevels.Get(1).FieldOfView <= 75 &&
            CampaignLevels.Get(1).SuspicionRate <= .6f, "Tutorial guard must be forgiving");
        Check(CampaignLevels.Get(1).Cans >= 4, "Tutorial must stock spare cans");
        Check(progress.HighestUnlocked == 6, "Final level must not create level 7");
        Check(new CampaignProgress(-20).HighestCompleted == 0, "Negative save clamps to fresh");
        Check(new CampaignProgress(999).HighestCompleted == 6, "Oversized save clamps");
        Check(CampaignLevels.Get(1).TargetIndices[0] == 2, "Tutorial target must be living-room console");
        Check(CampaignLevels.Get(2).TargetIndices[1] == 0, "Second target must be office drive");
        Check(CampaignLevels.Get(6).DifficultyScore > 90 && CampaignLevels.Get(6).DifficultyScore <= 100, "Final index");
        Check(CampaignLevels.Get(1).SecondsToDetect(3, false) >= 3.5f && CampaignLevels.Get(1).SecondsToDetect(3, true) >= 6f,
            "Tutorial guard gives several seconds in plain view");
        Check(CampaignLevels.Get(1).IdleMin >= 5f && CampaignLevels.Get(1).HearingScale <= .6f, "Tutorial guard rests and hears little");
        // Pickup spots (x, z, floor y) measured on each level's NavMesh for its fixed layout seed.
        float[][][] pickups =
        {
            new[] { new[] { 4.85f, -4.70f, .06f } },
            new[] { new[] { 5.30f, -2.70f, .06f }, new[] { -2.65f, 4.90f, .06f } },
            new[] { new[] { 5.50f, -3.60f, .06f }, new[] { -2.65f, 4.90f, .06f }, new[] { 5.85f, 3.55f, .06f } },
            new[] { new[] { 4.85f, -4.70f, .06f }, new[] { -4.15f, 14.65f, .06f }, new[] { 5.85f, 3.55f, .06f } },
            new[] { new[] { 5.50f, -3.60f, .06f }, new[] { -5.15f, 14.65f, .06f }, new[] { 5.80f, 10.35f, .06f } },
            new[] { new[] { 4.85f, -4.70f, .06f }, new[] { -4.90f, 11.20f, 3.66f }, new[] { 5.00f, 14.00f, .06f } }
        };
        for (int level = 1; level <= CampaignLevels.Count; level++)
        {
            var info = CampaignLevels.Get(level);
            var beats = PatrolPlans.For(level);
            Check(beats != null && beats.Count == info.Guards, "One patrol beat per guard, level " + level);
            foreach (var beat in beats)
            {
                Check(beat.Home.Count >= 4 && beat.Peek.Count >= 1, "Beat has a home loop and a peek, level " + level);
                Check(beat.PeekEvery >= 2 && beat.PeekEvery <= 3, "Peek cadence 2-3 laps, level " + level);
                Check(beat.Floor == (beat.Zone == "Upper office" ? 3.66f : .06f), "Beat floor height, level " + level);
                foreach (var spot in pickups[level - 1])
                {
                    if (Math.Abs(spot[2] - beat.Floor) > 1.5f) continue;
                    foreach (var p in beat.Home)
                        Check(Math.Sqrt((p.X - spot[0]) * (p.X - spot[0]) + (p.Z - spot[1]) * (p.Z - spot[1])) >= info.PatrolClearance,
                            "Home loop stays clear of the pickup, level " + level + " " + beat.Zone);
                }
            }
            foreach (var spot in pickups[level - 1])
            {
                bool visited = false;
                foreach (var beat in beats)
                {
                    if (Math.Abs(spot[2] - beat.Floor) > 1.5f) continue;
                    foreach (var p in beat.Peek)
                        visited |= Math.Sqrt((p.X - spot[0]) * (p.X - spot[0]) + (p.Z - spot[1]) * (p.Z - spot[1])) <= 3f;
                }
                Check(visited, "Some guard peeks at every pickup, level " + level);
            }
        }
        Check(PatrolPlans.For(0) == null && PatrolPlans.For(7) == null, "No beats for unknown levels");
        bool rejected = false;
        try { CampaignLevels.Get(0); } catch (ArgumentOutOfRangeException) { rejected = true; }
        Check(rejected, "Catalog rejects invalid request");
        Console.WriteLine("PASS: " + checks + " campaign assertions");
        return 0;
    }
}
