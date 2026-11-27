using System;

namespace SneakHouse
{
    public enum ThiefGoal { Target, Exit, Flee, Hide, StayHidden, LeaveHiding, Wait }

    // Per-episode personality, randomized so the Hunter trains against many play styles.
    [Serializable]
    public struct ThiefStyle
    {
        public float HideChance;   // chance to run for a hiding spot (instead of fleeing) when spotted
        public float CanChance;    // chance to drop a can when a guard closes in
        public float SneakRadius;  // sneak while a visible guard is closer than this (m)
        public float CalmSeconds;  // stay hidden this long after the last threat
        // 0 (default styles) disables both: wait when a recently seen/heard guard is on the route, and pick targets away from it.
        public float Caution;

        public const float SkillJitter = .15f;

        public static ThiefStyle Default => new ThiefStyle { HideChance = .5f, CanChance = .3f, SneakRadius = 8, CalmSeconds = 4 };
        public static ThiefStyle Random(System.Random rng) => new ThiefStyle
        {
            HideChance = Lerp(.2f, .8f, rng), CanChance = Lerp(.1f, .5f, rng),
            SneakRadius = Lerp(5, 10, rng), CalmSeconds = Lerp(3, 8, rng)
        };
        // -thiefbot-skill: each weight is drawn around the skill (±SkillJitter), so higher skill sneaks, hides, distracts and waits more.
        public static ThiefStyle ForSkill(float skill, System.Random rng) => new ThiefStyle
        {
            HideChance = Map(.1f, .9f, skill, rng), CanChance = Map(.05f, .7f, skill, rng),
            SneakRadius = Map(4, 12, skill, rng), CalmSeconds = Map(2, 8, skill, rng), Caution = Map(0, 1, skill, rng)
        };
        private static float Lerp(float a, float b, System.Random rng) => a + (b - a) * (float)rng.NextDouble();
        private static float Map(float a, float b, float skill, System.Random rng)
        {
            float t = skill + SkillJitter * (2 * (float)rng.NextDouble() - 1);
            return a + (b - a) * (t < 0 ? 0 : t > 1 ? 1 : t);
        }
    }

    public struct ThiefSense
    {
        public bool Hidden;
        public bool Spotted;          // a guard is chasing, or sees the thief with high suspicion
        public float NearestGuard;    // metres to the nearest guard in line of sight; PositiveInfinity if none
        public float SinceThreat;     // seconds since Spotted was last true
        public bool TargetsLeft;
        public bool HidingNearby;     // a free hiding spot within reach
        public int Cans;
        // Rolled once per event by ThiefBot so the chance applies per encounter, not per frame.
        public bool PrefersHiding, WantsCan;
        public bool ThreatOnRoute;    // a guard seen or heard in the last few seconds stands near the remaining path
        public bool WantsWait;        // rolled with Caution when ThreatOnRoute starts
        public float WaitedSeconds;   // how long the thief has been waiting for that guard to move
    }

    public struct ThiefIntent
    {
        public ThiefGoal Goal;
        public bool Sneak, Run, DropCan;
    }

    public static class ThiefPolicy
    {
        public const float LeaveHidingClearance = 6, CanRange = 5, MaxWaitSeconds = 8;

        public static ThiefIntent Decide(in ThiefSense s, in ThiefStyle style)
        {
            var intent = new ThiefIntent();
            if (s.Hidden)
            {
                intent.Goal = s.SinceThreat >= style.CalmSeconds && s.NearestGuard > LeaveHidingClearance ? ThiefGoal.LeaveHiding : ThiefGoal.StayHidden;
                return intent;
            }
            intent.DropCan = s.WantsCan && s.Cans > 0 && s.NearestGuard < CanRange;
            if (s.Spotted)
            {
                intent.Goal = s.PrefersHiding && s.HidingNearby ? ThiefGoal.Hide : ThiefGoal.Flee;
                intent.Run = true;
                return intent;
            }
            if (s.ThreatOnRoute && s.WantsWait && s.WaitedSeconds < MaxWaitSeconds)
            {
                intent.Goal = ThiefGoal.Wait; intent.Sneak = true;
                return intent;
            }
            intent.Goal = s.TargetsLeft ? ThiefGoal.Target : ThiefGoal.Exit;
            intent.Sneak = s.NearestGuard < style.SneakRadius;
            return intent;
        }
    }
}
