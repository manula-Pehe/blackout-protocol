using System;
using System.Collections.Generic;
using BlackoutProtocol.Core;
using UnityEngine;

namespace BlackoutProtocol.Body
{
    /// <summary>
    /// The shared body every enemy brain drives. A brain sends a <see cref="MoveCommand"/>
    /// (path + style + optional look target); the body follows the path, turns smoothly
    /// and reports <see cref="Arrived"/> or <see cref="Stuck"/> back. Brains never move the
    /// transform themselves, and the body never makes decisions.
    /// </summary>
    [RequireComponent(typeof(AgentMotor))]
    [RequireComponent(typeof(AgentRotation))]
    [DisallowMultipleComponent]
    public sealed class AgentBody : MonoBehaviour, IAgentBody
    {
        [Header("Speeds (m/s)")]
        [SerializeField] private float walkSpeed = 1.6f;
        [SerializeField] private float runSpeed = 4.0f;
        [SerializeField] private float sneakSpeed = 1.0f;
        [SerializeField] private float strafeSpeed = 1.8f;

        [Tooltip("How quickly speed changes, in m/s². Avoids instant starts and stops.")]
        [SerializeField] private float acceleration = 8f;

        [Header("Path following")]
        [Tooltip("Distance at which an intermediate waypoint counts as reached.")]
        [SerializeField] private float waypointAcceptanceRadius = 0.4f;

        [Tooltip("Distance at which the final waypoint counts as reached.")]
        [SerializeField] private float arrivalRadius = 0.15f;

        [Header("Stuck detection")]
        [Tooltip("Seconds without useful progress before Stuck is raised.")]
        [SerializeField] private float stuckTimeout = 2.5f;

        [Tooltip("Minimum reduction in waypoint distance that counts as progress.")]
        [SerializeField] private float minimumProgressDistance = 0.1f;

        [Header("Turning")]
        [Tooltip("If the next waypoint is further than this angle away, turn on the spot before walking.")]
        [SerializeField] private float turnInPlaceAngle = 100f;

        [Header("Debug")]
        [SerializeField] private bool drawPath = true;

        private readonly PathFollower follower = new PathFollower();

        private AgentMotor motor;
        private AgentRotation rotation;
        private MoveStyle style = MoveStyle.Idle;
        private Transform lookTarget;
        private float currentSpeed;
        private Vector3 lastMoveDirection;
        private int trackedWaypointIndex = -1;
        private float bestDistanceToWaypoint;
        private float lastProgressTime;

        /// <inheritdoc />
        public event Action Arrived;

        /// <inheritdoc />
        public event Action Stuck;

        /// <summary>Raised by <see cref="PlayAction"/>; the animator driver plays the matching state.</summary>
        public event Action<AgentAction> ActionRequested;

        /// <inheritdoc />
        public Vector3 Position => transform.position;

        /// <inheritdoc />
        public Vector3 Velocity => motor != null ? motor.Velocity : Vector3.zero;

        /// <inheritdoc />
        public bool IsMoving { get; private set; }

        /// <inheritdoc />
        public bool HasArrived { get; private set; }

        /// <summary>Style of the current command.</summary>
        public MoveStyle Style => style;

        /// <summary>Current speed the body is aiming for, in m/s (before collisions).</summary>
        public float CurrentSpeed => currentSpeed;

        /// <summary>Waypoints of the current path (for debug views and gizmos).</summary>
        public IReadOnlyList<Vector3> Waypoints => follower.Waypoints;

        /// <summary>Index of the waypoint currently being walked to.</summary>
        public int CurrentWaypointIndex => follower.CurrentIndex;

        private void Awake()
        {
            motor = GetComponent<AgentMotor>();
            rotation = GetComponent<AgentRotation>();
            lastMoveDirection = transform.forward;
        }

        private void OnDisable()
        {
            follower.Clear();
            IsMoving = false;
            currentSpeed = 0f;
        }

        /// <inheritdoc />
        public void Execute(MoveCommand command)
        {
            style = command.Style;
            lookTarget = command.LookTarget;
            HasArrived = false;

            if (!command.HasPath || command.Style == MoveStyle.Idle)
            {
                // Hold: stand still, optionally keep facing the look target.
                follower.Clear();
                IsMoving = false;
                return;
            }

            follower.SetPath(command.Path);
            IsMoving = true;

            // The first waypoint is usually the agent's own position, so skip anything
            // already reached. A path that is already complete arrives immediately.
            if (follower.Advance(Position, waypointAcceptanceRadius, arrivalRadius))
            {
                Arrive();
                return;
            }

            ResetProgressTracking();
        }

        /// <inheritdoc />
        public void Stop()
        {
            follower.Clear();
            IsMoving = false;
            HasArrived = false;
            style = MoveStyle.Idle;
            lookTarget = null;
            trackedWaypointIndex = -1;
        }

