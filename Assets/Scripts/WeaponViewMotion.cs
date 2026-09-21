using System.Collections;
using UnityEngine;

/// <summary>
/// First-person weapon pose motion: reload dip, fire recoil, and equip/holster slides.
/// Only writes local pose while sliding or recoiling so Animator clips on the same
/// transform (e.g. bat swings) are not overwritten every frame.
/// </summary>
public class WeaponViewMotion : MonoBehaviour
{
    [Header("Holster / reload")]
    public Vector3 holsterLocalOffset = new Vector3(0f, -0.55f, 0.12f);
    public float holsterDuration = 0.2f;
    public float equipDuration = 0.24f;

    [Header("Recoil")]
    public Vector3 recoilPosKick = new Vector3(0.01f, 0.025f, -0.055f);
    public Vector3 recoilRotKick = new Vector3(-4.5f, 1.2f, 0.8f);
    public float recoilRecoverPos = 14f;
    public float recoilRecoverRot = 16f;

    Vector3 restLocalPos;
    Quaternion restLocalRot;
    bool restCaptured;

    Vector3 slideOffset;
    Vector3 recoilPos;
    Vector3 recoilEuler;
    Coroutine slideRoutine;
    bool sliding;
    bool drivingPose;

    public bool IsSliding => sliding;

    public static WeaponViewMotion Ensure(GameObject weapon)
    {
        if (weapon == null)
            return null;

        // Melee uses an Animator on the same transform — never drive its pose.
        if (weapon.GetComponent<Melee>() != null)
        {
            WeaponViewMotion existing = weapon.GetComponent<WeaponViewMotion>();
            if (existing != null)
                Object.Destroy(existing);
            return null;
        }

        WeaponViewMotion motion = weapon.GetComponent<WeaponViewMotion>();
        if (motion == null)
            motion = weapon.AddComponent<WeaponViewMotion>();
        return motion;
    }

    void Awake()
    {
        TryCaptureRest();
    }

    void OnEnable()
    {
        TryCaptureRest();
        if (!sliding)
            ApplyPoseIfDriving();
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        recoilPos = Vector3.Lerp(recoilPos, Vector3.zero, 1f - Mathf.Exp(-recoilRecoverPos * dt));
        recoilEuler = Vector3.Lerp(recoilEuler, Vector3.zero, 1f - Mathf.Exp(-recoilRecoverRot * dt));

        bool hasRecoil = recoilPos.sqrMagnitude > 0.00000025f || recoilEuler.sqrMagnitude > 0.0001f;
        bool shouldDrive = sliding || hasRecoil || slideOffset.sqrMagnitude > 0.00000025f;

        if (shouldDrive)
        {
            drivingPose = true;
            ApplyPose();
        }
        else if (drivingPose)
        {
            // Settle back to rest once, then release control to Animator.
            slideOffset = Vector3.zero;
            recoilPos = Vector3.zero;
            recoilEuler = Vector3.zero;
            ApplyPose();
            drivingPose = false;
        }
    }

    void TryCaptureRest()
    {
        if (restCaptured)
            return;
        restLocalPos = transform.localPosition;
        restLocalRot = transform.localRotation;
        restCaptured = true;
    }

    void RefreshRestFromCurrent()
    {
        if (drivingPose || sliding)
            return;
        restLocalPos = transform.localPosition;
        restLocalRot = transform.localRotation;
        restCaptured = true;
    }

    void ApplyPose()
    {
        if (!restCaptured)
            TryCaptureRest();

        transform.localPosition = restLocalPos + slideOffset + recoilPos;
        transform.localRotation = restLocalRot * Quaternion.Euler(recoilEuler);
    }

    void ApplyPoseIfDriving()
    {
        if (drivingPose || sliding || slideOffset.sqrMagnitude > 0.00000025f)
            ApplyPose();
    }

    public void KickRecoil(float scale = 1f)
    {
        RefreshRestFromCurrent();
        TryCaptureRest();
        drivingPose = true;
        float sx = Random.Range(-1f, 1f);
        float sy = Random.Range(0.7f, 1.15f);
        recoilPos += new Vector3(
            recoilPosKick.x * sx * scale,
            recoilPosKick.y * sy * scale,
            recoilPosKick.z * sy * scale);
        recoilEuler += new Vector3(
            recoilRotKick.x * sy * scale,
            recoilRotKick.y * sx * scale,
            recoilRotKick.z * sx * scale);
    }

    public void SnapEquipped()
    {
        StopSlide();
        slideOffset = Vector3.zero;
        recoilPos = Vector3.zero;
        recoilEuler = Vector3.zero;
        ApplyPose();
        drivingPose = false;
    }

    public void SnapHolstered()
    {
        StopSlide();
        TryCaptureRest();
        slideOffset = holsterLocalOffset;
        recoilPos = Vector3.zero;
        recoilEuler = Vector3.zero;
        drivingPose = true;
        ApplyPose();
    }

    public Coroutine PlayHolster(float duration = -1f)
    {
        RefreshRestFromCurrent();
        StopSlide();
        float d = duration > 0f ? duration : holsterDuration;
        drivingPose = true;
        slideRoutine = StartCoroutine(SlideRoutine(holsterLocalOffset, d));
        return slideRoutine;
    }

    public Coroutine PlayEquip(float duration = -1f)
    {
        StopSlide();
        TryCaptureRest();
        slideOffset = holsterLocalOffset;
        drivingPose = true;
        ApplyPose();
        float d = duration > 0f ? duration : equipDuration;
        slideRoutine = StartCoroutine(SlideRoutine(Vector3.zero, d));
        return slideRoutine;
    }

    public void BeginReloadDip(float reloadDuration)
    {
        RefreshRestFromCurrent();
        StopSlide();
        drivingPose = true;
        slideRoutine = StartCoroutine(ReloadDipRoutine(Mathf.Max(0.35f, reloadDuration)));
    }

    IEnumerator ReloadDipRoutine(float duration)
    {
        sliding = true;
        float downTime = Mathf.Min(0.22f, duration * 0.22f);
        float upTime = Mathf.Min(0.28f, duration * 0.28f);
        float hold = Mathf.Max(0f, duration - downTime - upTime);

        yield return SlideOffset(holsterLocalOffset, downTime);
        if (hold > 0f)
            yield return new WaitForSeconds(hold);
        yield return SlideOffset(Vector3.zero, upTime);

        sliding = false;
        slideRoutine = null;
    }

    IEnumerator SlideRoutine(Vector3 target, float duration)
    {
        sliding = true;
        yield return SlideOffset(target, duration);
        sliding = false;
        slideRoutine = null;
    }

    IEnumerator SlideOffset(Vector3 target, float duration)
    {
        Vector3 start = slideOffset;
        if (duration <= 0.0001f)
        {
            slideOffset = target;
            ApplyPose();
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            u = u * u * (3f - 2f * u);
            slideOffset = Vector3.LerpUnclamped(start, target, u);
            ApplyPose();
            yield return null;
        }

        slideOffset = target;
        ApplyPose();
    }

    void StopSlide()
    {
        if (slideRoutine != null)
        {
            StopCoroutine(slideRoutine);
            slideRoutine = null;
        }
        sliding = false;
    }

    void OnDisable()
    {
        StopSlide();
        recoilPos = Vector3.zero;
        recoilEuler = Vector3.zero;
        slideOffset = Vector3.zero;
        drivingPose = false;
    }
}
