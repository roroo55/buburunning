using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class SecondLevelCameraPreview2D : MonoBehaviour
{
    [Header("References")]
    public Camera gameplayCamera;
    public EdgeScrollCamera2D cameraController;
    public Transform player;
    public Transform secondLevelBackground;
    public GameplayMessageUI2D messageUI;

    [Header("Preview Timing")]
    [Min(0f)]
    public float leftHoldDuration = 0.35f;
    [Min(0f)]
    public float moveToRightDuration = 2.5f;
    [Min(0f)]
    public float rightHoldDuration = 0.75f;
    [Min(0f)]
    public float returnToPlayerDuration = 1.75f;
    public AnimationCurve movementCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Tutorial")]
    [TextArea]
    public string instructionText =
        "Move the camera with the mouse cursor.";
    public bool freezeTimeDuringPreview = true;
    public bool showInstructionAfterPreview = true;
    public bool logPreviewState = true;

    public bool HasPlayed => hasPlayed;
    public bool IsPlaying => previewRunning;

    bool hasPlayed;
    bool previewRunning;
    bool cameraControllerWasEnabled;
    bool timeScaleWasChanged;
    float previousTimeScale = 1f;

    void Awake()
    {
        CacheReferences();
    }

    public void StartPreviewOnce()
    {
        if (!hasPlayed && !previewRunning)
        {
            StartCoroutine(PlayPreviewOnce());
        }
    }

    public IEnumerator PlayPreviewOnce()
    {
        if (hasPlayed || previewRunning)
        {
            yield break;
        }

        CacheReferences();
        if (!CanPlayPreview())
        {
            Debug.LogWarning(
                "Second-level camera preview could not start because a required reference is missing.");
            yield break;
        }

        hasPlayed = true;
        previewRunning = true;
        TakeCameraControl();

        if (messageUI != null && !string.IsNullOrWhiteSpace(instructionText))
        {
            messageUI.ShowPersistentMessage(instructionText);
        }

        float leftCameraX;
        float rightCameraX;
        GetSecondLevelCameraRange(out leftCameraX, out rightCameraX);
        SetCameraX(leftCameraX);

        if (logPreviewState)
        {
            Debug.Log(
                "Second-level camera preview started at left X "
                + leftCameraX.ToString("0.##")
                + " and will move to right X "
                + rightCameraX.ToString("0.##")
                + ".");
        }

        yield return WaitUnscaled(leftHoldDuration);
        yield return MoveCameraX(leftCameraX, rightCameraX, moveToRightDuration);
        yield return WaitUnscaled(rightHoldDuration);

        float playerCameraX = GetPlayerCameraX(leftCameraX, rightCameraX);
        yield return MoveCameraX(
            rightCameraX,
            playerCameraX,
            returnToPlayerDuration);
        SetCameraX(playerCameraX);

        RestoreCameraControl();
        previewRunning = false;

        if (messageUI != null)
        {
            if (showInstructionAfterPreview
                && !string.IsNullOrWhiteSpace(instructionText))
            {
                messageUI.ShowMessage(instructionText);
            }
            else
            {
                messageUI.HideMessage();
            }
        }

        if (logPreviewState)
        {
            Debug.Log(
                "Second-level camera preview completed at player camera X "
                + playerCameraX.ToString("0.##")
                + "; mouse edge camera control restored.");
        }
    }

    IEnumerator MoveCameraX(float fromX, float toX, float duration)
    {
        if (duration <= 0f || Mathf.Approximately(fromX, toX))
        {
            SetCameraX(toX);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            float curvedTime =
                movementCurve != null
                    ? movementCurve.Evaluate(normalizedTime)
                    : normalizedTime;
            SetCameraX(Mathf.LerpUnclamped(fromX, toX, curvedTime));
            yield return null;
        }

        SetCameraX(toX);
    }

    IEnumerator WaitUnscaled(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    void TakeCameraControl()
    {
        cameraControllerWasEnabled =
            cameraController != null && cameraController.enabled;
        if (cameraController != null)
        {
            cameraController.enabled = false;
        }

        timeScaleWasChanged = freezeTimeDuringPreview;
        if (timeScaleWasChanged)
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }
    }

    void RestoreCameraControl()
    {
        if (timeScaleWasChanged)
        {
            Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
            timeScaleWasChanged = false;
        }

        if (cameraController != null)
        {
            cameraController.enabled = cameraControllerWasEnabled;
        }
    }

    void OnDisable()
    {
        if (!previewRunning)
        {
            return;
        }

        RestoreCameraControl();
        previewRunning = false;
    }

    void CacheReferences()
    {
        if (gameplayCamera == null)
        {
            gameplayCamera = GetComponent<Camera>();
        }

        if (cameraController == null)
        {
            cameraController = GetComponent<EdgeScrollCamera2D>();
        }

        if (player == null)
        {
            GameObject playerObject = GameObject.Find(BubuRunningGame.PlayerRootName);
            if (playerObject != null)
            {
                player = playerObject.transform;
            }
        }

        if (secondLevelBackground == null)
        {
            GameObject backgroundObject = GameObject.Find("level2Background");
            if (backgroundObject != null)
            {
                secondLevelBackground = backgroundObject.transform;
            }
        }

        if (messageUI == null)
        {
            messageUI =
                FindAnyObjectByType<GameplayMessageUI2D>(
                    FindObjectsInactive.Include);
        }
    }

    bool CanPlayPreview()
    {
        return gameplayCamera != null
            && player != null
            && secondLevelBackground != null;
    }

    void GetSecondLevelCameraRange(out float leftCameraX, out float rightCameraX)
    {
        Bounds bounds = new Bounds();
        bool hasBounds =
            cameraController != null
            && cameraController.TryGetCombinedSecondLevelBounds(out bounds);
        if (!hasBounds
            && !TryGetRendererBounds(secondLevelBackground, out bounds))
        {
            leftCameraX = transform.position.x;
            rightCameraX = transform.position.x;
            return;
        }

        float halfWidth =
            gameplayCamera.orthographicSize * gameplayCamera.aspect;
        if (bounds.size.x <= halfWidth * 2f)
        {
            leftCameraX = bounds.center.x;
            rightCameraX = bounds.center.x;
            return;
        }

        leftCameraX = bounds.min.x + halfWidth;
        rightCameraX = bounds.max.x - halfWidth;
    }

    float GetPlayerCameraX(float leftCameraX, float rightCameraX)
    {
        return Mathf.Clamp(player.position.x, leftCameraX, rightCameraX);
    }

    void SetCameraX(float x)
    {
        Vector3 position = transform.position;
        position.x = x;
        transform.position = position;
    }

    static bool TryGetRendererBounds(Transform root, out Bounds bounds)
    {
        SpriteRenderer[] renderers =
            root.GetComponentsInChildren<SpriteRenderer>(true);
        bool hasBounds = false;
        bounds = new Bounds(root.position, Vector3.zero);

        foreach (SpriteRenderer spriteRenderer in renderers)
        {
            if (spriteRenderer == null || spriteRenderer.sprite == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = spriteRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(spriteRenderer.bounds);
            }
        }

        return hasBounds;
    }
}