        /// <inheritdoc />
        public void PlayAction(AgentAction action)
        {
            ActionRequested?.Invoke(action);
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            Vector3 desiredDirection = Vector3.zero;
            float targetSpeed = 0f;

            if (IsMoving)
            {
                if (follower.Advance(Position, waypointAcceptanceRadius, arrivalRadius))
                {
                    Arrive();
                }
                else
                {
                    Vector3 toWaypoint = follower.CurrentWaypoint - Position;
                    toWaypoint.y = 0f;
                    desiredDirection = toWaypoint.normalized;
                    targetSpeed = SpeedFor(style);
                }
            }

            UpdateRotation(desiredDirection, ref targetSpeed, deltaTime);

            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * deltaTime);

            if (desiredDirection != Vector3.zero)
            {
                lastMoveDirection = desiredDirection;
            }

            motor.Move(lastMoveDirection * currentSpeed, deltaTime);

            if (IsMoving)
            {
                DetectStuck();
            }
        }

        private void UpdateRotation(Vector3 desiredDirection, ref float targetSpeed, float deltaTime)
        {
            bool strafing = style == MoveStyle.Strafe && lookTarget != null;

            if (strafing)
            {
                // Strafe: move along the path but keep the body facing the target.
                rotation.FacePoint(lookTarget.position, deltaTime);
                return;
            }

            if (desiredDirection != Vector3.zero)
            {
                rotation.FaceDirection(desiredDirection, deltaTime);

                // Do not walk sideways or backwards: turn on the spot first.
                if (rotation.AngleTo(desiredDirection) > turnInPlaceAngle)
                {
                    targetSpeed = 0f;
                }

                return;
            }

            if (lookTarget != null)
            {
                rotation.FacePoint(lookTarget.position, deltaTime);
            }
        }

        private void Arrive()
        {
            IsMoving = false;
            HasArrived = true;
            trackedWaypointIndex = -1;

            // Temporary hard stop at the destination; the arrive (slowing radius)
            // behaviour in the steering branch makes the approach smooth.
            currentSpeed = 0f;

            Arrived?.Invoke();
        }

        /// <summary>Raises <see cref="Stuck"/>. Called by stuck detection (robustness branch).</summary>
        private void RaiseStuck()
        {
            // Restart the timer before notifying listeners so an unhandled event
            // cannot fire every frame. A listening brain normally stops or
            // replaces the current move command while replanning.
            ResetProgressTracking();
            Stuck?.Invoke();
        }

        private void DetectStuck()
        {
            if (follower.IsFinished)
            {
                return;
            }

            if (trackedWaypointIndex != follower.CurrentIndex)
            {
                ResetProgressTracking();
                return;
            }

            float distanceToWaypoint = HorizontalDistance(
                Position,
                follower.CurrentWaypoint);

            if (distanceToWaypoint <=
                bestDistanceToWaypoint - minimumProgressDistance)
            {
                bestDistanceToWaypoint = distanceToWaypoint;
                lastProgressTime = Time.time;
                return;
            }

            if (Time.time - lastProgressTime >= stuckTimeout)
            {
                RaiseStuck();
            }
        }

        private void ResetProgressTracking()
        {
            if (!IsMoving || follower.IsFinished)
            {
                trackedWaypointIndex = -1;
                bestDistanceToWaypoint = 0f;
                lastProgressTime = Time.time;
                return;
            }

            trackedWaypointIndex = follower.CurrentIndex;
            bestDistanceToWaypoint = HorizontalDistance(
                Position,
                follower.CurrentWaypoint);
            lastProgressTime = Time.time;
        }

        private static float HorizontalDistance(Vector3 first, Vector3 second)
        {
            first.y = 0f;
            second.y = 0f;
            return Vector3.Distance(first, second);
        }

        private float SpeedFor(MoveStyle moveStyle)
        {
            switch (moveStyle)
            {
                case MoveStyle.Walk: return walkSpeed;
                case MoveStyle.Run: return runSpeed;
                case MoveStyle.Sneak: return sneakSpeed;
                case MoveStyle.Strafe: return strafeSpeed;
                default: return 0f;
            }
        }

        private void OnValidate()
        {
            stuckTimeout = Mathf.Max(0.1f, stuckTimeout);
            minimumProgressDistance = Mathf.Max(
                0.001f,
                minimumProgressDistance);
        }

        private void OnDrawGizmos()
        {
            if (!drawPath || !follower.HasPath)
            {
                return;
            }

            IReadOnlyList<Vector3> points = follower.Waypoints;

            // Walked part in grey, remaining part in cyan.
            for (int i = 0; i < points.Count - 1; i++)
            {
                Gizmos.color = i < follower.CurrentIndex ? Color.gray : Color.cyan;
                Gizmos.DrawLine(points[i], points[i + 1]);
            }

            for (int i = 0; i < points.Count; i++)
            {
                Gizmos.color = i == follower.CurrentIndex ? Color.yellow : Color.cyan;
                Gizmos.DrawWireSphere(points[i], 0.12f);
            }

            if (IsMoving && !follower.IsFinished)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(Position, follower.CurrentWaypoint);
            }
        }
    }
}
