using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// コマ（PanelView）がアクティブになった時に順番に実行される演出タスクの基底クラス。
/// PanelView の _onActivatePanelTasks に並べた順で 1 つずつ await される。
/// </summary>
public abstract class OnActivatePanelTaskBase : MonoBehaviour
{
    public abstract UniTask OnActivateAsync(CancellationToken cancellationToken);

    /// <summary>コマが現在のコマでなくなった時に呼ばれる（必要なタスクだけ override）</summary>
    public virtual void OnDeactivate()
    {
    }
}
