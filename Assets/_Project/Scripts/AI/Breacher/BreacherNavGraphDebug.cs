using BlackoutProtocol.AI.Breacher.Logic;
using UnityEngine;

namespace BlackoutProtocol.AI.Breacher
{
    /// <summary>
    /// Builds and draws the Breacher's graph for development evidence.
    /// </summary>
    public sealed class BreacherNavGraphDebug : MonoBehaviour
    {
        [SerializeField] private bool drawGraph = true;
        [SerializeField] private float nodeRadius = 0.06f;
        [SerializeField] private float verticalOffset = 0.1f;
        [SerializeField] private Color nodeColour = Color.red;
        [SerializeField] private Color edgeColour = Color.yellow;

        [Header("Path Comparison")]
        [SerializeField] private Transform startMarker;
        [SerializeField] private Transform goalMarker;
        [SerializeField] private bool showUcsPath;
        [SerializeField] private Color pathColour = Color.cyan;
        [SerializeField] private float pathVerticalOffset = 0.25f;

        private NavGraph graph;
        private PathSearchResult aStarResult;
        private PathSearchResult ucsResult;

        public NavGraph Graph => graph;

        private void Start()
        {
            RebuildGraph();
        }

        [ContextMenu("Rebuild Graph")]
        public void RebuildGraph()
        {
            graph = NavMeshGraphBuilder.Build();
            Debug.Log(
                $"Breacher graph built with {graph.Nodes.Count} nodes.",
                this);

            CompareSearches();
        }

        [ContextMenu("Compare A Star And UCS")]
        public void CompareSearches()
        {
            aStarResult = null;
            ucsResult = null;

            if (graph == null || graph.Nodes.Count == 0 ||
                startMarker == null || goalMarker == null)
            {
                return;
            }

            NavNode start = FindClosestNode(startMarker.position);
            NavNode goal = FindClosestNode(goalMarker.position);

            aStarResult = PathSearch.FindWithAStar(start, goal);
            ucsResult = PathSearch.FindWithUcs(start, goal);

            LogResult("A*", aStarResult);
            LogResult("UCS", ucsResult);
        }

        private void OnDrawGizmos()
        {
            if (!drawGraph || graph == null)
            {
                return;
            }

            Gizmos.color = edgeColour;

            foreach (NavNode node in graph.Nodes)
            {
                foreach (NavEdge edge in node.Edges)
                {
                    if (node.Id < edge.Target.Id)
                    {
                        Gizmos.DrawLine(
                            GetDrawPosition(node),
                            GetDrawPosition(edge.Target));
                    }
                }
            }

            Gizmos.color = nodeColour;

            foreach (NavNode node in graph.Nodes)
            {
                Gizmos.DrawSphere(GetDrawPosition(node), nodeRadius);
            }

            DrawSelectedPath();
        }

        private Vector3 GetDrawPosition(NavNode node)
        {
            return node.Position + Vector3.up * verticalOffset;
        }

        private NavNode FindClosestNode(Vector3 position)
        {
            NavNode closest = graph.Nodes[0];
            float closestDistance =
                (closest.Position - position).sqrMagnitude;

            for (int index = 1; index < graph.Nodes.Count; index++)
            {
                NavNode candidate = graph.Nodes[index];
                float distance =
                    (candidate.Position - position).sqrMagnitude;

                if (distance < closestDistance)
                {
                    closest = candidate;
                    closestDistance = distance;
                }
            }

            return closest;
        }

        private void DrawSelectedPath()
        {
            PathSearchResult selected = showUcsPath
                ? ucsResult
                : aStarResult;

            if (selected == null || !selected.Succeeded)
            {
                return;
            }

            Gizmos.color = pathColour;

            for (int index = 0; index < selected.Path.Count; index++)
            {
                Vector3 position =
                    selected.Path[index].Position +
                    Vector3.up * pathVerticalOffset;

                Gizmos.DrawSphere(position, nodeRadius * 1.5f);

                if (index + 1 < selected.Path.Count)
                {
                    Vector3 nextPosition =
                        selected.Path[index + 1].Position +
                        Vector3.up * pathVerticalOffset;

                    Gizmos.DrawLine(position, nextPosition);
                }
            }
        }

        private void LogResult(string algorithm, PathSearchResult result)
        {
            Debug.Log(
                $"{algorithm}: success={result.Succeeded}, " +
                $"cost={result.TotalCost:F2}, " +
                $"expanded={result.ExpandedNodeCount}, " +
                $"time={result.ElapsedMilliseconds:F3} ms",
                this);
        }

        private void OnValidate()
        {
            nodeRadius = Mathf.Max(0.01f, nodeRadius);
            verticalOffset = Mathf.Max(0f, verticalOffset);
            pathVerticalOffset = Mathf.Max(0f, pathVerticalOffset);
        }
    }
}
