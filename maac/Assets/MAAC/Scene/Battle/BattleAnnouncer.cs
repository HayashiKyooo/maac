using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 「BATTLE START」「K.O.」などの演出テキスト。
/// 黒い帯が開く → 文字が大きい所から叩きつけられる → 少し揺れる → 拡大しながら消える。
/// スローモーション中でも一定の速さで動くよう unscaled time で再生する。
/// </summary>
public class BattleAnnouncer : MonoBehaviour
{
    [SerializeField] private RectTransform _band;
    [SerializeField] private Text _text;
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private float _bandDuration = 0.15f;
    [SerializeField] private float _slamDuration = 0.25f;
    [SerializeField] private float _slamStartScale = 3f;
    [SerializeField] private float _shakeStrength = 12f;
    [SerializeField] private float _shakeDuration = 0.2f;
    [SerializeField] private float _hideDuration = 0.25f;

    private Vector2 _textBasePosition;

    private void Awake()
    {
        if (_text != null) _textBasePosition = _text.rectTransform.anchoredPosition;
        SetVisible(false);
    }

    /// <summary>表示 → hold 秒待つ → 非表示</summary>
    public async UniTask PlayAsync(string message, float hold, CancellationToken ct)
    {
        await ShowAsync(message, ct);
        await UniTask.Delay(System.TimeSpan.FromSeconds(hold), DelayType.UnscaledDeltaTime, cancellationToken: ct);
        await HideAsync(ct);
    }

    public async UniTask ShowAsync(string message, CancellationToken ct)
    {
        SetVisible(true);
        _text.text = message;
        _text.rectTransform.anchoredPosition = _textBasePosition;
        SetTextAlpha(0f);
        SetBandScale(0f);

        // 帯が開く
        await Tween(_bandDuration, t => SetBandScale(EaseOut(t)), ct);

        // 文字を叩きつける
        await Tween(_slamDuration, t =>
        {
            var e = EaseOut(t);
            _text.rectTransform.localScale = Vector3.one * Mathf.Lerp(_slamStartScale, 1f, e);
            SetTextAlpha(Mathf.Clamp01(t * 2f));
        }, ct);

        // 揺れ
        await Tween(_shakeDuration, t =>
        {
            var s = _shakeStrength * (1f - t);
            _text.rectTransform.anchoredPosition = _textBasePosition + new Vector2(Random.Range(-s, s), Random.Range(-s, s));
        }, ct);
        _text.rectTransform.anchoredPosition = _textBasePosition;
    }

    public async UniTask HideAsync(CancellationToken ct)
    {
        await Tween(_hideDuration, t =>
        {
            _text.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 1.6f, t);
            SetTextAlpha(1f - t);
            SetBandScale(1f - EaseOut(t));
        }, ct);
        SetVisible(false);
    }

    private static async UniTask Tween(float duration, System.Action<float> apply, CancellationToken ct)
    {
        var time = 0f;
        while (time < duration)
        {
            apply(Mathf.Clamp01(time / duration));
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
            time += Time.unscaledDeltaTime;
        }
        apply(1f);
    }

    private static float EaseOut(float t) => 1f - (1f - t) * (1f - t) * (1f - t);

    private void SetVisible(bool visible)
    {
        if (_group != null) _group.alpha = visible ? 1f : 0f;
        if (_band != null) _band.gameObject.SetActive(visible);
        if (_text != null) _text.gameObject.SetActive(visible);
    }

    private void SetBandScale(float y)
    {
        if (_band != null) _band.localScale = new Vector3(1f, y, 1f);
    }

    private void SetTextAlpha(float a)
    {
        var c = _text.color;
        c.a = a;
        _text.color = c;
        foreach (var shadow in _text.GetComponents<Shadow>())
        {
            var sc = shadow.effectColor;
            sc.a = a;
            shadow.effectColor = sc;
        }
    }
}
