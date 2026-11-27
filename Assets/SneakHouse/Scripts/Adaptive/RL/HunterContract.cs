using System;

namespace SneakHouse
{
    // Training contract shared by Q-learning and ML-Agents (plans/rl-enemy-design.md section 3).
    // Order and sizes are baked into trained tables/models: append only, never reorder.
    public enum HunterAction { GoLastKnown, SweepSector, InterceptExit, AmbushTarget, CheckHidingSpot, GiveUp }
    public enum MemoryKind { Vision, Sound, SharedAlert, HidingEntry }

    // What the Hunter may know when it loses the player. Offsets are world XZ metres; never the live player position.
    public struct HunterSituation
    {
        public float ToLastKnownX, ToLastKnownZ;              // last known position - guard position
        public float MemoryAge;                               // seconds since the evidence
        public MemoryKind Memory;
        public float Suspicion;                               // 0..1
        public float LastSeenVelocityX, LastSeenVelocityZ;    // zero unless the memory came from vision
        public float LastKnownToExitX, LastKnownToExitZ;      // extraction zone - last known position
        public bool HasTarget;
        public float ToTargetX, ToTargetZ;                    // nearest uncollected required target - guard position
        public float CollectedRatio;                          // collected / required
        public bool HasHidingSpot;
        public float LastKnownToHidingX, LastKnownToHidingZ;  // nearest hiding spot - last known position (occupancy unknown)
        public int OtherGuardsHunting;                        // other guards in Search or Chase
        public bool HasPrevious;
        public HunterAction Previous;
    }

    public static class HunterContract
    {
        public const int ActionCount = 6, ObservationSize = 28, StateCount = 48;
        public const float FarDistance = 20, HidingScale = 10, MemoryHorizon = 15, RunSpeed = 4.2f, MaxOtherGuards = 3;
        // Discretization thresholds for the 48-state Q-table.
        public const float NearDistance = 3, MediumDistance = 8, FreshMemory = 4;
        // GuardBrain inspects spots within 1.1 m of evidence; 2.5 m also counts spots the player could reach in a second.
        public const float HidingNearLastKnown = 2.5f;
        // Stops InterceptExit from turning into permanent exit camping.
        public const float InterceptMemoryLimit = 15;

        public const float CatchReward = 1, EscapeReward = -1, ReacquireReward = .2f, ReacquireCooldown = 10;
        public const float FoundHiddenReward = .3f, EmptyHidingCheckReward = -.02f, StepReward = -.001f;

        public static MemoryKind KindFromSource(string source)
        {
            switch (source)
            {
                case "Vision": return MemoryKind.Vision;
                case "Saw hiding entry": return MemoryKind.HidingEntry;
                case "Shared alert": return MemoryKind.SharedAlert;
                default: return MemoryKind.Sound; // footsteps, running footsteps, dropped can
            }
        }

        public static bool IsAllowed(in HunterSituation s, HunterAction action)
        {
            switch (action)
            {
                case HunterAction.InterceptExit: return s.MemoryAge < InterceptMemoryLimit;
                case HunterAction.AmbushTarget: return s.HasTarget;
                case HunterAction.CheckHidingSpot: return s.HasHidingSpot;
                default: return true;
            }
        }

        // Vector observation for ML-Agents, every value in [-1, 1].
        public static int Write(in HunterSituation s, float[] o)
        {
            if (o == null || o.Length < ObservationSize) throw new ArgumentException("Observation buffer needs " + ObservationSize + " floats");
            Array.Clear(o, 0, ObservationSize);
            int i = Direction(o, 0, s.ToLastKnownX, s.ToLastKnownZ, FarDistance);
            o[i++] = Clamp01(s.MemoryAge / MemoryHorizon);
            o[i + (int)s.Memory] = 1; i += 4;
            o[i++] = Clamp01(s.Suspicion);
            o[i++] = Clamp(s.LastSeenVelocityX / RunSpeed);
            o[i++] = Clamp(s.LastSeenVelocityZ / RunSpeed);
            i = Direction(o, i, s.LastKnownToExitX, s.LastKnownToExitZ, FarDistance);
            i = s.HasTarget ? Direction(o, i, s.ToTargetX, s.ToTargetZ, FarDistance) : i + 3;
            o[i++] = Clamp01(s.CollectedRatio);
            i = s.HasHidingSpot ? Direction(o, i, s.LastKnownToHidingX, s.LastKnownToHidingZ, HidingScale) : i + 3;
            o[i++] = Clamp01(s.OtherGuardsHunting / MaxOtherGuards);
            if (s.HasPrevious) o[i + (int)s.Previous] = 1;
            return i + ActionCount;
        }

        // Q-learning state: distance(3) x memory age(2) x source(2) x targets left(2) x hiding near(2) = 48.
        public static int StateIndex(in HunterSituation s)
        {
            float distance = Length(s.ToLastKnownX, s.ToLastKnownZ);
            int near = distance < NearDistance ? 0 : distance < MediumDistance ? 1 : 2;
            int stale = s.MemoryAge < FreshMemory ? 0 : 1;
            int heard = s.Memory == MemoryKind.Vision || s.Memory == MemoryKind.HidingEntry ? 0 : 1;
            int noTarget = s.HasTarget ? 0 : 1;
            bool hidingNear = s.HasHidingSpot && Length(s.LastKnownToHidingX, s.LastKnownToHidingZ) < HidingNearLastKnown;
            return near * 16 + stale * 8 + heard * 4 + noTarget * 2 + (hidingNear ? 0 : 1);
        }

        private static int Direction(float[] o, int i, float x, float z, float scale)
        {
            float length = Length(x, z);
            if (length > 1e-4f) { o[i] = x / length; o[i + 1] = z / length; }
            o[i + 2] = Clamp01(length / scale);
            return i + 3;
        }
        private static float Length(float x, float z) => (float)Math.Sqrt(x * x + z * z);
        private static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;
        private static float Clamp(float v) => v < -1 ? -1 : v > 1 ? 1 : v;
    }
}
