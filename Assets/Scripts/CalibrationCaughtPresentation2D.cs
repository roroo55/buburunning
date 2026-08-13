using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class CalibrationCaughtPresentation2D : MonoBehaviour
{
    [Header("Presentation")]
    public GameObject presentationRoot;
    public RawImage cgDisplay;
    public VideoPlayer videoPlayer;
    public VideoClip caughtCg;

    [Header("Optional Audio")]
    public AudioSource optionalBgmSource;
    public AudioClip optionalBgm;
    public bool loopOptionalBgm = true;

    [Header("Flow")]
    public bool loopCg;
    public bool pauseGameplay = true;
    public bool pauseOtherAudio = true;
    public bool returnToStartOnMouseClick = true;
    [Min(0f)]
    public float mouseClickDelay = 0.25f;

    [Header("Events")]
    public UnityEvent onPresentationStarted = new UnityEvent();
    public UnityEvent onReturnToStart = new UnityEvent();

    RenderTexture runtimeTexture;
    float shownAtUnscaledTime;
    bool returningToStart;

    public bool IsShowing { get; private set; }
    public bool IsVideoPrepared { get; private set; }
    public long LastPresentedFrame { get; private set; } = -1;
    public string LastVideoError { get; private set; }

    void Awake()
    {
        SetPresentationVisible(false);
        ConfigurePlayers();
    }

    void OnEnable()
    {
        SubscribeVideoEvents();
    }

    void OnDisable()
    {
        UnsubscribeVideoEvents();
    }

    void Update()
    {
        if (!IsShowing
            || returningToStart
            || !returnToStartOnMouseClick
            || Time.unscaledTime - shownAtUnscaledTime < mouseClickDelay)
        {
            return;
        }

        if (WasPrimaryMouseButtonPressed())
        {
            ReturnToStartScreen();
        }
    }

    void OnDestroy()
    {
        ReleaseRuntimeTexture();
    }

    public void ShowCaught()
    {
        if (IsShowing)
        {
            return;
        }

        IsShowing = true;
        returningToStart = false;
        IsVideoPrepared = false;
        LastPresentedFrame = -1;
        LastVideoError = string.Empty;
        shownAtUnscaledTime = Time.unscaledTime;

        ConfigurePlayers();
        PrepareVideoTexture();
        SetPresentationVisible(true);

        if (pauseGameplay)
        {
            Time.timeScale = 0f;
        }

        if (pauseOtherAudio)
        {
            AudioListener.pause = true;
        }

        if (videoPlayer != null && caughtCg != null)
        {
            videoPlayer.Stop();
            videoPlayer.clip = caughtCg;
            videoPlayer.isLooping = loopCg;
            videoPlayer.Prepare();
        }

        if (optionalBgmSource != null && optionalBgm != null)
        {
            optionalBgmSource.clip = optionalBgm;
            optionalBgmSource.loop = loopOptionalBgm;
            optionalBgmSource.ignoreListenerPause = true;
            optionalBgmSource.Play();
        }

        onPresentationStarted?.Invoke();
    }

    public void StopPresentation()
    {
        videoPlayer?.Stop();
        optionalBgmSource?.Stop();
        IsShowing = false;
        SetPresentationVisible(false);
    }

    public void ReturnToStartScreen()
    {
        if (returningToStart)
        {
            return;
        }

        returningToStart = true;
        onReturnToStart?.Invoke();
        StopPresentation();
        Time.timeScale = 1f;
        AudioListener.pause = false;

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

    void ConfigurePlayers()
    {
        if (videoPlayer != null)
        {
            videoPlayer.playOnAwake = false;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            videoPlayer.aspectRatio = VideoAspectRatio.FitInside;
            videoPlayer.isLooping = loopCg;
            videoPlayer.timeUpdateMode =
                VideoTimeUpdateMode.UnscaledGameTime;
            videoPlayer.waitForFirstFrame = true;
            videoPlayer.sendFrameReadyEvents = true;
        }

        if (optionalBgmSource != null)
        {
            optionalBgmSource.playOnAwake = false;
            optionalBgmSource.loop = loopOptionalBgm;
            optionalBgmSource.spatialBlend = 0f;
            optionalBgmSource.ignoreListenerPause = true;
        }
    }

    void PrepareVideoTexture()
    {
        if (videoPlayer == null || cgDisplay == null)
        {
            return;
        }

        int width = Mathf.Max(640, Screen.width);
        int height = Mathf.Max(360, Screen.height);
        if (runtimeTexture == null
            || runtimeTexture.width != width
            || runtimeTexture.height != height)
        {
            ReleaseRuntimeTexture();
            runtimeTexture =
                new RenderTexture(
                    width,
                    height,
                    0,
                    RenderTextureFormat.ARGB32);
            runtimeTexture.name =
                "Calibration Caught CG Runtime Texture";
            runtimeTexture.Create();
        }

        videoPlayer.targetTexture = runtimeTexture;
        cgDisplay.texture = runtimeTexture;
        cgDisplay.color = Color.white;
        ClearRuntimeTexture();
    }

    void ClearRuntimeTexture()
    {
        if (runtimeTexture == null)
        {
            return;
        }

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = runtimeTexture;
        GL.Clear(true, true, Color.black);
        RenderTexture.active = previous;
    }

    void ReleaseRuntimeTexture()
    {
        if (videoPlayer != null
            && videoPlayer.targetTexture == runtimeTexture)
        {
            videoPlayer.targetTexture = null;
        }

        if (cgDisplay != null && cgDisplay.texture == runtimeTexture)
        {
            cgDisplay.texture = null;
        }

        if (runtimeTexture == null)
        {
            return;
        }

        runtimeTexture.Release();
        Destroy(runtimeTexture);
        runtimeTexture = null;
    }

    void SubscribeVideoEvents()
    {
        if (videoPlayer == null)
        {
            return;
        }

        UnsubscribeVideoEvents();
        videoPlayer.prepareCompleted += HandleVideoPrepared;
        videoPlayer.frameReady += HandleVideoFrameReady;
        videoPlayer.errorReceived += HandleVideoError;
    }

    void UnsubscribeVideoEvents()
    {
        if (videoPlayer == null)
        {
            return;
        }

        videoPlayer.prepareCompleted -= HandleVideoPrepared;
        videoPlayer.frameReady -= HandleVideoFrameReady;
        videoPlayer.errorReceived -= HandleVideoError;
    }

    void HandleVideoPrepared(VideoPlayer preparedPlayer)
    {
        IsVideoPrepared = true;
        if (IsShowing && preparedPlayer != null)
        {
            preparedPlayer.Play();
        }
    }

    void HandleVideoFrameReady(VideoPlayer source, long frameIndex)
    {
        LastPresentedFrame = frameIndex;
    }

    void HandleVideoError(VideoPlayer source, string message)
    {
        LastVideoError =
            message ?? "Unknown caught CG playback error.";
        Debug.LogError(
            "Calibration caught CG playback failed: "
            + LastVideoError,
            this);
    }

    void SetPresentationVisible(bool visible)
    {
        if (presentationRoot != null
            && presentationRoot.activeSelf != visible)
        {
            presentationRoot.SetActive(visible);
        }
    }

    static bool WasPrimaryMouseButtonPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null
            && Mouse.current.leftButton.wasPressedThisFrame)
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButtonDown(0);
#else
        return false;
#endif
    }
}
