using UnityEngine;
using UnityEngine.UI;

/// <summary>HP ゲージ。減った分は少し遅れて追従するダメージバーで表示する</summary>
public class HpGaugeView : MonoBehaviour
{
    [SerializeField] private RectTransform _fill;
    [SerializeField] private RectTransform _damage;
    [Tooltip("ON: 右端を基準に減る（画面右側のゲージ用）")]
    [SerializeField] private bool _anchorRight;
    [SerializeField] private Text _nameText;
    [SerializeField] private float _damageDelay = 0.4f;
    [Tooltip("ダメージバーが追従する速さ（ゲージ全体 = 1 / 秒）")]
    [SerializeField] private float _damageSpeed = 0.8f;
    [SerializeField] private float _shakeStrength = 6f;
    [SerializeField] private float _shakeDuration = 0.2f;

    private Fighter _fighter;
    private float _ratio = 1f;
    private float _damageRatio = 1f;
    private float _delayTimer;
    private float _shakeTimer;
    private Vector2 _basePosition;
    private RectTransform _rect;

    private void Awake()
    {
        _rect = (RectTransform)transform;
        _basePosition = _rect.anchoredPosition;
    }

    public void Bind(Fighter fighter)
    {
        if (_fighter != null) _fighter.HpChanged -= OnHpChanged;
        _fighter = fighter;
        if (_nameText != null) _nameText.text = fighter.DisplayName;
        _ratio = _damageRatio = fighter.HpRatio;
        SetBar(_fill, _ratio);
        SetBar(_damage, _damageRatio);
        fighter.HpChanged += OnHpChanged;
    }

    private void OnDestroy()
    {
        if (_fighter != null) _fighter.HpChanged -= OnHpChanged;
    }

    private void OnHpChanged(Fighter fighter)
    {
        _ratio = fighter.HpRatio;
        _delayTimer = _damageDelay;
        _shakeTimer = _shakeDuration;
        SetBar(_fill, _ratio);
    }

    private void Update()
    {
        var dt = Time.unscaledDeltaTime;
        if (_delayTimer > 0f) _delayTimer -= dt;
        else if (_damageRatio > _ratio)
        {
            _damageRatio = Mathf.MoveTowards(_damageRatio, _ratio, _damageSpeed * dt);
            SetBar(_damage, _damageRatio);
        }

        if (_shakeTimer > 0f)
        {
            _shakeTimer -= dt;
            var s = _shakeStrength * Mathf.Clamp01(_shakeTimer / _shakeDuration);
            _rect.anchoredPosition = _basePosition + new Vector2(Random.Range(-s, s), Random.Range(-s, s));
        }
        else
        {
            _rect.anchoredPosition = _basePosition;
        }
    }

    private void SetBar(RectTransform bar, float ratio)
    {
        if (bar == null) return;
        ratio = Mathf.Clamp01(ratio);
        if (_anchorRight)
        {
            bar.anchorMin = new Vector2(1f - ratio, 0f);
            bar.anchorMax = new Vector2(1f, 1f);
        }
        else
        {
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(ratio, 1f);
        }
        bar.offsetMin = Vector2.zero;
        bar.offsetMax = Vector2.zero;
    }
}
