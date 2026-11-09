using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ステージの入力を受け取り、プレイヤー（StagePlayerMover）へ渡す。
/// カメラは固定で、移動方向は「現在のコマ（PanelView）に指定されたカメラ」の向き（Y軸回転）基準。
/// コマにカメラが指定されていない場合は _cameraTransform、それも無ければ Camera.main を使う。
/// </summary>
public class StageMainPresenter : MonoBehaviour
{
    [SerializeField] private StagePlayerMover _playerMover;

    [Tooltip("現在のコマのカメラを移動方向の基準にするための PanelManager")]
    [SerializeField] private PanelManager _panelManager;

    [Tooltip("コマにカメラが無い時に移動方向の基準にするカメラ。未設定なら Camera.main")]
    [SerializeField] private Transform _cameraTransform;

    private Transform _panelCameraTransform;
    private PanelView _currentPanel;

    private InputAction _moveAction;
    private InputAction _jumpAction;
    private InputAction _sprintAction;

    private void Awake()
    {
        _moveAction = InputSystem.actions.FindAction("Move");
        _jumpAction = InputSystem.actions.FindAction("Jump");
        _sprintAction = InputSystem.actions.FindAction("Sprint");

        if (_panelManager != null)
        {
            _panelManager.PanelActivated += OnPanelActivated;
            if (_panelManager.CurrentPanel != null)
                OnPanelActivated(_panelManager.CurrentIndex, _panelManager.CurrentPanel);
        }
    }

    private void OnDestroy()
    {
        if (_panelManager != null)
            _panelManager.PanelActivated -= OnPanelActivated;
    }

    private void OnPanelActivated(int index, PanelView panel)
    {
        _currentPanel = panel;
        _panelCameraTransform = panel != null && panel.PanelCamera != null ? panel.PanelCamera.transform : null;
    }

    private void Update()
    {
        // 現在のコマが操作を禁止している間は入力を渡さない（重力・自動移動のための Tick は続ける）
        var acceptInput = _currentPanel == null || !_currentPanel.BlocksPlayerInput;

        if (acceptInput && _jumpAction != null && _jumpAction.WasPressedThisFrame())
            _playerMover.RequestJump();

        var input = new StagePlayerMover.MoveInput
        {
            Move = acceptInput ? _moveAction.ReadValue<Vector2>() : Vector2.zero,
            Sprint = acceptInput && IsSprintPressed(),
            ReferenceYaw = GetCameraYaw(),
        };
        _playerMover.Tick(input, Time.deltaTime);
    }

    /// <summary>
    /// ダッシュ入力。Input Actions の Sprint は左 Shift にしか割り当てられておらず、
    /// また Button 型のため「押しっぱなしのまま有効になった / フォーカスが戻った」時に反応しないことがある。
    /// そのためキーボードの Shift（左右どちらでも）の現在の状態も直接見る。
    /// </summary>
    private bool IsSprintPressed()
    {
        if (_sprintAction != null && _sprintAction.IsPressed()) return true;
        var keyboard = Keyboard.current;
        return keyboard != null && keyboard.shiftKey.isPressed;
    }

    private float GetCameraYaw()
    {
        var cam = _panelCameraTransform != null ? _panelCameraTransform : _cameraTransform;
        if (cam == null && Camera.main != null)
            cam = Camera.main.transform;
        return cam != null ? cam.eulerAngles.y : 0f;
    }
}
