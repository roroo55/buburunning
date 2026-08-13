using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class PrecisionNeedleChallenge2D : MonoBehaviour
{
    [Serializable]
    public class PrecisionRound
    {
        [Range(0f, 360f)]
        public float targetAngle = 30f;

        [Range(1f, 180f)]
        public float precisionWindow = 14f;

        [Min(0.01f)]
        public float speedMultiplier = 1f;
    }

    [Header("Challenge Rules")]
    [Min(1)]
    public int requiredSuccessfulNeedles = 3;
    public PrecisionRound[] rounds = Array.Empty<PrecisionRound>();
    [Min(0f)]
    public float baseRotationSpeed = 540f;
    public bool randomizeStartingAngle = true;
    public bool resetProgressOnFailure;
    [Min(0f)]
    public float resultPauseDuration = 0.55f;
    [Min(0f)]
    public float completionDisplayDuration = 0.9f;
    public bool pauseGameDuringChallenge = true;
    public bool allowCancel = true;

    [Header("Input")]
#if ENABLE_INPUT_SYSTEM
    public Key calibrationKey = Key.Space;
    public Key cancelKey = Key.Escape;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
    public KeyCode legacyCalibrationKey = KeyCode.Space;
    public KeyCode legacyCancelKey = KeyCode.Escape;
#endif

    [Header("UI References")]
    public GameObject challengePanel;
    public RectTransform pointer;
    public Image pointerImage;
    public Image targetZoneImage;
    public Image dollImage;
    public Image progressFillImage;
    public Text progressText;
    public Text statusText;
    public Text instructionText;
    public Image[] placedNeedleImages = Array.Empty<Image>();

    [Header("Replaceable Art")]
    [Tooltip("留空时使用简单指针；之后可直接拖入银针 Sprite。")]
    public Sprite needleSprite;
    public Sprite dollSprite;

    [Header("Editable Text")]
    [TextArea]
    public string instructionMessage =
        "Press Space to place the needle inside the red target zone.";
    [TextArea]
    public string precisionSuccessMessage = "Perfect hit.";
    [TextArea]
    public string precisionFailureMessage =
        "Missed the target. Calibrate again.";
    [TextArea]
    public string completionMessage =
        "All three needles placed successfully.";

    [Header("Events")]
    public UnityEvent onChallengeStarted = new UnityEvent();
    public UnityEvent onNeedlePlaced = new UnityEvent();
    public UnityEvent onPrecisionFailed = new UnityEvent();
    public UnityEvent onChallengeCompleted = new UnityEvent();
    public UnityEvent onChallengeCanceled = new UnityEvent();

    public bool IsActive { get; private set; }
    public bool IsCompleted { get; private set; }
    public int SuccessfulNeedles { get; private set; }
    public float CurrentPointerAngle => pointerAngle;

    float pointerAngle;
    float previousTimeScale = 1f;
    bool resolvingAttempt;
    Coroutine resultRoutine;
    Font runtimeFont;

    void Awake()
    {
        ConfigureArtwork();
        ConfigureFonts();
        UpdateProgressVisuals();
        SetPanelVisible(false);
    }

    void Update()
    {
        if (!IsActive || resolvingAttempt)
        {
            return;
        }

        PrecisionRound round = GetCurrentRound();
        float speedMultiplier = round != null ? Mathf.Max(0.01f, round.speedMultiplier) : 1f;
        pointerAngle =
            Mathf.Repeat(
                pointerAngle
                + Mathf.Max(0f, baseRotationSpeed)
                * speedMultiplier
                * Time.unscaledDeltaTime,
                360f);
        UpdatePointerVisual();

        if (WasCalibrationPressed())
        {
            SubmitCurrentNeedle();
        }
        else if (allowCancel && WasCancelPressed())
        {
            CancelChallenge();
        }
    }

    public bool StartChallenge()
    {
        if (IsCompleted || IsActive)
        {
            return false;
        }

        ConfigureArtwork();
        ConfigureFonts();
        IsActive = true;
        resolvingAttempt = false;
        pointerAngle =
            randomizeStartingAngle
                ? UnityEngine.Random.Range(0f, 360f)
                : 0f;

        if (pauseGameDuringChallenge)
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        SetPanelVisible(true);
        SetStatus(instructionMessage);
        UpdateProgressVisuals();
        UpdateTargetVisual();
        UpdatePointerVisual();
        onChallengeStarted?.Invoke();
        return true;
    }

    public void SubmitCurrentNeedle()
    {
        if (!IsActive || resolvingAttempt)
        {
            return;
        }

        PrecisionRound round = GetCurrentRound();
        float targetAngle = round != null ? round.targetAngle : 0f;
        float precisionWindow = round != null ? round.precisionWindow : 14f;
        float angularError =
            Mathf.Abs(Mathf.DeltaAngle(pointerAngle, targetAngle));
        bool preciseHit = angularError <= Mathf.Max(0.5f, precisionWindow * 0.5f);

        if (preciseHit)
        {
            HandlePrecisionSuccess();
        }
        else
        {
            HandlePrecisionFailure();
        }
    }

    public void CancelChallenge()
    {
        if (!IsActive || IsCompleted)
        {
            return;
        }

        StopResultRoutine();
        IsActive = false;
        resolvingAttempt = false;
        RestoreTimeScale();
        SetPanelVisible(false);
        onChallengeCanceled?.Invoke();
    }

    public void ResetChallenge()
    {
        StopResultRoutine();
        IsActive = false;
        IsCompleted = false;
        resolvingAttempt = false;
        SuccessfulNeedles = 0;
        pointerAngle = 0f;
        RestoreTimeScale();
        UpdateProgressVisuals();
        UpdateTargetVisual();
        UpdatePointerVisual();
        SetPanelVisible(false);
    }

    public void SetNeedleSprite(Sprite sprite)
    {
        needleSprite = sprite;
        ConfigureArtwork();
        UpdateProgressVisuals();
    }

    public void SetExternalStatusMessage(string message)
    {
        SetStatus(message);
    }

    public void AbortChallengeForFailure()
    {
        StopResultRoutine();
        IsActive = false;
        resolvingAttempt = false;
        RestoreTimeScale();
        SetPanelVisible(false);
    }

    void HandlePrecisionSuccess()
    {
        resolvingAttempt = true;
        SuccessfulNeedles =
            Mathf.Min(
                Mathf.Max(1, requiredSuccessfulNeedles),
                SuccessfulNeedles + 1);
        UpdateProgressVisuals();
        onNeedlePlaced?.Invoke();

        if (SuccessfulNeedles >= Mathf.Max(1, requiredSuccessfulNeedles))
        {
            IsCompleted = true;
            SetStatus(completionMessage);
            onChallengeCompleted?.Invoke();
            resultRoutine = StartCoroutine(FinishAfterDelay());
            return;
        }

        SetStatus(precisionSuccessMessage);
        resultRoutine = StartCoroutine(AdvanceAfterDelay());
    }

    void HandlePrecisionFailure()
    {
        resolvingAttempt = true;
        if (resetProgressOnFailure)
        {
            SuccessfulNeedles = 0;
            UpdateProgressVisuals();
        }

        SetStatus(precisionFailureMessage);
        onPrecisionFailed?.Invoke();
        if (!IsActive)
        {
            return;
        }

        resultRoutine = StartCoroutine(ResumeAfterDelay());
    }

    IEnumerator AdvanceAfterDelay()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, resultPauseDuration));
        resultRoutine = null;
        resolvingAttempt = false;
        pointerAngle =
            randomizeStartingAngle
                ? UnityEngine.Random.Range(0f, 360f)
                : 0f;
        SetStatus(instructionMessage);
        UpdateTargetVisual();
        UpdatePointerVisual();
    }

    IEnumerator ResumeAfterDelay()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, resultPauseDuration));
        resultRoutine = null;
        resolvingAttempt = false;
        SetStatus(instructionMessage);
    }

    IEnumerator FinishAfterDelay()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, completionDisplayDuration));
        resultRoutine = null;
        IsActive = false;
        resolvingAttempt = false;
        RestoreTimeScale();
        SetPanelVisible(false);
    }

    PrecisionRound GetCurrentRound()
    {
        if (rounds == null || rounds.Length == 0)
        {
            return null;
        }

        int index = Mathf.Clamp(SuccessfulNeedles, 0, rounds.Length - 1);
        return rounds[index];
    }

    void UpdatePointerVisual()
    {
        if (pointer != null)
        {
            pointer.localRotation = Quaternion.Euler(0f, 0f, -pointerAngle);
        }
    }

    void UpdateTargetVisual()
    {
        if (targetZoneImage == null)
        {
            return;
        }

        PrecisionRound round = GetCurrentRound();
        float targetAngle = round != null ? round.targetAngle : 0f;
        float precisionWindow =
            Mathf.Clamp(
                round != null ? round.precisionWindow : 14f,
                1f,
                180f);

        targetZoneImage.type = Image.Type.Filled;
        targetZoneImage.fillMethod = Image.FillMethod.Radial360;
        targetZoneImage.fillOrigin = (int)Image.Origin360.Top;
        targetZoneImage.fillClockwise = true;
        targetZoneImage.fillAmount = precisionWindow / 360f;
        targetZoneImage.rectTransform.localRotation =
            Quaternion.Euler(
                0f,
                0f,
                -(targetAngle - precisionWindow * 0.5f));
    }

    void UpdateProgressVisuals()
    {
        int required = Mathf.Max(1, requiredSuccessfulNeedles);
        float progress = Mathf.Clamp01((float)SuccessfulNeedles / required);

        if (progressFillImage != null)
        {
            progressFillImage.type = Image.Type.Filled;
            progressFillImage.fillMethod = Image.FillMethod.Horizontal;
            progressFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            progressFillImage.fillAmount = progress;
        }

        if (progressText != null)
        {
            progressText.text = SuccessfulNeedles + " / " + required;
        }

        if (placedNeedleImages == null)
        {
            return;
        }

        for (int index = 0; index < placedNeedleImages.Length; index++)
        {
            Image placedNeedle = placedNeedleImages[index];
            if (placedNeedle == null)
            {
                continue;
            }

            if (needleSprite != null)
            {
                placedNeedle.sprite = needleSprite;
                placedNeedle.preserveAspect = true;
            }

            placedNeedle.gameObject.SetActive(index < SuccessfulNeedles);
        }
    }

    void ConfigureArtwork()
    {
        if (pointerImage != null)
        {
            pointerImage.sprite = needleSprite;
            pointerImage.preserveAspect = needleSprite != null;
        }

        if (dollImage != null && dollSprite != null)
        {
            dollImage.sprite = dollSprite;
            dollImage.preserveAspect = true;
        }
    }

    void ConfigureFonts()
    {
        Text[] texts = { progressText, statusText, instructionText };
        if (runtimeFont == null)
        {
            string[] preferredFonts =
            {
                "Microsoft YaHei UI",
                "Microsoft YaHei",
                "PingFang SC",
                "Noto Sans CJK SC",
                "Source Han Sans SC",
                "SimHei",
                "Arial Unicode MS",
            };
            runtimeFont = Font.CreateDynamicFontFromOSFont(preferredFonts, 32);
        }

        if (runtimeFont == null)
        {
            return;
        }

        foreach (Text challengeText in texts)
        {
            if (challengeText != null)
            {
                challengeText.font = runtimeFont;
                ConfigureResponsiveText(challengeText, 16);
            }
        }
    }

    static void ConfigureResponsiveText(Text text, int minimumFontSize)
    {
        int maximumFontSize =
            Mathf.Max(minimumFontSize, text.fontSize);
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = Mathf.Max(1, minimumFontSize);
        text.resizeTextMaxSize = maximumFontSize;
        text.lineSpacing = 0.9f;
    }

    void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message ?? string.Empty;
        }

        if (instructionText != null)
        {
            instructionText.text = instructionMessage ?? string.Empty;
        }
    }

    void SetPanelVisible(bool visible)
    {
        if (challengePanel != null && challengePanel.activeSelf != visible)
        {
            challengePanel.SetActive(visible);
        }
    }

    void RestoreTimeScale()
    {
        if (pauseGameDuringChallenge)
        {
            Time.timeScale = previousTimeScale;
        }
    }

    void StopResultRoutine()
    {
        if (resultRoutine == null)
        {
            return;
        }

        StopCoroutine(resultRoutine);
        resultRoutine = null;
    }

    bool WasCalibrationPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null
            && keyboard[calibrationKey].wasPressedThisFrame)
        {
            return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(legacyCalibrationKey))
        {
            return true;
        }
#endif
        return false;
    }

    bool WasCancelPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null
            && keyboard[cancelKey].wasPressedThisFrame)
        {
            return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(legacyCancelKey))
        {
            return true;
        }
#endif
        return false;
    }
}
