using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlackoutProtocol.AI.Breacher.Logic
{
    /// <summary>
    /// A directed connection between two navigation nodes.
    /// </summary>
    public sealed class NavEdge
    {
        public NavEdge(NavNode target, float cost)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));

            if (cost <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(cost),
                    "An edge cost must be greater than zero.");
            }

            Cost = cost;
        }

        public NavNode Target { get; }
        public float Cost { get; }
    }

    /// <summary>
    /// One position in the navigation graph and its outgoing edges.
    /// </summary>
    public sealed class NavNode
    {
        private readonly List<NavEdge> edges = new List<NavEdge>();

        public NavNode(int id, Vector3 position)
        {
            Id = id;
            Position = position;
        }

        public int Id { get; }
        public Vector3 Position { get; }
        public IReadOnlyList<NavEdge> Edges => edges;

        public void ConnectTo(NavNode target, float cost)
        {
            edges.Add(new NavEdge(target, cost));
        }
    }

    /// <summary>
    /// Owns all nodes in one navigation graph.
    /// </summary>
    public sealed class NavGraph
    {
        private readonly List<NavNode> nodes = new List<NavNode>();

        public IReadOnlyList<NavNode> Nodes => nodes;

        public NavNode AddNode(Vector3 position)
        {
            NavNode node = new NavNode(nodes.Count, position);
            nodes.Add(node);
            return node;
        }

        public void ConnectBidirectional(
            NavNode first,
            NavNode second,
            float cost)
        {
            if (first == null)
            {
                throw new ArgumentNullException(nameof(first));
            }

            if (second == null)
            {
                throw new ArgumentNullException(nameof(second));
            }

            first.ConnectTo(second, cost);
            second.ConnectTo(first, cost);
        }
    }
}
