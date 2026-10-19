using System.Collections.Generic;
using BlackoutProtocol.AI.Hunter.Logic;
using BlackoutProtocol.Core;
using UnityEngine;
using UnityEngine.AI;

namespace BlackoutProtocol.AI.Hunter
{
    /// <summary>NavMesh geometry adapter; route search itself remains Hunter-owned pure C#.</summary>
    public sealed class HunterNavMeshPathfinder : MonoBehaviour, IPathfinder
    {
        [SerializeField, Min(0.01f)] private float sampleRadius = 2f;
        [SerializeField, Min(1)] private int maximumExpandedNodes = 4096;

        private HunterNavGraph graph;
        public HunterPathResult LastSearch { get; private set; }
        public HunterPathResult LastUniformCostSearch { get; private set; }
        public int GraphNodeCount => graph == null ? 0 : graph.NodeCount;

        /// <summary>Call after a NavMesh rebake or navigation-surface change.</summary>
        public void InvalidateGraph() => graph = null;

        public bool TryFindPath(Vector3 start, Vector3 goal, List<Vector3> result)
        {
            if (result == null) throw new System.ArgumentNullException(nameof(result));
            result.Clear();
            LastSearch = null;
            LastUniformCostSearch = null;
            if (!TryPrepare(start, goal, out Vector3 sampledStart, out Vector3 sampledGoal,
                out int startNode, out int goalNode)) return false;

            if ((sampledStart - sampledGoal).sqrMagnitude <= 0.000001f)
            {
                LastSearch = HunterPathSearch.AStar(graph, startNode, startNode, maximumExpandedNodes);
                if (!LastSearch.Succeeded) return false;
                result.Add(sampledStart);
                return true;
            }

            LastSearch = HunterPathSearch.AStar(graph, startNode, goalNode, maximumExpandedNodes);
            if (!LastSearch.Succeeded) return false;
            result.Add(sampledStart);
            var path = LastSearch.NodePath;
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 waypoint = ToUnity(graph.GetPoint(path[i]));
                if ((result[result.Count - 1] - waypoint).sqrMagnitude > 0.000001f) result.Add(waypoint);
            }
            if ((result[result.Count - 1] - sampledGoal).sqrMagnitude > 0.000001f) result.Add(sampledGoal);
            return true;
        }

        /// <summary>Run both searches on the same cached graph and endpoint anchors.</summary>
        public bool TryCompareSearches(Vector3 start, Vector3 goal,
            out HunterPathResult aStar, out HunterPathResult uniformCost)
        {
            aStar = null;
            uniformCost = null;
            if (!TryPrepare(start, goal, out _, out _, out int startNode, out int goalNode)) return false;
            aStar = HunterPathSearch.AStar(graph, startNode, goalNode, maximumExpandedNodes);
            uniformCost = HunterPathSearch.UniformCost(graph, startNode, goalNode, maximumExpandedNodes);
            LastSearch = aStar;
            LastUniformCostSearch = uniformCost;
            return aStar.Succeeded && uniformCost.Succeeded;
        }

        private bool TryPrepare(Vector3 start, Vector3 goal, out Vector3 sampledStart,
            out Vector3 sampledGoal, out int startNode, out int goalNode)
        {
            sampledStart = sampledGoal = default(Vector3);
            startNode = goalNode = -1;
            if (!Finite(start) || !Finite(goal) || !NavMesh.SamplePosition(start, out NavMeshHit startHit, sampleRadius, NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(goal, out NavMeshHit goalHit, sampleRadius, NavMesh.AllAreas)) return false;
            sampledStart = startHit.position;
            sampledGoal = goalHit.position;
            EnsureGraph();
            if (graph.NodeCount == 0) return false;
            startNode = FindVisibleAnchor(sampledStart);
            goalNode = FindVisibleAnchor(sampledGoal);
            return startNode >= 0 && goalNode >= 0;
        }

        private void EnsureGraph()
        {
            if (graph != null) return;
            NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
            var vertices = new HunterNavPoint[triangulation.vertices.Length];
            for (int i = 0; i < vertices.Length; i++) vertices[i] = ToPoint(triangulation.vertices[i]);
            graph = HunterTriangulationGraphBuilder.Build(vertices, triangulation.indices,
                (a, b) => !NavMesh.Raycast(ToUnity(a), ToUnity(b), out _, NavMesh.AllAreas));
        }

        private int FindVisibleAnchor(Vector3 position)
        {
            int selected = -1;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < graph.NodeCount; i++)
            {
                Vector3 candidate = ToUnity(graph.GetPoint(i));
                float distance = (candidate - position).sqrMagnitude;
                if (distance >= bestDistance || NavMesh.Raycast(position, candidate, out _, NavMesh.AllAreas)) continue;
                selected = i;
                bestDistance = distance;
            }
            return selected;
        }

        private static bool Finite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        private static HunterNavPoint ToPoint(Vector3 p) => new HunterNavPoint(p.x, p.y, p.z);
        private static Vector3 ToUnity(HunterNavPoint p) => new Vector3(p.X, p.Y, p.Z);
    }
}
