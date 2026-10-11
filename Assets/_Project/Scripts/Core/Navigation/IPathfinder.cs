using System.Collections.Generic;
using UnityEngine;

namespace BlackoutProtocol.Core
{
    /// <summary>
    /// Common signature for each agent's own pathfinding implementation.
    /// The search algorithm, graph and costs belong to each agent.
    /// </summary>
    public interface IPathfinder
    {
        /// <summary>
        /// Finds a path between two world positions.
        /// </summary>
        /// <param name="start">Start position in world space.</param>
        /// <param name="goal">Goal position in world space.</param>
        /// <param name="result">
        /// Cleared, then filled with world-space waypoints from start to goal.
        /// Passing a reused list avoids allocating a new one on every search.
        /// </param>
        /// <returns>True if a path was found.</returns>
        bool TryFindPath(Vector3 start, Vector3 goal, List<Vector3> result);
    }
}
