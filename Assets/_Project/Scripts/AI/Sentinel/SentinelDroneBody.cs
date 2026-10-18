using System;
using System.Collections.Generic;
using BlackoutProtocol.Core;
using UnityEngine;
using UnityEngine.AI;

namespace BlackoutProtocol.AI.Sentinel
{
    /// <summary>
    /// Executes brain commands in 3D while maintaining a steady hover above the
    /// baked NavMesh. It is the Sentinel-specific adapter for the shared body contract.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SentinelDroneBody : MonoBehaviour, IAgentBody
    {
        [SerializeField, Min(0.1f)] private float hoverHeight = 2.2f;
        [SerializeField, Min(0.1f)] private float walkSpeed = 2.2f;
        [SerializeField, Min(0.1f)] private float runSpeed = 4.2f;
        [SerializeField, Min(0.1f)] private float searchSpeed = 1.5f;
        [SerializeField, Min(0.01f)] private float arrivalRadius = 0.25f;
        [SerializeField, Min(0f)] private float hoverBobAmplitude = 0.04f;
        [SerializeField, Min(0.1f)] private float hoverBobFrequency = 1.8f;
        [SerializeField, Min(0.1f)] private float turnSpeed = 240f;
        [SerializeField, Min(0.01f)] private float collisionRadius = 0.18f;
        [SerializeField, Min(0.1f)] private float stuckTimeout = 2.5f;
        [SerializeField, Min(0.01f)] private float minimumProgressDistance = 0.08f;

        private readonly List<Vector3> waypoints = new List<Vector3>(16);
        private readonly RaycastHit[] movementHits = new RaycastHit[12];
        private int waypointIndex;
        private MoveStyle style = MoveStyle.Idle;
        private Transform lookTarget;
        private float bestDistanceToWaypoint;
        private float lastProgressTime;

        /// <inheritdoc />
        public event Action Arrived;

        /// <inheritdoc />
        public event Action Stuck;

        /// <summary>Raised when a brain asks the drone to play a one-shot action.</summary>
        public event Action<AgentAction> ActionRequested;

        /// <inheritdoc />
        public Vector3 Position => transform.position;

        /// <inheritdoc />
        public Vector3 Velocity { get; private set; }

        /// <inheritdoc />
        public bool IsMoving { get; private set; }

        /// <inheritdoc />
        public bool HasArrived { get; private set; }

        /// <summary>The configured distance between the NavMesh surface and the drone pivot.</summary>
        public float HoverHeight => hoverHeight;

        /// <inheritdoc />
        public void Execute(MoveCommand command)
        {
            style = command.Style;
            lookTarget = command.LookTarget;
            HasArrived = false;
            waypoints.Clear();

            if (!command.HasPath || style == MoveStyle.Idle)
            {
                IsMoving = false;
                return;
            }

            for (int i = 0; i < command.Path.Count; i++)
            {
                waypoints.Add(command.Path[i]);
            }

            waypointIndex = 0;
            IsMoving = waypoints.Count > 0;
            AdvanceReachedWaypoints();
            if (waypointIndex >= waypoints.Count)
            {
                Arrive();
            }
            ResetStuckTracking();
        }

        /// <inheritdoc />
        public void Stop()
        {
            waypoints.Clear();
            waypointIndex = 0;
            IsMoving = false;
            HasArrived = false;
            style = MoveStyle.Idle;
            lookTarget = null;
            Velocity = Vector3.zero;
            ResetStuckTracking();
        }

        /// <inheritdoc />
        public void PlayAction(AgentAction action)
        {
            ActionRequested?.Invoke(action);
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
            {
                return;
            }

            Vector3 before = transform.position;
            Vector3 desiredDirection = Vector3.zero;
            float speed = walkSpeed;
            Vector3 hoverTarget = GetHoverTarget();

            if (IsMoving)
            {
                AdvanceReachedWaypoints();
                if (waypointIndex >= waypoints.Count)
                {
                    Arrive();
                }
            }

            if (IsMoving && waypointIndex < waypoints.Count)
            {
                Vector3 groundTarget = waypoints[waypointIndex];
                hoverTarget = new Vector3(groundTarget.x, groundTarget.y + hoverHeight, groundTarget.z);
                Vector3 horizontalToTarget = groundTarget - transform.position;
                horizontalToTarget.y = 0f;
                desiredDirection = horizontalToTarget.normalized;
                speed = SpeedFor(style);
                AdvanceReachedWaypoints();
                if (waypointIndex >= waypoints.Count)
                {
                    Arrive();
                    hoverTarget = GetHoverTarget();
                    desiredDirection = Vector3.zero;
                    speed = 0f;
                }
            }

            hoverTarget.y += Mathf.Sin(Time.time * hoverBobFrequency) * hoverBobAmplitude;
            Vector3 next = Vector3.MoveTowards(transform.position, hoverTarget, speed * deltaTime);
            Vector3 displacement = next - transform.position;
            float distance = displacement.magnitude;
            if (distance > 0.0001f && HasMovementBlock(transform.position, displacement / distance, distance, out RaycastHit obstruction))
            {
                float allowedDistance = Mathf.Max(0f, obstruction.distance - 0.015f);
                next = transform.position + displacement / distance * allowedDistance;
            }
            transform.position = next;

            Vector3 facing = (style == MoveStyle.Strafe || !IsMoving) && lookTarget != null
                ? lookTarget.position - transform.position
                : desiredDirection;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(facing.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * deltaTime);
            }

            Velocity = (transform.position - before) / deltaTime;

            if (IsMoving)
            {
                DetectStuck();
            }
        }

