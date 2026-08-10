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
        private Transform _originalParent;

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
        /// Snaps to the anchor and parents to it, so the player rides the boat with no platform
        /// maths at all while seated. Returns false if already at a station.
        /// </summary>
        public bool TryOccupy(Transform anchor)
        {
            if (anchor == null || IsOccupyingStation || _movement == null || _characterController == null)
            {
                return false;
            }

            _currentAnchor = anchor;
            _originalParent = transform.parent;

            // The controller must be switched off before the transform is moved, otherwise it
            // fights the assignment and the player is left jittering at the seat.
            _characterController.enabled = false;
            _movement.enabled = false;

            transform.SetParent(anchor, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

            // Dropped explicitly rather than left to whichever script order happens to run first:
            // if the rider kept its reference here it would survive the whole seated period.
            if (_platformRider != null)
            {
                _platformRider.ResetTracking();
            }

            return true;
        }

        public void Release()
        {
            if (!IsOccupyingStation)
            {
                return;
            }

            transform.SetParent(_originalParent, true);

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
            _originalParent = null;
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

            // Distance from the transform origin down to the base of the capsule, honouring a
            // non-zero centre rather than assuming the origin sits mid-capsule.
            float originToBase = halfHeight - _characterController.center.y;

            Vector3 capsuleCentre = transform.TransformPoint(_characterController.center);
            float lift = _characterController.height + radius;
            Vector3 castOrigin = capsuleCentre + (Vector3.up * lift);

            // A sphere rather than a ray, so the result is a surface the capsule's width can rest on
            // rather than a point that might sit on an edge.
            if (!Physics.SphereCast(castOrigin, radius, Vector3.down, out RaycastHit hit, lift * 2f,
                    _platformRider.PlatformLayers, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            // Only the height is taken from the hit; the seat already decided where on the deck the
            // player stands, and skin width keeps the capsule just clear of the surface.
            standingPosition.y = hit.point.y + originToBase + _characterController.skinWidth;
            return true;
        }
    }
}
