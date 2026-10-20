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
        private int startAnchor = -1, goalAnchor = -1;
        private int searchLimit;
        private bool hasSearch;
        public HunterPathResult LastSearch { get; private set; }
        public HunterPathResult LastUniformCostSearch { get; private set; }
        public int GraphNodeCount => graph == null ? 0 : graph.NodeCount;
        public int GraphBuildCount { get; private set; }
        public int AStarSearchCount { get; private set; }
        public int UniformCostSearchCount { get; private set; }
        public int QueryVersion { get; private set; }
        public int StartAnchor => startAnchor;
        public int GoalAnchor => goalAnchor;
        public Vector3 LastStart { get; private set; }
        public Vector3 LastGoal { get; private set; }
        public bool CanCompareLastSearch => hasSearch && graph != null && LastSearch != null;
        public bool CompareOnRouteRequest { get; set; }
        public string NavigationStatus { get; private set; } = "No route requested.";

        /// <summary>Call after a NavMesh rebake or navigation-surface change.</summary>
        public void InvalidateGraph()
        {
            graph = null;
            hasSearch = false;
            LastSearch = LastUniformCostSearch = null;
            startAnchor = goalAnchor = -1;
            QueryVersion++;
            NavigationStatus = "Graph invalidated; waiting for a new route.";
        }

        public bool TryFindPath(Vector3 start, Vector3 goal, List<Vector3> result)
        {
            if (result == null) throw new System.ArgumentNullException(nameof(result));
            result.Clear();
            if (!Search(start, goal)) return false;
            if ((LastStart - LastGoal).sqrMagnitude <= 0.000001f)
            {
                result.Add(LastStart);
                return true;
            }
            result.Add(LastStart);
            var path = LastSearch.NodePath;
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 waypoint = ToUnity(graph.GetPoint(path[i]));
                if ((result[result.Count - 1] - waypoint).sqrMagnitude > 0.000001f) result.Add(waypoint);
            }
            if ((result[result.Count - 1] - LastGoal).sqrMagnitude > 0.000001f) result.Add(LastGoal);
            return true;
        }

        /// <summary>Run both searches on the same cached graph and endpoint anchors.</summary>
        public bool TryCompareSearches(Vector3 start, Vector3 goal,
            out HunterPathResult aStar, out HunterPathResult uniformCost)
        {
            Search(start, goal);
            return TryCompareLastSearch(out aStar, out uniformCost);
        }

        /// <summary>Reuses the existing A* result and exact cached graph anchors; UCS runs at most once per query.</summary>
        public bool TryCompareLastSearch(out HunterPathResult aStar, out HunterPathResult uniformCost)
        {
            aStar = LastSearch;
            uniformCost = LastUniformCostSearch;
            if (!CanCompareLastSearch) return false;
            if (uniformCost == null)
            {
                uniformCost = HunterPathSearch.UniformCost(graph, startAnchor, goalAnchor, searchLimit);
                LastUniformCostSearch = uniformCost;
                UniformCostSearchCount++;
            }
            return aStar.Succeeded && uniformCost.Succeeded;
        }

        private bool Search(Vector3 start, Vector3 goal)
        {
            QueryVersion++;
            hasSearch = false;
            startAnchor = goalAnchor = -1;
            LastSearch = LastUniformCostSearch = null;
            LastStart = LastGoal = default;
            if (!TryPrepare(start, goal, out Vector3 sampledStart, out Vector3 sampledGoal,
                out int startNode, out int goalNode)) return false;
            LastStart = sampledStart;
            LastGoal = sampledGoal;
            startAnchor = startNode;
            goalAnchor = (sampledStart - sampledGoal).sqrMagnitude <= 0.000001f ? startNode : goalNode;
            hasSearch = true;
            searchLimit = maximumExpandedNodes;
            AStarSearchCount++;
            LastSearch = HunterPathSearch.AStar(graph, startAnchor, goalAnchor, searchLimit);
            NavigationStatus = LastSearch.Succeeded ? "Route found." : "Unreachable graph goal or search expansion limit reached.";
            if (CompareOnRouteRequest) TryCompareLastSearch(out _, out _);
            return LastSearch.Succeeded;
        }

        private bool TryPrepare(Vector3 start, Vector3 goal, out Vector3 sampledStart,
            out Vector3 sampledGoal, out int startNode, out int goalNode)
        {
            sampledStart = sampledGoal = default(Vector3);
            startNode = goalNode = -1;
            if (!Finite(start) || !Finite(goal)) { NavigationStatus = "Invalid non-finite start or goal."; return false; }
            EnsureGraph();
            if (graph.NodeCount == 0) { NavigationStatus = "Missing or empty baked NavMesh data."; return false; }
            if (!NavMesh.SamplePosition(start, out NavMeshHit startHit, sampleRadius, NavMesh.AllAreas))
            { NavigationStatus = "Start is outside the baked NavMesh sampling radius."; return false; }
            if (!NavMesh.SamplePosition(goal, out NavMeshHit goalHit, sampleRadius, NavMesh.AllAreas))
            { NavigationStatus = "Goal is outside the baked NavMesh sampling radius."; return false; }
            sampledStart = startHit.position;
            sampledGoal = goalHit.position;
            startNode = FindVisibleAnchor(sampledStart);
            goalNode = FindVisibleAnchor(sampledGoal);
            if (startNode < 0 || goalNode < 0) { NavigationStatus = "No walkable visible graph anchor for an endpoint."; return false; }
            return true;
        }

        private void EnsureGraph()
        {
            if (graph != null) return;
            GraphBuildCount++;
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
