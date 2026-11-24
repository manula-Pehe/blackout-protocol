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
        [Header("Acoustic graph")]
        [SerializeField, Min(0f)] private float soundLossPerMetre = 1f;
        [SerializeField, Min(0f)] private float closedDoorSoundPenalty = 8f;
        [SerializeField, Min(1f)] private float doorwayNodeSearchRadius = 7f;

        private HunterNavGraph graph;
        private readonly List<Vector3> rawRoute = new List<Vector3>(64);
        private readonly List<HunterAcousticPortal> acousticPortals = new List<HunterAcousticPortal>(8);
        private readonly List<MonoBehaviour> doorComponents = new List<MonoBehaviour>(8);
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
        public string AcousticStatus { get; private set; } = "No sound propagated.";
        public HunterSoundPropagationResult LastSoundPropagation { get; private set; }

        /// <summary>Call after a NavMesh rebake or navigation-surface change.</summary>
        public void InvalidateGraph()
        {
            graph = null;
            hasSearch = false;
            LastSearch = LastUniformCostSearch = null;
            startAnchor = goalAnchor = -1;
            doorComponents.Clear();
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
            SmoothRoute(result);
            return true;
        }

        /// <summary>
        /// Runs Hunter-owned acoustic Dijkstra on the cached triangulation. Doorway-only
        /// links let sound cross a closed carved opening with an explicit attenuation.
        /// </summary>
        public bool TryPropagateSound(Vector3 source, Vector3 listener, float loudness,
            out HunterSoundPropagationResult result)
        {
            result = default;
            LastSoundPropagation = default;
            if (!Finite(source) || !Finite(listener) || !Finite(loudness) || loudness <= 0f)
            {
                AcousticStatus = "Invalid sound source, listener, or loudness.";
                return false;
            }
            EnsureGraph();
            if (graph == null || graph.NodeCount == 0 ||
                !NavMesh.SamplePosition(source, out NavMeshHit sourceHit, sampleRadius, NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(listener, out NavMeshHit listenerHit, sampleRadius, NavMesh.AllAreas))
            {
                AcousticStatus = "Sound source or listener is outside the baked NavMesh.";
                return false;
            }
            int sourceNode = FindVisibleAnchor(sourceHit.position);
            int listenerNode = FindVisibleAnchor(listenerHit.position);
            if (sourceNode < 0 || listenerNode < 0)
            {
                AcousticStatus = "Sound source or listener has no visible acoustic graph anchor.";
                return false;
            }
            BuildAcousticPortals();
            result = HunterSoundPropagation.Propagate(graph, sourceNode, listenerNode, loudness,
                soundLossPerMetre, acousticPortals, maximumExpandedNodes);
            LastSoundPropagation = result;
            AcousticStatus = result.Heard
                ? $"Heard after {result.TravelDistance:F1} m, {result.ClosedDoors} closed door(s), {result.PerceivedLoudness:F1} loudness remaining."
                : "No connected acoustic route retained enough loudness.";
            return result.Heard;
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
            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
            for (int i = 0; i < behaviours.Length; i++)
                if (behaviours[i] is IDoor) doorComponents.Add(behaviours[i]);
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

        private void SmoothRoute(List<Vector3> route)
        {
            if (route == null || route.Count < 3) return;
            rawRoute.Clear();
            rawRoute.AddRange(route);
            route.Clear();
            int anchor = 0;
            route.Add(rawRoute[0]);
            while (anchor < rawRoute.Count - 1)
            {
                int next = rawRoute.Count - 1;
                while (next > anchor + 1 && NavMesh.Raycast(rawRoute[anchor], rawRoute[next], out _, NavMesh.AllAreas))
                    next--;
                route.Add(rawRoute[next]);
                anchor = next;
            }
        }

        private void BuildAcousticPortals()
        {
            acousticPortals.Clear();
            for (int i = 0; i < doorComponents.Count; i++)
            {
                MonoBehaviour behaviour = doorComponents[i];
                if (behaviour == null || !(behaviour is IDoor door)) continue;
                Component component = behaviour;
                Collider collider = component.GetComponentInChildren<Collider>();
                Vector3 center = door.Position;
                Vector3 normal = component.transform.forward;
                float halfWidth = 2f;
                if (collider != null)
                {
                    Bounds bounds = collider.bounds;
                    center = bounds.center;
                    bool normalIsX = bounds.size.x <= bounds.size.z;
                    normal = normalIsX ? Vector3.right : Vector3.forward;
                    halfWidth = 0.5f * (normalIsX ? bounds.size.z : bounds.size.x) + 1.5f;
                }
                int first = FindDoorSideNode(center, normal, halfWidth, true);
                int second = FindDoorSideNode(center, normal, halfWidth, false);
                if (first < 0 || second < 0 || first == second) continue;
                double distance = HunterNavPoint.Distance(graph.GetPoint(first), graph.GetPoint(second));
                acousticPortals.Add(new HunterAcousticPortal(first, second, distance,
                    door.IsOpen ? 0f : closedDoorSoundPenalty, !door.IsOpen));
            }
        }

        private int FindDoorSideNode(Vector3 center, Vector3 normal, float halfWidth, bool positive)
        {
            int selected = -1;
            float best = doorwayNodeSearchRadius * doorwayNodeSearchRadius;
            normal.y = 0f;
            if (normal.sqrMagnitude < 0.001f) normal = Vector3.forward;
            normal.Normalize();
            Vector3 tangent = new Vector3(-normal.z, 0f, normal.x);
            for (int i = 0; i < graph.NodeCount; i++)
            {
                Vector3 offset = ToUnity(graph.GetPoint(i)) - center;
                float side = Vector3.Dot(offset, normal);
                if ((positive && side <= 0.05f) || (!positive && side >= -0.05f) ||
                    Mathf.Abs(Vector3.Dot(offset, tangent)) > halfWidth) continue;
                float distance = offset.sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                selected = i;
            }
            return selected;
        }

        private static bool Finite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static HunterNavPoint ToPoint(Vector3 p) => new HunterNavPoint(p.x, p.y, p.z);
        private static Vector3 ToUnity(HunterNavPoint p) => new Vector3(p.X, p.Y, p.Z);
    }
}
