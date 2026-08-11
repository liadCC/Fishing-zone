using FishingZone.Core;
using UnityEngine;

namespace FishingZone.Player
{
    /// <summary>
    /// Seats and releases the player at a station anchor.
    /// The player owns this rather than the station, so every future station type — fishing,
    /// observer — reuses the same seating behaviour instead of reaching into player internals.
    ///
    /// Only movement is suspended. Look and interaction stay live, because the player still needs
    /// to see where they are going and to press the key that gets them out again.
    /// </summary>
    public class PlayerStationController : MonoBehaviour
    {
        [SerializeField]
        private PlayerMovement _movement;

        [SerializeField]
        private CharacterController _characterController;

        [SerializeField]
        private PlayerPlatformRider _platformRider;

        public bool IsOccupyingStation => _currentAnchor != null;

        private Transform _currentAnchor;
        private float _lastAnchorYaw;

        private void Awake()
        {
            // Resolved automatically so the prefab cannot be half-wired.
            if (_movement == null)
            {
                _movement = GetComponent<PlayerMovement>();
            }

            if (_characterController == null)
            {
                _characterController = GetComponent<CharacterController>();
            }

            if (_platformRider == null)
            {
                _platformRider = GetComponent<PlayerPlatformRider>();
            }
        }

        /// <summary>
        /// Seats the player at the anchor and holds them there for as long as they occupy it.
        ///
        /// The player is deliberately NOT re-parented under the boat. A NetworkObject may only be
        /// parented to another NetworkObject, and Netcode reverts anything else, so parenting a
        /// player under a plain anchor object silently undoes itself the moment the hull becomes a
        /// networked object and the boat then sails out from under them. Following the anchor gives
        /// the same result without depending on parenting at all.
        /// </summary>
        public bool TryOccupy(Transform anchor)
        {
            if (anchor == null || IsOccupyingStation || _movement == null || _characterController == null)
            {
                return false;
            }

            _currentAnchor = anchor;

            // The controller must be switched off before the transform is moved, otherwise it
            // fights the assignment and the player is left jittering at the seat.
            _characterController.enabled = false;
            _movement.enabled = false;

            transform.position = anchor.position;
            FaceAnchorHeading(anchor);
            _lastAnchorYaw = anchor.eulerAngles.y;

            // Dropped explicitly rather than left to whichever script order happens to run first:
            // if the rider kept its reference here it would survive the whole seated period.
            if (_platformRider != null)
            {
                _platformRider.ResetTracking();
            }

            return true;
        }

        /// <summary>
        /// Holds the seated player on the anchor and turns them with it.
        ///
        /// Runs after every Update so it sees the hull's final pose for the frame, whether that came
        /// from local physics on the server or from the replicated transform on a client. Yaw is
        /// applied as a delta so the look component's own turning survives, and position is assigned
        /// outright so nothing can accumulate drift.
        /// </summary>
        private void LateUpdate()
        {
            if (!IsOccupyingStation)
            {
                return;
            }

            float anchorYaw = _currentAnchor.eulerAngles.y;
            float yawDelta = Mathf.DeltaAngle(_lastAnchorYaw, anchorYaw);
            if (!Mathf.Approximately(yawDelta, 0f))
            {
                transform.Rotate(0f, yawDelta, 0f, Space.World);
            }

            _lastAnchorYaw = anchorYaw;
            transform.position = _currentAnchor.position;
        }

        private void FaceAnchorHeading(Transform anchor)
        {
            Vector3 heading = Vector3.ProjectOnPlane(anchor.forward, Vector3.up);
            if (heading.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(heading, Vector3.up);
            }
        }

