using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class HuajiaoMovement : MonoBehaviour
{
    const string FailureRangeName = "Huajiao Failure Range";

    public float moveSpeed = 0.5f;
    public float startDelaySeconds = 30f;
    public float startOutsideLeftPadding = 0.5f;
    public Vector2 failureRangeSize = new Vector2(1.2f, 2.8f);
    public float failureRangeGap = 0.1f;
    public float failureRangeOffsetY = 0f;
    public bool startOutsideCameraLeft = true;
    public string playerObjectName = BubuRunningGame.PlayerRootName;
    public HuajiaoFailurePresentation2D failurePresentation;
    public bool disableWhenPlayerLeavesFirstLevel = true;
    public Transform firstLevelEndMarker;
    public float firstLevelEndPadding = 0.15f;
    [Tooltip(
        "Keep the chase timer and movement running while puzzle UI "
        + "sets Time.timeScale to zero.")]
    public bool continueWhilePuzzlePaused = true;

    SpriteRenderer visualRenderer;
    BoxCollider2D failureRangeCollider;
    Collider2D playerCollider;
    Transform playerTransform;
    float lockedY;
    float lockedZ;
    float elapsedSeconds;
    bool restarting;
    bool pursuitStarted;
    bool pursuitStopped;

    public bool PursuitStarted => pursuitStarted;
    public bool PursuitStopped => pursuitStopped;
    public float ElapsedSeconds => elapsedSeconds;

    void Awake()
    {
        lockedY = transform.position.y;
        lockedZ = transform.position.z;
        CacheVisualRenderer();
        EnsureFailureRange();
        RefreshFailureRange();
    }

    void Start()
    {
        if (startOutsideCameraLeft)
        {
            MoveOutsideCameraLeft();
        }

        RefreshFailureRange();
    }

    void Update()
    {
        if (restarting)
        {
            return;
        }

        if (HasPlayerLeftFirstLevel())
        {
            StopForSecondLevel();
            return;
        }

        float pursuitDeltaTime =
            continueWhilePuzzlePaused
                ? Time.unscaledDeltaTime
                : Time.deltaTime;
        elapsedSeconds += pursuitDeltaTime;
        if (elapsedSeconds < startDelaySeconds)
        {
            RefreshFailureRange();
            return;
        }

        pursuitStarted = true;
        MoveRightOnly(pursuitDeltaTime);
        RefreshFailureRange();
        CheckPlayerFailureRange();
    }

    void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        startDelaySeconds = Mathf.Max(0f, startDelaySeconds);
        startOutsideLeftPadding = Mathf.Max(0f, startOutsideLeftPadding);
        failureRangeSize = new Vector2(Mathf.Max(0.01f, failureRangeSize.x), Mathf.Max(0.01f, failureRangeSize.y));
        failureRangeGap = Mathf.Max(0f, failureRangeGap);
        firstLevelEndPadding = Mathf.Max(0f, firstLevelEndPadding);

        Transform existingRange = transform.Find(FailureRangeName);
        if (existingRange != null && existingRange.TryGetComponent(out BoxCollider2D rangeCollider))
        {
            failureRangeCollider = rangeCollider;
            RefreshFailureRange();
        }
    }

    void OnDrawGizmosSelected()
    {
        Bounds rangeBounds = GetFailureRangeWorldBounds();
        if (rangeBounds.size == Vector3.zero)
        {
            return;
        }

        Gizmos.color = new Color(1f, 0.15f, 0.1f, 1f);
        Gizmos.DrawWireCube(rangeBounds.center, rangeBounds.size);
    }

    void MoveRightOnly(float deltaTime)
    {
        Vector3 position = transform.position;
        position.x += moveSpeed * Mathf.Max(0f, deltaTime);
        position.y = lockedY;
        position.z = lockedZ;
        transform.position = position;
    }

    void MoveOutsideCameraLeft()
    {
        Camera gameplayCamera = Camera.main;
        if (gameplayCamera == null)
        {
            return;
        }

        float depth = Mathf.Abs(gameplayCamera.transform.position.z - transform.position.z);
        float leftEdge = gameplayCamera.ViewportToWorldPoint(new Vector3(0f, 0.5f, depth)).x;
        float rightOffset = GetVisualRightOffset();

        Vector3 position = transform.position;
        position.x = leftEdge - startOutsideLeftPadding - rightOffset;
        position.y = lockedY;
        position.z = lockedZ;
        transform.position = position;
    }

    void EnsureFailureRange()
    {
        Transform range = transform.Find(FailureRangeName);
        if (range == null)
        {
            GameObject rangeObject = new GameObject(FailureRangeName);
            rangeObject.transform.SetParent(transform, false);
            range = rangeObject.transform;
        }

        failureRangeCollider = range.GetComponent<BoxCollider2D>();
        if (failureRangeCollider == null)
        {
            failureRangeCollider = range.gameObject.AddComponent<BoxCollider2D>();
        }

        failureRangeCollider.isTrigger = true;
    }

    void RefreshFailureRange()
    {
        if (failureRangeCollider == null)
        {
            return;
        }

        float rightEdge = GetVisualRightEdgeLocal();
        Transform range = failureRangeCollider.transform;
        range.localRotation = Quaternion.identity;
        range.localScale = Vector3.one;
        range.localPosition = new Vector3(
            rightEdge + failureRangeGap + failureRangeSize.x * 0.5f,
            failureRangeOffsetY,
            0f);

        failureRangeCollider.offset = Vector2.zero;
        failureRangeCollider.size = failureRangeSize;
    }

    void CheckPlayerFailureRange()
    {
        if (failureRangeCollider == null)
        {
            return;
        }

        Collider2D player = GetPlayerCollider();
        if (player == null)
        {
            return;
        }

        if (failureRangeCollider.bounds.Intersects(player.bounds))
        {
            TriggerPlayerCaught();
        }
    }

    public void TriggerPlayerCaught()
    {
        if (restarting)
        {
            return;
        }

        restarting = true;
        if (failurePresentation == null)
        {
            failurePresentation =
                FindAnyObjectByType<HuajiaoFailurePresentation2D>(
                    FindObjectsInactive.Include);
        }

        if (failurePresentation != null)
        {
            failurePresentation.ShowFailure();
            return;
        }

        RestartSceneAsFallback();
    }

    public void StopForSecondLevel()
    {
        if (pursuitStopped)
        {
            return;
        }

        pursuitStopped = true;
        gameObject.SetActive(false);
    }

    void RestartSceneAsFallback()
    {
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

    Collider2D GetPlayerCollider()
    {
        if (playerCollider != null)
        {
            return playerCollider;
        }

        Transform player = GetPlayerTransform();
        if (player != null)
        {
            playerCollider = player.GetComponentInChildren<Collider2D>();
        }

        return playerCollider;
    }

    Transform GetPlayerTransform()
    {
        if (playerTransform != null)
        {
            return playerTransform;
        }

        GameObject player = GameObject.Find(playerObjectName);
        if (player != null)
        {
            playerTransform = player.transform;
        }

        return playerTransform;
    }

    bool HasPlayerLeftFirstLevel()
    {
        if (!disableWhenPlayerLeavesFirstLevel
            || firstLevelEndMarker == null)
        {
            return false;
        }

        Transform player = GetPlayerTransform();
        if (player == null)
        {
            return false;
        }

        float endX = firstLevelEndMarker.position.x;
        Collider2D endCollider =
            firstLevelEndMarker.GetComponent<Collider2D>();
        if (endCollider != null && endCollider.bounds.size.x > 0f)
        {
            endX = endCollider.bounds.max.x;
        }

        return player.position.x
            > endX + Mathf.Max(0f, firstLevelEndPadding);
    }

    void CacheVisualRenderer()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();
        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer != null && renderer.transform.name != FailureRangeName)
            {
                visualRenderer = renderer;
                return;
            }
        }
    }

    float GetVisualRightOffset()
    {
        CacheVisualRenderer();
        if (visualRenderer == null || visualRenderer.sprite == null)
        {
            return 0.5f;
        }

        return visualRenderer.bounds.max.x - transform.position.x;
    }

    float GetVisualRightEdgeLocal()
    {
        CacheVisualRenderer();
        if (visualRenderer == null || visualRenderer.sprite == null)
        {
            return 0.5f;
        }

        Vector3 localRight = transform.InverseTransformPoint(visualRenderer.bounds.max);
        return localRight.x;
    }

    Bounds GetFailureRangeWorldBounds()
    {
        Transform range = transform.Find(FailureRangeName);
        if (range != null && range.TryGetComponent(out BoxCollider2D rangeCollider))
        {
            return rangeCollider.bounds;
        }

        return new Bounds(Vector3.zero, Vector3.zero);
    }
}
