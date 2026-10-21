using UnityEngine;

namespace BlackoutProtocol.Body
{
    /// <summary>
    /// Bounded local steering for ground bodies only. Never edits waypoints or moves a transform.
    /// Physics queries fail closed; CharacterController remains the final collision authority.
    /// </summary>
    internal sealed class AgentSeparation
    {
        private readonly Collider[] neighbours = new Collider[32];
        private readonly RaycastHit[] hits = new RaycastHit[32];
        private readonly AgentBody owner;
        private readonly CharacterController controller;
        private int encounterId;
        private float passingSide, bias;
        private Vector3 routeStart;

        public bool IsSteering { get; private set; }
        public bool QuerySaturated { get; private set; }

        public AgentSeparation(AgentBody owner)
        {
            this.owner = owner;
            controller = owner.GetComponent<CharacterController>();
            Reset();
        }

        public void Reset()
        {
            encounterId = 0;
            bias = 0;
            IsSteering = QuerySaturated = false;
            routeStart = owner.Position;
        }

        public Vector3 Steer(Vector3 direction, float distanceToWaypoint, float radius, float clearance,
            float maxDeviation, float response, float dt)
        {
            IsSteering = QuerySaturated = false;
            if (controller == null || !controller.enabled || !controller.isGrounded || direction.sqrMagnitude < 0.0001f)
            { bias = 0; encounterId = 0; return direction; }

            Vector3 center = controller.bounds.center;
            float ownRadius = WorldRadius(controller);
            int count = Physics.OverlapSphereNonAlloc(center, radius, neighbours, ~0, QueryTriggerInteraction.Ignore);
            if (count == neighbours.Length) { QuerySaturated = true; bias = 0; return direction; }
            AgentBody selected = null;
            float best = float.PositiveInfinity, selectedThreat = 0, selectedClearance = 0;
            for (int i = 0; i < count; i++)
            {
                Collider collider = neighbours[i];
                if (Ignored(collider)) continue;
                var other = collider.GetComponentInParent<AgentBody>();
                if (other == null || other == owner || !other.isActiveAndEnabled) continue;
                var cc = other.GetComponent<CharacterController>();
                if (cc == null || !cc.enabled || Mathf.Abs(center.y - cc.bounds.center.y) >
                    Mathf.Min(controller.bounds.extents.y, cc.bounds.extents.y)) continue;
                Vector3 offset = other.Position - owner.Position; offset.y = 0;
                float pairClearance = ownRadius + WorldRadius(cc) + clearance;
                float threat = SeparationMath.Threat(direction, offset, pairClearance, radius);
                // Hold a chosen passing side until the other body is actually behind.
                // Recentring as soon as lateral clearance appears causes head-on oscillation.
                bool locked = other.GetInstanceID() == encounterId &&
                    Vector3.Dot(offset, direction) > -pairClearance - 0.25f;
                if (locked) threat = Mathf.Max(threat, 0.5f);
                if (threat <= 0) continue;
                float rank = locked ? -1 : offset.sqrMagnitude;
                if (rank > best || (rank == best && selected != null && other.GetInstanceID() > selected.GetInstanceID())) continue;
                best = rank; selected = other; selectedThreat = threat; selectedClearance = pairClearance;
            }

            float desiredBias = 0;
            if (selected != null)
            {
                if (encounterId != selected.GetInstanceID())
                {
                    encounterId = selected.GetInstanceID();
                    Vector3 otherForward = selected.IsMoving && selected.CurrentWaypointIndex < selected.Waypoints.Count
                        ? selected.Waypoints[selected.CurrentWaypointIndex] - selected.Position : Vector3.zero;
                    passingSide = SeparationMath.PassingSide(direction, selected.Position - owner.Position,
                        otherForward, owner.GetInstanceID(), encounterId);
                }
                desiredBias = passingSide * selectedThreat * 1.5f;
            }
            else encounterId = 0;
            bias = Mathf.MoveTowards(bias, desiredBias, response * Mathf.Max(0,dt));
            if (Mathf.Abs(bias) < 0.0001f) return direction;

            Vector3 candidate = (direction + Vector3.Cross(Vector3.up,direction) * bias).normalized;
            // Stay in a narrow band around this segment, not a shortcut to a later waypoint.
            int index = owner.CurrentWaypointIndex;
            Vector3 origin = index > 0 ? owner.Waypoints[index-1] : routeStart;
            Vector3 axis = owner.Waypoints[index] - origin; axis.y = 0;
            if (axis.sqrMagnitude > 0.0001f)
            {
                axis.Normalize(); Vector3 right = Vector3.Cross(Vector3.up,axis);
                float lateral = Vector3.Dot(owner.Position - origin,right);
                float nextLateral = lateral + Vector3.Dot(candidate,right)*0.4f;
                if (Mathf.Abs(nextLateral) > maxDeviation)
                {
                    float allowed = (Mathf.Clamp(nextLateral,-maxDeviation,maxDeviation)-lateral)/0.4f;
                    candidate = (axis * Mathf.Max(0.1f,Vector3.Dot(candidate,axis)) + right*allowed).normalized;
                }
            }
            // Near the actual waypoint, continue closing on it. If occupied, the
            // normal collision/stuck contract handles it; never orbit a blocked goal.
            if (distanceToWaypoint < selectedClearance + 0.25f && selected != null &&
                PathFollower.HorizontalDistance(selected.Position,owner.Waypoints[index]) < selectedClearance)
                return direction;
            if (!SafeDirection(candidate)) { bias = 0; return direction; }
            IsSteering = true;
            return candidate;
        }

