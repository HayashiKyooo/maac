using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// キーボード入力。A: 左 / D: 右 / S: しゃがみ / Space: ジャンプ / Enter: 攻撃。
/// 波動コマンド: ↓ → ↓+前 → 前（または ↓）の後に Enter。
/// （右側にいて左を向いている場合は S → S+A → A(またはS) → Enter）
/// </summary>
public class KeyboardFighterInput : FighterInputBase
{
    [Tooltip("コマンド全体の受付時間 (秒)")]
    [SerializeField] private float _commandWindow = 0.6f;
    [Tooltip("最後の方向入力から Enter までの猶予 (秒)")]
    [SerializeField] private float _attackGrace = 0.3f;

    private struct DirEntry
    {
        public int Dir; // テンキー表記（前向き基準）: 2=下, 3=下前, 6=前 ...
        public float Time;
    }

    private readonly List<DirEntry> _history = new();
    private int _lastDir = 5;
    private bool _prevAttack;
    private bool _prevJump;

    public override FighterCommand ReadCommand(Fighter self, float deltaTime)
    {
        var kb = Keyboard.current;
        if (kb == null) return default;

        var h = (kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0);
        var down = kb.sKey.isPressed;

        RecordDirection(ToNumpad(h * self.FacingSign, down));

        // 押した瞬間は前フレームとの比較で判定する（入力を受け付けない間に押されていた分は捨てる）
        var attackHeld = kb.enterKey.isPressed || kb.numpadEnterKey.isPressed;
        var jumpHeld = kb.spaceKey.isPressed;
        var attack = attackHeld && !_prevAttack;
        var jump = jumpHeld && !_prevJump;
        _prevAttack = attackHeld;
        _prevJump = jumpHeld;

        var special = attack && ConsumeHadoukenCommand();

        return new FighterCommand
        {
            Move = h,
            Crouch = down,
            Jump = jump,
            Attack = attack && !special,
            Special = special,
        };
    }

    private static int ToNumpad(int relative, bool down)
    {
        if (down) return relative > 0 ? 3 : relative < 0 ? 1 : 2;
        return relative > 0 ? 6 : relative < 0 ? 4 : 5;
    }

    private void RecordDirection(int dir)
    {
        var now = Time.time;
        if (dir != _lastDir)
        {
            _history.Add(new DirEntry { Dir = dir, Time = now });
            _lastDir = dir;
        }
        // 古い入力を捨てる
        while (_history.Count > 0 && now - _history[0].Time > _commandWindow)
            _history.RemoveAt(0);
    }

    /// <summary>↓ → ↓前 → 前(or ↓) が受付時間内に入力されていれば成立</summary>
    private bool ConsumeHadoukenCommand()
    {
        var step = 0;
        var lastTime = 0f;
        foreach (var e in _history)
        {
            if (step == 0 && e.Dir == 2) step = 1;
            else if (step == 1 && e.Dir == 3) step = 2;
            else if (step == 2 && (e.Dir == 6 || e.Dir == 2))
            {
                step = 3;
                lastTime = e.Time;
                break;
            }
        }

        if (step != 3 || Time.time - lastTime > _attackGrace) return false;
        _history.Clear();
        return true;
    }
}
