using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlackoutProtocol.Body
{
    /// <summary>
    /// Tracks progress along a path of waypoints. Pure C# so it can be unit tested
    /// without a scene. Distances are measured on the horizontal plane only, because
    /// NavMesh waypoints and the agent's pivot can sit at slightly different heights.
    /// </summary>
    public sealed class PathFollower
    {
        private readonly List<Vector3> waypoints = new List<Vector3>(32);

        /// <summary>Index of the waypoint the agent is currently heading for.</summary>
        public int CurrentIndex { get; private set; }

        /// <summary>All waypoints of the current path (a private copy).</summary>
        public IReadOnlyList<Vector3> Waypoints => waypoints;

        /// <summary>True when a path with at least one waypoint is loaded.</summary>
        public bool HasPath => waypoints.Count > 0;

        /// <summary>True when every waypoint has been reached (or there is no path).</summary>
        public bool IsFinished => CurrentIndex >= waypoints.Count;

        /// <summary>True when the current waypoint is the final one.</summary>
        public bool IsOnLastWaypoint => waypoints.Count > 0 && CurrentIndex == waypoints.Count - 1;

        /// <summary>The waypoint the agent is currently heading for.</summary>
        public Vector3 CurrentWaypoint
        {
            get
            {
                if (IsFinished)
                {
                    throw new InvalidOperationException("The path is finished.");
                }

                return waypoints[CurrentIndex];
            }
        }

        /// <summary>The last waypoint of the path.</summary>
        public Vector3 Destination
        {
            get
            {
                if (!HasPath)
                {
                    throw new InvalidOperationException("There is no path.");
                }

                return waypoints[waypoints.Count - 1];
            }
        }

        /// <summary>
        /// Loads a new path. The waypoints are copied, because brains reuse their path
        /// lists between searches (see <c>IPathfinder</c>).
        /// </summary>
        public void SetPath(IReadOnlyList<Vector3> path)
        {
            waypoints.Clear();
            CurrentIndex = 0;

            if (path == null)
            {
                return;
            }

            for (int i = 0; i < path.Count; i++)
            {
                waypoints.Add(path[i]);
            }
        }

        /// <summary>Removes the current path.</summary>
        public void Clear()
        {
            waypoints.Clear();
            CurrentIndex = 0;
        }

        /// <summary>
        /// Moves past every waypoint the agent is already close enough to.
        /// Intermediate waypoints use <paramref name="acceptanceRadius"/>; the final one
        /// uses the tighter <paramref name="arrivalRadius"/> so the agent stops on the spot.
        /// </summary>
        /// <returns>True when the path is finished.</returns>
        public bool Advance(Vector3 position, float acceptanceRadius, float arrivalRadius)
        {
            while (!IsFinished)
            {
                float radius = IsOnLastWaypoint ? arrivalRadius : acceptanceRadius;

                if (HorizontalDistance(position, waypoints[CurrentIndex]) > radius)
                {
                    break;
                }

                CurrentIndex++;
            }

            return IsFinished;
        }

        /// <summary>
        /// Horizontal distance still to travel: to the current waypoint, then along the
        /// remaining segments. Used by the arrive behaviour to slow down near the end.
        /// </summary>
        public float RemainingDistance(Vector3 position)
        {
            if (IsFinished)
            {
                return 0f;
            }

            float distance = HorizontalDistance(position, waypoints[CurrentIndex]);

            for (int i = CurrentIndex; i < waypoints.Count - 1; i++)
            {
                distance += HorizontalDistance(waypoints[i], waypoints[i + 1]);
            }

            return distance;
        }

        /// <summary>Distance between two points ignoring height.</summary>
        public static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