        private bool SafeDirection(Vector3 direction)
        {
            float r = Mathf.Max(0.02f,WorldRadius(controller)-controller.skinWidth-0.01f);
            Vector3 center = controller.bounds.center;
            float half = Mathf.Max(0,controller.bounds.extents.y-WorldRadius(controller));
            int n = Physics.CapsuleCastNonAlloc(center+Vector3.up*half, center-Vector3.up*half, r,
                direction,hits,0.45f,~0,QueryTriggerInteraction.Ignore);
            if (n == hits.Length) { QuerySaturated = true; return false; }
            float walkable = Mathf.Cos(controller.slopeLimit*Mathf.Deg2Rad);
            for (int i = 0; i < n; i++)
            {
                var collider = hits[i].collider;
                if (Ignored(collider) || collider.GetComponentInParent<AgentBody>() != null) continue;
                if (hits[i].normal.y < walkable) return false;
            }
            // Separation must not steer into unsupported space or down an unbounded drop.
            Vector3 foot = center-Vector3.up*controller.bounds.extents.y+direction*0.35f;
            // Include a small support margin, not just the capsule centre. Otherwise
            // successive tiny accepted sidesteps can creep to an unsupported edge.
            Vector3 side = Vector3.Cross(Vector3.up,direction)*(WorldRadius(controller)+0.15f);
            return Supported(foot,walkable) && Supported(foot-side,walkable) && Supported(foot+side,walkable);
        }

        private bool Supported(Vector3 foot,float walkable)
        {
            int n = Physics.RaycastNonAlloc(foot+Vector3.up*0.15f,Vector3.down,hits,0.45f,~0,QueryTriggerInteraction.Ignore);
            if (n == hits.Length) { QuerySaturated = true; return false; }
            float nearest = float.PositiveInfinity; int selected = -1;
            for (int i = 0; i < n; i++)
                if (!Ignored(hits[i].collider) && hits[i].distance < nearest) { nearest=hits[i].distance; selected=i; }
            return selected >= 0 && hits[selected].normal.y >= walkable &&
                hits[selected].collider.GetComponentInParent<AgentBody>() == null;
        }

        private bool Ignored(Collider collider) => collider == null || collider == controller ||
            collider.transform.IsChildOf(owner.transform) || Physics.GetIgnoreLayerCollision(owner.gameObject.layer,collider.gameObject.layer) ||
            Physics.GetIgnoreCollision(controller,collider);

        private static float WorldRadius(CharacterController cc) => cc.radius *
            Mathf.Max(Mathf.Abs(cc.transform.lossyScale.x),Mathf.Abs(cc.transform.lossyScale.z));
    }
}