        public void Release()
        {
            if (!IsOccupyingStation)
            {
                return;
            }

            // Nothing to unparent: seating held the player by following the anchor, not by
            // re-parenting them under it.

            // Uprighted before the deck is measured, so the capsule's offsets are purely vertical
            // and the cast below is measuring the same shape that will exist afterwards.
            RestoreUprightHeading();

            // The seat is a fixed point on the hull, which is not necessarily a point the capsule can
            // legally stand at. Resolving it here means the controller is switched on already clear
            // of the deck, instead of being switched on inside it and ejected by depenetration.
            if (TryResolveStandingPosition(out Vector3 standingPosition))
            {
                transform.position = standingPosition;
            }
            else
            {
                // Released in place on purpose. Somewhere arbitrary would be worse than somewhere
                // wrong but predictable, and trapping the player at the wheel would be worse still.
                GameLog.Warn(LogCategory.Input, $"No deck found below the station on '{name}' release; standing position left unchanged.");
            }

            // After the position is settled, so the rider's first sample is taken from where the
            // player actually stands. Clearing here still guarantees the first walking frame can
            // only re-acquire and return a zero delta.
            if (_platformRider != null)
            {
                _platformRider.ResetTracking();
            }

            _characterController.enabled = true;
            _movement.enabled = true;

            _currentAnchor = null;
        }

        /// <summary>
        /// Restores an upright body while keeping the direction the player was facing.
        /// Reading eulerAngles.y would not do: on a rolled or pitched hull Unity's decomposition
        /// redistributes the angles, so the extracted yaw is not the visible heading. Projecting the
        /// forward axis onto the ground plane is exact at any hull attitude.
        /// </summary>
        private void RestoreUprightHeading()
        {
            Vector3 heading = Vector3.ProjectOnPlane(transform.forward, Vector3.up);

            if (heading.sqrMagnitude < 0.0001f)
            {
                // Forward is vertical, which needs an extreme hull attitude. Up still projects to a
                // usable heading there.
                heading = Vector3.ProjectOnPlane(transform.up, Vector3.up);
            }

            if (heading.sqrMagnitude < 0.0001f)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(heading, Vector3.up);
        }

        /// <summary>
        /// Finds the point on the deck directly below the seat and returns the position that rests
        /// the capsule's base on it.
        ///
        /// The cast starts above the player rather than at them, because at the seat the capsule is
        /// usually already inside the hull, and a cast never reports the collider it begins inside.
        /// Everything is measured from the live CharacterController and evaluated in world space at
        /// this instant, so a hull that has translated, turned, rolled or pitched is handled without
        /// any assumption about deck height or orientation.
        ///
        /// Returns false when no deck is found, in which case the caller must leave the player where
        /// they are: teleporting to a guessed point is worse than releasing in place.
        /// </summary>
        private bool TryResolveStandingPosition(out Vector3 standingPosition)
        {
            standingPosition = transform.position;

            if (_characterController == null || _platformRider == null)
            {
                return false;
            }

            float halfHeight = _characterController.height * 0.5f;
            float radius = _characterController.radius;

            // Distance from the transform origin down to the centre of the capsule's bottom
            // hemisphere, honouring a non-zero centre rather than assuming the origin sits
            // mid-capsule. Clamped because a capsule shorter than its own diameter is just a sphere.
            float originToBottomSphere = Mathf.Max(halfHeight - radius, 0f) - _characterController.center.y;

            Vector3 capsuleCentre = transform.TransformPoint(_characterController.center);
            float lift = _characterController.height + radius;
            Vector3 castOrigin = capsuleCentre + (Vector3.up * lift);

            // A sphere rather than a ray, because the capsule's base is a hemisphere: on a rolled
            // deck a point test would sit the capsule too low by radius * (1/cos(roll) - 1).
            if (!Physics.SphereCast(castOrigin, radius, Vector3.down, out RaycastHit hit, lift * 2f,
                    _platformRider.PlatformLayers, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            // A zero distance means the sweep began already touching something, which makes the
            // contact data meaningless. Treated as a miss so the caller releases in place.
            if (hit.distance <= 0f)
            {
                return false;
            }

            // Derived from the swept sphere's resting centre rather than from hit.point. On a rolled
            // deck the contact point lies off to the up-slope side by -normal * radius, so its height
            // belongs to a different X/Z than the player's and cannot be combined with theirs. The
            // swept centre, by contrast, is always on the vertical line the cast travelled.
            float restingSphereCentreY = castOrigin.y - hit.distance;
            standingPosition.y = restingSphereCentreY + originToBottomSphere + _characterController.skinWidth;
            return true;
        }
    }
}
