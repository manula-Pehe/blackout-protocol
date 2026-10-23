using System.Collections.Generic;
using BlackoutProtocol.AI.Flanker.Logic;
using BlackoutProtocol.Core;
using UnityEngine;
using UnityEngine.AI;

namespace BlackoutProtocol.AI.Flanker
{
    /// <summary>NavMesh geometry adapter for Flanker-owned A*, with frozen player-view exposure costs.</summary>
    [DisallowMultipleComponent]
    public sealed class FlankerNavMeshPathfinder : MonoBehaviour, IPathfinder
    {
        [SerializeField] private int agentTypeId = 0;
        [SerializeField, Range(0f, 20f)] private float exposurePenalty = 5f;
        [SerializeField, Min(1)] private int maximumExpanded = 4096;
        private FlankerGraph graph;
        private FlankerAStar search;
        private float[] exposure;
        private readonly List<int> route = new List<int>();
        private bool exposureDirty = true;
        private bool threatKnown;
        private Vector3 threatEye;
        private Vector3 threatForward;
        private float penaltyScale = 1f;

        public double LastCost { get; private set; }
        public int Expanded => search == null ? 0 : search.Expanded;
        public int NodeCount => graph == null ? 0 : graph.Count;
        private NavMeshQueryFilter Filter => new NavMeshQueryFilter { agentTypeID = agentTypeId, areaMask = NavMesh.AllAreas };

        /// <summary>Rebuild on the next query, after the caller has allowed carving to settle.</summary>
        public void InvalidateGraph()
        {
            graph = null;
            search = null;
            exposureDirty = true;
        }

        /// <summary>Supply perceived or remembered threat information; never reads the live player itself.</summary>
        public void SetThreat(bool known, Vector3 eye, Vector3 forward, float scale)
        {
            if (known != threatKnown || (eye - threatEye).sqrMagnitude > 0.01f ||
                (forward - threatForward).sqrMagnitude > 0.01f || !Mathf.Approximately(scale, penaltyScale)) exposureDirty = true;
            threatKnown = known;
            threatEye = eye;
            threatForward = forward;
            penaltyScale = Mathf.Clamp(scale, 0f, 1f);
        }

        /// <summary>Small-radius sampling avoids silently snapping feet onto distant cover tops.</summary>
        public bool TrySample(Vector3 position, out Vector3 sampled)
        {
            sampled = position;
            if (!Finite(position) || !NavMesh.SamplePosition(position, out NavMeshHit hit, 0.5f, Filter) ||
                Mathf.Abs(position.y - hit.position.y) > 0.35f) return false;
            sampled = hit.position;
            return true;
        }

        public bool TryFindPath(Vector3 start, Vector3 goal, List<Vector3> result)
        {
            result.Clear();
            LastCost = double.PositiveInfinity;
            if (!TrySample(start, out Vector3 sampledStart) || !TrySample(goal, out Vector3 sampledGoal)) return false;
            EnsureGraph();
            if (graph.Count == 0) return false;
            int first = Anchor(sampledStart);
            int last = Anchor(sampledGoal);
            if (first < 0 || last < 0) return false;
            RefreshExposure();
            if (!search.Find(first, last, exposure, Mathf.Max(0f, exposurePenalty) * penaltyScale,
                route, out double cost, maximumExpanded)) return false;

            result.Add(sampledStart);
            for (int index = 0; index < route.Count; index++)
            {
                Vector3 point = ToUnity(graph.Point(route[index]));
                if (NavMesh.Raycast(result[result.Count - 1], point, out _, Filter))
                {
                    result.Clear();
                    return false;
                }
                if ((point - result[result.Count - 1]).sqrMagnitude > 0.0001f) result.Add(point);
            }
            if (NavMesh.Raycast(result[result.Count - 1], sampledGoal, out _, Filter))
            {
                result.Clear();
                return false;
            }
            if ((sampledGoal - result[result.Count - 1]).sqrMagnitude > 0.0001f) result.Add(sampledGoal);
            LastCost = cost;
            return true;
        }

        private void EnsureGraph()
        {
            if (graph != null) return;
            NavMeshTriangulation mesh = NavMesh.CalculateTriangulation();
            var vertices = new FlankerPoint[mesh.vertices.Length];
            for (int index = 0; index < vertices.Length; index++) vertices[index] = ToPoint(mesh.vertices[index]);
            graph = FlankerTriangulation.Build(vertices, mesh.indices,
                (first, second) => !NavMesh.Raycast(ToUnity(first), ToUnity(second), out _, Filter));
            search = new FlankerAStar(graph);
            exposure = new float[graph.Count];
            exposureDirty = true;
        }

        private int Anchor(Vector3 position)
        {
            int selected = -1;
            float best = float.PositiveInfinity;
            for (int index = 0; index < graph.Count; index++)
            {
                Vector3 point = ToUnity(graph.Point(index));
                float distance = (point - position).sqrMagnitude;
                if (distance >= best || NavMesh.Raycast(position, point, out _, Filter)) continue;
                selected = index;
                best = distance;
            }
            return selected;
        }

        private void RefreshExposure()
        {
            if (!exposureDirty) return;
            for (int index = 0; index < graph.Count; index++)
            {
                exposure[index] = 0f;
                if (!threatKnown) continue;
                Vector3 target = ToUnity(graph.Point(index)) + Vector3.up * 0.9f;
                Vector3 direction = target - threatEye;
                direction.y = 0f;
                Vector3 forward = threatForward;
                forward.y = 0f;
                if (forward.sqrMagnitude > 0.01f && Vector3.Dot(forward.normalized, direction.normalized) < 0.5f) continue;
                if (!Physics.Linecast(threatEye, target, GameLayers.SightBlockers, QueryTriggerInteraction.Ignore)) exposure[index] = 1f;
            }
            exposureDirty = false;
        }

        public static FlankerPoint ToPoint(Vector3 point) => new FlankerPoint(point.x, point.y, point.z);
        public static Vector3 ToUnity(FlankerPoint point) => new Vector3(point.X, point.Y, point.Z);
        private static bool Finite(Vector3 point) => FlankerPoint.Finite(point.x) && FlankerPoint.Finite(point.y) && FlankerPoint.Finite(point.z);
    }
}
