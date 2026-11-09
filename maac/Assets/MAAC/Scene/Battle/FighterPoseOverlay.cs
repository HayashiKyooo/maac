using UnityEngine;

/// <summary>
/// 攻撃・しゃがみ・ガード・のけぞり等のクリップが無いため、
/// Animator の評価後（LateUpdate）にヒューマノイドのボーンを手続き的に曲げてポーズを作る。
/// ボーンの向きはワールド空間の方向で指定するので、リグ（Player / Aika）に依存しない。
/// </summary>
[DefaultExecutionOrder(100)]
public class FighterPoseOverlay : MonoBehaviour
{
    [SerializeField] private Fighter _fighter;
    [SerializeField] private Animator _animator;
    [Tooltip("しゃがみ時に腰を下げる量 (m)")]
    [SerializeField] private float _crouchDrop = 0.38f;
    [Tooltip("しゃがみ / ガードの切り替わりの速さ")]
    [SerializeField] private float _smoothing = 20f;

    private Transform _hips, _spine, _chest, _head;
    private Transform _lUpperArm, _lLowerArm, _lHand, _rUpperArm, _rLowerArm, _rHand;
    private Transform _lUpperLeg, _lLowerLeg, _lFoot, _rUpperLeg, _rLowerLeg, _rFoot;
    private float _crouch;
    private float _guard;

    private void Awake()
    {
        if (_fighter == null) _fighter = GetComponent<Fighter>();
        if (_animator == null) _animator = GetComponent<Animator>();
        if (_animator == null || !_animator.isHuman) return;

        _hips = Bone(HumanBodyBones.Hips);
        _spine = Bone(HumanBodyBones.Spine);
        _chest = Bone(HumanBodyBones.Chest) ?? _spine;
        _head = Bone(HumanBodyBones.Head);
        _lUpperArm = Bone(HumanBodyBones.LeftUpperArm);
        _lLowerArm = Bone(HumanBodyBones.LeftLowerArm);
        _lHand = Bone(HumanBodyBones.LeftHand);
        _rUpperArm = Bone(HumanBodyBones.RightUpperArm);
        _rLowerArm = Bone(HumanBodyBones.RightLowerArm);
        _rHand = Bone(HumanBodyBones.RightHand);
        _lUpperLeg = Bone(HumanBodyBones.LeftUpperLeg);
        _lLowerLeg = Bone(HumanBodyBones.LeftLowerLeg);
        _lFoot = Bone(HumanBodyBones.LeftFoot);
        _rUpperLeg = Bone(HumanBodyBones.RightUpperLeg);
        _rLowerLeg = Bone(HumanBodyBones.RightLowerLeg);
        _rFoot = Bone(HumanBodyBones.RightFoot);
    }

    private Transform Bone(HumanBodyBones b) => _animator.GetBoneTransform(b);

