using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class BubuRunningGame : MonoBehaviour
{
    public const string PlayerRootName = "Bubu Player";
    public const string PlayerVisualName = "Bubu Visual";
    public const string LegacyPlayerRootName = "bubu";
    public const string SoldierGroupName = "Patrolling Soldiers";
    public const string SoldierRootName = "Patrolling Soldier";
    public const string LegacySoldierRootName = "Patrolling Soldier Placeholder";
    public const string BackgroundGroupName = "Background Group";
    public const string Background1Name = "background1";
    public const string BackgroundName = "Background";
    public const float PlayerSpeed = 7f;
    public const float SoldierPatrolSpeed = 2.4f;
    public const float PlayerWidth = 0.8f;
    public const float PlayerHeight = 1.8f;
    public const float SoldierWidth = 0.8f;
    public const float SoldierHeight = 0.8f;

    const float GameplayDepth = 0f;
    const float BackgroundDepth = 1f;

    [SerializeField] Sprite background1Sprite;
    [SerializeField] Sprite backgroundSprite;
    [SerializeField] Sprite playerSprite;

    Transform backgroundRoot;
    Transform player;
    Rigidbody2D playerBody;
    TemporaryMovementSpeedModifier2D playerSpeedModifier;
    Vector2 playerInput;
    PatrollingSoldier2D[] soldiers = new PatrollingSoldier2D[0];
    Camera gameplayCamera;
    EdgeScrollCamera2D edgeScrollCamera;
    LevelSegmentTransition2D levelTransition;
    Rect backgroundBounds;
    Rect playBounds;
    float fixedCameraY;
    bool restarting;

    void Start()
    {
        SetupCamera();
        CreateBackground();
        CreateSimpleTestLevel();
        ConfigureCameraController();
    }

    void Update()
    {
        if (restarting)
        {
            return;
        }

        ReadPlayerMovement();
        CheckLossConditions();
    }

    void FixedUpdate()
    {
        if (restarting)
        {
            return;
        }

        MovePlayerWithPhysics();
    }

    void SetupCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            mainCamera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
        }

        mainCamera.orthographic = true;
        mainCamera.orthographicSize = 4.5f;
        mainCamera.clearFlags = CameraClearFlags.SolidColor;
        mainCamera.backgroundColor = Color.black;
        mainCamera.transform.position = new Vector3(0f, 0f, -10f);
        gameplayCamera = mainCamera;
        edgeScrollCamera = mainCamera.GetComponent<EdgeScrollCamera2D>();
        if (edgeScrollCamera == null)
        {
            edgeScrollCamera = mainCamera.gameObject.AddComponent<EdgeScrollCamera2D>();
        }

        fixedCameraY = 0f;
    }

    void CreateBackground()
    {
        backgroundRoot = EnsureBackgroundGroup();
        FitCameraHeightToBackground();
        CacheLevelBounds();
    }

    Transform EnsureBackgroundGroup()
    {
        GameObject group = FindSceneObject(BackgroundGroupName);
        if (group == null)
        {
            group = new GameObject(BackgroundGroupName);
        }

        group.name = BackgroundGroupName;
        group.transform.SetParent(null);
        group.transform.position = Vector3.zero;
        group.transform.localScale = Vector3.one;

        SpriteRenderer background1 = EnsureBackgroundSegment(group.transform, Background1Name, background1Sprite, -101);
        SpriteRenderer background = EnsureBackgroundSegment(group.transform, BackgroundName, backgroundSprite, -100);
        float cursorX = 0f;
        cursorX = PositionBackgroundSegment(background1, cursorX);
        PositionBackgroundSegment(background, cursorX);

        return group.transform;
    }

    SpriteRenderer EnsureBackgroundSegment(Transform group, string segmentName, Sprite fallbackSprite, int sortingOrder)
    {
        GameObject segment = FindSceneObject(segmentName);
        if (segment == null)
        {
            segment = new GameObject(segmentName);
        }

        segment.name = segmentName;
        segment.transform.SetParent(group, true);
        segment.transform.localScale = Vector3.one;

        SpriteRenderer renderer = segment.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = segment.AddComponent<SpriteRenderer>();
        }

        if (renderer.sprite == null)
        {
            renderer.sprite = fallbackSprite != null
                ? fallbackSprite
                : Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        }

        renderer.color = renderer.sprite == null ? new Color(0.08f, 0.08f, 0.08f) : Color.white;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    float PositionBackgroundSegment(SpriteRenderer renderer, float minX)
    {
        if (renderer == null || renderer.sprite == null)
        {
            return minX;
        }

        Bounds spriteBounds = renderer.sprite.bounds;
        renderer.transform.position = new Vector3(
            minX - spriteBounds.min.x,
            fixedCameraY - spriteBounds.center.y,
            BackgroundDepth);

        return minX + spriteBounds.size.x;
    }

    void FitCameraHeightToBackground()
    {
        Bounds bounds = GetBackgroundBounds();
        if (bounds.size.y <= 0f)
        {
            return;
        }

        fixedCameraY = bounds.center.y;
        gameplayCamera.orthographicSize = bounds.size.y * 0.5f;

        Vector3 cameraPosition = gameplayCamera.transform.position;
        cameraPosition.y = fixedCameraY;
        gameplayCamera.transform.position = cameraPosition;
    }

    void CacheLevelBounds()
    {
        Bounds bounds = GetBackgroundBounds();
        backgroundBounds = new Rect(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y);

        float halfPlayerWidth = PlayerWidth * 0.5f;
        float halfPlayerHeight = PlayerHeight * 0.5f;
        float visibleBottom = fixedCameraY - gameplayCamera.orthographicSize + halfPlayerHeight;
        float visibleTop = fixedCameraY + gameplayCamera.orthographicSize - halfPlayerHeight;
        float minY = Mathf.Max(backgroundBounds.yMin + halfPlayerHeight, visibleBottom);
        float maxY = Mathf.Min(backgroundBounds.yMax - halfPlayerHeight, visibleTop);

        if (maxY < minY)
        {
            minY = fixedCameraY;
            maxY = fixedCameraY;
        }

        playBounds = Rect.MinMaxRect(
            backgroundBounds.xMin + halfPlayerWidth,
            minY,
            backgroundBounds.xMax - halfPlayerWidth,
            maxY);
    }

    void CreateSimpleTestLevel()
    {
        Vector2 playerStart = GetPlayerStartPosition();
        player = EnsureBubuPlayer(playerStart, 10);
        playerSpeedModifier = player.GetComponent<TemporaryMovementSpeedModifier2D>();
        EnsureExistingSoldiersConfigured();
    }

    Vector2 GetPlayerStartPosition()
    {
        CacheLevelTransition();
        if (levelTransition != null
            && levelTransition.startInOriginalSecondLevel
            && levelTransition.secondLevelStartPoint != null)
        {
            return levelTransition.secondLevelStartPoint.position;
        }

        float startX = playBounds.xMin;
        float startY = Mathf.Clamp(backgroundBounds.center.y, playBounds.yMin, playBounds.yMax);
        return new Vector2(startX, startY);
    }

    void ConfigureCameraController()
    {
        if (edgeScrollCamera == null || player == null || backgroundRoot == null)
        {
            return;
        }

        edgeScrollCamera.Configure(player, backgroundRoot);
        CacheLevelTransition();
        if (levelTransition != null && levelTransition.startInOriginalSecondLevel)
        {
            edgeScrollCamera.ConfigureStartingSecondLevelFollow(
                levelTransition.GetOriginalSecondLevelBackground());
            edgeScrollCamera.SnapToPlayer();
        }
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

    Transform FindExistingSoldierObject()
    {
        GameObject existingSoldier = GameObject.Find(SoldierRootName);
        if (existingSoldier != null)
        {
            return existingSoldier.transform;
        }

        existingSoldier = GameObject.Find(LegacySoldierRootName);
        if (existingSoldier != null)
        {
            existingSoldier.name = SoldierRootName;
            return existingSoldier.transform;
        }

        return null;
    }

    void EnsureExistingSoldiersConfigured()
    {
        Transform legacySoldier = FindExistingSoldierObject();
        if (legacySoldier != null)
        {
            ConfigurePatrollingSoldier(legacySoldier.gameObject, 11);
        }

        soldiers = FindObjectsByType<PatrollingSoldier2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (PatrollingSoldier2D patrol in soldiers)
        {
            if (patrol != null)
            {
                ConfigurePatrollingSoldier(patrol.gameObject, 11);
            }
        }
    }

    void ConfigurePatrollingSoldier(GameObject soldierObject, int sortingOrder)
    {
        SpriteRenderer renderer = soldierObject.GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = sortingOrder;
        }

        BoxCollider2D collider = soldierObject.GetComponent<BoxCollider2D>();
        if (collider == null)
        {
            collider = soldierObject.AddComponent<BoxCollider2D>();
        }

        collider.isTrigger = false;
        collider.offset = Vector2.zero;
        collider.size = Vector2.one;

        if (soldierObject.GetComponent<PatrollingSoldier2D>() == null)
        {
            soldierObject.AddComponent<PatrollingSoldier2D>();
        }
    }

    Transform EnsureBubuPlayer(Vector2 position, int sortingOrder)
    {
        GameObject root = FindExistingPlayerObject();
        if (root == null)
        {
            return CreateBubuPlayer(position, sortingOrder);
        }

        root.name = PlayerRootName;
        root.transform.position = new Vector3(position.x, position.y, GameplayDepth);
        root.transform.localScale = Vector3.one;
        ConfigureBubuPlayerVisual(root, sortingOrder);
        ConfigureBubuPlayerPhysics(root);
        return root.transform;
    }

    GameObject FindExistingPlayerObject()
    {
        GameObject existingPlayer = GameObject.Find(PlayerRootName);
        if (existingPlayer != null)
        {
            return existingPlayer;
        }

        return GameObject.Find(LegacyPlayerRootName);
    }

    Transform CreateBubuPlayer(Vector2 position, int sortingOrder)
    {
        GameObject root = new GameObject(PlayerRootName);
        root.transform.position = new Vector3(position.x, position.y, GameplayDepth);
        ConfigureBubuPlayerVisual(root, sortingOrder);
        ConfigureBubuPlayerPhysics(root);
        return root.transform;
    }

    void ConfigureBubuPlayerPhysics(GameObject root)
    {
        playerBody = root.GetComponent<Rigidbody2D>();
        if (playerBody == null)
        {
            playerBody = root.AddComponent<Rigidbody2D>();
        }

        playerBody.bodyType = RigidbodyType2D.Dynamic;
        playerBody.simulated = true;
        playerBody.gravityScale = 0f;
        playerBody.constraints = RigidbodyConstraints2D.FreezeRotation;
        playerBody.interpolation = RigidbodyInterpolation2D.Interpolate;
        playerBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    void ConfigureBubuPlayerVisual(GameObject root, int sortingOrder)
    {
        SpriteRenderer rootRenderer = root.GetComponent<SpriteRenderer>();
        if (rootRenderer != null)
        {
            rootRenderer.enabled = false;
            Destroy(rootRenderer);
        }

        BoxCollider2D rootCollider = root.GetComponent<BoxCollider2D>();
        if (rootCollider != null)
        {
            rootCollider.enabled = false;
            Destroy(rootCollider);
        }

        Transform existingVisual = root.transform.Find(PlayerVisualName);
        GameObject visual = existingVisual != null ? existingVisual.gameObject : new GameObject(PlayerVisualName);
        visual.transform.SetParent(root.transform, false);
        SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = visual.AddComponent<SpriteRenderer>();
        }

        renderer.sprite = playerSprite != null
            ? playerSprite
            : Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        renderer.color = playerSprite != null ? Color.white : new Color(0.95f, 0.9f, 0.75f);
        renderer.sortingOrder = sortingOrder;

        BoxCollider2D collider = visual.GetComponent<BoxCollider2D>();
        if (collider == null)
        {
            collider = visual.AddComponent<BoxCollider2D>();
        }

        collider.isTrigger = false;
        collider.size = renderer.sprite.bounds.size;
        collider.offset = renderer.sprite.bounds.center;

        float spriteHeight = Mathf.Max(renderer.sprite.bounds.size.y, 0.001f);
        float visualScale = PlayerHeight / spriteHeight;
        visual.transform.localScale = Vector3.one * visualScale;
        visual.transform.localPosition = -renderer.sprite.bounds.center * visualScale;
    }

    void ReadPlayerMovement()
    {
        playerInput = ReadWasdInput();
    }

    void MovePlayerWithPhysics()
    {
        if (playerBody == null)
        {
            return;
        }

        float speedMultiplier = GetPlayerSpeedMultiplier();
        Vector2 nextPosition =
            playerBody.position
            + playerInput * PlayerSpeed * speedMultiplier * Time.fixedDeltaTime;
        nextPosition = ClampPlayerMovementPosition(nextPosition);
        playerBody.MovePosition(nextPosition);
    }

    float GetPlayerSpeedMultiplier()
    {
        if (playerSpeedModifier == null && player != null)
        {
            playerSpeedModifier = player.GetComponent<TemporaryMovementSpeedModifier2D>();
        }

        return playerSpeedModifier != null
            ? playerSpeedModifier.CurrentSpeedMultiplier
            : 1f;
    }

    Vector2 ClampPlayerMovementPosition(Vector2 targetPosition)
    {
        CacheLevelTransition();
        float minX = playBounds.xMin;
        if (levelTransition != null)
        {
            minX = levelTransition.GetMinimumPlayerCenterX(minX);
        }

        float maxX = playBounds.xMax;

        targetPosition.x = Mathf.Clamp(targetPosition.x, minX, maxX);
        targetPosition.y = Mathf.Clamp(targetPosition.y, playBounds.yMin, playBounds.yMax);

        return targetPosition;
    }

    void CacheLevelTransition()
    {
        if (levelTransition == null)
        {
            levelTransition =
                FindAnyObjectByType<LevelSegmentTransition2D>(
                    FindObjectsInactive.Include);
        }
    }

    Vector2 ReadWasdInput()
    {
        bool w = false;
        bool a = false;
        bool s = false;
        bool d = false;

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            w = keyboard.wKey.isPressed;
            a = keyboard.aKey.isPressed;
            s = keyboard.sKey.isPressed;
            d = keyboard.dKey.isPressed;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        w = w || Input.GetKey(KeyCode.W);
        a = a || Input.GetKey(KeyCode.A);
        s = s || Input.GetKey(KeyCode.S);
        d = d || Input.GetKey(KeyCode.D);
#endif

        return GetWasdInput(w, a, s, d);
    }

    void CheckLossConditions()
    {
        if (player == null)
        {
            return;
        }

        Collider2D playerCollider = player.GetComponentInChildren<Collider2D>();
        if (playerCollider == null)
        {
            return;
        }

        if (soldiers == null || soldiers.Length == 0)
        {
            soldiers = FindObjectsByType<PatrollingSoldier2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        }

        foreach (PatrollingSoldier2D patrol in soldiers)
        {
            if (patrol == null || !patrol.gameObject.activeInHierarchy)
            {
                continue;
            }

            Collider2D soldierCollider = patrol.GetComponent<Collider2D>();
            if (soldierCollider == null)
            {
                soldierCollider = patrol.GetComponentInChildren<Collider2D>();
            }

            if (soldierCollider != null && playerCollider.bounds.Intersects(soldierCollider.bounds))
            {
                RestartScene();
                return;
            }
        }
    }

    void RestartScene()
    {
        restarting = true;
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

    float GetCameraHalfWidth()
    {
        return gameplayCamera.orthographicSize * gameplayCamera.aspect;
    }

    public static Vector2 GetWasdInput(bool w, bool a, bool s, bool d)
    {
        float x = 0f;
        float y = 0f;

        if (a)
        {
            x -= 1f;
        }

        if (d)
        {
            x += 1f;
        }

        if (s)
        {
            y -= 1f;
        }

        if (w)
        {
            y += 1f;
        }

        Vector2 input = new Vector2(x, y);
        return input.sqrMagnitude > 1f ? input.normalized : input;
    }

    public static bool RectsTouch(Vector2 firstPosition, float firstWidth, float firstHeight, Vector2 secondPosition, float secondWidth, float secondHeight)
    {
        bool touchesX = Mathf.Abs(firstPosition.x - secondPosition.x) <= GetTouchWidth(firstWidth, secondWidth);
        bool touchesY = Mathf.Abs(firstPosition.y - secondPosition.y) <= GetTouchHeight(firstHeight, secondHeight);
        return touchesX && touchesY;
    }

    public static float GetTouchWidth(float firstWidth, float secondWidth)
    {
        return firstWidth * 0.5f + secondWidth * 0.5f;
    }

    public static float GetTouchHeight(float firstHeight, float secondHeight)
    {
        return firstHeight * 0.5f + secondHeight * 0.5f;
    }

    static Vector2 GetObjectTouchSize(Transform target, float fallbackWidth, float fallbackHeight)
    {
        Collider2D collider = target.GetComponentInChildren<Collider2D>();
        if (collider != null)
        {
            return collider.bounds.size;
        }

        return new Vector2(fallbackWidth, fallbackHeight);
    }

    static GameObject FindSceneObject(string objectName)
    {
        GameObject[] objects = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (GameObject sceneObject in objects)
        {
            if (sceneObject != null && sceneObject.name == objectName)
            {
                return sceneObject;
            }
        }

        return null;
    }
}
