using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 指定の入力（InputSystem.actions のアクション）が押されたら満たされる条件。
/// 例: 「決定ボタンで次のコマへ」
/// </summary>
public class InputPressedCondition : PanelProceedConditionBase
{
    [Tooltip("InputSystem.actions 内のアクション名（例: UI/Submit, Player/Interact）")]
    [SerializeField] private string _actionName = "UI/Submit";

    [Tooltip("コマの演出が全て終わってからの入力のみ受け付ける")]
    [SerializeField] private bool _acceptOnlyAfterActivateCompleted = true;

    public override async UniTask WaitAsync(PanelView panel, CancellationToken cancellationToken)
    {
        var action = InputSystem.actions != null ? InputSystem.actions.FindAction(_actionName) : null;
        if (action == null)
        {
            Debug.LogError($"[InputPressedCondition] {name}: アクション '{_actionName}' が見つかりません", this);
            await UniTask.Never(cancellationToken);
            return;
        }

        if (_acceptOnlyAfterActivateCompleted)
        {
            await panel.ActivateCompletedTask.AttachExternalCancellation(cancellationToken);
            // 演出を終わらせた入力と同じフレームで進まないよう 1 フレーム待つ
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }

        await UniTask.WaitUntil(() => action.WasPressedThisFrame(), PlayerLoopTiming.Update, cancellationToken);
    }
}
