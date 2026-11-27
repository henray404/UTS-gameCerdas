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
        q.Episodes = QLearningHunter.EpsilonDecayEpisodes / 2;
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
