using System.Collections.Generic;
using BlackoutProtocol.Core;
using UnityEngine;

namespace BlackoutProtocol.Body.Testing
{
    /// <summary>
    /// Sandbox-only stand-in for a brain: sends a hand-placed route to the body as a
    /// <see cref="MoveCommand"/> and walks it back and forth. Lets the body be tested
    /// before any A* exists. Not used in the real game.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WaypointRouteDriver : MonoBehaviour
    {
        [SerializeField] private Transform[] waypoints = new Transform[0];
        [SerializeField] private MoveStyle style = MoveStyle.Walk;

        [Tooltip("Optional. Used with the Strafe style to face something while moving.")]
        [SerializeField] private Transform lookTarget;

        [SerializeField] private bool playOnStart = true;
        [SerializeField] private bool pingPong = true;
        [SerializeField] private float pauseAtEnd = 1f;

        // Reused on purpose: the body must copy the path, not keep this list.
        private readonly List<Vector3> path = new List<Vector3>();

        private IAgentBody body;
        private bool reversed;

        private void Awake()
        {
            body = GetComponent<IAgentBody>();

            if (body == null)
            {
                Debug.LogError("WaypointRouteDriver needs an IAgentBody on the same object.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (body != null)
            {
                body.Arrived += OnArrived;
            }
        }

        private void OnDisable()
        {
            if (body != null)
            {
                body.Arrived -= OnArrived;
            }

            CancelInvoke();
        }

        private void Start()
        {
            if (playOnStart)
            {
                SendRoute();
            }
        }

        /// <summary>Sends the route to the body (also available from the component menu).</summary>
        [ContextMenu("Send Route")]
        public void SendRoute()
        {
            path.Clear();

            foreach (Transform waypoint in waypoints)
            {
                if (waypoint != null)
                {
                    path.Add(waypoint.position);
                }
            }

            if (reversed)
            {
                path.Reverse();
            }

            body.Execute(new MoveCommand(path, style, lookTarget));
        }

        /// <summary>Stops the body (also available from the component menu).</summary>
        [ContextMenu("Stop")]
        public void StopRoute()
        {
            CancelInvoke();
            body.Stop();
        }

        private void OnArrived()
        {
            if (!pingPong)
            {
                return;
            }

            reversed = !reversed;
            Invoke(nameof(SendRoute), pauseAtEnd);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.magenta;

            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null)
                {
                    continue;
                }

                Gizmos.DrawSphere(waypoints[i].position, 0.08f);

                if (i + 1 < waypoints.Length && waypoints[i + 1] != null)
                {
                    Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
                }
            }
        }
    }
}