    private void LateUpdate()
    {
        if (_fighter == null || _hips == null) return;

        var pose = _fighter.Pose;
        var k = 1f - Mathf.Exp(-_smoothing * Time.deltaTime);
        _crouch = Mathf.Lerp(_crouch, pose.Crouch, k);
        _guard = Mathf.Lerp(_guard, pose.Guard, k);

        var f = _fighter.Forward;
        var u = Vector3.up;
        var r = Vector3.Cross(u, f); // キャラの右

        // カメラに近い側の腕・脚を「前の手足」にして、攻撃が画面から見えるようにする
        var cam = _fighter.Arena != null ? _fighter.Arena.Camera : null;
        var rightIsFront = true;
        if (cam != null && _lUpperArm != null && _rUpperArm != null)
        {
            var c = cam.transform.position;
            rightIsFront = (c - _rUpperArm.position).sqrMagnitude < (c - _lUpperArm.position).sqrMagnitude;
        }
        var s = rightIsFront ? r : -r; // 前の手足がある側
        var frontUA = rightIsFront ? _rUpperArm : _lUpperArm;
        var frontLA = rightIsFront ? _rLowerArm : _lLowerArm;
        var frontH = rightIsFront ? _rHand : _lHand;
        var backUA = rightIsFront ? _lUpperArm : _rUpperArm;
        var backLA = rightIsFront ? _lLowerArm : _rLowerArm;
        var backH = rightIsFront ? _lHand : _rHand;
        var frontUL = rightIsFront ? _rUpperLeg : _lUpperLeg;
        var frontLL = rightIsFront ? _rLowerLeg : _lLowerLeg;
        var frontF = rightIsFront ? _rFoot : _lFoot;
        var backUL = rightIsFront ? _lUpperLeg : _rUpperLeg;
        var backLL = rightIsFront ? _lLowerLeg : _rLowerLeg;
        var backF = rightIsFront ? _lFoot : _rFoot;

        // ---- 腰（しゃがみ）
        if (_crouch > 0.001f)
            _hips.position -= u * (_crouchDrop * _crouch);

        // ---- 上半身
        Rotate(_spine, Quaternion.AngleAxis(12f * _crouch - 22f * pose.Hit, r));
        Rotate(_chest, Quaternion.AngleAxis(-10f * pose.Hit, r));
        var twist = (rightIsFront ? -1f : 1f) * (25f * pose.Punch + 15f * pose.SpecialRelease);
        Rotate(_chest, Quaternion.AngleAxis(twist, u));
        Rotate(_head, Quaternion.AngleAxis(-15f * pose.Hit, r));

        // ---- 腕
        // 構え（ガード / 攻撃中の逆の手）
        var stance = Mathf.Max(_guard, pose.Punch * 0.8f, pose.AirKick * 0.8f);
        Aim(backUA, backLA, (f * 0.45f - u * 0.7f - s * 0.1f).normalized, stance);
        Aim(backLA, backH, (u * 0.8f + f * 0.45f + s * 0.15f).normalized, stance);
        Aim(frontUA, frontLA, (f * 0.45f - u * 0.7f + s * 0.15f).normalized, _guard);
        Aim(frontLA, frontH, (u * 0.8f + f * 0.45f - s * 0.2f).normalized, _guard);

        // パンチ（前の手）
        Aim(frontUA, frontLA, (f - u * 0.08f - s * 0.12f).normalized, pose.Punch);
        Aim(frontLA, frontH, f, pose.Punch);

        // 波動拳: 溜め（腰に引く）→ 両手を前に突き出す
        Aim(frontUA, frontLA, (-f * 0.35f - u * 0.9f).normalized, pose.SpecialCharge);
        Aim(frontLA, frontH, (f * 0.7f + u * 0.15f).normalized, pose.SpecialCharge);
        Aim(backUA, backLA, (-f * 0.35f - u * 0.9f).normalized, pose.SpecialCharge);
        Aim(backLA, backH, (f * 0.7f + u * 0.15f).normalized, pose.SpecialCharge);
        Aim(frontUA, frontLA, (f - s * 0.25f).normalized, pose.SpecialRelease);
        Aim(frontLA, frontH, f, pose.SpecialRelease);
        Aim(backUA, backLA, (f + s * 0.25f).normalized, pose.SpecialRelease);
        Aim(backLA, backH, f, pose.SpecialRelease);

        // のけぞり: 腕が後ろに流れる
        Aim(frontUA, frontLA, (-f * 0.3f - u * 0.8f + s * 0.4f).normalized, pose.Hit * 0.6f);
        Aim(backUA, backLA, (-f * 0.3f - u * 0.8f - s * 0.4f).normalized, pose.Hit * 0.6f);

        // ---- 脚
        if (_crouch > 0.001f)
        {
            Aim(frontUL, frontLL, (f * 0.85f - u * 0.55f + s * 0.12f).normalized, _crouch);
            Aim(frontLL, frontF, (-f * 0.4f - u).normalized, _crouch);
            Aim(backUL, backLL, (f * 0.6f - u * 0.75f - s * 0.12f).normalized, _crouch);
            Aim(backLL, backF, (-f * 0.6f - u * 0.8f).normalized, _crouch);
        }

        // ジャンプキック（前の脚を伸ばし、後ろの脚はたたむ）
        Aim(frontUL, frontLL, (f - u * 0.35f).normalized, pose.AirKick);
        Aim(frontLL, frontF, (f - u * 0.3f).normalized, pose.AirKick);
        Aim(backUL, backLL, (f * 0.7f - u * 0.3f).normalized, pose.AirKick);
        Aim(backLL, backF, (-f * 0.4f - u * 0.6f).normalized, pose.AirKick);
    }

    private static void Rotate(Transform bone, Quaternion worldRotation)
    {
        if (bone == null) return;
        bone.rotation = worldRotation * bone.rotation;
    }

    /// <summary>bone → child の向きを、ワールド方向 dir に weight 分だけ向ける</summary>
    private static void Aim(Transform bone, Transform child, Vector3 dir, float weight)
    {
        if (bone == null || child == null || weight <= 0.001f) return;
        var current = child.position - bone.position;
        if (current.sqrMagnitude < 1e-8f) return;
        var delta = Quaternion.FromToRotation(current, dir);
        bone.rotation = Quaternion.Slerp(Quaternion.identity, delta, Mathf.Clamp01(weight)) * bone.rotation;
    }
}
