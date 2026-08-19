using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class LevelSegmentTransition2D : MonoBehaviour
{
    public Transform player;
    public Collider2D playerCollider;
    public Camera gameplayCamera;
    public Transform backgroundRoot;
    public EdgeScrollCamera2D cameraController;
    public SecondLevelCameraPreview2D secondLevelCameraPreview;
    public DoorProgressGate2D firstLevelDoorGate;
    public Collider2D firstLevelDoorCollider;
    public Transform firstLevelStartPoint;
    public Transform secondLevelStartPoint;
    public GameObject fadeScreenRoot;
    public Image fadeImage;
    public GameObject[] objectsEnabledInSecondLevel = new GameObject[0];
    public MonoBehaviour[] behavioursEnabledInSecondLevel = new MonoBehaviour[0];
    public bool disableSecondLevelContentOnStart = true;
    public bool startInOriginalSecondLevel;
    public bool requireDoorSolved = true;
    public bool transitionWhenPlayerPassesDoor = true;
    public bool freezeTimeDuringTransition = true;
    public bool unlockCameraForSecondLevel = true;
    public bool avoidSoldierSpawnOverlap = true;
    public bool waitUntilPlayerFullyPassesDoor = true;
    public bool respawnAtDestinationLeftEdge = true;
    [Min(0f)]
    public float soldierSpawnSafetyPadding = 0.35f;
    [Min(0f)]
    public float destinationLeftEdgePadding = 0.15f;
    public float triggerPadding = 0.05f;
    public float fadeOutDuration = 0.65f;
    public float holdBlackDuration = 0.2f;
    public float fadeInDuration = 0.65f;

    bool transitionStarted;
    bool transitionComplete;
    float previousTimeScale = 1f;

    void Awake()
    {
        CacheReferences();
        if (disableSecondLevelContentOnStart)
        {
            SetSecondLevelContentActive(false);
        }

        SetFadeAlpha(0f);
        SetFadeVisible(false);
    }

    IEnumerator Start()
    {
        CacheReferences();
        if (!startInOriginalSecondLevel)
        {
            yield break;
        }

        // BubuRunningGame configures the runtime player and camera in Start.
        // Waiting one frame makes the reversed spawn deterministic regardless
        // of Unity's component Start ordering.
        yield return null;
        CacheReferences();
        MovePlayerToSecondLevel();
    }

    void Update()
    {
        if (transitionStarted || transitionComplete || !transitionWhenPlayerPassesDoor)
        {
            return;
        }

        CacheReferences();
        if (ShouldTransition())
        {
            TriggerTransition();
        }
    }

    public void TriggerTransition()
    {
        if (transitionStarted
            || transitionComplete
            || !ShouldTransition())
        {
            return;
        }

        StartCoroutine(RunTransition());
    }

    bool ShouldTransition()
    {
        if (player == null)
        {
            return false;
        }

        if (requireDoorSolved
            && (firstLevelDoorGate == null
                || !firstLevelDoorGate.puzzleSolved))
        {
            return false;
        }

        float doorRightEdge = GetDoorRightEdgeX();
        float playerEdge =
            waitUntilPlayerFullyPassesDoor
                ? GetPlayerLeftEdgeX()
                : GetPlayerRightEdgeX();
        return playerEdge > doorRightEdge + Mathf.Max(0f, triggerPadding);
    }

    IEnumerator RunTransition()
    {
        transitionStarted = true;
        previousTimeScale = Time.timeScale;

        if (freezeTimeDuringTransition)
        {
            Time.timeScale = 0f;
        }

        SetFadeVisible(true);
        yield return Fade(0f, 1f, fadeOutDuration);

        if (startInOriginalSecondLevel)
        {
            MovePlayerToFirstLevel();
        }
        else
        {
            MovePlayerToSecondLevel();
        }

        ActivateSecondLevelContent();

        if (holdBlackDuration > 0f)
        {
            yield return WaitUnscaled(holdBlackDuration);
        }

        yield return Fade(1f, 0f, fadeInDuration);
        SetFadeVisible(false);

        if (freezeTimeDuringTransition)
        {
            Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
        }

        if (secondLevelCameraPreview != null)
        {
            yield return secondLevelCameraPreview.PlayPreviewOnce();
        }

        transitionComplete = true;
        transitionStarted = false;
    }

    IEnumerator Fade(float fromAlpha, float toAlpha, float duration)
    {
        if (duration <= 0f)
        {
            SetFadeAlpha(toAlpha);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetFadeAlpha(Mathf.Lerp(fromAlpha, toAlpha, t));
            yield return null;
        }

        SetFadeAlpha(toAlpha);
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

    void MovePlayerToSecondLevel()
    {
        if (player == null || secondLevelStartPoint == null)
        {
            return;
        }

        Vector3 targetPosition = GetDestinationSpawnPosition(
            secondLevelStartPoint.position);
        targetPosition = GetSafeSecondLevelStartPosition(targetPosition);
        targetPosition = ClampToDestinationBounds(targetPosition);
        EnsurePlayerActive();
        Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
        if (playerBody != null)
        {
            playerBody.linearVelocity = Vector2.zero;
            playerBody.position = new Vector2(targetPosition.x, targetPosition.y);
        }

        player.position = new Vector3(targetPosition.x, targetPosition.y, player.position.z);
        Physics2D.SyncTransforms();

        if (unlockCameraForSecondLevel && cameraController != null)
        {
            if (startInOriginalSecondLevel)
            {
                cameraController.ConfigureStartingSecondLevelFollow(
                    GetOriginalSecondLevelBackground());
            }
            else
            {
                cameraController.UnlockForSecondLevel();
            }
        }

        SnapCameraToPlayer();
    }

    void MovePlayerToFirstLevel()
    {
        if (player == null || firstLevelStartPoint == null)
        {
            return;
        }

        Vector3 targetPosition = GetDestinationSpawnPosition(
            firstLevelStartPoint.position);
        targetPosition = GetSafeSecondLevelStartPosition(targetPosition);
        targetPosition = ClampToDestinationBounds(targetPosition);
        EnsurePlayerActive();
        Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
        if (playerBody != null)
        {
            playerBody.linearVelocity = Vector2.zero;
            playerBody.position = new Vector2(targetPosition.x, targetPosition.y);
        }

        player.position =
            new Vector3(targetPosition.x, targetPosition.y, player.position.z);
        Physics2D.SyncTransforms();

        if (cameraController != null)
        {
            cameraController.ConfigureFirstLevelMouseCamera();
        }

        if (secondLevelCameraPreview != null
            && cameraController != null
            && cameraController.firstLevelBackground != null)
        {
            secondLevelCameraPreview.secondLevelBackground =
                cameraController.firstLevelBackground;
        }

        SnapCameraToPlayer();
    }

    public Transform GetOriginalSecondLevelBackground()
    {
        return secondLevelCameraPreview != null
            ? secondLevelCameraPreview.secondLevelBackground
            : null;
    }

    public float GetMinimumPlayerCenterX(float defaultMinimumX)
    {
        if (!startInOriginalSecondLevel
            || transitionStarted
            || transitionComplete)
        {
            return defaultMinimumX;
        }

        Transform secondLevelBackground = GetOriginalSecondLevelBackground();
        Bounds bounds;
        if (secondLevelBackground == null
            || !TryGetRendererBounds(secondLevelBackground, out bounds))
        {
            return defaultMinimumX;
        }

        float playerHalfWidth = BubuRunningGame.PlayerWidth * 0.5f;
        if (playerCollider != null && playerCollider.enabled)
        {
            playerHalfWidth = Mathf.Max(
                playerHalfWidth,
                playerCollider.bounds.extents.x);
        }

        return Mathf.Max(defaultMinimumX, bounds.min.x + playerHalfWidth);
    }

    Vector3 GetDestinationSpawnPosition(Vector3 fallbackPosition)
    {
        if (!respawnAtDestinationLeftEdge)
        {
            return fallbackPosition;
        }

        Bounds destinationBounds;
        if (!TryGetDestinationLevelBounds(out destinationBounds))
        {
            return fallbackPosition;
        }

        Vector2 playerHalfExtents = GetPlayerHalfExtents();
        float minimumCenterX =
            destinationBounds.min.x
            + playerHalfExtents.x
            + Mathf.Max(0f, destinationLeftEdgePadding);
        float maximumCenterX = destinationBounds.max.x - playerHalfExtents.x;
        fallbackPosition.x =
            minimumCenterX <= maximumCenterX
                ? minimumCenterX
                : destinationBounds.center.x;

        float minimumCenterY = destinationBounds.min.y + playerHalfExtents.y;
        float maximumCenterY = destinationBounds.max.y - playerHalfExtents.y;
        fallbackPosition.y =
            minimumCenterY <= maximumCenterY
                ? Mathf.Clamp(
                    fallbackPosition.y,
                    minimumCenterY,
                    maximumCenterY)
                : destinationBounds.center.y;
        return fallbackPosition;
    }

    Vector3 ClampToDestinationBounds(Vector3 position)
    {
        Bounds destinationBounds;
        if (!TryGetDestinationLevelBounds(out destinationBounds))
        {
            return position;
        }

        Vector2 playerHalfExtents = GetPlayerHalfExtents();
        float minimumCenterX = destinationBounds.min.x + playerHalfExtents.x;
        float maximumCenterX = destinationBounds.max.x - playerHalfExtents.x;
        float minimumCenterY = destinationBounds.min.y + playerHalfExtents.y;
        float maximumCenterY = destinationBounds.max.y - playerHalfExtents.y;

        position.x =
            minimumCenterX <= maximumCenterX
                ? Mathf.Clamp(position.x, minimumCenterX, maximumCenterX)
                : destinationBounds.center.x;
        position.y =
            minimumCenterY <= maximumCenterY
                ? Mathf.Clamp(position.y, minimumCenterY, maximumCenterY)
                : destinationBounds.center.y;
        return position;
    }

    bool TryGetDestinationLevelBounds(out Bounds bounds)
    {
        if (startInOriginalSecondLevel)
        {
            if (cameraController != null
                && cameraController.TryGetCombinedSecondLevelBounds(out bounds))
            {
                return true;
            }

            if (cameraController != null
                && cameraController.firstLevelBackground != null
                && TryGetRendererBounds(
                    cameraController.firstLevelBackground,
                    out bounds))
            {
                return true;
            }
        }
        else
        {
            Transform secondLevelBackground = GetOriginalSecondLevelBackground();
            if (secondLevelBackground != null
                && TryGetRendererBounds(secondLevelBackground, out bounds))
            {
                return true;
            }
        }

        bounds = new Bounds();
        return false;
    }

    Vector2 GetPlayerHalfExtents()
    {
        float halfWidth = BubuRunningGame.PlayerWidth * 0.5f;
        float halfHeight = BubuRunningGame.PlayerHeight * 0.5f;
        if (playerCollider != null && playerCollider.enabled)
        {
            Bounds playerBounds = playerCollider.bounds;
            halfWidth = Mathf.Max(halfWidth, playerBounds.extents.x);
            halfHeight = Mathf.Max(halfHeight, playerBounds.extents.y);
        }

        return new Vector2(halfWidth, halfHeight);
    }

    void EnsurePlayerActive()
    {
        if (player != null && !player.gameObject.activeSelf)
        {
            player.gameObject.SetActive(true);
        }
    }

    Vector3 GetSafeSecondLevelStartPosition(Vector3 requestedPosition)
    {
        if (!avoidSoldierSpawnOverlap)
        {
            return requestedPosition;
        }

        float playerHalfWidth = BubuRunningGame.PlayerWidth * 0.5f;
        float playerHalfHeight = BubuRunningGame.PlayerHeight * 0.5f;
        if (playerCollider != null && playerCollider.enabled)
        {
            Bounds playerBounds = playerCollider.bounds;
            if (playerBounds.extents.x > 0f)
            {
                playerHalfWidth = playerBounds.extents.x;
            }

            if (playerBounds.extents.y > 0f)
            {
                playerHalfHeight = playerBounds.extents.y;
            }
        }

        float safeX = requestedPosition.x;
        PatrollingSoldier2D[] soldiers =
            FindObjectsByType<PatrollingSoldier2D>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        foreach (PatrollingSoldier2D soldier in soldiers)
        {
            if (soldier == null)
            {
                continue;
            }

            BoxCollider2D bodyCollider = soldier.GetComponent<BoxCollider2D>();
            Vector2 bodySize =
                bodyCollider != null ? bodyCollider.size : Vector2.one;
            Vector2 bodyOffset =
                bodyCollider != null ? bodyCollider.offset : Vector2.zero;
            Vector3 scale = soldier.transform.lossyScale;
            float scaleX = Mathf.Abs(scale.x);
            float scaleY = Mathf.Abs(scale.y);

            float failureHalfWidth =
                (bodySize.x + soldier.failureRangePadding.x * 2f)
                * scaleX
                * 0.5f;
            float failureHalfHeight =
                (bodySize.y + soldier.failureRangePadding.y * 2f)
                * scaleY
                * 0.5f;

            float startingOffsetY =
                soldier.startMovingUp
                    ? Mathf.Min(
                        soldier.patrolLowestOffsetY,
                        soldier.patrolHighestOffsetY)
                    : Mathf.Max(
                        soldier.patrolLowestOffsetY,
                        soldier.patrolHighestOffsetY);
            float failureCenterX =
                soldier.transform.position.x
                + (bodyOffset.x + soldier.failureRangeOffset.x) * scaleX;
            float failureCenterY =
                soldier.transform.position.y
                + startingOffsetY
                + (bodyOffset.y + soldier.failureRangeOffset.y) * scaleY;

            float verticalSafeDistance =
                failureHalfHeight
                + playerHalfHeight
                + Mathf.Max(0f, soldierSpawnSafetyPadding);
            if (Mathf.Abs(requestedPosition.y - failureCenterY)
                > verticalSafeDistance)
            {
                continue;
            }

            float horizontalSafeDistance =
                failureHalfWidth
                + playerHalfWidth
                + Mathf.Max(0f, soldierSpawnSafetyPadding);
            float safeLeftX = failureCenterX - horizontalSafeDistance;
            float safeRightX = failureCenterX + horizontalSafeDistance;
            if (safeX > safeLeftX && safeX < safeRightX)
            {
                safeX = safeLeftX;
            }
        }

        if (!Mathf.Approximately(safeX, requestedPosition.x))
        {
            Debug.Log(
                "Adjusted second-level player spawn X from "
                + requestedPosition.x.ToString("0.##")
                + " to "
                + safeX.ToString("0.##")
                + " to avoid a patrolling soldier failure range.");
            requestedPosition.x = safeX;
        }

        return requestedPosition;
    }

    void ActivateSecondLevelContent()
    {
        SetSecondLevelContentActive(true);
    }

    void SetSecondLevelContentActive(bool active)
    {
        foreach (GameObject targetObject in objectsEnabledInSecondLevel)
        {
            if (targetObject != null)
            {
                targetObject.SetActive(active);
            }
        }

        foreach (MonoBehaviour behaviour in behavioursEnabledInSecondLevel)
        {
            if (behaviour != null)
            {
                behaviour.enabled = active;
            }
        }
    }

    void SnapCameraToPlayer()
    {
        if (cameraController != null)
        {
            cameraController.SnapToPlayer();
            return;
        }

        if (gameplayCamera == null || player == null)
        {
            return;
        }

        Vector3 position = gameplayCamera.transform.position;
        position.x = GetClampedCameraX(player.position.x);
        gameplayCamera.transform.position = position;
    }

    float GetClampedCameraX(float targetX)
    {
        Bounds bounds = GetBackgroundBounds();
        if (gameplayCamera == null || bounds.size.x <= 0f)
        {
            return targetX;
        }

        float halfWidth = gameplayCamera.orthographicSize * gameplayCamera.aspect;
        float minX = bounds.min.x + halfWidth;
        float maxX = bounds.max.x - halfWidth;

        if (minX > maxX)
        {
            return bounds.center.x;
        }

        return Mathf.Clamp(targetX, minX, maxX);
    }

    void SetFadeVisible(bool visible)
    {
        if (fadeScreenRoot != null)
        {
            fadeScreenRoot.SetActive(visible);
        }
    }

    void SetFadeAlpha(float alpha)
    {
        if (fadeImage == null)
        {
            return;
        }

        Color color = fadeImage.color;
        color.a = Mathf.Clamp01(alpha);
        fadeImage.color = color;
    }

    void CacheReferences()
    {
        if (gameplayCamera == null)
        {
            gameplayCamera = Camera.main;
        }

        if (cameraController == null && gameplayCamera != null)
        {
            cameraController = gameplayCamera.GetComponent<EdgeScrollCamera2D>();
        }

        if (secondLevelCameraPreview == null && gameplayCamera != null)
        {
            secondLevelCameraPreview =
                gameplayCamera.GetComponent<SecondLevelCameraPreview2D>();
        }

        if (playerCollider == null && player != null)
        {
            playerCollider = player.GetComponentInChildren<Collider2D>();
        }
    }

    float GetPlayerRightEdgeX()
    {
        if (playerCollider != null && playerCollider.enabled)
        {
            return playerCollider.bounds.max.x;
        }

        return player != null ? player.position.x : 0f;
    }

    float GetPlayerLeftEdgeX()
    {
        if (playerCollider != null && playerCollider.enabled)
        {
            return playerCollider.bounds.min.x;
        }

        return player != null ? player.position.x : 0f;
    }

    float GetDoorRightEdgeX()
    {
        if (firstLevelDoorCollider != null && firstLevelDoorCollider.enabled && firstLevelDoorCollider.bounds.size.x > 0f)
        {
            return firstLevelDoorCollider.bounds.max.x;
        }

        Bounds rendererBounds;
        if (firstLevelDoorGate != null && TryGetRendererBounds(firstLevelDoorGate.transform, out rendererBounds))
        {
            return rendererBounds.max.x;
        }

        return firstLevelDoorGate != null ? firstLevelDoorGate.transform.position.x : 0f;
    }

    Bounds GetBackgroundBounds()
    {
        if (backgroundRoot == null)
        {
            return new Bounds(Vector3.zero, Vector3.zero);
        }

        SpriteRenderer[] renderers = backgroundRoot.GetComponentsInChildren<SpriteRenderer>();
        bool hasBounds = false;
        Bounds bounds = new Bounds(backgroundRoot.position, Vector3.zero);

        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer == null || renderer.sprite == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return bounds;
    }

    static bool TryGetRendererBounds(Transform root, out Bounds bounds)
    {
        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        bool hasBounds = false;
        bounds = new Bounds(root.position, Vector3.zero);

        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer == null || renderer.sprite == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }
}
