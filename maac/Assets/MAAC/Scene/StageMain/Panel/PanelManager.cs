using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// コマ（PanelView）を順番に進行させる。
/// 現在のコマが「次へ進んでよい」状態になったら、次のコマの OnActivateAsync を再生する。
/// </summary>
public class PanelManager : MonoBehaviour
{
    [SerializeField] private PanelView[] _panelViews;

    [Tooltip("Start で自動的に最初のコマから再生する")]
    [SerializeField] private bool _playOnStart = true;

    private bool _isPlaying;

    /// <summary>現在アクティブなコマのインデックス（未再生なら -1）</summary>
    public int CurrentIndex { get; private set; } = -1;

    public PanelView CurrentPanel =>
        CurrentIndex >= 0 && CurrentIndex < _panelViews.Length ? _panelViews[CurrentIndex] : null;

    /// <summary>コマがアクティブになった時（index, panel）</summary>
    public event Action<int, PanelView> PanelActivated;

    /// <summary>最後のコマが進行可能になった時</summary>
    public event Action Completed;

    private void Start()
    {
        if (_playOnStart)
            PlayAsync(destroyCancellationToken).Forget();
    }

    /// <summary>最初のコマから順に再生する</summary>
    public async UniTask PlayAsync(CancellationToken cancellationToken = default)
    {
        if (_isPlaying)
        {
            Debug.LogWarning("[PanelManager] 既に再生中です", this);
            return;
        }
        _isPlaying = true;

        try
        {
            for (var i = 0; i < _panelViews.Length; i++)
            {
                var panel = _panelViews[i];
                if (panel == null) continue;

                CurrentIndex = i;
                PanelActivated?.Invoke(i, panel);

                // 演出は待たずに再生し、進行条件を満たすまで待つ
                // （演出の完了を待ちたい場合は PanelView 側で ActivateCompletedCondition を使う）
                panel.OnActivateAsync(cancellationToken).Forget();
                await panel.WaitUntilCanProceedAsync(cancellationToken);
            }

            Completed?.Invoke();
        }
        finally
        {
            _isPlaying = false;
        }
    }
}
