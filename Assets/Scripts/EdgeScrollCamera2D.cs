using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class EdgeScrollCamera2D : MonoBehaviour
{
    public float cameraMovementSpeed = 6f;
    public float horizontalFollowThreshold = 2f;
    public float mouseEdgeThresholdPixels = 28f;
    public float mouseEdgeModeStartLeftEdgeX = 20f;
    public bool limitCameraUntilDoorPassed = true;
    public float lockedCameraMaxPositionX = 20f;
    public float doorPassedPadding = 0.05f;
    public bool limitToFirstLevelBackgroundUntilDoorPassed = true;
    public Transform firstLevelBackground;
    public bool limitCameraLeftEdge = true;
    public float maxCameraLeftEdgeX = 29.6f;
    public Transform player;
    public Transform background;
    public Transform door;

    Camera cameraComponent;
    float fixedY;
    bool doorPassed;

    void Awake()
    {
        Initialize();
        SnapToLeftEdge();
    }

    void LateUpdate()
    {
        if (background == null)
        {
            return;
        }

        Initialize();
        FitCameraHeightToBackground();
        UpdateDoorPassedState();
        MoveCameraByActiveEdgeMode();
    }

    public void Configure(Transform playerTransform, Transform backgroundTransform)
    {
        player = playerTransform;
        background = backgroundTransform;
        CacheFirstLevelBackground();
        Initialize();
        FitCameraHeightToBackground();
        SnapToLeftEdge();
    }

    public void SnapToLeftEdge()
    {
        if (background == null)
        {
            return;
        }

        Vector3 position = transform.position;
        position.x = GetLeftmostCameraX();
        position.y = fixedY;
        transform.position = position;
    }

    public void UnlockForSecondLevel()
    {
        doorPassed = true;
        limitCameraUntilDoorPassed = false;
        limitToFirstLevelBackgroundUntilDoorPassed = false;
        limitCameraLeftEdge = false;
    }

    public void SnapToPlayer()
    {
        if (player == null || background == null)
        {
            return;
        }

        Initialize();
        FitCameraHeightToBackground();

        Vector3 position = transform.position;
        position.x = ClampCameraXToBackground(player.position.x);
        position.y = fixedY;
        transform.position = position;
    }

    void UpdateDoorPassedState()
    {
        if (doorPassed || !limitCameraUntilDoorPassed)
        {
            return;
        }

        if (player == null)
        {
            return;
        }

        if (door == null)
        {
            GameObject doorObject = GameObject.Find("Door");
            if (doorObject != null)
            {
                door = doorObject.transform;
            }
        }

        if (door == null)
        {
            return;
        }

        float playerLeftEdgeX = GetPlayerLeftEdgeX();
        float doorRightEdgeX = GetDoorRightEdgeX();
        if (playerLeftEdgeX > doorRightEdgeX + Mathf.Max(0f, doorPassedPadding))
        {
            doorPassed = true;
        }
    }

    void Initialize()
    {
        if (cameraComponent == null)
        {
            cameraComponent = GetComponent<Camera>();
        }

        fixedY = transform.position.y;
        cameraComponent.orthographic = true;
    }

    void FitCameraHeightToBackground()
    {
        Bounds bounds = GetBackgroundBounds();
        if (bounds.size.y <= 0f)
        {
            return;
        }

        cameraComponent.orthographicSize = bounds.size.y * 0.5f;
        fixedY = bounds.center.y;
    }

    void MoveCameraByActiveEdgeMode()
    {
        float leftEdge = transform.position.x - GetCameraHalfWidth();
        if (leftEdge < mouseEdgeModeStartLeftEdgeX)
        {
            MoveWhenPlayerTouchesHorizontalEdge();
        }
        else
        {
            MoveWhenMouseTouchesHorizontalEdge();
        }
    }

    void MoveWhenPlayerTouchesHorizontalEdge()
    {
        if (player == null)
        {
            return;
        }

        float halfWidth = GetCameraHalfWidth();
        float worldThreshold = Mathf.Clamp(horizontalFollowThreshold, 0f, halfWidth);

        if (halfWidth <= 0f)
        {
            return;
        }

        float leftEdge = transform.position.x - halfWidth;
        float rightEdge = transform.position.x + halfWidth;
        float playerX = player.position.x;

        float direction = 0f;
        if (playerX <= leftEdge + worldThreshold)
        {
            direction = -1f;
        }
        else if (playerX >= rightEdge - worldThreshold)
        {
            direction = 1f;
        }

        if (Mathf.Approximately(direction, 0f))
        {
            return;
        }

        float targetX = transform.position.x + direction * cameraMovementSpeed * Time.deltaTime;
        targetX = ClampCameraXToBackground(targetX);

        Vector3 position = transform.position;
        position.x = targetX;
        position.y = fixedY;
        transform.position = position;
    }

    void MoveWhenMouseTouchesHorizontalEdge()
    {
        if (!TryGetMousePosition(out Vector2 mousePosition))
        {
            return;
        }

        float threshold = Mathf.Max(0f, mouseEdgeThresholdPixels);
        float direction = 0f;
        if (mousePosition.x <= threshold)
        {
            direction = -1f;
        }
        else if (mousePosition.x >= Screen.width - threshold)
        {
            direction = 1f;
        }

        if (Mathf.Approximately(direction, 0f))
        {
            return;
        }

        float targetX = transform.position.x + direction * cameraMovementSpeed * Time.deltaTime;
        targetX = ClampCameraXToBackground(targetX);

        Vector3 position = transform.position;
        position.x = targetX;
        position.y = fixedY;
        transform.position = position;
    }

    bool TryGetMousePosition(out Vector2 mousePosition)
    {
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Mouse.current != null)
        {
            mousePosition = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        mousePosition = Input.mousePosition;
        return true;
#else
        mousePosition = Vector2.zero;
        return false;
#endif
    }

    float GetLeftmostCameraX()
    {
        Bounds bounds = GetBackgroundBounds();
        float halfWidth = GetCameraHalfWidth();
        return bounds.size.x <= halfWidth * 2f ? bounds.center.x : bounds.min.x + halfWidth;
    }

    float ClampCameraXToBackground(float targetX)
    {
        Bounds bounds = GetBackgroundBounds();
        float halfWidth = GetCameraHalfWidth();
        float minX = bounds.min.x + halfWidth;
        float backgroundMaxX = bounds.max.x - halfWidth;
        float maxX = backgroundMaxX;

        if (doorPassed && door != null)
        {
            float secondLevelMinCameraX = GetDoorRightEdgeX() + halfWidth;
            minX = Mathf.Max(minX, secondLevelMinCameraX);
        }

        if (limitCameraLeftEdge)
        {
            float leftEdgeLimitMaxX = maxCameraLeftEdgeX + halfWidth;
            maxX = Mathf.Min(maxX, leftEdgeLimitMaxX);
        }

        if (limitToFirstLevelBackgroundUntilDoorPassed && !doorPassed && firstLevelBackground != null)
        {
            Bounds firstLevelBounds;
            if (TryGetRendererBounds(firstLevelBackground, out firstLevelBounds))
            {
                maxX = Mathf.Min(maxX, firstLevelBounds.max.x - halfWidth);
            }
        }

        if (limitCameraUntilDoorPassed && !doorPassed)
        {
            maxX = Mathf.Min(maxX, lockedCameraMaxPositionX);
        }

        if (minX > maxX)
        {
            return bounds.center.x;
        }

        return Mathf.Clamp(targetX, minX, maxX);
    }

    void CacheFirstLevelBackground()
    {
        if (firstLevelBackground != null || background == null)
        {
            return;
        }

        Transform firstLevel = background.Find(BubuRunningGame.Background1Name);
        if (firstLevel != null)
        {
            firstLevelBackground = firstLevel;
        }
    }

    Bounds GetBackgroundBounds()
    {
        SpriteRenderer[] backgroundRenderers = background.GetComponentsInChildren<SpriteRenderer>();
        bool hasBounds = false;
        Bounds bounds = new Bounds(background.position, Vector3.zero);

        foreach (SpriteRenderer backgroundRenderer in backgroundRenderers)
        {
            if (backgroundRenderer == null || backgroundRenderer.sprite == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = backgroundRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(backgroundRenderer.bounds);
            }
        }

        return bounds;
    }

    float GetPlayerLeftEdgeX()
    {
        Collider2D playerCollider = player.GetComponentInChildren<Collider2D>();
        if (playerCollider != null)
        {
            return playerCollider.bounds.min.x;
        }

        return player.position.x;
    }

    float GetDoorRightEdgeX()
    {
        Bounds doorBounds;
        if (TryGetRendererBounds(door, out doorBounds))
        {
            return doorBounds.max.x;
        }

        Collider2D doorCollider = door.GetComponentInChildren<Collider2D>();
        if (doorCollider != null)
        {
            return doorCollider.bounds.max.x;
        }

        return door.position.x;
    }

    bool TryGetRendererBounds(Transform root, out Bounds bounds)
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

    float GetCameraHalfWidth()
    {
        return cameraComponent.orthographicSize * cameraComponent.aspect;
    }
}
