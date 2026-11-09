using System;
using UnityEngine;

/// <summary>
/// 簡易 2D 格闘ゲームのファイター。
/// 移動はワールド X 軸上のみ（Z は BattleArena の奥行きに固定）。
/// 立ち / しゃがみ / ジャンプ攻撃、波動拳、ガード（後ろ入力）、のけぞり、KO を持つ。
/// </summary>
[DisallowMultipleComponent]
public class Fighter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private string _displayName = "PLAYER";
    [SerializeField] private FighterInputBase _input;
    [SerializeField] private Fighter _opponent;
    [SerializeField] private BattleArena _arena;
    [SerializeField] private Animator _animator;
    [SerializeField] private HadoukenProjectile _projectilePrefab;

    [Header("Status")]
    [SerializeField] private int _maxHp = 100;

    [Header("Body")]
    [SerializeField] private float _bodyHalfWidth = 0.28f;
    [SerializeField] private float _standHeight = 1.7f;
    [SerializeField] private float _crouchHeight = 1.05f;

    [Header("Move")]
    [SerializeField] private float _walkSpeed = 2.0f;
    [SerializeField] private float _backSpeed = 1.5f;
    [SerializeField] private float _jumpVelocity = 7.0f;
    [SerializeField] private float _jumpHorizontalSpeed = 2.6f;
    [SerializeField] private float _gravity = -22f;

    [Header("Attacks")]
    [SerializeField] private FighterAttackData _standAttack = new()
    {
        Damage = 8, Startup = 0.08f, Active = 0.1f, Recovery = 0.2f, Reach = 0.95f,
        HitMinHeight = 1.0f, HitMaxHeight = 1.55f, Height = AttackHeight.Mid, HitStun = 0.35f, BlockStun = 0.2f, Knockback = 2.5f,
    };
    [SerializeField] private FighterAttackData _crouchAttack = new()
    {
        Damage = 6, Startup = 0.07f, Active = 0.1f, Recovery = 0.22f, Reach = 0.9f,
        HitMinHeight = 0.5f, HitMaxHeight = 1.0f, Height = AttackHeight.Low, HitStun = 0.3f, BlockStun = 0.18f, Knockback = 2.0f,
    };
    [SerializeField] private FighterAttackData _airAttack = new()
    {
        Damage = 10, Startup = 0.06f, Active = 0.3f, Recovery = 0f, Reach = 0.85f,
        HitMinHeight = 0.0f, HitMaxHeight = 0.9f, Height = AttackHeight.Overhead, HitStun = 0.4f, BlockStun = 0.22f, Knockback = 2.8f,
    };

    [Header("Special (波動拳)")]
    [SerializeField] private FighterAttackData _special = new()
    {
        Damage = 12, Startup = 0.3f, Active = 0f, Recovery = 0.45f, Reach = 0f,
        HitMinHeight = 0f, HitMaxHeight = 0f, Height = AttackHeight.Mid, HitStun = 0.45f, BlockStun = 0.3f, Knockback = 3.0f,
    };
    [SerializeField] private float _projectileSpeed = 7f;
    [Tooltip("弾の出現位置（x: 前方, y: 高さ）")]
    [SerializeField] private Vector2 _projectileSpawnOffset = new(0.6f, 1.15f);

    [Header("Hit / KO")]
    [SerializeField] private float _hitStopDuration = 0.07f;
    [SerializeField] private float _koFallAngle = 85f;
    [SerializeField] private float _koFallDuration = 0.5f;

    private static readonly int AnimMoveX = Animator.StringToHash("MoveX");
    private static readonly int AnimGrounded = Animator.StringToHash("Grounded");

    private FighterCommand _command;
    private Vector2 _velocity;
    private float _actionTime;
    private float _stunDuration;
    private bool _hitApplied;
    private bool _specialFired;
    private bool _airAttackUsed;
    private float _hitStopTimer;
    private float _koTime;
    private bool _koLanded;

    public string DisplayName => _displayName;
    public Fighter Opponent => _opponent;
    public BattleArena Arena => _arena;
    public int MaxHp => _maxHp;
    public int Hp { get; private set; }
    public float HpRatio => _maxHp > 0 ? (float)Hp / _maxHp : 0f;
    public bool IsKO => Action == FighterAction.KO;

    /// <summary>false の間は入力を受け付けない（バトル開始前 / 決着後）</summary>
    public bool ControlEnabled { get; set; }

    public FighterAction Action { get; private set; }
    public FighterAttackKind AttackKind { get; private set; }
    public bool IsGrounded { get; private set; } = true;
    public bool IsCrouching { get; private set; }
    /// <summary>向き（+1: ワールド +X を向いている, -1: -X）</summary>
    public int FacingSign { get; private set; } = 1;
    public Vector3 Forward => new(FacingSign, 0f, 0f);
    public float BodyHalfWidth => _bodyHalfWidth;
    public float HurtHeight => IsCrouching ? _crouchHeight : _standHeight;
    public float StandHeight => _standHeight;
    public bool IsInHitStop => _hitStopTimer > 0f;
    public HadoukenProjectile ActiveProjectile { get; private set; }
    public FighterPose Pose { get; private set; }

    /// <summary>後ろ（相手と逆方向）を入力しているか</summary>
    public bool IsHoldingBack => _command.Move != 0 && _command.Move == -FacingSign;

    /// <summary>攻撃の発生前〜持続中（AI のガード判断用）</summary>
    public bool IsThreatening =>
        (Action == FighterAction.Attack && _actionTime < CurrentAttack.Startup + CurrentAttack.Active) ||
        (Action == FighterAction.Special && !_specialFired);

    public FighterAttackData CurrentAttack => AttackKind switch
    {
        FighterAttackKind.Crouch => _crouchAttack,
        FighterAttackKind.Air => _airAttack,
        _ => _standAttack,
    };

    public event Action<Fighter> HpChanged;
    public event Action<Fighter> KnockedOut;
    /// <summary>攻撃を受けた（victim, 当たった位置, ガードしたか, 飛び道具か）</summary>
    public event Action<Fighter, Vector3, bool, bool> Damaged;

    private void Awake()
    {
        Hp = _maxHp;
        if (_animator == null) _animator = GetComponent<Animator>();
        if (_animator != null) _animator.applyRootMotion = false;

        // 格闘ゲーム中は CharacterController を使わず、Transform を直接動かす
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        FacingSign = transform.forward.x >= 0f ? 1 : -1;
    }

    private void Start()
    {
        var p = transform.position;
        if (_arena != null)
        {
            p.y = _arena.GroundY;
            p.z = _arena.DepthZ;
        }
        transform.position = p;
        ApplyFacingRotation();
    }

    private void Update()
    {
        var dt = Time.deltaTime;

        if (_hitStopTimer > 0f)
        {
            _hitStopTimer -= dt;
            SetAnimatorSpeed(0f);
            return;
        }
        SetAnimatorSpeed(Action == FighterAction.KO && _koLanded && _koTime >= _koFallDuration ? 0f : 1f);

        if (Action == FighterAction.KO)
        {
            UpdateKO(dt);
            UpdatePose();
            return;
        }

        _command = ControlEnabled && _input != null ? _input.ReadCommand(this, dt) : default;

        UpdateFacing();

        switch (Action)
        {
            case FighterAction.None: UpdateNeutral(); break;
            case FighterAction.Attack: UpdateAttack(dt); break;
            case FighterAction.Special: UpdateSpecial(dt); break;
            case FighterAction.HitStun:
            case FighterAction.BlockStun: UpdateStun(dt); break;
        }

        ApplyPhysics(dt);
        UpdateAnimator(dt);
        UpdatePose();
    }

    // ---------------------------------------------------------------- 行動

    private void UpdateNeutral()
    {
        if (IsGrounded)
        {
            if (_command.Special && ActiveProjectile == null)
            {
                StartSpecial();
                return;
            }
            if (_command.Attack || _command.Special)
            {
                StartAttack(_command.Crouch ? FighterAttackKind.Crouch : FighterAttackKind.Stand);
                return;
            }
            if (_command.Jump)
            {
                IsGrounded = false;
                IsCrouching = false;
                _airAttackUsed = false;
                _velocity = new Vector2(_command.Move * _jumpHorizontalSpeed, _jumpVelocity);
                return;
            }

            IsCrouching = _command.Crouch;
            if (IsCrouching)
            {
                _velocity.x = 0f;
            }
            else
            {
                var relative = _command.Move * FacingSign;
                _velocity.x = _command.Move * (relative > 0 ? _walkSpeed : _backSpeed);
            }
        }
        else if ((_command.Attack || _command.Special) && !_airAttackUsed)
        {
            _airAttackUsed = true;
            StartAttack(FighterAttackKind.Air);
        }
    }

    private void StartAttack(FighterAttackKind kind)
    {
        Action = FighterAction.Attack;
        AttackKind = kind;
        _actionTime = 0f;
        _hitApplied = false;
        if (kind != FighterAttackKind.Air)
        {
            _velocity.x = 0f;
            IsCrouching = kind == FighterAttackKind.Crouch;
        }
    }

    private void UpdateAttack(float dt)
    {
        _actionTime += dt;
        var a = CurrentAttack;

        if (!_hitApplied && _actionTime >= a.Startup && _actionTime < a.Startup + a.Active)
            TryHit(a);

        if (AttackKind == FighterAttackKind.Air)
        {
            // ジャンプ攻撃は着地 or 持続終了で終わる
            if (IsGrounded || _actionTime >= a.Startup + a.Active)
                Action = FighterAction.None;
        }
        else if (_actionTime >= a.TotalDuration)
        {
            Action = FighterAction.None;
        }
    }

    private void TryHit(FighterAttackData a)
    {
        var opp = _opponent;
        if (opp == null || opp.IsKO) return;

        var pos = transform.position;
        var opos = opp.transform.position;
        var forwardDist = (opos.x - pos.x) * FacingSign;
        if (forwardDist < -0.1f || forwardDist - opp.BodyHalfWidth > a.Reach) return;

        var hitMin = pos.y + a.HitMinHeight;
        var hitMax = pos.y + a.HitMaxHeight;
        if (hitMax < opos.y || hitMin > opos.y + opp.HurtHeight) return;

        _hitApplied = true;
        var hitY = Mathf.Clamp((hitMin + hitMax) * 0.5f, opos.y + 0.1f, opos.y + opp.HurtHeight - 0.1f);
        var point = new Vector3(pos.x + FacingSign * Mathf.Clamp(forwardDist - opp.BodyHalfWidth * 0.5f, 0.2f, a.Reach), hitY, pos.z);

        opp.ReceiveAttack(a, FacingSign, point, false);
        ApplyHitStop(_hitStopDuration);
        opp.ApplyHitStop(_hitStopDuration);
    }

    private void StartSpecial()
    {
        Action = FighterAction.Special;
        _actionTime = 0f;
        _specialFired = false;
        _velocity.x = 0f;
        IsCrouching = false;
    }

    private void UpdateSpecial(float dt)
    {
        _actionTime += dt;
        if (!_specialFired && _actionTime >= _special.Startup)
        {
            _specialFired = true;
            FireProjectile();
        }
        if (_actionTime >= _special.Startup + _special.Recovery)
            Action = FighterAction.None;
    }

    private void FireProjectile()
    {
        if (_projectilePrefab == null) return;
        var pos = transform.position + new Vector3(FacingSign * _projectileSpawnOffset.x, _projectileSpawnOffset.y, 0f);
        var projectile = Instantiate(_projectilePrefab, pos, Quaternion.identity);
        projectile.Launch(this, FacingSign, _projectileSpeed, _special, _arena);
        ActiveProjectile = projectile;
    }

    /// <summary>弾が消えた時に HadoukenProjectile から呼ばれる</summary>
    public void NotifyProjectileGone(HadoukenProjectile projectile)
    {
        if (ActiveProjectile == projectile) ActiveProjectile = null;
    }

    private void UpdateStun(float dt)
    {
        _actionTime += dt;
        if (IsGrounded)
            _velocity.x = Mathf.MoveTowards(_velocity.x, 0f, 12f * dt);
        if (_actionTime >= _stunDuration && IsGrounded)
            Action = FighterAction.None;
    }

    // ---------------------------------------------------------------- 被弾

    /// <summary>体（やられ判定）と点が重なっているか</summary>
    public bool HurtOverlaps(Vector3 point, float radius)
    {
        var p = transform.position;
        return Mathf.Abs(point.x - p.x) < _bodyHalfWidth + radius &&
               point.y > p.y - radius && point.y < p.y + HurtHeight + radius;
    }

    /// <summary>攻撃を受ける。ガードした場合は false</summary>
    public bool ReceiveAttack(FighterAttackData a, int attackerFacing, Vector3 point, bool isProjectile)
    {
        if (IsKO) return false;

        var canBlock = (Action == FighterAction.None || Action == FighterAction.BlockStun) && IsGrounded && IsHoldingBack;
        if (canBlock)
        {
            var crouchGuard = _command.Crouch;
            if (a.Height == AttackHeight.Low && !crouchGuard) canBlock = false;
            if (a.Height == AttackHeight.Overhead && crouchGuard) canBlock = false;
        }

        if (canBlock)
        {
            Action = FighterAction.BlockStun;
            IsCrouching = _command.Crouch;
            _actionTime = 0f;
            _stunDuration = a.BlockStun;
            _velocity.x = attackerFacing * a.Knockback * 0.6f;
            ApplyDamage(isProjectile ? Mathf.Max(1, a.Damage / 6) : 0);
            Damaged?.Invoke(this, point, true, isProjectile);
            if (Hp <= 0) StartKO(attackerFacing);
            return false;
        }

        ApplyDamage(a.Damage);
        Damaged?.Invoke(this, point, false, isProjectile);

        if (Hp <= 0)
        {
            StartKO(attackerFacing);
            return true;
        }

        Action = FighterAction.HitStun;
        _actionTime = 0f;
        _stunDuration = a.HitStun;
        _velocity.x = attackerFacing * a.Knockback;
        if (!IsGrounded) _velocity.y = Mathf.Max(_velocity.y, 2.5f);
        return true;
    }

    public void ApplyHitStop(float duration)
    {
        _hitStopTimer = Mathf.Max(_hitStopTimer, duration);
    }

    private void ApplyDamage(int damage)
    {
        if (damage <= 0) return;
        Hp = Mathf.Max(0, Hp - damage);
        HpChanged?.Invoke(this);
    }

    private void StartKO(int attackerFacing)
    {
        Action = FighterAction.KO;
        IsCrouching = false;
        _velocity = new Vector2(attackerFacing * 3f, IsGrounded ? 4f : Mathf.Max(_velocity.y, 3f));
        IsGrounded = false;
        _koTime = 0f;
        _koLanded = false;
        KnockedOut?.Invoke(this);
    }

    private void UpdateKO(float dt)
    {
        _koTime += dt;
        if (_koLanded)
            _velocity.x = Mathf.MoveTowards(_velocity.x, 0f, 8f * dt);
        ApplyPhysics(dt);
        if (IsGrounded) _koLanded = true;

        // 後ろ向きに倒れる（足元を支点に回転）
        var k = Mathf.Clamp01(_koTime / _koFallDuration);
        k = 1f - (1f - k) * (1f - k);
        var baseRot = Quaternion.Euler(0f, FacingSign > 0 ? 90f : 270f, 0f);
        var right = baseRot * Vector3.right;
        transform.rotation = Quaternion.AngleAxis(-_koFallAngle * k, right) * baseRot;
    }

    // ---------------------------------------------------------------- 物理・向き・アニメ

    private void UpdateFacing()
    {
        if (_opponent != null && IsGrounded && Action == FighterAction.None)
        {
            var dx = _opponent.transform.position.x - transform.position.x;
            if (Mathf.Abs(dx) > 0.05f) FacingSign = dx > 0f ? 1 : -1;
        }
        ApplyFacingRotation();
    }

    private void ApplyFacingRotation()
    {
        transform.rotation = Quaternion.Euler(0f, FacingSign > 0 ? 90f : 270f, 0f);
    }

    private void ApplyPhysics(float dt)
    {
        var p = transform.position;
        var ground = _arena != null ? _arena.GroundY : 0f;

        if (!IsGrounded)
        {
            _velocity.y += _gravity * dt;
            p.x += _velocity.x * dt;
            p.y += _velocity.y * dt;
            if (p.y <= ground && _velocity.y <= 0f)
            {
                p.y = ground;
                _velocity.y = 0f;
                IsGrounded = true;
                if (Action == FighterAction.None || Action == FighterAction.Attack)
                {
                    _velocity.x = 0f;
                    if (Action == FighterAction.Attack) Action = FighterAction.None;
                }
            }
        }
        else
        {
            p.x += _velocity.x * dt;
            p.y = ground;
        }

        if (_arena != null) p.z = _arena.DepthZ;
        transform.position = p;
    }

    /// <summary>BattleArena から画面内への押し戻し・重なり解消に使う</summary>
    public void SetPositionX(float x)
    {
        var p = transform.position;
        p.x = x;
        transform.position = p;
    }

    private void UpdateAnimator(float dt)
    {
        if (_animator == null) return;
        var moveX = 0f;
        if (Action == FighterAction.None && IsGrounded && !IsCrouching)
        {
            var relative = _velocity.x * FacingSign;
            moveX = relative > 0.01f ? 1f : relative < -0.01f ? -1f : 0f;
        }
        _animator.SetFloat(AnimMoveX, moveX, 0.08f, dt);
        _animator.SetBool(AnimGrounded, IsGrounded || Action == FighterAction.KO);
    }

    private void SetAnimatorSpeed(float speed)
    {
        if (_animator != null && !Mathf.Approximately(_animator.speed, speed))
            _animator.speed = speed;
    }

    private void UpdatePose()
    {
        var pose = new FighterPose();
        pose.Crouch = IsGrounded && IsCrouching && Action != FighterAction.KO ? 1f : 0f;

        switch (Action)
        {
            case FighterAction.Attack:
            {
                var a = CurrentAttack;
                var w = Phase01(_actionTime, a.Startup, a.Startup + a.Active, a.TotalDuration);
                if (AttackKind == FighterAttackKind.Air) pose.AirKick = w;
                else pose.Punch = w;
                pose.Guard = 0.5f;
                break;
            }
            case FighterAction.Special:
            {
                var s = _special.Startup;
                pose.SpecialCharge = _actionTime < s ? Mathf.Clamp01(_actionTime / (s * 0.5f)) : 0f;
                pose.SpecialRelease = _actionTime >= s
                    ? 1f - Mathf.Clamp01((_actionTime - s - _special.Recovery * 0.6f) / (_special.Recovery * 0.4f))
                    : 0f;
                break;
            }
            case FighterAction.HitStun:
                pose.Hit = 1f - Mathf.Clamp01(_actionTime / Mathf.Max(0.01f, _stunDuration));
                break;
            case FighterAction.BlockStun:
                pose.Guard = 1f;
                break;
            case FighterAction.KO:
                pose.Hit = 1f;
                break;
            default:
                // 相手が攻撃してきている時に後ろを入れていればガードの構え
                if (IsHoldingBack && IsGrounded && _opponent != null &&
                    (_opponent.IsThreatening || _opponent.ActiveProjectile != null))
                    pose.Guard = 1f;
                break;
        }

        Pose = pose;
    }

    /// <summary>発生中 0→1、持続 1、硬直 1→0</summary>
    private static float Phase01(float t, float activeStart, float activeEnd, float end)
    {
        if (t < activeStart) return activeStart > 0f ? Mathf.Clamp01(t / activeStart) : 1f;
        if (t < activeEnd) return 1f;
        return end > activeEnd ? 1f - Mathf.Clamp01((t - activeEnd) / (end - activeEnd)) : 0f;
    }
}
