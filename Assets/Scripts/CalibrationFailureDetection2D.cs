using System;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class CalibrationFailureDetection2D : MonoBehaviour
{
    [Header("References")]
    public PrecisionNeedleChallenge2D needleChallenge;
    public Transform detectionOrigin;
    public PatrollingSoldier2D[] soldiers =
        Array.Empty<PatrollingSoldier2D>();
    public GameplayMessageUI2D messageUI;
    public CalibrationCaughtPresentation2D caughtPresentation;

    [Header("Detection Rules")]
    [Min(1)]
    public int failuresRequiredToBeCaught = 3;
    [Min(0f)]
    public float soldierDetectionRadius = 12f;
    public bool requireActiveSoldierInRange = true;
    public bool resetFailuresWhenChallengeStarts;
    public bool resetFailuresWhenChallengeCompletes = true;

    [Header("Editable Messages")]
    [TextArea]
    public string detectedMessageFormat =
        "A guard heard the noise ({0} / {1}).";
    [TextArea]
    public string outsideDetectionRangeMessage =
        "The guards were too far away to hear that mistake.";
    [TextArea]
    public string caughtMessage = "The guards have found you.";

    [Header("Events")]
    public UnityEvent onSoldierAlerted = new UnityEvent();
    public UnityEvent onFailureOutsideDetectionRange = new UnityEvent();
    public UnityEvent onCaughtAfterRepeatedFailures = new UnityEvent();

    public int DetectedFailureCount { get; private set; }
    public bool IsCaught { get; private set; }
    public float ClosestActiveSoldierDistance { get; private set; } =
        float.PositiveInfinity;

    bool subscribed;

    void Awake()
    {
        CacheReferences();
        Subscribe();
    }

    void OnEnable()
    {
        CacheReferences();
        Subscribe();
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    void OnDestroy()
    {
        Unsubscribe();
    }

    void OnValidate()
    {
        failuresRequiredToBeCaught =
            Mathf.Max(1, failuresRequiredToBeCaught);
        soldierDetectionRadius =
            Mathf.Max(0f, soldierDetectionRadius);
    }

    public void ResetDetectionProgress()
    {
        DetectedFailureCount = 0;
        IsCaught = false;
        ClosestActiveSoldierDistance = float.PositiveInfinity;
    }

    public bool IsAnySoldierInDetectionRange()
    {
        CacheReferences();
        ClosestActiveSoldierDistance = float.PositiveInfinity;
        if (detectionOrigin == null)
        {
            return !requireActiveSoldierInRange;
        }

        Vector2 origin = detectionOrigin.position;
        if (soldiers != null)
        {
            foreach (PatrollingSoldier2D soldier in soldiers)
            {
                if (soldier == null
                    || (requireActiveSoldierInRange
                        && !soldier.gameObject.activeInHierarchy))
                {
                    continue;
                }

                float distance =
                    Vector2.Distance(origin, soldier.transform.position);
                ClosestActiveSoldierDistance =
                    Mathf.Min(ClosestActiveSoldierDistance, distance);
            }
        }

        if (!requireActiveSoldierInRange)
        {
            return true;
        }

        return ClosestActiveSoldierDistance
            <= Mathf.Max(0f, soldierDetectionRadius);
    }

    public void RegisterCalibrationFailure()
    {
        if (IsCaught)
        {
            return;
        }

        if (!IsAnySoldierInDetectionRange())
        {
            ShowFeedback(outsideDetectionRangeMessage);
            onFailureOutsideDetectionRange?.Invoke();
            return;
        }

        int required = Mathf.Max(1, failuresRequiredToBeCaught);
        DetectedFailureCount =
            Mathf.Min(required, DetectedFailureCount + 1);
        onSoldierAlerted?.Invoke();

        if (DetectedFailureCount < required)
        {
            string alertMessage =
                SafeFormat(
                    detectedMessageFormat,
                    DetectedFailureCount,
                    required);
            ShowFeedback(alertMessage);
            return;
        }

        IsCaught = true;
        ShowFeedback(caughtMessage);
        needleChallenge?.AbortChallengeForFailure();
        onCaughtAfterRepeatedFailures?.Invoke();
        caughtPresentation?.ShowCaught();
    }

    void HandleChallengeStarted()
    {
        if (resetFailuresWhenChallengeStarts)
        {
            ResetDetectionProgress();
        }
    }

    void HandleChallengeCompleted()
    {
        if (resetFailuresWhenChallengeCompletes)
        {
            ResetDetectionProgress();
        }
    }

    void ShowFeedback(string message)
    {
        needleChallenge?.SetExternalStatusMessage(message);
        messageUI?.ShowMessage(message);
    }

    void CacheReferences()
    {
        if (needleChallenge == null)
        {
            needleChallenge = GetComponent<PrecisionNeedleChallenge2D>();
        }

        if (detectionOrigin == null)
        {
            GameObject player =
                FindSceneObjectByExactName(BubuRunningGame.PlayerRootName);
            if (player != null)
            {
                detectionOrigin = player.transform;
            }
        }

        if (soldiers == null || soldiers.Length == 0)
        {
            soldiers =
                FindObjectsByType<PatrollingSoldier2D>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
        }

        if (messageUI == null)
        {
            messageUI =
                FindAnyObjectByType<GameplayMessageUI2D>(
                    FindObjectsInactive.Include);
        }

        if (caughtPresentation == null)
        {
            caughtPresentation =
                FindAnyObjectByType<CalibrationCaughtPresentation2D>(
                    FindObjectsInactive.Include);
        }
    }

    void Subscribe()
    {
        if (subscribed || needleChallenge == null)
        {
            return;
        }

        needleChallenge.onPrecisionFailed.AddListener(
            RegisterCalibrationFailure);
        needleChallenge.onChallengeStarted.AddListener(
            HandleChallengeStarted);
        needleChallenge.onChallengeCompleted.AddListener(
            HandleChallengeCompleted);
        subscribed = true;
    }

    void Unsubscribe()
    {
        if (!subscribed || needleChallenge == null)
        {
            return;
        }

        needleChallenge.onPrecisionFailed.RemoveListener(
            RegisterCalibrationFailure);
        needleChallenge.onChallengeStarted.RemoveListener(
            HandleChallengeStarted);
        needleChallenge.onChallengeCompleted.RemoveListener(
            HandleChallengeCompleted);
        subscribed = false;
    }

    void OnDrawGizmosSelected()
    {
        Transform origin =
            detectionOrigin != null ? detectionOrigin : transform;
        Gizmos.color = new Color(1f, 0.35f, 0.1f, 0.55f);
        Gizmos.DrawWireSphere(
            origin.position,
            Mathf.Max(0f, soldierDetectionRadius));
    }

    static string SafeFormat(
        string format,
        int current,
        int required)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return current + " / " + required;
        }

        try
        {
            return string.Format(format, current, required);
        }
        catch (FormatException)
        {
            return format;
        }
    }

    static GameObject FindSceneObjectByExactName(string objectName)
    {
        GameObject[] sceneObjects =
            FindObjectsByType<GameObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        foreach (GameObject sceneObject in sceneObjects)
        {
            if (sceneObject != null
                && string.Equals(
                    sceneObject.name,
                    objectName,
                    StringComparison.Ordinal))
            {
                return sceneObject;
            }
        }

        return null;
    }
}
