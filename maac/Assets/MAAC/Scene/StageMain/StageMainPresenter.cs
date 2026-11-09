using UnityEngine;
using UnityEngine.InputSystem;

public class StageMainPresenter : MonoBehaviour
{
    [SerializeField] private Transform _playerTrans;
    [SerializeField] private float _moveSpeed;

    private InputAction _moveAction;

    private void Awake()
    {
        _moveAction = InputSystem.actions.FindAction("Move");
    }

    private void Update()
    {
        // プレイヤーを移動させる（InputSystemを使用）(InputSystem.actions.FindAction("Move");)
        // x, z軸に対して
        var moveValue = _moveAction.ReadValue<Vector2>();
        _playerTrans.Translate(new Vector3(moveValue.x, 0, moveValue.y) * _moveSpeed * Time.deltaTime);
    }
}
