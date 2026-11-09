using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 漫画のコマの吹き出しに、セリフを 1 文字ずつ表示する演出タスク。
/// ・複数のセリフを順番に表示（Auto: 一定時間で次へ / Input: 入力で次へ）
/// ・文字送り中に入力するとそのセリフを一気に表示（スキップ）
/// ・句読点の後は少し間を空ける
/// 表示先は TextMeshPro（推奨）か uGUI Text のどちらか一方を設定する。
/// 全てのセリフを表示し終えたら OnActivateAsync が完了する（ActivateCompletedCondition と組み合わせる）。
/// </summary>
public class SpeechBalloonTypewriterTask : OnActivatePanelTaskBase
{
    public enum AdvanceMode
    {
        /// <summary>一定時間待って次のセリフへ</summary>
        Auto,
        /// <summary>入力で次のセリフへ</summary>
        Input,
    }

    [Header("表示先")]
    [Tooltip("吹き出し全体。アクティブ化されるまで非表示にする場合に設定")]
    [SerializeField] private GameObject _balloonRoot;
    [SerializeField] private TMP_Text _tmpText;
    [SerializeField] private Text _legacyText;
    [SerializeField] private bool _hideBalloonUntilActivated = true;

    [Header("セリフ")]
    [TextArea(2, 5)]
    [SerializeField] private string[] _messages = { "" };

    [Header("文字送り")]
    [Tooltip("1 秒あたりに表示する文字数")]
    [Min(1f)]
    [SerializeField] private float _charactersPerSecond = 20f;
    [Tooltip("句読点の後に追加で待つ秒数")]
    [Min(0f)]
    [SerializeField] private float _punctuationWait = 0.15f;
    [SerializeField] private string _punctuationCharacters = "、。，．！？!?…";
    [Tooltip("表示開始前の待ち時間")]
    [Min(0f)]
    [SerializeField] private float _startDelay = 0.2f;

    [Header("セリフ送り")]
    [SerializeField] private AdvanceMode _advanceMode = AdvanceMode.Auto;
    [Tooltip("Auto: セリフを表示し終えてから次のセリフまでの秒数")]
    [Min(0f)]
    [SerializeField] private float _autoAdvanceDelay = 1.2f;
    [Tooltip("最後のセリフの後もセリフ送り（Auto の待ち / Input の入力）を待ってから完了する")]
    [SerializeField] private bool _waitAdvanceAfterLastMessage;
    [Tooltip("スキップ・セリフ送りに使う InputSystem.actions のアクション名")]
    [SerializeField] private string _advanceActionName = "UI/Submit";
    [Tooltip("文字送り中の入力でそのセリフを一気に表示する")]
    [SerializeField] private bool _allowSkip = true;

    private InputAction _advanceAction;

    private void Awake()
    {
        if (InputSystem.actions != null && !string.IsNullOrEmpty(_advanceActionName))
            _advanceAction = InputSystem.actions.FindAction(_advanceActionName);

        if (_hideBalloonUntilActivated && _balloonRoot != null)
            _balloonRoot.SetActive(false);
        SetMessage(string.Empty);
    }

    public override async UniTask OnActivateAsync(CancellationToken cancellationToken)
    {
        if (_tmpText == null && _legacyText == null)
        {
            Debug.LogError($"[SpeechBalloonTypewriterTask] {name}: 表示先の Text が未設定です", this);
            return;
        }

        SetMessage(string.Empty);
        if (_balloonRoot != null)
            _balloonRoot.SetActive(true);

        if (_startDelay > 0f)
            await UniTask.Delay(TimeSpan.FromSeconds(_startDelay), cancellationToken: cancellationToken);

        for (var i = 0; i < _messages.Length; i++)
        {
            await TypeMessageAsync(_messages[i] ?? string.Empty, cancellationToken);

            var isLast = i == _messages.Length - 1;
            if (!isLast || _waitAdvanceAfterLastMessage)
                await WaitAdvanceAsync(cancellationToken);
        }
    }

    private async UniTask TypeMessageAsync(string message, CancellationToken cancellationToken)
    {
        SetMessage(message);
        var total = GetCharacterCount(message);
        SetVisibleCount(0);

        var budget = 0f;   // 表示してよい文字数の「貯金」
        var visible = 0;

        // スキップ判定が前のセリフ送りの入力と被らないよう 1 フレーム待ってから開始
        await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);

        while (visible < total)
        {
            if (_allowSkip && IsAdvancePressed())
            {
                visible = total;
                break;
            }

            budget += Time.deltaTime * _charactersPerSecond;
            while (budget >= 1f && visible < total)
            {
                visible++;
                budget -= 1f;
                if (IsPunctuation(GetCharacter(message, visible - 1)))
                    budget -= _punctuationWait * _charactersPerSecond;
            }

            SetVisibleCount(visible);
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }

        SetVisibleCount(total);
    }

    private async UniTask WaitAdvanceAsync(CancellationToken cancellationToken)
    {
        // スキップに使った入力と同じフレームで次へ進まないよう 1 フレーム待つ
        await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);

        if (_advanceMode == AdvanceMode.Input && _advanceAction != null)
        {
            await UniTask.WaitUntil(IsAdvancePressed, PlayerLoopTiming.Update, cancellationToken);
            return;
        }

        // Auto（入力で待ち時間を短縮可能）
        var elapsed = 0f;
        while (elapsed < _autoAdvanceDelay)
        {
            if (_allowSkip && IsAdvancePressed()) break;
            elapsed += Time.deltaTime;
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }
    }

    private bool IsAdvancePressed() => _advanceAction != null && _advanceAction.WasPressedThisFrame();

    private bool IsPunctuation(char c) => _punctuationCharacters.IndexOf(c) >= 0;

    // ---- 表示先の差し替え（TMP / uGUI Text） ---------------------------------

    private string _currentMessage = string.Empty;

    private void SetMessage(string message)
    {
        _currentMessage = message;
        if (_tmpText != null)
        {
            _tmpText.text = message;
            _tmpText.maxVisibleCharacters = 0;
            _tmpText.ForceMeshUpdate();
        }
        else if (_legacyText != null)
        {
            _legacyText.text = string.Empty;
        }
    }

    /// <summary>表示対象の文字数（TMP はリッチテキストのタグを除いた数）</summary>
    private int GetCharacterCount(string message)
    {
        if (_tmpText != null)
            return _tmpText.textInfo.characterCount;
        return message.Length;
    }

    private char GetCharacter(string message, int index)
    {
        if (_tmpText != null)
        {
            var info = _tmpText.textInfo;
            return index >= 0 && index < info.characterCount ? info.characterInfo[index].character : '\0';
        }
        return index >= 0 && index < message.Length ? message[index] : '\0';
    }

    private void SetVisibleCount(int count)
    {
        if (_tmpText != null)
            _tmpText.maxVisibleCharacters = count;
        else if (_legacyText != null)
            _legacyText.text = _currentMessage.Substring(0, Mathf.Clamp(count, 0, _currentMessage.Length));
    }
}
