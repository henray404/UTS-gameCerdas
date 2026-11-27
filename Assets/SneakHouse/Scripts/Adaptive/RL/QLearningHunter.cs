using System;
using System.Globalization;
using System.Text;

namespace SneakHouse
{
    // Tabular Q-learning over the 48 x 6 Hunter contract (Pertemuan 13). Pure C#, so it is unit-tested with Mono.
    public sealed class QLearningHunter
    {
        public const float Alpha = .1f, Gamma = .95f, EpsilonStart = 1, EpsilonEnd = .05f;
        public const int EpsilonDecayEpisodes = 150;
        public readonly float[] Q = new float[HunterContract.StateCount * HunterContract.ActionCount];
        public int Episodes;

        public float Get(int state, HunterAction action) => Q[state * HunterContract.ActionCount + (int)action];

        // Linear decay from full exploration to 5% over the first 150 training missions.
        public float Epsilon => Math.Max(EpsilonEnd, EpsilonStart - (EpsilonStart - EpsilonEnd) * Episodes / EpsilonDecayEpisodes);

        public static bool[] Allowed(in HunterSituation s)
        {
            var allowed = new bool[HunterContract.ActionCount];
            for (int a = 0; a < allowed.Length; a++) allowed[a] = HunterContract.IsAllowed(s, (HunterAction)a);
            return allowed;
        }

        // Greedy over allowed actions; ties go to the lowest index so an untrained table behaves like GoLastKnown (the FSM default).
        public HunterAction Best(int state, bool[] allowed)
        {
            int best = -1;
            for (int a = 0; a < HunterContract.ActionCount; a++)
                if (allowed[a] && (best < 0 || Get(state, (HunterAction)a) > Get(state, (HunterAction)best))) best = a;
            return best < 0 ? HunterAction.GoLastKnown : (HunterAction)best;
        }

        public HunterAction Choose(int state, bool[] allowed, float epsilon, Random rng)
        {
            if (rng.NextDouble() >= epsilon) return Best(state, allowed);
            int count = 0;
            foreach (bool ok in allowed) if (ok) count++;
            int pick = rng.Next(Math.Max(1, count));
            for (int a = 0; a < allowed.Length; a++)
                if (allowed[a] && pick-- == 0) return (HunterAction)a;
            return HunterAction.GoLastKnown;
        }

        // Q(s,a) += alpha * (r + gamma * max_a' Q(s',a') - Q(s,a)); terminal transitions do not bootstrap.
        public void Update(int state, HunterAction action, float reward, int next, bool[] nextAllowed, bool terminal)
        {
            float target = reward;
            if (!terminal) target += Gamma * Get(next, Best(next, nextAllowed));
            int i = state * HunterContract.ActionCount + (int)action;
            Q[i] += Alpha * (target - Q[i]);
        }

        public string ToJson()
        {
            var json = new StringBuilder("{\"episodes\":").Append(Episodes).Append(",\"q\":[");
            for (int i = 0; i < Q.Length; i++) json.Append(i == 0 ? "" : ",").Append(Q[i].ToString("R", CultureInfo.InvariantCulture));
            return json.Append("]}").ToString();
        }

        // Accepts only what ToJson writes; a wrong size means a contract change, so the table is rejected rather than misread.
        public static bool TryParse(string json, out QLearningHunter table)
        {
            table = null;
            if (string.IsNullOrEmpty(json)) return false;
            int episodes = json.IndexOf("\"episodes\":", StringComparison.Ordinal), open = json.IndexOf('['), close = json.LastIndexOf(']');
            if (episodes < 0 || open < 0 || close < open) return false;
            int comma = json.IndexOf(',', episodes);
            if (comma < 0) return false;
            string count = json.Substring(episodes + 11, comma - episodes - 11);
            string[] values = json.Substring(open + 1, close - open - 1).Split(',');
            var parsed = new QLearningHunter();
            if (values.Length != parsed.Q.Length || !int.TryParse(count, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed.Episodes)) return false;
            for (int i = 0; i < values.Length; i++)
                if (!float.TryParse(values[i], NumberStyles.Float, CultureInfo.InvariantCulture, out parsed.Q[i]) || float.IsNaN(parsed.Q[i])) return false;
            table = parsed;
            return true;
        }
    }
}
