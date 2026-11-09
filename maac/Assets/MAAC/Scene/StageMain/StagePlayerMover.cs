using UnityEngine;

/// <summary>
/// プレイヤーの移動処理（Starter Assets ThirdPersonController の Move / JumpAndGravity / GroundedCheck を移植）。
/// 入力の取得やカメラの扱いは持たず、呼び出し側（StageMainPresenter）から毎フレーム Tick で入力を受け取る。
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class StagePlayerMover : MonoBehaviour
{
    /// <summary>1フレーム分の移動入力</summary>
    public struct MoveInput
    {
        /// <summary>移動入力 (x: 左右, y: 前後)</summary>
        public Vector2 Move;
        /// <summary>ダッシュ中か</summary>
        public bool Sprint;
        /// <summary>移動方向の基準となるヨー角（度）。固定カメラの eulerAngles.y など</summary>
        public float ReferenceYaw;
    }

    [Header("Move")]
    [Tooltip("歩行速度 (m/s)")]
    [SerializeField] private float _moveSpeed = 2.0f;
    [Tooltip("ダッシュ速度 (m/s)")]
    [SerializeField] private float _sprintSpeed = 5.335f;
    [Tooltip("移動方向へ向き直る速さ")]
    [Range(0.0f, 0.3f)]
    [SerializeField] private float _rotationSmoothTime = 0.12f;
    [Tooltip("加減速")]
    [SerializeField] private float _speedChangeRate = 10.0f;
    [Tooltip("スティック入力の大きさを速度に反映するか")]
    [SerializeField] private bool _analogMovement;

    [Header("Jump / Gravity")]
    [SerializeField] private float _jumpHeight = 1.2f;
    [SerializeField] private float _gravity = -15.0f;
    [SerializeField] private float _jumpTimeout = 0.30f;
    [SerializeField] private float _fallTimeout = 0.15f;

    [Header("Grounded")]
    [SerializeField] private float _groundedOffset = -0.14f;
    [SerializeField] private float _groundedRadius = 0.28f;
    [SerializeField] private LayerMask _groundLayers = 1;

    private const float TerminalVelocity = -53.0f;

    private static readonly int AnimIDSpeed = Animator.StringToHash("Speed");
    private static readonly int AnimIDGrounded = Animator.StringToHash("Grounded");
    private static readonly int AnimIDJump = Animator.StringToHash("Jump");
    private static readonly int AnimIDFreeFall = Animator.StringToHash("FreeFall");
    private static readonly int AnimIDMotionSpeed = Animator.StringToHash("MotionSpeed");

    private CharacterController _controller;
    private Animator _animator;

    private bool _jumpRequested;
    private float _speed;
    private float _animationBlend;
    private float _targetRotation;
    private float _rotationVelocity;
    private float _verticalVelocity;
    private float _jumpTimeoutDelta;
    private float _fallTimeoutDelta;

    public bool IsGrounded { get; private set; } = true;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        TryGetComponent(out _animator);
        _targetRotation = transform.eulerAngles.y;
        _jumpTimeoutDelta = _jumpTimeout;
        _fallTimeoutDelta = _fallTimeout;
    }

    /// <summary>ジャンプを要求する（接地中のみ実行、空中で要求は破棄）</summary>
    public void RequestJump() => _jumpRequested = true;

    /// <summary>1フレーム分の移動を行う（ThirdPersonController.Update と同じ順序）</summary>
    public void Tick(MoveInput input, float deltaTime)
    {
        JumpAndGravity(deltaTime);
        GroundedCheck();
        Move(input, deltaTime);
    }

    private void GroundedCheck()
    {
        var pos = transform.position;
        var spherePosition = new Vector3(pos.x, pos.y - _groundedOffset, pos.z);
        IsGrounded = Physics.CheckSphere(spherePosition, _groundedRadius, _groundLayers, QueryTriggerInteraction.Ignore);

        if (_animator != null)
            _animator.SetBool(AnimIDGrounded, IsGrounded);
    }

    private void Move(MoveInput input, float deltaTime)
    {
        var move = input.Move;
        var targetSpeed = input.Sprint ? _sprintSpeed : _moveSpeed;
        if (move == Vector2.zero) targetSpeed = 0.0f;

        var velocity = _controller.velocity;
        var currentHorizontalSpeed = new Vector3(velocity.x, 0.0f, velocity.z).magnitude;

        const float speedOffset = 0.1f;
        var inputMagnitude = _analogMovement ? move.magnitude : 1f;

        if (currentHorizontalSpeed < targetSpeed - speedOffset ||
            currentHorizontalSpeed > targetSpeed + speedOffset)
        {
            _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude, deltaTime * _speedChangeRate);
            _speed = Mathf.Round(_speed * 1000f) / 1000f;
        }
        else
        {
            _speed = targetSpeed;
        }

        _animationBlend = Mathf.Lerp(_animationBlend, targetSpeed, deltaTime * _speedChangeRate);
        if (_animationBlend < 0.01f) _animationBlend = 0f;

        var inputDirection = new Vector3(move.x, 0.0f, move.y).normalized;

        if (move != Vector2.zero)
        {
            _targetRotation = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + input.ReferenceYaw;
            var rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation, ref _rotationVelocity,
                _rotationSmoothTime, Mathf.Infinity, deltaTime);
            transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
        }

        var targetDirection = Quaternion.Euler(0.0f, _targetRotation, 0.0f) * Vector3.forward;

        _controller.Move(targetDirection.normalized * (_speed * deltaTime) +
                         new Vector3(0.0f, _verticalVelocity, 0.0f) * deltaTime);

        if (_animator != null)
        {
            _animator.SetFloat(AnimIDSpeed, _animationBlend);
            _animator.SetFloat(AnimIDMotionSpeed, inputMagnitude);
        }
    }

    private void JumpAndGravity(float deltaTime)
    {
        if (IsGrounded)
        {
            _fallTimeoutDelta = _fallTimeout;

            if (_animator != null)
            {
                _animator.SetBool(AnimIDJump, false);
                _animator.SetBool(AnimIDFreeFall, false);
            }

            if (_verticalVelocity < 0.0f)
                _verticalVelocity = -2f;

            if (_jumpRequested && _jumpTimeoutDelta <= 0.0f)
            {
                _verticalVelocity = Mathf.Sqrt(_jumpHeight * -2f * _gravity);
                if (_animator != null)
                    _animator.SetBool(AnimIDJump, true);
            }

            if (_jumpTimeoutDelta >= 0.0f)
                _jumpTimeoutDelta -= deltaTime;
        }
        else
        {
            _jumpTimeoutDelta = _jumpTimeout;

            if (_fallTimeoutDelta >= 0.0f)
                _fallTimeoutDelta -= deltaTime;
            else if (_animator != null)
                _animator.SetBool(AnimIDFreeFall, true);

            _jumpRequested = false;
        }

        if (_verticalVelocity > TerminalVelocity)
            _verticalVelocity += _gravity * deltaTime;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = IsGrounded ? new Color(0f, 1f, 0f, 0.35f) : new Color(1f, 0f, 0f, 0.35f);
        var pos = transform.position;
        Gizmos.DrawSphere(new Vector3(pos.x, pos.y - _groundedOffset, pos.z), _groundedRadius);
    }
}
