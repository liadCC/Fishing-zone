using FishingZone.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FishingZone.Player
{
    /// <summary>
    /// Ground movement and jumping for a first-person player, driven by a CharacterController.
    /// Movement is relative to where the body is facing, so the look component owns yaw and this
    /// component simply follows it.
    /// Every value is exposed for tuning rather than baked in, following the "no hardcoded physics
    /// values" rule the Technical Specification sets out for the boat and which applies equally here.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovement : MonoBehaviour
    {
        [SerializeField]
        private InputActionReference _moveAction;

        [SerializeField]
        private InputActionReference _jumpAction;

        [SerializeField]
        private float _moveSpeed = 4.5f;

        [SerializeField]
        private float _jumpHeight = 1.1f;

        /// <summary>Negative. Higher magnitude than real gravity keeps jumps snappy rather than floaty.</summary>
        [SerializeField]
        private float _gravity = -20f;

        /// <summary>
        /// A small downward velocity held while grounded. CharacterController.isGrounded only stays
        /// true if the controller keeps being pushed into the floor, so without this it flickers on slopes.
        /// </summary>
        [SerializeField]
        private float _groundedStickVelocity = -2f;

        /// <summary>Optional. When absent the player simply does not inherit platform motion.</summary>
        [SerializeField]
        private PlayerPlatformRider _platformRider;

        private CharacterController _controller;
        private float _verticalVelocity;
        private bool _isGrounded;
        private bool _isConfigured;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();

            if (_platformRider == null)
            {
                _platformRider = GetComponent<PlayerPlatformRider>();
            }

            _isConfigured = _moveAction != null && _jumpAction != null;
            if (!_isConfigured)
            {
                GameLog.Error(LogCategory.Input, "PlayerMovement is missing a Move or Jump action reference. Assign both in the Inspector.");
            }
        }

        private void Update()
        {
            if (!_isConfigured)
            {
                return;
            }

            UpdateVerticalVelocity();

            Vector2 input = _moveAction.action.ReadValue<Vector2>();
            Vector3 horizontal = (transform.right * input.x) + (transform.forward * input.y);
            if (horizontal.sqrMagnitude > 1f)
            {
                horizontal.Normalize();
            }

            Vector3 motion = (horizontal * _moveSpeed) + (Vector3.up * _verticalVelocity);

            // isGrounded alone is not enough to mean "on the deck": it still reads true on the frame
            // a jump is pressed, because it reflects the previous Move. The rising velocity set just
            // above is what identifies that frame, so the deck is let go of the instant we jump.
            bool isAirborne = !_isGrounded || _verticalVelocity > 0f;

            // Already a displacement rather than a velocity, so it is added after the delta time
            // scaling and folded into the single Move call, which keeps collisions resolved once.
            Vector3 platformDelta = _platformRider != null
                ? _platformRider.ConsumePlatformDelta(isAirborne)
                : Vector3.zero;

            _controller.Move((motion * Time.deltaTime) + platformDelta);
        }

        private void UpdateVerticalVelocity()
        {
            // Reflects the previous Move, so on the frame a jump is pressed this is still true.
            bool isGrounded = _controller.isGrounded;
            _isGrounded = isGrounded;

            if (isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = _groundedStickVelocity;
            }

            if (isGrounded && _jumpAction.action.WasPressedThisFrame())
            {
                // Velocity needed to reach _jumpHeight. Abs keeps this valid if gravity is mis-signed.
                _verticalVelocity = Mathf.Sqrt(_jumpHeight * 2f * Mathf.Abs(_gravity));
            }

            _verticalVelocity += _gravity * Time.deltaTime;
        }
    }
}
