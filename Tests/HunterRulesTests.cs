using System;
using System.Collections.Generic;
using SneakHouse;

public static class HunterRulesTests
{
    private static int checks;
    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }
    private static HunterSituation Sample() => new HunterSituation
    {
        ToLastKnownX = 6, ToLastKnownZ = -8, MemoryAge = 3, Memory = MemoryKind.Vision, Suspicion = .9f,
        LastSeenVelocityX = 4.2f, LastSeenVelocityZ = -9, LastKnownToExitX = -30, LastKnownToExitZ = 0,
        HasTarget = true, ToTargetX = 0, ToTargetZ = 5, CollectedRatio = 1f / 3,
        HasHidingSpot = true, LastKnownToHidingX = 1, LastKnownToHidingZ = 1, OtherGuardsHunting = 5,
        HasPrevious = true, Previous = HunterAction.AmbushTarget
    };
    public static int Main()
    {
        // Contract sizes are baked into trained tables and models.
        Check(Enum.GetValues(typeof(HunterAction)).Length == HunterContract.ActionCount, "Action count");
        Check((int)HunterAction.GoLastKnown == 0 && (int)HunterAction.GiveUp == 5, "Action order");
        Check(Enum.GetValues(typeof(MemoryKind)).Length == 4, "Memory kinds");

        float[] o = new float[HunterContract.ObservationSize];
        HunterSituation s = Sample();
        Check(HunterContract.Write(s, o) == HunterContract.ObservationSize, "Write fills exactly the observation size");
        foreach (float v in o) Check(v >= -1 && v <= 1 && !float.IsNaN(v), "Observation in [-1, 1]");
        Check(Math.Abs(o[0] - .6f) < 1e-5f && Math.Abs(o[1] + .8f) < 1e-5f && Math.Abs(o[2] - .5f) < 1e-5f, "Direction and distance to last known");
        Check(Math.Abs(o[3] - .2f) < 1e-5f, "Memory age normalized by horizon");
        Check(o[4] == 1 && o[5] == 0 && o[6] == 0 && o[7] == 0, "Memory one-hot");
        Check(o[9] == 1 && o[10] == -1, "Velocity clamped to run speed");
        Check(o[11] == -1 && o[13] == 1, "Exit direction and clamped distance");
        Check(o[21] == 1, "Other guards clamped");
        Check(o[22 + (int)HunterAction.AmbushTarget] == 1 && Sum(o, 22, 6) == 1, "Previous action one-hot");

        s.HasTarget = false; s.HasHidingSpot = false; s.HasPrevious = false;
        HunterContract.Write(s, o);
        Check(o[14] == 0 && o[15] == 0 && o[16] == 0, "No target writes zeros");
        Check(o[18] == 0 && o[19] == 0 && o[20] == 0, "No hiding spot writes zeros");
        Check(Sum(o, 22, 6) == 0, "No previous action writes zeros");
        var zero = new HunterSituation();
        HunterContract.Write(zero, o);
        foreach (float v in o) Check(!float.IsNaN(v), "Zero vectors never produce NaN");
        bool threw = false;
        try { HunterContract.Write(zero, new float[10]); } catch (ArgumentException) { threw = true; }
        Check(threw, "Short buffer rejected");

        // Masking.
        s = Sample();
        foreach (HunterAction a in Enum.GetValues(typeof(HunterAction))) Check(HunterContract.IsAllowed(s, a), "All actions allowed with full knowledge: " + a);
        s.HasTarget = false; Check(!HunterContract.IsAllowed(s, HunterAction.AmbushTarget), "Ambush needs a target");
        s.HasHidingSpot = false; Check(!HunterContract.IsAllowed(s, HunterAction.CheckHidingSpot), "Check needs a hiding spot");
        s.MemoryAge = HunterContract.InterceptMemoryLimit; Check(!HunterContract.IsAllowed(s, HunterAction.InterceptExit), "Old memory cannot intercept");
        Check(HunterContract.IsAllowed(s, HunterAction.GoLastKnown) && HunterContract.IsAllowed(s, HunterAction.SweepSector) &&
              HunterContract.IsAllowed(s, HunterAction.GiveUp), "Fallback actions always allowed");

        // Discretization covers exactly 48 distinct states.
        var seen = new HashSet<int>();
        foreach (float distance in new[] { 1f, 5f, 12f })
        foreach (float age in new[] { 1f, 9f })
        foreach (MemoryKind kind in Enum.GetValues(typeof(MemoryKind)))
        foreach (bool target in new[] { true, false })
        foreach (bool hidingNear in new[] { true, false })
        {
            var d = new HunterSituation
            {
                ToLastKnownX = distance, MemoryAge = age, Memory = kind, HasTarget = target,
                HasHidingSpot = true, LastKnownToHidingX = hidingNear ? 1 : 6
            };
            int index = HunterContract.StateIndex(d);
            Check(index >= 0 && index < HunterContract.StateCount, "State index in range");
            seen.Add(index);
        }
        Check(seen.Count == HunterContract.StateCount, "All 48 states reachable, got " + seen.Count);
        Check(HunterContract.StateIndex(new HunterSituation { Memory = MemoryKind.HidingEntry }) ==
              HunterContract.StateIndex(new HunterSituation { Memory = MemoryKind.Vision }), "Hiding entry counts as seen");
        Check(HunterContract.StateIndex(new HunterSituation { Memory = MemoryKind.SharedAlert }) ==
              HunterContract.StateIndex(new HunterSituation { Memory = MemoryKind.Sound }), "Shared alert counts as heard");
        Check(HunterContract.StateIndex(new HunterSituation { HasHidingSpot = false }) % 2 == 1, "No spot means not near");

        // GuardBrain memory sources.
        Check(HunterContract.KindFromSource("Vision") == MemoryKind.Vision, "Vision source");
        Check(HunterContract.KindFromSource("Saw hiding entry") == MemoryKind.HidingEntry, "Hiding entry source");
        Check(HunterContract.KindFromSource("Shared alert") == MemoryKind.SharedAlert, "Shared alert source");
        foreach (string noise in new[] { "Footsteps", "Running footsteps", "Dropped can", "-" })
            Check(HunterContract.KindFromSource(noise) == MemoryKind.Sound, "Noise source: " + noise);

        // Reward scale: terminal rewards dominate shaping.
        Check(HunterContract.CatchReward == 1 && HunterContract.EscapeReward == -1, "Terminal rewards");
        // StepReward is paid per decision (at most ~1/s over a 240 s mission), never per Academy step.
        Check(HunterContract.ReacquireReward < HunterContract.CatchReward && -HunterContract.StepReward * 300 < .5f, "Shaping smaller than terminal");

        // Thief policy.
        ThiefStyle style = ThiefStyle.Default;
        var calm = new ThiefSense { NearestGuard = float.PositiveInfinity, TargetsLeft = true, SinceThreat = 99, Cans = 3 };
        ThiefIntent intent = ThiefPolicy.Decide(calm, style);
        Check(intent.Goal == ThiefGoal.Target && !intent.Run && !intent.Sneak && !intent.DropCan, "Calm thief walks to target");
        calm.TargetsLeft = false;
        Check(ThiefPolicy.Decide(calm, style).Goal == ThiefGoal.Exit, "All targets taken: go to exit");
        calm.NearestGuard = 6;
        Check(ThiefPolicy.Decide(calm, style).Sneak, "Sneak inside sneak radius");
        calm.NearestGuard = 4; calm.WantsCan = true;
        Check(ThiefPolicy.Decide(calm, style).DropCan, "Drop can when guard close and roll succeeded");
        calm.Cans = 0;
        Check(!ThiefPolicy.Decide(calm, style).DropCan, "No can left");

        var spotted = new ThiefSense { Spotted = true, NearestGuard = 3, TargetsLeft = true, HidingNearby = true, PrefersHiding = true };
        intent = ThiefPolicy.Decide(spotted, style);
        Check(intent.Goal == ThiefGoal.Hide && intent.Run, "Spotted hider runs to hiding spot");
        spotted.HidingNearby = false;
        Check(ThiefPolicy.Decide(spotted, style).Goal == ThiefGoal.Flee, "No spot nearby: flee");
        spotted.HidingNearby = true; spotted.PrefersHiding = false;
        Check(ThiefPolicy.Decide(spotted, style).Goal == ThiefGoal.Flee, "Runner flees");

        var hidden = new ThiefSense { Hidden = true, SinceThreat = 1, NearestGuard = 20, Spotted = true };
        Check(ThiefPolicy.Decide(hidden, style).Goal == ThiefGoal.StayHidden, "Stay hidden while threat is recent");
        hidden.SinceThreat = style.CalmSeconds; hidden.NearestGuard = 3;
        Check(ThiefPolicy.Decide(hidden, style).Goal == ThiefGoal.StayHidden, "Stay hidden while a guard is near");
        hidden.NearestGuard = float.PositiveInfinity;
        intent = ThiefPolicy.Decide(hidden, style);
        Check(intent.Goal == ThiefGoal.LeaveHiding && !intent.DropCan, "Leave when calm and clear");

        var rng = new Random(7);
        for (int i = 0; i < 200; i++)
        {
            ThiefStyle r = ThiefStyle.Random(rng);
            Check(r.HideChance >= .2f && r.HideChance <= .8f && r.CanChance >= .1f && r.CanChance <= .5f &&
                  r.SneakRadius >= 5 && r.SneakRadius <= 10 && r.CalmSeconds >= 3 && r.CalmSeconds <= 8, "Random style in range");
        }

        // Q-learning.
        var q = new QLearningHunter();
        bool[] all = { true, true, true, true, true, true };
        Check(q.Best(0, all) == HunterAction.GoLastKnown, "Untrained table falls back to GoLastKnown");
        Check(q.Epsilon == 1 && Math.Abs(q.Epsilon - QLearningHunter.EpsilonStart) < 1e-6f, "Training starts fully exploring");
        q.Episodes = QLearningHunter.DefaultEpsilonDecayEpisodes / 2;
        Check(Math.Abs(q.Epsilon - .525f) < 1e-4f, "Epsilon decays linearly");
        q.Episodes = 10000;
        Check(q.Epsilon == QLearningHunter.EpsilonEnd, "Epsilon floor");
        q.Episodes = 0;
        bool[] noAmbush = { true, true, true, false, true, true };
        q.Q[7 * HunterContract.ActionCount + (int)HunterAction.AmbushTarget] = 5;
        q.Q[7 * HunterContract.ActionCount + (int)HunterAction.InterceptExit] = 2;
        Check(q.Best(7, all) == HunterAction.AmbushTarget, "Greedy picks highest Q");
        Check(q.Best(7, noAmbush) == HunterAction.InterceptExit, "Greedy respects mask");
        var qrng = new Random(3);
        var picked = new int[HunterContract.ActionCount];
        for (int i = 0; i < 3000; i++) picked[(int)q.Choose(7, noAmbush, 1, qrng)]++;
        Check(picked[(int)HunterAction.AmbushTarget] == 0, "Exploration never picks a masked action");
        for (int a = 0; a < picked.Length; a++) if (a != (int)HunterAction.AmbushTarget) Check(picked[a] > 400, "Exploration covers allowed action " + a);
        for (int i = 0; i < 50; i++) Check(q.Choose(7, noAmbush, 0, qrng) == HunterAction.InterceptExit, "Epsilon 0 is greedy");

        var u = new QLearningHunter();
        u.Update(1, HunterAction.SweepSector, 1, 0, all, true);
        Check(Math.Abs(u.Get(1, HunterAction.SweepSector) - QLearningHunter.Alpha) < 1e-6f, "Terminal update moves alpha toward reward");
        u.Q[2 * HunterContract.ActionCount + (int)HunterAction.GiveUp] = 1;
        u.Update(3, HunterAction.GoLastKnown, 0, 2, all, false);
        Check(Math.Abs(u.Get(3, HunterAction.GoLastKnown) - QLearningHunter.Alpha * QLearningHunter.Gamma) < 1e-6f, "Non-terminal bootstraps from best next Q");
        u.Update(4, HunterAction.GoLastKnown, 0, 2, new[] { true, true, true, true, true, false }, false);
        Check(u.Get(4, HunterAction.GoLastKnown) == 0, "Bootstrap ignores masked next actions");
        for (int i = 0; i < 500; i++) u.Update(5, HunterAction.CheckHidingSpot, -1, 0, all, true);
        Check(Math.Abs(u.Get(5, HunterAction.CheckHidingSpot) + 1) < 1e-3f, "Repeated terminal reward converges");

        u.Episodes = 42; u.Q[287] = -0.123456789f;
        Check(QLearningHunter.TryParse(u.ToJson(), out QLearningHunter round) && round.Episodes == 42, "JSON round trip keeps episodes");
        bool same = true;
        for (int i = 0; i < u.Q.Length; i++) same &= u.Q[i] == round.Q[i];
        Check(same, "JSON round trip keeps every Q value exactly");
        Check(!QLearningHunter.TryParse("{\"episodes\":1,\"q\":[0,1]}", out _), "Wrong table size rejected");
        Check(!QLearningHunter.TryParse("", out _) && !QLearningHunter.TryParse(null, out _) && !QLearningHunter.TryParse("garbage", out _), "Garbage rejected");
        Check(!QLearningHunter.TryParse(u.ToJson().Replace("-0.123", "NaN"), out _), "NaN rejected");

        // Command-line flags for lab runs.
        string[] argv = { "Game", "-batchmode", "-hunter-train", "-hunter-level", "random", "-hunter-episodes", "1500", "-hunter-table", "results/q.json", "-hunter-ppo-eval" };
        Check(HunterArgs.Has(argv, "-hunter-train") && !HunterArgs.Has(argv, "-hunter"), "Has matches whole flags only");
        Check(HunterArgs.Value(argv, "-hunter-table") == "results/q.json", "Value after flag");
        Check(HunterArgs.Value(argv, "-hunter-ppo-eval") == null, "Last flag has no value");
        Check(HunterArgs.Value(argv, "-missing") == null, "Missing flag has no value");
        Check(HunterArgs.Value(new[] { "-hunter-table", "-hunter-train" }, "-hunter-table") == null, "A following flag is not a value");
        Check(HunterArgs.Int(argv, "-hunter-episodes", 0) == 1500, "Int parses");
        Check(HunterArgs.Int(argv, "-hunter-level", 7) == 7, "Int falls back on text");
        var lrng = new Random(11);
        var levels = new HashSet<int>();
        for (int i = 0; i < 600; i++)
        {
            int level = HunterArgs.Level("random", 1, lrng);
            Check(level >= 1 && level <= HunterArgs.MaxLevel, "Random level in 1..6");
            levels.Add(level);
        }
        Check(levels.Count == HunterArgs.MaxLevel, "Random covers all six levels");
        Check(HunterArgs.Level("RANDOM", 1, lrng) >= 1, "random is case-insensitive");
        Check(HunterArgs.Level("4", 1, lrng) == 4, "Fixed level");
        Check(HunterArgs.Level("0", 1, lrng) == 1 && HunterArgs.Level("7", 2, lrng) == 2, "Out-of-range level falls back");
        Check(HunterArgs.Level(null, 3, lrng) == 3 && HunterArgs.Level("abc", 3, lrng) == 3, "Missing or bad level falls back");

        // Expansion flags: skill, seed shift, epsilon decay.
        string[] ext = { "Game", "-thiefbot-skill", "0.7", "-hunter-seed-offset", "25", "-hunter-eps-decay", "600", "-hunter-solo" };
        Check(Math.Abs(HunterArgs.Float(ext, "-thiefbot-skill", -1) - .7f) < 1e-6f, "Float parses with invariant culture");
        Check(HunterArgs.Float(ext, "-missing", -1) == -1 && HunterArgs.Float(new[] { "-thiefbot-skill", "abc" }, "-thiefbot-skill", -1) == -1, "Float falls back");
        Check(HunterArgs.Int(ext, "-hunter-eps-decay", QLearningHunter.DefaultEpsilonDecayEpisodes) == 600, "Eps decay flag parses");
        var srng = new Random(5);
        Check(HunterArgs.SeedOffset(null, null, srng) == 0, "No seed flags keep the level seed");
        Check(HunterArgs.SeedOffset("25", null, srng) == 25, "Fixed seed offset");
        Check(HunterArgs.SeedOffset("abc", null, srng) == 0, "Bad seed offset falls back to 0");
        var offsets = new HashSet<int>();
        for (int i = 0; i < 3000; i++)
        {
            int k = HunterArgs.SeedOffset("25", "random", srng);
            Check(k >= 0 && k < 1000, "Random seed offset in 0..999");
            offsets.Add(k);
        }
        Check(offsets.Count > 900, "Random seed offsets spread over the range");
        Check(HunterArgs.SeedOffset(null, "RANDOM", srng) >= 0, "random seed is case-insensitive");

        var decay = new QLearningHunter { EpsilonDecayEpisodes = 600, Episodes = 300 };
        Check(Math.Abs(decay.Epsilon - .525f) < 1e-4f, "Custom decay halves epsilon at its midpoint");
        decay.Episodes = 600;
        Check(Math.Abs(decay.Epsilon - QLearningHunter.EpsilonEnd) < 1e-5f, "Custom decay reaches the floor at N");
        Check(new QLearningHunter().EpsilonDecayEpisodes == 150, "Default decay stays 150");
        decay.EpsilonDecayEpisodes = 0; decay.Episodes = 0;
        Check(decay.Epsilon == QLearningHunter.EpsilonStart, "Zero decay never divides by zero");

        Check(ThiefStyle.Default.Caution == 0 && ThiefStyle.Random(new Random(1)).Caution == 0, "Default styles never wait or reroute");
        var krng = new Random(9);
        float lowHide = 0, highHide = 0, lowSneak = 0, highSneak = 0;
        for (int i = 0; i < 500; i++)
        {
            ThiefStyle low = ThiefStyle.ForSkill(.1f, krng), high = ThiefStyle.ForSkill(.9f, krng);
            foreach (ThiefStyle st in new[] { low, high })
                Check(st.HideChance >= .1f && st.HideChance <= .9f && st.CanChance >= .05f && st.CanChance <= .7f &&
                      st.SneakRadius >= 4 && st.SneakRadius <= 12 && st.CalmSeconds >= 2 && st.CalmSeconds <= 8 &&
                      st.Caution >= 0 && st.Caution <= 1, "Skill style in range");
            lowHide += low.HideChance; highHide += high.HideChance; lowSneak += low.SneakRadius; highSneak += high.SneakRadius;
        }
        Check(highHide > lowHide && highSneak > lowSneak, "Higher skill hides and sneaks more on average");
        ThiefStyle unskilled = ThiefStyle.ForSkill(0, new Random(2));
        Check(unskilled.Caution <= ThiefStyle.SkillJitter + 1e-6f, "Skill 0 stays near zero caution");

        var route = new ThiefSense { NearestGuard = 7, TargetsLeft = true, SinceThreat = 99, ThreatOnRoute = true, WantsWait = true, WaitedSeconds = 2 };
        intent = ThiefPolicy.Decide(route, style);
        Check(intent.Goal == ThiefGoal.Wait && intent.Sneak && !intent.Run, "Wait while a guard is on the route");
        route.WaitedSeconds = ThiefPolicy.MaxWaitSeconds;
        Check(ThiefPolicy.Decide(route, style).Goal == ThiefGoal.Target, "Stop waiting after MaxWaitSeconds");
        route.WaitedSeconds = 0; route.WantsWait = false;
        Check(ThiefPolicy.Decide(route, style).Goal == ThiefGoal.Target, "No wait without the caution roll");
        route.WantsWait = true; route.Spotted = true;
        Check(ThiefPolicy.Decide(route, style).Goal == ThiefGoal.Flee, "Being spotted beats waiting");
        Check((int)ThiefGoal.Wait == 6 && (int)ThiefGoal.LeaveHiding == 5, "ThiefGoal appended, old values unchanged");

        Console.WriteLine("PASS: " + checks + " hunter and thief assertions");
        return 0;
    }
    private static float Sum(float[] o, int start, int count)
    {
        float sum = 0;
        for (int i = start; i < start + count; i++) sum += o[i];
        return sum;
    }
}
