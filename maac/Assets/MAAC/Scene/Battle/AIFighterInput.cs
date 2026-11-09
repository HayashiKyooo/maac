using UnityEngine;

/// <summary>簡易 CPU。距離に応じて 接近 / 攻撃 / 波動拳 / ジャンプ / ガード を選ぶ</summary>
public class AIFighterInput : FighterInputBase
{
    [Tooltip("相手の攻撃をガードしようとする確率")]
    [Range(0f, 1f)] [SerializeField] private float _guardChance = 0.45f;
    [Tooltip("飛んでくる波動拳をジャンプで避けようとする確率")]
    [Range(0f, 1f)] [SerializeField] private float _jumpOverProjectileChance = 0.3f;
    [SerializeField] private Vector2 _decisionInterval = new(0.15f, 0.4f);
    [SerializeField] private float _specialCooldown = 2.5f;
    [Tooltip("近距離とみなす距離 (m)")]
    [SerializeField] private float _closeRange = 1.15f;
    [Tooltip("遠距離とみなす距離 (m)")]
    [SerializeField] private float _farRange = 3.0f;
    [Tooltip("攻撃した後、次の攻撃を選ぶまでの休み (秒)")]
    [SerializeField] private Vector2 _attackRest = new(0.35f, 0.9f);

    private enum Intent
    {
        Idle,
        Approach,
        Retreat,
        Guard,
        CrouchGuard,
        JumpIn,
        JumpBack,
        Attack,
        CrouchAttack,
        Special,
    }

    private Intent _intent;
    private float _intentTimer;
    private float _specialTimer = 1.0f;
    private bool _intentStarted;
    private bool _wasThreatened;
    private bool _airAttackDone;
    private float _restTimer;

    public override FighterCommand ReadCommand(Fighter self, float deltaTime)
    {
        var opp = self.Opponent;
        if (opp == null || opp.IsKO) return default;

        _intentTimer -= deltaTime;
        _specialTimer -= deltaTime;
        _restTimer -= deltaTime;

        var dx = opp.transform.position.x - self.transform.position.x;
        var dist = Mathf.Abs(dx);
        var toward = dx >= 0f ? 1 : -1;

        // ---- 相手の攻撃 / 弾への反応（脅威が始まった瞬間に 1 回だけ判定）
        var projectile = opp.ActiveProjectile;
        var projectileIncoming = projectile != null &&
                                 Mathf.Abs(projectile.transform.position.x - self.transform.position.x) < 2.8f &&
                                 projectile.Direction == -toward;
        var threatened = (opp.IsThreatening && dist < 1.8f) || projectileIncoming;
        if (threatened && !_wasThreatened)
        {
            if (projectileIncoming && Random.value < _jumpOverProjectileChance)
                SetIntent(Intent.JumpIn, 0.6f);
            else if (Random.value < _guardChance)
                SetIntent(opp.IsThreatening && opp.AttackKind == FighterAttackKind.Crouch ? Intent.CrouchGuard : Intent.Guard, 0.45f);
        }
        _wasThreatened = threatened;

        if (_intentTimer <= 0f)
            Decide(self, dist);

        // ---- 空中: 近ければジャンプ攻撃
        if (!self.IsGrounded)
        {
            var cmdAir = new FighterCommand { Move = 0 };
            if (!_airAttackDone && dist < 1.4f && self.transform.position.y > 0.4f)
            {
                cmdAir.Attack = true;
                _airAttackDone = true;
            }
            return cmdAir;
        }
        _airAttackDone = false;

        var cmd = new FighterCommand();
        var first = !_intentStarted && self.Action == FighterAction.None;
        switch (_intent)
        {
            case Intent.Approach:
                cmd.Move = dist > _closeRange * 0.8f ? toward : 0;
                break;
            case Intent.Retreat:
                cmd.Move = -toward;
                break;
            case Intent.Guard:
                cmd.Move = -toward;
                break;
            case Intent.CrouchGuard:
                cmd.Move = -toward;
                cmd.Crouch = true;
                break;
            case Intent.JumpIn:
                cmd.Move = toward;
                cmd.Jump = first;
                break;
            case Intent.JumpBack:
                cmd.Move = -toward;
                cmd.Jump = first;
                break;
            case Intent.Attack:
                cmd.Attack = first;
                break;
            case Intent.CrouchAttack:
                cmd.Crouch = true;
                cmd.Attack = first;
                break;
            case Intent.Special:
                cmd.Special = first && self.ActiveProjectile == null;
                if (cmd.Special) _specialTimer = _specialCooldown;
                break;
        }
        if (first)
        {
            _intentStarted = true;
            if (cmd.Attack || cmd.Special)
                _restTimer = Random.Range(_attackRest.x, _attackRest.y);
        }
        return cmd;
    }

    private void Decide(Fighter self, float dist)
    {
        var r = Random.value;
        var duration = Random.Range(_decisionInterval.x, _decisionInterval.y);

        if (dist > _farRange)
        {
            if (_specialTimer <= 0f && self.ActiveProjectile == null && r < 0.35f) SetIntent(Intent.Special, 0.8f);
            else if (r < 0.85f) SetIntent(Intent.Approach, duration * 2f);
            else SetIntent(Intent.JumpIn, 0.7f);
        }
        else if (dist > _closeRange)
        {
            if (_specialTimer <= 0f && self.ActiveProjectile == null && r < 0.15f) SetIntent(Intent.Special, 0.8f);
            else if (r < 0.65f) SetIntent(Intent.Approach, duration);
            else if (r < 0.78f) SetIntent(Intent.JumpIn, 0.7f);
            else if (r < 0.9f) SetIntent(Intent.Idle, duration);
            else SetIntent(Intent.Retreat, duration);
        }
        else if (_restTimer > 0f)
        {
            // 攻撃直後は様子見
            if (r < 0.45f) SetIntent(Intent.Guard, duration);
            else if (r < 0.75f) SetIntent(Intent.Idle, duration);
            else SetIntent(Intent.Retreat, duration);
        }
        else
        {
            if (r < 0.42f) SetIntent(Intent.Attack, 0.35f);
            else if (r < 0.62f) SetIntent(Intent.CrouchAttack, 0.35f);
            else if (r < 0.75f) SetIntent(Intent.Retreat, duration);
            else if (r < 0.82f) SetIntent(Intent.JumpBack, 0.7f);
            else SetIntent(Intent.Guard, duration);
        }
    }

    private void SetIntent(Intent intent, float duration)
    {
        _intent = intent;
        _intentTimer = duration;
        _intentStarted = false;
    }
}
