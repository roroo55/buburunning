using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class TemporaryMovementSpeedModifier2D : MonoBehaviour
{
    [Header("Configured Slowdown")]
    [Min(0f)]
    [Tooltip("0.5 表示移动速度降低到原来的 50%。")]
    public float speedMultiplier = 0.5f;

    [Min(0f)]
    public float minimumDuration = 10f;

    [Min(0f)]
    public float maximumDuration = 15f;

    [Tooltip("效果仍在持续时再次触发，会重新随机计时，但不会继续叠加减速。")]
    public bool refreshDurationOnRetrigger = true;

    [Tooltip("开启后，10～15 秒按真实时间计算，不会被开始界面或暂停菜单冻结。")]
    public bool useUnscaledTime = true;
    public bool logEffectChanges = true;

    [Header("Optional Feedback")]
    public UnityEvent onSlowdownStarted = new UnityEvent();
    public UnityEvent onSlowdownEnded = new UnityEvent();

    public float CurrentSpeedMultiplier { get; private set; } = 1f;
    public float RemainingDuration { get; private set; }
    public bool IsEffectActive => slowdownRoutine != null;

    Coroutine slowdownRoutine;

    [ContextMenu("Apply Configured Slowdown")]
    public void ApplyConfiguredSlowdown()
    {
        float minimum = Mathf.Max(0f, minimumDuration);
        float maximum = Mathf.Max(minimum, maximumDuration);
        float duration =
            Mathf.Approximately(minimum, maximum)
                ? minimum
                : Random.Range(minimum, maximum);

        ApplySpeedModifier(speedMultiplier, duration);
    }

    public void ApplySpeedModifier(float multiplier, float duration)
    {
        if (slowdownRoutine != null)
        {
            if (!refreshDurationOnRetrigger)
            {
                return;
            }

            StopCoroutine(slowdownRoutine);
            slowdownRoutine = null;
        }

        float safeDuration = Mathf.Max(0f, duration);
        if (safeDuration <= 0f)
        {
            RestoreNormalSpeed(false);
            return;
        }

        CurrentSpeedMultiplier = Mathf.Max(0f, multiplier);
        RemainingDuration = safeDuration;
        onSlowdownStarted?.Invoke();

        if (logEffectChanges)
        {
            Debug.Log(
                "Temporary player slowdown started at "
                + CurrentSpeedMultiplier.ToString("0.##")
                + "x speed for "
                + RemainingDuration.ToString("0.##")
                + " seconds.");
        }

        slowdownRoutine = StartCoroutine(RunSpeedModifier());
    }

    IEnumerator RunSpeedModifier()
    {
        while (RemainingDuration > 0f)
        {
            yield return null;
            RemainingDuration -= useUnscaledTime
                ? Time.unscaledDeltaTime
                : Time.deltaTime;
        }

        slowdownRoutine = null;
        RestoreNormalSpeed(true);
    }

    void OnDisable()
    {
        if (slowdownRoutine != null)
        {
            StopCoroutine(slowdownRoutine);
            slowdownRoutine = null;
        }

        RestoreNormalSpeed(false);
    }

    void RestoreNormalSpeed(bool notify)
    {
        bool wasModified = !Mathf.Approximately(CurrentSpeedMultiplier, 1f);
        CurrentSpeedMultiplier = 1f;
        RemainingDuration = 0f;

        if (!wasModified || !notify)
        {
            return;
        }

        onSlowdownEnded?.Invoke();
        if (logEffectChanges)
        {
            Debug.Log("Temporary player slowdown ended; movement speed restored.");
        }
    }
}
