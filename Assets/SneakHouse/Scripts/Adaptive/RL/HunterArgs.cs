using System;

namespace SneakHouse
{
    // Command-line flags for Hunter training/eval (see plans/rl-training-lab.md). Pure C#, unit-tested with Mono.
    public static class HunterArgs
    {
        public const int MaxLevel = 6;

        public static bool Has(string[] args, string flag) => Array.IndexOf(args, flag) >= 0;

        // Value after a flag, or null when the flag is missing, last, or followed by another flag.
        public static string Value(string[] args, string flag)
        {
            int at = Array.IndexOf(args, flag);
            return at >= 0 && at + 1 < args.Length && !args[at + 1].StartsWith("-", StringComparison.Ordinal) ? args[at + 1] : null;
        }

        public static int Int(string[] args, string flag, int fallback) =>
            int.TryParse(Value(args, flag), out int n) ? n : fallback;

        // "-hunter-level 3" fixes the level, "random" draws 1..6 per mission; anything else (or out of range) gives fallback.
        public static int Level(string spec, int fallback, Random rng)
        {
            if (string.Equals(spec, "random", StringComparison.OrdinalIgnoreCase)) return rng.Next(1, MaxLevel + 1);
            return int.TryParse(spec, out int level) && level >= 1 && level <= MaxLevel ? level : fallback;
        }
    }
}
