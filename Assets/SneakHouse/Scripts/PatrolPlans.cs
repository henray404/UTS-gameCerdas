using System.Collections.Generic;

namespace SneakHouse
{
    public readonly struct PatrolPoint
    {
        public readonly float X, Z;
        public PatrolPoint(float x, float z) { X = x; Z = z; }
    }

    // One guard's beat. Home is the loop walked every lap, kept clear of the objectives.
    // Peek is walked once every PeekEvery laps and passes close to a target, so each pickup
    // has windows when nobody is near it instead of a guard circling it forever.
    public sealed class GuardBeat
    {
        public string Zone { get; }
        public float Floor { get; }
        public IReadOnlyList<PatrolPoint> Home { get; }
        public IReadOnlyList<PatrolPoint> Peek { get; }
        public int PeekEvery { get; }
        internal GuardBeat(string zone, float floor, PatrolPoint[] home, PatrolPoint[] peek, int peekEvery)
        {
            Zone = zone; Floor = floor; Home = System.Array.AsReadOnly(home);
            Peek = System.Array.AsReadOnly(peek); PeekEvery = peekEvery;
        }
    }

    // World coordinates measured on the NavMesh of each level's fixed map and interior seed
    // (CampaignLevel.LayoutSeed). If a layout changes, GuardEncounter rejects stale beats and falls
    // back to the authored room routes, so a mismatch never blocks a mission.
    public static class PatrolPlans
    {
        private const float Ground = .06f, Upper = 3.66f;
        private static PatrolPoint P(float x, float z) => new PatrolPoint(x, z);
        private static GuardBeat Beat(string zone, PatrolPoint[] home, PatrolPoint[] peek, int every, float floor = Ground) =>
            new GuardBeat(zone, floor, home, peek, every);

        private static PatrolPoint[] LivingWest() => new[] { P(-3, -.5f), P(2, -.5f), P(1.5f, -3), P(-3, -3) };
        private static PatrolPoint[] LivingEast() => new[] { P(-.5f, -.5f), P(3, -.5f), P(2, -3.5f), P(-.5f, -3.5f) };
        private static PatrolPoint[] Office() => new[] { P(-6, 2), P(-6, 3.5f), P(-4.5f, 2.5f), P(-3.5f, 2) };
        private static PatrolPoint[] Collection() => new[] { P(1, 2), P(1, 4), P(2.5f, 4), P(2.5f, 3.5f) };
        private static PatrolPoint[] Hall() => new[] { P(-6, 2.5f), P(-3, 2.5f), P(-3, 4), P(-6, 4) };

        private static readonly GuardBeat[][] levels =
        {
            new[] { Beat("Living room", LivingWest(), new[] { P(3, -3) }, 3) },
            new[]
            {
                Beat("Living room", new[] { P(0, -.5f), P(3, -.5f), P(2, -3.5f), P(0, -3.5f) }, new[] { P(3.5f, -2) }, 3),
                Beat("Office", Office(), new[] { P(-3, 3) }, 3)
            },
            new[]
            {
                Beat("Living room", LivingEast(), new[] { P(4, -2) }, 3),
                Beat("Office", Office(), new[] { P(-3, 3) }, 2),
                Beat("Collection room", Collection(), new[] { P(5, 3) }, 2)
            },
            new[]
            {
                Beat("Living room", LivingWest(), new[] { P(3, -3) }, 2),
                Beat("Library wing", new[] { P(-6, 7.5f), P(-2, 7.5f), P(-1.5f, 11.5f), P(-6, 11.5f) }, new[] { P(-4.5f, 12.5f) }, 2),
                Beat("Collection room", Collection(), new[] { P(5, 3) }, 2)
            },
            new[]
            {
                Beat("Living room", LivingEast(), new[] { P(4, -2) }, 2),
                Beat("Library wing", new[] { P(-6, 7.5f), P(-3.5f, 7.5f), P(-3, 11.5f), P(-6, 11.5f) }, new[] { P(-5.5f, 12) }, 2),
                Beat("Gallery wing", new[] { P(4, 7), P(6, 7), P(6, 7.5f), P(4, 7.5f) }, new[] { P(4.5f, 11.5f), P(6, 11.5f) }, 2),
                Beat("Connecting hall", Hall(), new[] { P(-5, 7.5f) }, 3)
            },
            new[]
            {
                Beat("Living room", LivingEast(), new[] { P(3, -2.5f) }, 2),
                Beat("Upper office", new[] { P(-6.5f, 7), P(-3.5f, 7), P(-3.5f, 8), P(-6.5f, 8) },
                    new[] { P(-6, 12.5f), P(-3.5f, 12) }, 2, Upper),
                Beat("Gallery wing", new[] { P(4, 7), P(6, 7), P(6, 8), P(4, 7.5f) }, new[] { P(4, 11.5f), P(4, 13) }, 2),
                Beat("Connecting hall", Hall(), new[] { P(-5, 7) }, 3)
            }
        };

        // Beats for a campaign level; null when none are authored (callers then keep the room routes).
        public static IReadOnlyList<GuardBeat> For(int level) =>
            level >= 1 && level <= levels.Length ? levels[level - 1] : null;
    }
}
