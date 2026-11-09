using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// コマがアクティブになってから指定秒数経ったら満たされる条件。
/// </summary>
public class WaitSecondsCondition : PanelProceedConditionBase
{
    [Min(0f)]
    [SerializeField] private float _seconds = 1.0f;

    public override UniTask WaitAsync(PanelView panel, CancellationToken cancellationToken)
    {
        return UniTask.Delay(TimeSpan.FromSeconds(_seconds), cancellationToken: cancellationToken);
    }
}