        private Vector3 GetHoverTarget()
        {
            Vector3 probe = transform.position;
            probe.y -= hoverHeight;
            if (NavMesh.SamplePosition(probe, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                return hit.position + Vector3.up * hoverHeight;
            }

            return transform.position;
        }

        private void AdvanceReachedWaypoints()
        {
            int previousIndex = waypointIndex;
            while (waypointIndex < waypoints.Count)
            {
                Vector3 target = waypoints[waypointIndex];
                Vector2 currentXZ = new Vector2(transform.position.x, transform.position.z);
                Vector2 targetXZ = new Vector2(target.x, target.z);
                if (Vector2.Distance(currentXZ, targetXZ) > arrivalRadius)
                {
                    break;
                }
                waypointIndex++;
            }
            if (previousIndex != waypointIndex)
            {
                ResetStuckTracking();
            }
        }

        private void Arrive()
        {
            if (!IsMoving)
            {
                return;
            }
            IsMoving = false;
            HasArrived = true;
            style = MoveStyle.Idle;
            Arrived?.Invoke();
        }

        private bool HasMovementBlock(Vector3 origin, Vector3 direction, float distance, out RaycastHit nearestHit)
        {
            int hitCount = Physics.SphereCastNonAlloc(
                origin,
                collisionRadius,
                direction,
                movementHits,
                distance,
                Physics.AllLayers,
                QueryTriggerInteraction.Ignore);
            float nearestDistance = float.PositiveInfinity;
            nearestHit = default;
            bool blocked = false;
            for (int i = 0; i < hitCount; i++)
            {
                Transform hitTransform = movementHits[i].transform;
                if (hitTransform == null || hitTransform == transform || hitTransform.IsChildOf(transform))
                {
                    continue;
                }
                if (movementHits[i].distance < nearestDistance)
                {
                    nearestDistance = movementHits[i].distance;
                    nearestHit = movementHits[i];
                    blocked = true;
                }
            }
            return blocked;
        }

        private void DetectStuck()
        {
            if (waypointIndex >= waypoints.Count)
            {
                return;
            }
            Vector3 target = waypoints[waypointIndex];
            Vector2 currentXZ = new Vector2(transform.position.x, transform.position.z);
            Vector2 targetXZ = new Vector2(target.x, target.z);
            float distance = Vector2.Distance(currentXZ, targetXZ);
            if (distance < bestDistanceToWaypoint - minimumProgressDistance)
            {
                bestDistanceToWaypoint = distance;
                lastProgressTime = Time.time;
            }
            else if (Time.time - lastProgressTime >= stuckTimeout)
            {
                ResetStuckTracking();
                Stuck?.Invoke();
            }
        }

        private void ResetStuckTracking()
        {
            if (IsMoving && waypointIndex < waypoints.Count)
            {
                Vector3 target = waypoints[waypointIndex];
                Vector2 currentXZ = new Vector2(transform.position.x, transform.position.z);
                Vector2 targetXZ = new Vector2(target.x, target.z);
                bestDistanceToWaypoint = Vector2.Distance(currentXZ, targetXZ);
            }
            else
            {
                bestDistanceToWaypoint = float.PositiveInfinity;
            }
            lastProgressTime = Time.time;
        }

        private float SpeedFor(MoveStyle movementStyle)
        {
            switch (movementStyle)
            {
                case MoveStyle.Walk: return walkSpeed;
                case MoveStyle.Run: return runSpeed;
                case MoveStyle.Sneak: return searchSpeed;
                case MoveStyle.Strafe: return walkSpeed;
                default: return 0f;
            }
        }

        private void OnValidate()
        {
            hoverHeight = Mathf.Max(0.1f, hoverHeight);
            walkSpeed = Mathf.Max(0.1f, walkSpeed);
            runSpeed = Mathf.Max(0.1f, runSpeed);
            searchSpeed = Mathf.Max(0.1f, searchSpeed);
            arrivalRadius = Mathf.Max(0.01f, arrivalRadius);
            collisionRadius = Mathf.Max(0.01f, collisionRadius);
            stuckTimeout = Mathf.Max(0.1f, stuckTimeout);
            minimumProgressDistance = Mathf.Max(0.01f, minimumProgressDistance);
        }
    }
}
