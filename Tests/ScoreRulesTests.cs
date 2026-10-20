using System;
using SneakHouse;

public static class ScoreRulesTests
{
    private static int checks;
    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }
    public static int Main()
    {
        ScoreBreakdown loss = ScoreRules.Calculate(false, 0, 10, 300);
        Check(loss.Total == 0 && loss.Objective == 0 && loss.Stealth == 0 && loss.Time == 0, "Loss scores nothing");
        ScoreBreakdown perfect = ScoreRules.Calculate(true, 0, 0, 300);
        Check(perfect.Objective == 6000, "Objective points");
        Check(perfect.Stealth == 2500, "Full stealth bonus");
        Check(perfect.Time == 1500, "Full time bonus");
        Check(perfect.Total == ScoreRules.Max && ScoreRules.Max == 10000, "Maximum score");
        Check(ScoreRules.Calculate(true, 1, 0, 300).Stealth == 2000, "One alert costs 500");
        Check(ScoreRules.Calculate(true, 5, 0, 300).Stealth == 0, "Five alerts empty stealth");
        Check(ScoreRules.Calculate(true, 9, 0, 300).Stealth == 0, "Stealth never negative");
        Check(ScoreRules.Calculate(true, -3, 0, 300).Stealth == 2500, "Negative alerts clamp");
        Check(ScoreRules.Calculate(true, 0, 150, 300).Time == 750, "Half reference halves time bonus");
        Check(ScoreRules.Calculate(true, 0, 300, 300).Time == 0, "Reference time ends bonus");
        Check(ScoreRules.Calculate(true, 0, 900, 300).Time == 0, "Slow win keeps zero bonus");
        Check(ScoreRules.Calculate(true, 0, 900, 300).Total == 8500, "Slow win still scores");
        Check(ScoreRules.Calculate(true, 0, -5, 300).Time == 1500, "Negative elapsed clamps");
        Check(ScoreRules.Calculate(true, 0, 0, 0).Time == 1500, "Zero reference is safe");
        Check(ScoreRules.Calculate(true, 0, 100, 300).Time > ScoreRules.Calculate(true, 0, 200, 300).Time, "Faster is better");

        var record = new LevelRecord { level = 1 };
        RecordResult first = record.Record(new ScoreEntry(9000, 120, 2, "2026-10-01"));
        Check(first.NewBestScore && first.NewBestTime, "First win sets both records");
        Check(first.ScoreRank == 0 && first.TimeRank == 0, "First win ranks first");
        Check(record.bestScore == 9000 && Math.Abs(record.bestTime - 120) < .001f, "Best values stored");
        RecordResult slower = record.Record(new ScoreEntry(9500, 150, 1, "2026-10-02"));
        Check(slower.NewBestScore && !slower.NewBestTime, "Higher but slower run");
        Check(record.scores[0].score == 9500 && record.times[0].time == 120, "Boards rank independently");
        RecordResult faster = record.Record(new ScoreEntry(8000, 90, 3, "2026-10-03"));
        Check(!faster.NewBestScore && faster.NewBestTime, "Faster but lower run");
        Check(Math.Abs(record.bestTime - 90) < .001f && record.bestScore == 9500, "Bests track separately");
        Check(faster.ScoreRank == 2 && faster.TimeRank == 0, "Ranks reported per board");

        var ties = new LevelRecord { level = 2 };
        ties.Record(new ScoreEntry(9000, 100, 0, "2026-10-05"));
        ties.Record(new ScoreEntry(9000, 80, 0, "2026-10-06"));
        ties.Record(new ScoreEntry(9000, 80, 0, "2026-10-04"));
        Check(ties.scores[0].time == 80 && ties.scores[0].date == "2026-10-04", "Score tie: faster then earlier");
        Check(ties.scores[2].time == 100, "Score tie: slowest last");
        var timeTies = new LevelRecord { level = 3 };
        timeTies.Record(new ScoreEntry(7000, 60, 0, "2026-10-01"));
        timeTies.Record(new ScoreEntry(9000, 60, 0, "2026-10-02"));
        Check(timeTies.times[0].score == 9000, "Time tie: higher score first");

        var full = new LevelRecord { level = 4 };
        for (int i = 0; i < 8; i++) full.Record(new ScoreEntry(8000 + i * 100, 200 - i * 10, 0, "2026-10-1" + i));
        Check(full.scores.Count == LevelRecord.Keep && full.times.Count == LevelRecord.Keep, "Top five kept");
        Check(full.scores[0].score == 8700 && full.scores[4].score == 8300, "Top five scores ordered");
        Check(full.times[0].time == 130 && full.times[4].time == 170, "Top five times ordered");
        RecordResult outside = full.Record(new ScoreEntry(100, 999, 0, "2026-10-20"));
        Check(outside.ScoreRank == -1 && outside.TimeRank == -1, "Weak run stays off boards");
        Check(full.scores.Count == 5 && full.times.Count == 5, "Boards stay bounded");

        var legacy = new LevelRecord { level = 5 };
        Check(legacy.MigrateLegacy(8800), "Legacy best migrates into empty record");
        Check(legacy.bestScore == 8800 && legacy.scores.Count == 1, "Legacy enters score board");
        Check(legacy.times.Count == 0 && !legacy.HasTime, "Legacy never enters speed board");
        Check(!legacy.scores[0].Timed && legacy.scores[0].date == ScoreEntry.LegacyDate, "Legacy entry marked");
        Check(!legacy.MigrateLegacy(8800), "Equal legacy is not duplicated");
        Check(!legacy.MigrateLegacy(7000), "Lower legacy ignored");
        legacy.Record(new ScoreEntry(8800, 140, 0, "2026-10-09"));
        Check(legacy.scores[0].Timed, "Timed run beats legacy at equal score");
        Check(Math.Abs(legacy.bestTime - 140) < .001f && legacy.times.Count == 1, "Timed run sets speed board");
        var already = new LevelRecord { level = 6 };
        already.Record(new ScoreEntry(9900, 100, 0, "2026-10-09"));
        Check(!already.MigrateLegacy(9000), "Legacy below v2 best ignored");

        var dirty = new LevelRecord { level = 1, bestTime = -4, scores = null, times = null };
        dirty.Normalize();
        Check(dirty.scores != null && dirty.times != null && dirty.bestTime == 0, "Corrupt record normalizes");
        var spoofed = new LevelRecord { level = 1 };
        spoofed.times.Add(ScoreEntry.Legacy(9999));
        spoofed.Normalize();
        Check(spoofed.times.Count == 0 && !spoofed.HasTime, "Untimed entries removed from speed board");

        var profile = new ScoreProfile();
        Check(profile.PlayerName == "Agent", "Default local profile name");
        profile.player = "   ";
        Check(profile.PlayerName == "Agent", "Blank profile name falls back");
        profile.player = "  Rina ";
        Check(profile.PlayerName == "Rina", "Profile name trimmed");
        Check(profile.TotalBestScore(6) == 0 && profile.TimedLevels(6) == 0 && profile.TotalBestTime(6) == 0, "Empty totals");
        profile.Record(1, new ScoreEntry(9000, 60, 0, "2026-10-10"));
        profile.Record(2, new ScoreEntry(8500, 90, 1, "2026-10-10"));
        profile.Level(3).MigrateLegacy(7000);
        Check(profile.TotalBestScore(6) == 24500, "Total best score sums levels");
        Check(Math.Abs(profile.TotalBestTime(6) - 150) < .001f, "Speed total sums timed levels only");
        Check(profile.TimedLevels(6) == 2, "Timed level count");
        Check(profile.BestScore(4) == 0 && profile.BestTime(4) == 0 && profile.Find(4) == null, "Missing level reads empty");
        Check(profile.Level(1) == profile.Find(1), "Level lookup is stable");

        var duplicate = new ScoreProfile();
        duplicate.levels.Add(new LevelRecord { level = 2 });
        duplicate.levels[0].Record(new ScoreEntry(7000, 100, 0, "2026-10-01"));
        duplicate.levels.Add(new LevelRecord { level = 2 });
        duplicate.levels[1].Record(new ScoreEntry(8000, 120, 0, "2026-10-02"));
        duplicate.levels.Add(null);
        duplicate.levels.Add(new LevelRecord { level = 0 });
        duplicate.Normalize();
        Check(duplicate.levels.Count == 1, "Duplicate and invalid levels merge");
        Check(duplicate.BestScore(2) == 8000 && Math.Abs(duplicate.BestTime(2) - 100) < .001f, "Merged bests kept");
        Check(duplicate.levels[0].scores.Count == 2 && duplicate.levels[0].times.Count == 2, "Merged boards kept");

        Console.WriteLine("PASS: " + checks + " score assertions");
        return 0;
    }
}
