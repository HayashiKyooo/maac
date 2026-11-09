using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 「ドンッ」のような効果音（描き文字）をドンと出す演出タスク。
/// 大きく表示してから縮んで止まり、少し揺れる。コマからはみ出させたい場合は、
/// 表示オブジェクトに Canvas（Override Sorting）を付けて他のコマより手前に描く。
/// </summary>
public class ImpactTextTask : OnActivatePanelTaskBase
{
    [SerializeField] private RectTransform _target;
    [SerializeField] private bool _hideUntilActivated = true;
    [Tooltip("次のコマへ進んだら非表示にする（コマ外にはみ出した描き文字が、暗くなったコマの上に残らないように）")]
    [SerializeField] private bool _hideOnDeactivate = true;

    [Tooltip("出現時の拡大率")]
    [Min(1f)]
    [SerializeField] private float _startScale = 2.2f;
    [Tooltip("縮んで止まるまでの秒数")]
    [Min(0.01f)]
    [SerializeField] private float _popDuration = 0.15f;
    [Tooltip("揺れの大きさ (px)")]
    [Min(0f)]
    [SerializeField] private float _shakeStrength = 10f;
    [Min(0f)]
    [SerializeField] private float _shakeDuration = 0.3f;

    private Vector2 _basePosition;
    private Vector3 _baseScale = Vector3.one;

    private void Awake()
    {
        if (_target == null) return;
        _basePosition = _target.anchoredPosition;
        _baseScale = _target.localScale;
        if (_hideUntilActivated) _target.gameObject.SetActive(false);
    }

    public override async UniTask OnActivateAsync(CancellationToken cancellationToken)
    {
        if (_target == null)
        {
            Debug.LogError($"[ImpactTextTask] {name}: Target が未設定です", this);
            return;
        }

        _target.gameObject.SetActive(true);

        var t = 0f;
        while (t < _popDuration)
        {
            t += Time.deltaTime;
            var k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / _popDuration), 3f);
            _target.localScale = _baseScale * Mathf.Lerp(_startScale, 1f, k);
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }
        _target.localScale = _baseScale;

        t = 0f;
        while (t < _shakeDuration)
        {
            t += Time.deltaTime;
            var strength = _shakeStrength * (1f - Mathf.Clamp01(t / _shakeDuration));
            _target.anchoredPosition = _basePosition + Random.insideUnitCircle * strength;
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }
        _target.anchoredPosition = _basePosition;
    }

    public override void OnDeactivate()
    {
        if (_hideOnDeactivate && _target != null)
            _target.gameObject.SetActive(false);
    }
}
