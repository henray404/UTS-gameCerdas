using System;

namespace SneakHouse
{
    public struct ScoreBreakdown
    {
        public int Objective;
        public int Stealth;
        public int Time;
        public int Total => Objective + Stealth + Time;
    }

    public static class ScoreRules
    {
        public const int ObjectiveMax = 6000;
        public const int StealthMax = 2500;
        public const int AlertPenalty = 500;
        public const int TimeMax = 1500;
        public const int Max = ObjectiveMax + StealthMax + TimeMax;

        public static ScoreBreakdown Calculate(bool won, int alerts, float elapsed, float referenceSeconds)
        {
            if (!won) return new ScoreBreakdown();
            double ratio = 1 - Math.Max(0, elapsed) / Math.Max(1, referenceSeconds);
            ratio = Math.Max(0, Math.Min(1, ratio));
            return new ScoreBreakdown
            {
                Objective = ObjectiveMax,
                Stealth = Math.Max(0, StealthMax - Math.Max(0, alerts) * AlertPenalty),
                Time = (int)Math.Round(TimeMax * ratio, MidpointRounding.AwayFromZero)
            };
        }
    }
}
