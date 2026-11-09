using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 漫画の 1 コマ。
/// ・アクティブ化時の演出（OnActivatePanelTaskBase）を順番に実行する
/// ・「次のコマへ進んでよいか」を進行条件（PanelProceedConditionBase）で管理する
/// 進行条件は演出と同時に評価が始まる。条件が 1 つも無い場合は「演出が全て終わったら」進める。
/// </summary>
public class PanelView : MonoBehaviour
{
    public enum ProceedConditionMode
    {
        /// <summary>全ての条件を満たしたら進める</summary>
        All,
        /// <summary>どれか 1 つの条件を満たしたら進める</summary>
        Any,
    }

    [SerializeField] private OnActivatePanelTaskBase[] _onActivatePanelTasks;

    [Header("次のコマへ進む条件")]
    [SerializeField] private PanelProceedConditionBase[] _proceedConditions;
    [SerializeField] private ProceedConditionMode _proceedConditionMode = ProceedConditionMode.All;

    private readonly UniTaskCompletionSource _activateCompletion = new();
    private readonly UniTaskCompletionSource _proceedCompletion = new();

    /// <summary>OnActivateAsync が呼ばれたか</summary>
    public bool IsActivated { get; private set; }

    /// <summary>アクティブ化時の演出が全て終わったか</summary>
    public bool IsActivateCompleted { get; private set; }

    /// <summary>次のコマへ進んでよい状態か</summary>
    public bool CanProceed { get; private set; }

    /// <summary>演出が全て終わった時に完了する（何度でも await 可）</summary>
    public UniTask ActivateCompletedTask => _activateCompletion.Task;

    /// <summary>次のコマへ進んでよくなった時に完了する（何度でも await 可）</summary>
    public UniTask CanProceedTask => _proceedCompletion.Task;

    /// <summary>
    /// コマをアクティブにする。演出を順番に再生し、全て終わったら完了する。
    /// 進行条件の評価もここで開始される。
    /// </summary>
    public async UniTask OnActivateAsync(CancellationToken cancellationToken = default)
    {
        if (IsActivated)
        {
            Debug.LogWarning($"[PanelView] {name} は既にアクティブ化されています", this);
            await ActivateCompletedTask;
            return;
        }
        IsActivated = true;

        var token = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, destroyCancellationToken).Token;

        // 進行条件の監視は演出と並行して開始する（例: 文字送り中でも目的地に着いたら進める）
        WatchProceedConditionsAsync(token).Forget();

        try
        {
            if (_onActivatePanelTasks != null)
            {
                foreach (var task in _onActivatePanelTasks)
                {
                    if (task == null) continue;
                    await task.OnActivateAsync(token);
                }
            }
        }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                IsActivateCompleted = true;
                _activateCompletion.TrySetResult();
            }
            else
            {
                _activateCompletion.TrySetCanceled(token);
            }
        }
    }

    /// <summary>次のコマへ進んでよくなるまで待つ</summary>
    public UniTask WaitUntilCanProceedAsync(CancellationToken cancellationToken = default)
    {
        return CanProceedTask.AttachExternalCancellation(cancellationToken);
    }

    /// <summary>条件に関係なく、強制的に進行可能にする（デバッグやスキップ用）</summary>
    public void ForceProceed()
    {
        CanProceed = true;
        _proceedCompletion.TrySetResult();
    }

    private async UniTaskVoid WatchProceedConditionsAsync(CancellationToken token)
    {
        try
        {
            var waits = new List<UniTask>();
            if (_proceedConditions != null)
            {
                foreach (var condition in _proceedConditions)
                {
                    if (condition != null)
                        waits.Add(condition.WaitAsync(this, token));
                }
            }

            if (waits.Count == 0)
                await ActivateCompletedTask;
            else if (_proceedConditionMode == ProceedConditionMode.All)
                await UniTask.WhenAll(waits);
            else
                await UniTask.WhenAny(waits);

            ForceProceed();
        }
        catch (System.OperationCanceledException)
        {
            _proceedCompletion.TrySetCanceled(token);
        }
    }
}
