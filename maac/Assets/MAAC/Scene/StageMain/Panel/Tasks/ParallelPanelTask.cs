using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 複数の演出タスクを同時に実行し、全て終わったら完了するタスク。
/// 例: 「Player の自動移動」と「吹き出しの文字送り」を同時進行させる。
/// PanelView の _onActivatePanelTasks にはこのタスクだけを登録し、同時に動かしたいタスクを _tasks に並べる。
/// </summary>
public class ParallelPanelTask : OnActivatePanelTaskBase
{
    [SerializeField] private OnActivatePanelTaskBase[] _tasks;

    public override UniTask OnActivateAsync(CancellationToken cancellationToken)
    {
        var running = new List<UniTask>();
        if (_tasks != null)
        {
            foreach (var task in _tasks)
            {
                if (task != null && task != this)
                    running.Add(task.OnActivateAsync(cancellationToken));
            }
        }
        return UniTask.WhenAll(running);
    }

    public override void OnDeactivate()
    {
        if (_tasks == null) return;
        foreach (var task in _tasks)
        {
            if (task != null && task != this)
                task.OnDeactivate();
        }
    }
}
