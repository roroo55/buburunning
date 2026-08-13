using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class ShoeCutMiniGame2D : MonoBehaviour
{
    [Header("References")]
    public GameObject challengePanel;
    public RectTransform scissorsRect;
    public RectTransform scissorsTip;
    public RectTransform[] cutCheckpoints = new RectTransform[0];
    public Image[] checkpointImages = new Image[0];
    public Image progressFill;
    public Text progressText;
    public Text statusText;
    public Text titleText;
    public Text instructionText;
    public Text closeButtonText;
    public Button closeButton;
    public DraggableScissorsUI2D scissorsDrag;
    public PenzaiSearchController2D inventory;
    public IncenseShoeCutInteraction2D interactionOwner;

    [Header("Cut Rules")]
    [Min(1f)]
    public float checkpointRadius = 48f;
    [Min(0f)]
    public float completionPauseDuration = 0.65f;
    public bool pauseGameDuringChallenge = true;
    public bool allowCancel = true;

    [Header("Reward")]
    public string rewardItemName = "key";
    public Sprite rewardIcon;
    [TextArea]
    public string rewardMessage =
        "You found a key inside the embroidered shoe.";

    [Header("Editable Text")]
    [TextArea]
    public string instructionMessage =
        "Drag the scissors through each marker on the embroidered shoe.";
    [TextArea]
    public string cuttingMessage = "Keep cutting along the markers.";
    [TextArea]
    public string completionMessage =
        "The embroidered shoe has been cut open.";

    [Header("Checkpoint Colors")]
    public Color pendingCheckpointColor =
        new Color(0.72f, 0.12f, 0.08f, 0.85f);
    public Color reachedCheckpointColor =
        new Color(0.9f, 0.66f, 0.18f, 1f);

    [Header("Events")]
    public UnityEvent onChallengeStarted = new UnityEvent();
    public UnityEvent onCheckpointReached = new UnityEvent();
    public UnityEvent onChallengeCompleted = new UnityEvent();
    public UnityEvent onChallengeCanceled = new UnityEvent();

    public bool IsActive { get; private set; }
    public bool IsCompleted { get; private set; }
    public int ReachedCheckpointCount { get; private set; }

    float previousTimeScale = 1f;
    bool scissorsIsDragging;
    Coroutine completionRoutine;
    Font runtimeFont;

    void Awake()
    {
        CacheReferences();
        ConfigureFonts();
        SetPanelVisible(false);
        ResetChallengeVisuals();
    }

    void OnEnable()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CancelChallenge);
            closeButton.onClick.AddListener(CancelChallenge);
        }
    }

    void OnDisable()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CancelChallenge);
        }
    }

    void Update()
    {
        if (IsActive && allowCancel && WasCancelPressed())
        {
            CancelChallenge();
        }
    }

    public bool StartChallenge()
    {
        if (IsActive || IsCompleted || challengePanel == null)
        {
            return false;
        }

        CacheReferences();
        ConfigureFonts();
        StopCompletionRoutine();
        IsActive = true;
        scissorsIsDragging = false;
        ReachedCheckpointCount = 0;
        if (pauseGameDuringChallenge)
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        ResetChallengeVisuals();
        SetStatus(instructionMessage);
        if (instructionText != null)
        {
            instructionText.text = instructionMessage;
        }

        SetPanelVisible(true);
        scissorsDrag?.ResetToStart();
        onChallengeStarted?.Invoke();
        return true;
    }

    public void NotifyScissorsDragStarted()
    {
        if (!IsActive || IsCompleted)
        {
            return;
        }

        scissorsIsDragging = true;
        SetStatus(cuttingMessage);
    }

    public void NotifyScissorsDragged()
    {
        if (!IsActive
            || IsCompleted
            || !scissorsIsDragging
            || scissorsTip == null
            || cutCheckpoints == null
            || ReachedCheckpointCount >= cutCheckpoints.Length)
        {
            return;
        }

        RectTransform checkpoint =
            cutCheckpoints[ReachedCheckpointCount];
        if (checkpoint == null)
        {
            ReachCurrentCheckpoint();
            return;
        }

        Canvas canvas = challengePanel.GetComponentInParent<Canvas>();
        float scaleFactor =
            canvas != null ? Mathf.Max(0.01f, canvas.scaleFactor) : 1f;
        float screenRadius =
            Mathf.Max(1f, checkpointRadius) * scaleFactor;
        if (Vector2.Distance(
                scissorsTip.position,
                checkpoint.position)
            <= screenRadius)
        {
            ReachCurrentCheckpoint();
        }
    }

    public void NotifyScissorsDragEnded()
    {
        scissorsIsDragging = false;
    }

    public void CancelChallenge()
    {
        if (!IsActive || IsCompleted)
        {
            return;
        }

        StopCompletionRoutine();
        IsActive = false;
        scissorsIsDragging = false;
        RestoreTimeScale();
        SetPanelVisible(false);
        onChallengeCanceled?.Invoke();
    }

    public void ResetChallenge()
    {
        StopCompletionRoutine();
        IsActive = false;
        IsCompleted = false;
        scissorsIsDragging = false;
        ReachedCheckpointCount = 0;
        RestoreTimeScale();
        SetPanelVisible(false);
        ResetChallengeVisuals();
        scissorsDrag?.ResetToStart();
    }

    void ReachCurrentCheckpoint()
    {
        if (ReachedCheckpointCount < 0)
        {
            ReachedCheckpointCount = 0;
        }

        if (checkpointImages != null
            && ReachedCheckpointCount < checkpointImages.Length
            && checkpointImages[ReachedCheckpointCount] != null)
        {
            checkpointImages[ReachedCheckpointCount].color =
                reachedCheckpointColor;
        }

        ReachedCheckpointCount++;
        UpdateProgress();
        onCheckpointReached?.Invoke();
        if (cutCheckpoints == null
            || ReachedCheckpointCount < cutCheckpoints.Length)
        {
            return;
        }

        IsCompleted = true;
        scissorsIsDragging = false;
        SetStatus(completionMessage);
        completionRoutine = StartCoroutine(FinishSuccessAfterDelay());
    }

    IEnumerator FinishSuccessAfterDelay()
    {
        yield return new WaitForSecondsRealtime(
            Mathf.Max(0f, completionPauseDuration));
        completionRoutine = null;
        IsActive = false;
        RestoreTimeScale();
        SetPanelVisible(false);
        interactionOwner?.NotifyShoeCutCompleted();
        inventory?.GrantItem(
            rewardItemName,
            rewardMessage,
            rewardIcon);
        onChallengeCompleted?.Invoke();
    }

    void ResetChallengeVisuals()
    {
        if (checkpointImages != null)
        {
            foreach (Image checkpointImage in checkpointImages)
            {
                if (checkpointImage != null)
                {
                    checkpointImage.color = pendingCheckpointColor;
                }
            }
        }

        UpdateProgress();
    }

    void UpdateProgress()
    {
        int total =
            cutCheckpoints != null
                ? Mathf.Max(1, cutCheckpoints.Length)
                : 1;
        float progress =
            Mathf.Clamp01((float)ReachedCheckpointCount / total);
        if (progressFill != null)
        {
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Horizontal;
            progressFill.fillOrigin =
                (int)Image.OriginHorizontal.Left;
            progressFill.fillAmount = progress;
        }

        if (progressText != null)
        {
            progressText.text =
                ReachedCheckpointCount + " / " + total;
        }
    }

    void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message ?? string.Empty;
        }
    }

    void ConfigureFonts()
    {
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
            runtimeFont =
                Font.CreateDynamicFontFromOSFont(preferredFonts, 32);
        }

        if (runtimeFont == null)
        {
            return;
        }

        Text[] texts =
        {
            progressText,
            statusText,
            titleText,
            instructionText,
            closeButtonText,
        };
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

    void CacheReferences()
    {
        if (inventory == null)
        {
            inventory =
                FindAnyObjectByType<PenzaiSearchController2D>(
                    FindObjectsInactive.Include);
        }

        if (interactionOwner == null)
        {
            interactionOwner =
                FindAnyObjectByType<IncenseShoeCutInteraction2D>(
                    FindObjectsInactive.Include);
        }
    }

    void SetPanelVisible(bool visible)
    {
        if (challengePanel != null
            && challengePanel.activeSelf != visible)
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

    void StopCompletionRoutine()
    {
        if (completionRoutine == null)
        {
            return;
        }

        StopCoroutine(completionRoutine);
        completionRoutine = null;
    }

    bool WasCancelPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null
            && keyboard.escapeKey.wasPressedThisFrame)
        {
            return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            return true;
        }
#endif
        return false;
    }
}
