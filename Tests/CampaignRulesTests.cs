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
            Check(level.Cans >= 1 && level.Cans <= 5, "Can supply must stay bounded");
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
            previousChase = level.ChaseSpeed;
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
        Check(Math.Abs(CampaignLevels.Get(6).DifficultyScore - 100) < .01, "Final index");
        bool rejected = false;
        try { CampaignLevels.Get(0); } catch (ArgumentOutOfRangeException) { rejected = true; }
        Check(rejected, "Catalog rejects invalid request");
        Console.WriteLine("PASS: " + checks + " campaign assertions");
        return 0;
    }
}
