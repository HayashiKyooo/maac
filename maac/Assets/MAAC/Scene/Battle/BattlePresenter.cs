using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// バトルの進行。
/// フェードイン → 「BATTLE START」→ 操作開始 → どちらかの KO → スロー → 「K.O.」→ 勝敗表示 → Enter でリトライ
/// </summary>
public class BattlePresenter : MonoBehaviour
{
    [SerializeField] private Fighter _player;
    [SerializeField] private Fighter _enemy;
    [SerializeField] private BattleAnnouncer _announcer;
    [SerializeField] private HpGaugeView _playerGauge;
    [SerializeField] private HpGaugeView _enemyGauge;
    [SerializeField] private BattleHitEffect _hitEffect;

    [Header("Fade")]
    [SerializeField] private Graphic _fadeGraphic;
    [SerializeField] private float _fadeInDuration = 0.5f;

    [Header("Texts")]
    [SerializeField] private string _startText = "BATTLE START";
    [SerializeField] private string _koText = "K.O.";
    [SerializeField] private string _winText = "YOU WIN";
    [SerializeField] private string _loseText = "YOU LOSE";

    [Header("Timing")]
    [SerializeField] private float _startHold = 0.7f;
    [SerializeField] private float _koSlowTimeScale = 0.3f;
    [SerializeField] private float _koSlowDuration = 1.0f;

    private void Start()
    {
        SetControl(false);
        if (_playerGauge != null && _player != null) _playerGauge.Bind(_player);
        if (_enemyGauge != null && _enemy != null) _enemyGauge.Bind(_enemy);
        if (_player != null) _player.Damaged += OnDamaged;
        if (_enemy != null) _enemy.Damaged += OnDamaged;

        RunAsync(destroyCancellationToken).Forget();
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
        if (_player != null) _player.Damaged -= OnDamaged;
        if (_enemy != null) _enemy.Damaged -= OnDamaged;
    }

    private async UniTaskVoid RunAsync(CancellationToken ct)
    {
        await FadeInAsync(ct);

        // ---- バトル開始
        await _announcer.PlayAsync(_startText, _startHold, ct);
        SetControl(true);

        // ---- 決着待ち
        await UniTask.WaitUntil(() => _player.IsKO || _enemy.IsKO, cancellationToken: ct);
        SetControl(false);

        Time.timeScale = _koSlowTimeScale;
        await UniTask.Delay(System.TimeSpan.FromSeconds(_koSlowDuration), DelayType.UnscaledDeltaTime, cancellationToken: ct);
        Time.timeScale = 1f;

        await _announcer.PlayAsync(_koText, 0.8f, ct);
        await _announcer.ShowAsync(_enemy.IsKO ? _winText : _loseText, ct);

        // ---- Enter でリトライ
        await UniTask.WaitUntil(() =>
        {
            var kb = Keyboard.current;
            return kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame);
        }, cancellationToken: ct);
        SceneManager.LoadScene(gameObject.scene.name);
    }

    private async UniTask FadeInAsync(CancellationToken ct)
    {
        if (_fadeGraphic == null) return;
        _fadeGraphic.raycastTarget = false;
        var c = _fadeGraphic.color;
        var t = 0f;
        while (t < _fadeInDuration)
        {
            c.a = 1f - t / _fadeInDuration;
            _fadeGraphic.color = c;
            await UniTask.Yield(ct);
            t += Time.unscaledDeltaTime;
        }
        c.a = 0f;
        _fadeGraphic.color = c;
    }

    private void SetControl(bool enabled)
    {
        if (_player != null) _player.ControlEnabled = enabled;
        if (_enemy != null) _enemy.ControlEnabled = enabled;
    }

    private void OnDamaged(Fighter victim, Vector3 point, bool blocked, bool projectile)
    {
        if (_hitEffect == null) return;
        if (blocked) _hitEffect.Spawn(point, "ガッ", 0.8f);
        else if (projectile) _hitEffect.Spawn(point, "ドォン", 1.2f);
        else _hitEffect.Spawn(point, "バシッ", 1f);
    }
}
