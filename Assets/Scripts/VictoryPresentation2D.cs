using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class VictoryPresentation2D : MonoBehaviour
{
    [Header("Presentation")]
    public GameObject victoryRoot;
    public Image cgImage;
    public Sprite victoryCgSprite;
    public Sprite[] victoryCgSlides = new Sprite[0];
    [Min(0.1f)]
    public float secondsPerSlide = 3f;
    public bool loopCgSlides;
    public GameObject customCgPresentationRoot;

    [Header("Settlement UI")]
    public Text victoryTitle;
    public Text victorySummary;
    public Button returnToStartButton;
    public Button restartButton;

    [Header("Editable Text")]
    public string victoryTitleText = "ESCAPED";
    [TextArea]
    public string victorySummaryText = "GAME COMPLETE";

    [Header("Victory State")]
    public bool pauseGameOnVictory = true;
    public bool hideGameplayMessageOnVictory = true;
    public GameplayMessageUI2D gameplayMessageUI;
    public MonoBehaviour[] gameplayBehavioursToDisable =
        new MonoBehaviour[0];
    public bool allowKeyboardReturn;

    [Header("Events")]
    public UnityEvent onEscapeStarted = new UnityEvent();
    public UnityEvent onVictoryReached = new UnityEvent();
    public UnityEvent onVictoryPresentationStarted = new UnityEvent();
    public UnityEvent onReturnToStartRequested = new UnityEvent();
    public UnityEvent onRestartRequested = new UnityEvent();

    public bool HasWon { get; private set; }

    float previousTimeScale = 1f;
    Coroutine slideshowRoutine;
    Font runtimeFont;

    void Awake()
    {
        CacheReferences();
        ConfigureFonts();
        SetVictoryVisible(false);
    }

    void OnEnable()
    {
        if (returnToStartButton != null)
        {
            returnToStartButton.onClick.RemoveListener(ReturnToStart);
            returnToStartButton.onClick.AddListener(ReturnToStart);
        }

        if (restartButton != null)
        {
            restartButton.onClick.RemoveListener(RestartGame);
            restartButton.onClick.AddListener(RestartGame);
        }
    }

    void OnDisable()
    {
        if (returnToStartButton != null)
        {
            returnToStartButton.onClick.RemoveListener(ReturnToStart);
        }

        if (restartButton != null)
        {
            restartButton.onClick.RemoveListener(RestartGame);
        }

        StopSlideshow();
    }

    void Update()
    {
        if (!HasWon || !allowKeyboardReturn)
        {
            return;
        }

        if (WasReturnPressed())
        {
            ReturnToStart();
        }
    }

    public bool TriggerVictory()
    {
        if (HasWon)
        {
            return false;
        }

        HasWon = true;
        onEscapeStarted?.Invoke();
        onVictoryReached?.Invoke();

        if (hideGameplayMessageOnVictory)
        {
            gameplayMessageUI?.HideMessage();
        }

        DisableGameplayBehaviours();
        if (pauseGameOnVictory)
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        ConfigureFonts();
        if (victoryTitle != null)
        {
            victoryTitle.text = victoryTitleText ?? string.Empty;
        }

        if (victorySummary != null)
        {
            victorySummary.text = victorySummaryText ?? string.Empty;
        }

        SetVictoryVisible(true);
        StartCgPresentation();
        onVictoryPresentationStarted?.Invoke();
        return true;
    }

    public void ReturnToStart()
    {
        onReturnToStartRequested?.Invoke();
        ReloadActiveScene();
    }

    public void RestartGame()
    {
        onRestartRequested?.Invoke();
        ReloadActiveScene();
    }

    public void SetVictoryCgSprite(Sprite sprite)
    {
        victoryCgSprite = sprite;
        if (HasWon)
        {
            StartCgPresentation();
        }
    }

    void StartCgPresentation()
    {
        StopSlideshow();

        if (customCgPresentationRoot != null)
        {
            customCgPresentationRoot.SetActive(true);
        }

        if (cgImage == null)
        {
            return;
        }

        if (victoryCgSlides != null && victoryCgSlides.Length > 0)
        {
            cgImage.gameObject.SetActive(true);
            slideshowRoutine = StartCoroutine(PlaySlideshow());
            return;
        }

        cgImage.sprite = victoryCgSprite;
        cgImage.preserveAspect = true;
        cgImage.gameObject.SetActive(victoryCgSprite != null);
    }

    IEnumerator PlaySlideshow()
    {
        int index = 0;
        do
        {
            Sprite slide = victoryCgSlides[index];
            if (slide != null)
            {
                cgImage.sprite = slide;
                cgImage.preserveAspect = true;
            }

            yield return new WaitForSecondsRealtime(
                Mathf.Max(0.1f, secondsPerSlide));
            index++;
            if (index >= victoryCgSlides.Length)
            {
                if (!loopCgSlides)
                {
                    break;
                }

                index = 0;
            }
        }
        while (victoryCgSlides.Length > 0);

        slideshowRoutine = null;
    }

    void StopSlideshow()
    {
        if (slideshowRoutine == null)
        {
            return;
        }

        StopCoroutine(slideshowRoutine);
        slideshowRoutine = null;
    }

    void CacheReferences()
    {
        if (gameplayMessageUI == null)
        {
            gameplayMessageUI =
                FindAnyObjectByType<GameplayMessageUI2D>(
                    FindObjectsInactive.Include);
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
            runtimeFont = Font.CreateDynamicFontFromOSFont(preferredFonts, 40);
        }

        if (runtimeFont == null)
        {
            return;
        }

        if (victoryTitle != null)
        {
            victoryTitle.font = runtimeFont;
            ConfigureResponsiveText(victoryTitle, 24);
        }

        if (victorySummary != null)
        {
            victorySummary.font = runtimeFont;
            ConfigureResponsiveText(victorySummary, 20);
        }

        ConfigureButtonFont(returnToStartButton);
        ConfigureButtonFont(restartButton);
    }

    void ConfigureButtonFont(Button button)
    {
        if (button == null || runtimeFont == null)
        {
            return;
        }

        Text[] labels = button.GetComponentsInChildren<Text>(true);
        foreach (Text label in labels)
        {
            if (label != null)
            {
                label.font = runtimeFont;
                ConfigureResponsiveText(label, 14);
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

    void DisableGameplayBehaviours()
    {
        if (gameplayBehavioursToDisable == null)
        {
            return;
        }

        foreach (MonoBehaviour behaviour in gameplayBehavioursToDisable)
        {
            if (behaviour != null && behaviour != this)
            {
                behaviour.enabled = false;
            }
        }
    }

    void SetVictoryVisible(bool visible)
    {
        if (victoryRoot != null && victoryRoot.activeSelf != visible)
        {
            victoryRoot.SetActive(visible);
        }

        if (!visible && customCgPresentationRoot != null)
        {
            customCgPresentationRoot.SetActive(false);
        }
    }

    void ReloadActiveScene()
    {
        StopSlideshow();
        Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.buildIndex >= 0)
        {
            SceneManager.LoadScene(activeScene.buildIndex);
        }
        else
        {
            SceneManager.LoadScene(activeScene.name);
        }
    }

    static bool WasReturnPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            return keyboard.enterKey.wasPressedThisFrame
                || keyboard.numpadEnterKey.wasPressedThisFrame;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Return)
            || Input.GetKeyDown(KeyCode.KeypadEnter);
#else
        return false;
#endif
    }
}
