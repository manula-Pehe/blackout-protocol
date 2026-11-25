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
            if (ReferenceEquals(path, waypoints))
            {
                // Public debug views can also be submitted as a command. They are
                // already validated; clearing the backing list would erase the input.
                CurrentIndex = 0;
                return;
            }
            waypoints.Clear();
            CurrentIndex = 0;

            if (path == null)
            {
                return;
            }

            for (int i = 0; i < path.Count; i++)
            {
                Vector3 point = path[i];
                if (!Finite(point.x) || !Finite(point.y) || !Finite(point.z) || !Finite(point.sqrMagnitude))
                {
                    // Reject the whole route: dropping only a bad point could bridge a wall.
                    waypoints.Clear();
                    return;
                }
                // Identical consecutive points must not disguise a bend as two straight segments.
                if (waypoints.Count == 0 || !point.Equals(waypoints[waypoints.Count - 1])) waypoints.Add(point);
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
            return Advance(position, acceptanceRadius, arrivalRadius, acceptanceRadius);
        }

        /// <summary>At bends, use a tighter radius without projecting onto or skipping to a later segment.</summary>
        public bool Advance(Vector3 position, float acceptanceRadius, float arrivalRadius, float cornerRadius)
        {
            while (!IsFinished)
            {
                float radius = CurrentAcceptanceRadius(acceptanceRadius, arrivalRadius, cornerRadius);

                if (HorizontalDistance(position, waypoints[CurrentIndex]) > radius)
                {
                    break;
                }

                CurrentIndex++;
            }

            return IsFinished;
        }

        /// <summary>The first waypoint and any non-collinear turn are conservative approach points.</summary>
        public bool IsCurrentCorner
        {
            get
            {
                if (IsFinished || IsOnLastWaypoint) return false;
                if (CurrentIndex == 0) return true;
                Vector3 incoming = waypoints[CurrentIndex] - waypoints[CurrentIndex - 1];
                Vector3 outgoing = waypoints[CurrentIndex + 1] - waypoints[CurrentIndex];
                incoming.y = outgoing.y = 0;
                // Duplicate points are advanced normally; never divide by a zero segment.
                return incoming.sqrMagnitude > 0.000001f && outgoing.sqrMagnitude > 0.000001f &&
                    Vector3.Angle(incoming, outgoing) > 5f;
            }
        }

        public float CurrentAcceptanceRadius(float acceptanceRadius, float arrivalRadius, float cornerRadius)
        {
            if (IsOnLastWaypoint) return Mathf.Max(0, arrivalRadius);
            float radius = Mathf.Max(0, acceptanceRadius);
            if (!IsCurrentCorner) return radius;
            radius = Mathf.Min(radius, Mathf.Max(0, cornerRadius));
            // Do not swallow a short bend merely because several points fit in one radius.
            float outgoing = HorizontalDistance(waypoints[CurrentIndex], waypoints[CurrentIndex + 1]);
            if (outgoing > 0.001f) radius = Mathf.Min(radius, outgoing * 0.25f);
            if (CurrentIndex > 0)
            {
                float incoming = HorizontalDistance(waypoints[CurrentIndex - 1], waypoints[CurrentIndex]);
                if (incoming > 0.001f) radius = Mathf.Min(radius, incoming * 0.25f);
            }
            return radius;
        }

        /// <summary>Horizontal bend angle. The initial point is a conservative approach.</summary>
        public float CurrentTurnAngle
        {
            get
            {
                if (IsFinished || IsOnLastWaypoint) return 0f;
                if (CurrentIndex == 0) return 180f;
                Vector3 incoming = waypoints[CurrentIndex] - waypoints[CurrentIndex - 1];
                Vector3 outgoing = waypoints[CurrentIndex + 1] - waypoints[CurrentIndex];
                incoming.y = outgoing.y = 0f;
                return Vector3.Angle(incoming, outgoing);
            }
        }

        /// <summary>Find a point on the next segment without bypassing current waypoint acceptance.</summary>
        public bool TryGetLookAhead(Vector3 position, float distance, float acceptance, out Vector3 target)
        {
            target = default;
            if (IsFinished || IsOnLastWaypoint || !Finite(distance) || !Finite(acceptance) ||
                distance <= 0f || acceptance <= 0f) return false;
            Vector3 corner = CurrentWaypoint;
            Vector3 next = waypoints[CurrentIndex + 1];
            float remaining = distance - HorizontalDistance(position, corner);
            float outgoingLength = HorizontalDistance(corner, next);
            if (remaining <= 0f || outgoingLength <= 0.0001f) return false;
            // Never aim beyond the next segment. Shrink the chord until it passes
            // comfortably inside the acceptance disk of the current point.
            float fraction = Mathf.Min(0.75f, remaining / outgoingLength);
            for (int attempt = 0; attempt < 6 && fraction > 0.0001f; attempt++, fraction *= 0.5f)
            {
                Vector3 candidate = Vector3.Lerp(corner, next, fraction);
                float t = HorizontalProjection(position, candidate, corner);
                if (t <= 0f) continue;
                Vector3 closest = Vector3.Lerp(position, candidate, t);
                if (HorizontalDistance(closest, corner) > acceptance * 0.5f) continue;
                target = candidate;
                return true;
            }
            return false;
        }

        /// <summary>Find a nearby forward segment for an optional replacement-route join.</summary>
        public bool TryGetNearestJoin(Vector3 position, float maxDistance, out int segment, out Vector3 point)
        {
            segment = -1;
            point = default;
            if (waypoints.Count < 2 || !Finite(maxDistance) || maxDistance <= 0f) return false;
            float initialDistance = HorizontalDistance(position, waypoints[0]);
            // A normal route that starts at the body already needs no repair.
            if (initialDistance <= 0.1f) return false;
            float best = Mathf.Min(maxDistance, initialDistance - 0.05f);
            for (int i = 0; i < waypoints.Count - 1; i++)
            {
                Vector3 a = waypoints[i], b = waypoints[i + 1];
                if (HorizontalDistance(a, b) < 0.0001f) continue;
                Vector3 candidate = Vector3.Lerp(a, b, HorizontalProjection(a, b, position));
                // Avoid joining a different floor even when its XZ projection overlaps.
                if (Mathf.Abs(candidate.y - position.y) > 0.35f) continue;
                float gap = HorizontalDistance(position, candidate);
                if (gap >= best) continue; // Earlier segment wins ties.
                best = gap;
                segment = i;
                point = candidate;
            }
            return segment >= 0;
        }

        /// <summary>Commit a join only after the movement owner validates its NavMesh connection.</summary>
        public void JoinAtSegment(int segment, Vector3 point)
        {
            if (segment < 0 || segment >= waypoints.Count - 1)
                throw new ArgumentOutOfRangeException(nameof(segment));
            waypoints[segment] = point;
            CurrentIndex = segment;
        }

        private static float HorizontalProjection(Vector3 a, Vector3 b, Vector3 point)
        {
            Vector3 line = b - a, offset = point - a;
            line.y = offset.y = 0f;
            return line.sqrMagnitude > 0.000001f
                ? Mathf.Clamp01(Vector3.Dot(offset, line) / line.sqrMagnitude) : 0f;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

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
