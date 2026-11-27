using System.Collections.Generic;
using Unity.InferenceEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace SneakHouse
{
    // Tahap B (PPO, Pertemuan 14): the same 6-action contract as QLearningHunter, chosen by an ML-Agents policy.
    // The Academy is stepped by hand so a decision comes back in the same frame GuardBrain asks for it.
    public class HunterAgent : Agent
    {
        public const string BehaviorName = "HunterGuard", ModelResource = "Adaptive/HunterGuard";
        private readonly float[] observation = new float[HunterContract.ObservationSize];
        private HunterSituation situation;
        private bool[] allowed;
        private int chosen;

        // Call before any agent exists: decisions are requested only when the Hunter loses the player.
        public static void PrepareAcademy() => Academy.Instance.AutomaticSteppingEnabled = false;

        // Curriculum lesson from the trainer YAML (environment_parameters.level); fallback when no trainer sets it.
        public static int CurriculumLevel(int fallback) =>
            Mathf.Clamp(Mathf.RoundToInt(Academy.Instance.EnvironmentParameters.GetWithDefault("level", fallback)), 1, HunterArgs.MaxLevel);

        public static HunterAgent AddTo(GameObject guard, bool inference)
        {
            var behavior = guard.AddComponent<BehaviorParameters>();
            behavior.BehaviorName = BehaviorName;
            behavior.BrainParameters.VectorObservationSize = HunterContract.ObservationSize;
            behavior.BrainParameters.ActionSpec = ActionSpec.MakeDiscrete(HunterContract.ActionCount);
            var rays = guard.AddComponent<RayPerceptionSensorComponent3D>();
            rays.SensorName = "HunterWalls";
            rays.DetectableTags = new List<string>(); // walls only: each ray reports hit distance
            rays.RaysPerDirection = 4; rays.MaxRayDegrees = 180; rays.RayLength = 12;
            rays.RayLayerMask = 1 << 8; // same wall layer GuardBrain.Sees raycasts against
            rays.StartVerticalOffset = rays.EndVerticalOffset = 1.2f;
            if (inference)
            {
                ModelAsset model = Resources.Load<ModelAsset>(ModelResource);
                if (model != null) { behavior.Model = model; behavior.BehaviorType = BehaviorType.InferenceOnly; }
                else Debug.LogWarning("HUNTER: no Resources/" + ModelResource + ".onnx; PPO eval falls back to the GoLastKnown heuristic.");
            }
            return guard.AddComponent<HunterAgent>();
        }

        // reward is what the previous action earned since the last decision.
        public HunterAction Decide(in HunterSituation s, bool[] mask, float reward)
        {
            situation = s; allowed = mask; chosen = -1;
            AddReward(reward);
            RequestDecision();
            Academy.Instance.EnvironmentStep();
            return chosen >= 0 && chosen < mask.Length && mask[chosen] ? (HunterAction)chosen : HunterAction.GoLastKnown;
        }

        public void Finish(float reward)
        {
            AddReward(reward);
            EndEpisode();
            Academy.Instance.EnvironmentStep(); // send the terminal step before the scene reload destroys this agent
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            HunterContract.Write(situation, observation);
            foreach (float value in observation) sensor.AddObservation(value);
        }

        public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
        {
            if (allowed == null) return;
            for (int a = 0; a < allowed.Length; a++) actionMask.SetActionEnabled(0, a, allowed[a]);
        }

        public override void OnActionReceived(ActionBuffers actions) => chosen = actions.DiscreteActions[0];

        // FSM-like default, used when neither a trainer nor a model is attached.
        public override void Heuristic(in ActionBuffers actionsOut)
        {
            ActionSegment<int> discrete = actionsOut.DiscreteActions;
            discrete[0] = (int)HunterAction.GoLastKnown;
        }
    }
}
