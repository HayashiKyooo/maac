using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ステージの入力を受け取り、プレイヤー（StagePlayerMover）へ渡す。
/// カメラは固定で、移動方向はカメラの向き（Y軸回転）基準。
/// </summary>
public class StageMainPresenter : MonoBehaviour
{
    [SerializeField] private StagePlayerMover _playerMover;

    [Tooltip("移動方向の基準にするカメラ。未設定なら Camera.main")]
    [SerializeField] private Transform _cameraTransform;

    private InputAction _moveAction;
    private InputAction _jumpAction;
    private InputAction _sprintAction;

    private void Awake()
    {
        _moveAction = InputSystem.actions.FindAction("Move");
        _jumpAction = InputSystem.actions.FindAction("Jump");
        _sprintAction = InputSystem.actions.FindAction("Sprint");
    }

    private void Update()
    {
        if (_jumpAction != null && _jumpAction.WasPressedThisFrame())
            _playerMover.RequestJump();

        var input = new StagePlayerMover.MoveInput
        {
            Move = _moveAction.ReadValue<Vector2>(),
            Sprint = _sprintAction != null && _sprintAction.IsPressed(),
            ReferenceYaw = GetCameraYaw(),
        };
        _playerMover.Tick(input, Time.deltaTime);
    }

    private float GetCameraYaw()
    {
        var cam = _cameraTransform;
        if (cam == null && Camera.main != null)
            cam = Camera.main.transform;
        return cam != null ? cam.eulerAngles.y : 0f;
    }
}
