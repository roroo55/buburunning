using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class MazeKeyDrag2D : MonoBehaviour
{
    const string ControlPointName = "Maze Key Control Point";
    const string KeyColliderName = "Maze Key 2D Collider";
    const string LockName = "lock";
    const string KeyWallRootName = "KeyWalls";
    const string MazeKeyLayerName = "MazeKey";
    const string MazeKeyWallLayerName = "MazeKeyWall";
    const float MinSqrMagnitude = 0.00000001f;

    public Camera worldCamera;
    public Rigidbody2D keyBody;
    public TargetJoint2D dragJoint;
    public Collider2D keyCollider;
    public Transform controlPoint;
    public Collider2D controlPointCollider;
    public Transform keyStartPoint;
    public Transform wallRoot;
    public Transform mazeBoundsRoot;
    public Collider2D mazeBoundsCollider;
    public Transform lockObject;
    public Collider2D lockCollider;
    public Collider2D exitCollider;
    public MazeExitZone2D exitZone;
    public MazePuzzleController mazePuzzleController;

    public bool constrainToMazeBounds = true;
    public Vector2 mazeBoundsPadding = Vector2.zero;
    public Vector2 mazeBoundsExtraReach = Vector2.zero;
    public float pickFallbackRadius = 0.35f;
    public float lockReachPadding = 0.05f;
    public bool useCollisionPointForLockCheck = true;
    public bool alsoCheckDragControlForLock = true;
    public float maxFollowSpeed = 5f;
    public float jointMaxForce = 80f;
    public float jointFrequency = 12f;
    public float jointDampingRatio = 1f;
    public float bodyLinearDamping = 1.5f;
    public bool stopImmediatelyOnRelease = true;
    public bool mergeWallCollidersAtRuntime = true;
    public bool useLowFrictionMaterial = true;
    public bool resetToStartOnEnable = true;
    public bool logSolved = true;
    public bool drawControlPointGizmo = true;

    Transform configuredWallRoot;
    CompositeCollider2D wallCompositeCollider;
    PhysicsMaterial2D smoothPhysicsMaterial;
    Transform materialWallRoot;
    Collider2D materialKeyCollider;
    Transform collisionLayerWallRoot;
    Transform cachedMazeBoundsRoot;
    Bounds cachedMazeBounds;
    bool mazeBoundsCacheInitialized;
    bool cachedMazeBoundsAvailable;
    bool dragging;
    bool solved;
    Vector2 dragTarget;

    public Collider2D DragControlCollider
    {
        get
        {
            CacheReferences();
            return GetPointerCollider();
        }
    }

    public Collider2D CollisionCollider
    {
        get
        {
            CacheReferences();
            return GetCollisionCollider();
        }
    }

    void Awake()
    {
        CacheReferences();
        ConfigureKeyPhysics();
    }

    void OnEnable()
    {
        solved = false;
        dragging = false;
        InvalidateMazeBoundsCache();
        CacheReferences();
        ConfigureKeyPhysics();
        DisableDragJoint();
        StopBodyMotion();

        if (resetToStartOnEnable)
        {
            ResetToStart();
        }
    }

    void OnDisable()
    {
        EndDrag(true);
    }

    void OnDestroy()
    {
        if (smoothPhysicsMaterial != null)
        {
            Destroy(smoothPhysicsMaterial);
        }
    }

    void Update()
    {
        if (solved)
        {
            return;
        }

        CacheReferences();

        if (WasPointerPressedThisFrame())
        {
            TryBeginDrag();
        }

        if (dragging && IsPointerHeld())
        {
            UpdateDragTarget();
            CheckExitReached();
        }

        if (dragging && WasPointerReleasedThisFrame())
        {
            EndDrag(stopImmediatelyOnRelease);
        }
    }

    void FixedUpdate()
    {
        if (solved || !dragging || keyBody == null || dragJoint == null)
        {
            return;
        }

        ApplyJointSettings();
        dragJoint.target = dragTarget;
        LimitBodySpeed();
    }

    public void ResetToStart()
    {
        CacheReferences();
        solved = false;
        EndDrag(true);

        if (keyStartPoint == null)
        {
            return;
        }

        Vector3 startPosition = keyStartPoint.position;
        startPosition.z = transform.position.z;
        if (keyBody != null)
        {
            keyBody.position = startPosition;
        }
        else
        {
            transform.position = startPosition;
        }

        Physics2D.SyncTransforms();
    }

    void TryBeginDrag()
    {
        Vector3 pointerWorld = GetPointerWorldPosition();
        if (!IsPointerOnKey(pointerWorld))
        {
            return;
        }

        ConfigureKeyPhysics();
        if (keyBody == null || dragJoint == null)
        {
            return;
        }

        dragging = true;
        dragJoint.anchor = transform.InverseTransformPoint(pointerWorld);
        dragTarget = ClampJointTargetToMazeBounds(pointerWorld);
        dragJoint.target = dragTarget;
        dragJoint.enabled = true;
        keyBody.WakeUp();
    }

    void UpdateDragTarget()
    {
        dragTarget = ClampJointTargetToMazeBounds(GetPointerWorldPosition());
    }

    void EndDrag(bool stopMotion)
    {
        dragging = false;
        DisableDragJoint();

        if (stopMotion)
        {
            StopBodyMotion();
        }
    }

    void DisableDragJoint()
    {
        if (dragJoint != null)
        {
            dragJoint.enabled = false;
        }
    }

    void StopBodyMotion()
    {
        if (keyBody == null)
        {
            return;
        }

        keyBody.linearVelocity = Vector2.zero;
        keyBody.angularVelocity = 0f;
    }

    void LimitBodySpeed()
    {
        maxFollowSpeed = Mathf.Max(0f, maxFollowSpeed);
        if (maxFollowSpeed <= 0f || keyBody.linearVelocity.sqrMagnitude <= maxFollowSpeed * maxFollowSpeed)
        {
            return;
        }

        keyBody.linearVelocity = keyBody.linearVelocity.normalized * maxFollowSpeed;
    }

    void ConfigureKeyPhysics()
    {
        RemoveConflicting3DPhysics();

        if (keyBody == null)
        {
            keyBody = GetComponent<Rigidbody2D>();
        }

        if (keyBody == null && Application.isPlaying)
        {
            keyBody = gameObject.AddComponent<Rigidbody2D>();
        }

        if (dragJoint == null)
        {
            dragJoint = GetComponent<TargetJoint2D>();
        }

        if (dragJoint == null && Application.isPlaying)
        {
            dragJoint = gameObject.AddComponent<TargetJoint2D>();
        }

        if (keyBody != null)
        {
            keyBody.bodyType = RigidbodyType2D.Dynamic;
            keyBody.simulated = true;
            keyBody.gravityScale = 0f;
            keyBody.mass = Mathf.Max(0.01f, keyBody.mass);
            keyBody.linearDamping = Mathf.Max(0f, bodyLinearDamping);
            keyBody.angularDamping = 0f;
            keyBody.freezeRotation = true;
            keyBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            keyBody.interpolation = RigidbodyInterpolation2D.Interpolate;
            keyBody.sleepMode = RigidbodySleepMode2D.NeverSleep;
        }

        ApplyJointSettings();
        ApplySmoothPhysicsMaterial();
    }

    void RemoveConflicting3DPhysics()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        Collider[] legacyColliders = GetComponents<Collider>();
        foreach (Collider legacyCollider in legacyColliders)
        {
            if (legacyCollider != null)
            {
                DestroyImmediate(legacyCollider);
            }
        }

        Rigidbody legacyBody = GetComponent<Rigidbody>();
        if (legacyBody != null)
        {
            DestroyImmediate(legacyBody);
        }
    }

    void ApplyJointSettings()
    {
        if (dragJoint == null)
        {
            return;
        }

        jointMaxForce = Mathf.Max(0f, jointMaxForce);
        jointFrequency = Mathf.Max(0f, jointFrequency);
        jointDampingRatio = Mathf.Clamp01(jointDampingRatio);

        dragJoint.autoConfigureTarget = false;
        dragJoint.maxForce = jointMaxForce;
        dragJoint.frequency = jointFrequency;
        dragJoint.dampingRatio = jointDampingRatio;
    }

    Vector2 ClampJointTargetToMazeBounds(Vector2 pointerTarget)
    {
        if (!constrainToMazeBounds || dragJoint == null)
        {
            return pointerTarget;
        }

        Vector2 anchorWorldOffset = transform.TransformVector(dragJoint.anchor);
        Vector2 desiredRootPosition = pointerTarget - anchorWorldOffset;
        Vector2 clampedRootPosition = ClampRootPositionToMazeBounds(desiredRootPosition);
        return clampedRootPosition + anchorWorldOffset;
    }

    Vector2 ClampRootPositionToMazeBounds(Vector2 desiredRootPosition)
    {
        if (!TryGetMazeBounds(out Bounds mazeBounds))
        {
            return desiredRootPosition;
        }

        Collider2D collisionCollider = GetCollisionCollider();
        Bounds keyBounds = collisionCollider != null
            ? collisionCollider.bounds
            : new Bounds(transform.position, Vector3.zero);

        Vector2 padding = new Vector2(
            Mathf.Max(0f, mazeBoundsPadding.x),
            Mathf.Max(0f, mazeBoundsPadding.y));
        Vector2 extraReach = new Vector2(
            Mathf.Max(0f, mazeBoundsExtraReach.x),
            Mathf.Max(0f, mazeBoundsExtraReach.y));

        Vector2 keyExtents = keyBounds.extents;
        Vector2 colliderOffsetFromRoot = keyBounds.center - transform.position;
        float minX = mazeBounds.min.x + padding.x - extraReach.x + keyExtents.x - colliderOffsetFromRoot.x;
        float maxX = mazeBounds.max.x - padding.x + extraReach.x - keyExtents.x - colliderOffsetFromRoot.x;
        float minY = mazeBounds.min.y + padding.y - extraReach.y + keyExtents.y - colliderOffsetFromRoot.y;
        float maxY = mazeBounds.max.y - padding.y + extraReach.y - keyExtents.y - colliderOffsetFromRoot.y;

        if (minX > maxX)
        {
            minX = mazeBounds.center.x - colliderOffsetFromRoot.x;
            maxX = minX;
        }

        if (minY > maxY)
        {
            minY = mazeBounds.center.y - colliderOffsetFromRoot.y;
            maxY = minY;
        }

        desiredRootPosition.x = Mathf.Clamp(desiredRootPosition.x, minX, maxX);
        desiredRootPosition.y = Mathf.Clamp(desiredRootPosition.y, minY, maxY);
        return desiredRootPosition;
    }

    bool TryGetMazeBounds(out Bounds bounds)
    {
        if (mazeBoundsCollider != null)
        {
            bounds = mazeBoundsCollider.bounds;
            return true;
        }

        if (mazeBoundsRoot == null)
        {
            bounds = default;
            return false;
        }

        if (mazeBoundsCacheInitialized && cachedMazeBoundsRoot == mazeBoundsRoot)
        {
            bounds = cachedMazeBounds;
            return cachedMazeBoundsAvailable;
        }

        bool hasBounds = false;
        bounds = default;

        SpriteRenderer[] spriteRenderers = mazeBoundsRoot.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer spriteRenderer in spriteRenderers)
        {
            if (spriteRenderer == null
                || spriteRenderer.sprite == null
                || spriteRenderer.transform == transform
                || spriteRenderer.transform.IsChildOf(transform))
            {
                continue;
            }

            Bounds spriteBounds = GetSpriteWorldBounds(spriteRenderer);
            if (!hasBounds)
            {
                bounds = spriteBounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(spriteBounds);
            }
        }

        if (!hasBounds)
        {
            Collider2D collisionCollider = GetCollisionCollider();
            Collider2D[] colliders = mazeBoundsRoot.GetComponentsInChildren<Collider2D>(true);
            foreach (Collider2D mazeCollider in colliders)
            {
                if (mazeCollider == null
                    || IsOwnKeyCollider(mazeCollider)
                    || mazeCollider == collisionCollider
                    || mazeCollider == lockCollider
                    || mazeCollider == exitCollider
                    || mazeCollider.transform == transform
                    || mazeCollider.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = mazeCollider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(mazeCollider.bounds);
                }
            }
        }

        CacheMazeBounds(bounds, hasBounds);
        return hasBounds;
    }

    void CacheMazeBounds(Bounds bounds, bool available)
    {
        cachedMazeBoundsRoot = mazeBoundsRoot;
        cachedMazeBounds = bounds;
        cachedMazeBoundsAvailable = available;
        mazeBoundsCacheInitialized = true;
    }

    void InvalidateMazeBoundsCache()
    {
        cachedMazeBoundsRoot = null;
        cachedMazeBounds = default;
        cachedMazeBoundsAvailable = false;
        mazeBoundsCacheInitialized = false;
    }

    static Bounds GetSpriteWorldBounds(SpriteRenderer spriteRenderer)
    {
        Bounds localBounds = spriteRenderer.sprite.bounds;
        Vector3[] corners =
        {
            new Vector3(localBounds.min.x, localBounds.min.y, 0f),
            new Vector3(localBounds.min.x, localBounds.max.y, 0f),
            new Vector3(localBounds.max.x, localBounds.min.y, 0f),
            new Vector3(localBounds.max.x, localBounds.max.y, 0f),
        };

        Bounds worldBounds = new Bounds(spriteRenderer.transform.TransformPoint(corners[0]), Vector3.zero);
        for (int index = 1; index < corners.Length; index++)
        {
            worldBounds.Encapsulate(spriteRenderer.transform.TransformPoint(corners[index]));
        }

        return worldBounds;
    }

    void CheckExitReached()
    {
        Collider2D targetCollider = GetLockCollider();
        if (targetCollider == null
            || !targetCollider.enabled
            || !targetCollider.gameObject.activeInHierarchy)
        {
            return;
        }

        bool reached = false;
        if (useCollisionPointForLockCheck)
        {
            reached = DoesColliderReachLock(GetCollisionCollider(), targetCollider);
        }

        if (!reached && alsoCheckDragControlForLock)
        {
            reached = DoesColliderReachLock(GetPointerCollider(), targetCollider);
        }

        if (!reached && !useCollisionPointForLockCheck)
        {
            reached = DoesColliderReachLock(GetCollisionCollider(), targetCollider);
        }

        if (!reached)
        {
            return;
        }

        solved = true;
        EndDrag(true);

        if (exitZone != null)
        {
            exitZone.MarkSolvedByKey(this);
        }
        else if (mazePuzzleController != null)
        {
            mazePuzzleController.NotifyMazeSolved();
        }

        if (logSolved)
        {
            Debug.Log("Maze key reached the lock.");
        }
    }

    bool DoesColliderReachLock(Collider2D sourceCollider, Collider2D targetCollider)
    {
        if (sourceCollider == null
            || targetCollider == null
            || !sourceCollider.enabled
            || !sourceCollider.gameObject.activeInHierarchy)
        {
            return false;
        }

        ColliderDistance2D distance = sourceCollider.Distance(targetCollider);
        return distance.isOverlapped || distance.distance <= Mathf.Max(0f, lockReachPadding);
    }

    bool IsPointerOnKey(Vector3 pointerWorld)
    {
        Collider2D pointerCollider = GetPointerCollider();
        if (pointerCollider != null && pointerCollider.enabled && pointerCollider.OverlapPoint(pointerWorld))
        {
            return true;
        }

        Collider2D collisionCollider = GetCollisionCollider();
        if (collisionCollider != null && collisionCollider.enabled && collisionCollider.OverlapPoint(pointerWorld))
        {
            return true;
        }

        Vector3 fallbackCenter = controlPointCollider != null
            ? controlPointCollider.bounds.center
            : (controlPoint != null ? controlPoint.position : transform.position);
        return Vector2.Distance(pointerWorld, fallbackCenter) <= Mathf.Max(0f, pickFallbackRadius);
    }

    void CacheReferences()
    {
        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }

        if (keyBody == null)
        {
            keyBody = GetComponent<Rigidbody2D>();
        }

        if (dragJoint == null)
        {
            dragJoint = GetComponent<TargetJoint2D>();
        }

        if (controlPoint == null)
        {
            controlPoint = FindChildByExactName(transform, ControlPointName);
        }

        if (controlPoint != null && (controlPointCollider == null || controlPointCollider.transform != controlPoint))
        {
            Collider2D childCollider = controlPoint.GetComponent<Collider2D>();
            if (childCollider != null)
            {
                controlPointCollider = childCollider;
            }
        }

        if (keyCollider == null || keyCollider == controlPointCollider)
        {
            Collider2D bodyCollider = FindNamedChildCollider(KeyColliderName);
            if (bodyCollider != null)
            {
                keyCollider = bodyCollider;
            }
        }

        if (keyCollider == null)
        {
            keyCollider = GetComponent<Collider2D>();
        }

        if (controlPointCollider == null && keyCollider != null)
        {
            controlPointCollider = keyCollider;
        }

        if (wallRoot == null || wallRoot.name != KeyWallRootName)
        {
            Transform keyWallRoot = FindWallRoot();
            if (keyWallRoot != null && wallRoot != keyWallRoot)
            {
                wallRoot = keyWallRoot;
                configuredWallRoot = null;
                wallCompositeCollider = null;
                materialWallRoot = null;
            }
        }

        if (mazeBoundsRoot == null && wallRoot != null)
        {
            mazeBoundsRoot = wallRoot.parent != null ? wallRoot.parent : wallRoot;
            InvalidateMazeBoundsCache();
        }

        if (exitZone != null && exitCollider == null)
        {
            exitCollider = exitZone.ExitCollider;
        }

        if (lockObject == null)
        {
            GameObject lockGameObject = FindSceneObjectByExactName(LockName);
            if (lockGameObject != null)
            {
                lockObject = lockGameObject.transform;
            }
        }

        if (lockCollider == null && lockObject != null)
        {
            lockCollider = lockObject.GetComponent<Collider2D>();
        }

        if (lockCollider == null && exitCollider != null)
        {
            lockCollider = exitCollider;
        }

        ConfigureCollisionLayers();
        ConfigureWallCollisionSurface();
        ApplySmoothPhysicsMaterial();
    }

    void ConfigureCollisionLayers()
    {
        int keyLayer = LayerMask.NameToLayer(MazeKeyLayerName);
        int wallLayer = LayerMask.NameToLayer(MazeKeyWallLayerName);
        if (keyLayer < 0 || wallLayer < 0)
        {
            return;
        }

        if (gameObject.layer != keyLayer)
        {
            SetLayerRecursively(transform, keyLayer);
        }

        if (wallRoot != null && collisionLayerWallRoot != wallRoot)
        {
            SetLayerRecursively(wallRoot, wallLayer);
            collisionLayerWallRoot = wallRoot;
        }

        for (int layer = 0; layer < 32; layer++)
        {
            bool shouldIgnore = layer != wallLayer;
            if (Physics2D.GetIgnoreLayerCollision(keyLayer, layer) != shouldIgnore)
            {
                Physics2D.IgnoreLayerCollision(keyLayer, layer, shouldIgnore);
            }
        }
    }

    void ConfigureWallCollisionSurface()
    {
        if (wallRoot == null || configuredWallRoot == wallRoot)
        {
            return;
        }

        configuredWallRoot = wallRoot;
        wallCompositeCollider = wallRoot.GetComponent<CompositeCollider2D>();

        if (mergeWallCollidersAtRuntime && Application.isPlaying)
        {
            Rigidbody2D wallBody = wallRoot.GetComponent<Rigidbody2D>();
            if (wallBody == null)
            {
                wallBody = wallRoot.gameObject.AddComponent<Rigidbody2D>();
            }

            wallBody.bodyType = RigidbodyType2D.Static;
            wallBody.simulated = true;

            if (wallCompositeCollider == null)
            {
                wallCompositeCollider = wallRoot.gameObject.AddComponent<CompositeCollider2D>();
            }

            wallCompositeCollider.geometryType = CompositeCollider2D.GeometryType.Polygons;
            wallCompositeCollider.generationType = CompositeCollider2D.GenerationType.Synchronous;
            wallCompositeCollider.isTrigger = false;

            Collider2D[] sourceColliders = wallRoot.GetComponentsInChildren<Collider2D>(true);
            foreach (Collider2D sourceCollider in sourceColliders)
            {
                if (sourceCollider == null || sourceCollider == wallCompositeCollider)
                {
                    continue;
                }

                sourceCollider.enabled = true;
                sourceCollider.isTrigger = false;
                sourceCollider.compositeOperation = Collider2D.CompositeOperation.Merge;
            }

            wallCompositeCollider.GenerateGeometry();
            Physics2D.SyncTransforms();
        }
    }

    void ApplySmoothPhysicsMaterial()
    {
        if (!useLowFrictionMaterial)
        {
            return;
        }

        if (smoothPhysicsMaterial == null)
        {
            smoothPhysicsMaterial = new PhysicsMaterial2D("Maze Key Smooth")
            {
                friction = 0f,
                bounciness = 0f,
                hideFlags = HideFlags.HideAndDontSave,
            };
        }

        Collider2D collisionCollider = GetCollisionCollider();
        if (collisionCollider != null && materialKeyCollider != collisionCollider)
        {
            collisionCollider.sharedMaterial = smoothPhysicsMaterial;
            materialKeyCollider = collisionCollider;
        }

        if (wallRoot == null || materialWallRoot == wallRoot)
        {
            return;
        }

        Collider2D[] wallColliders = wallRoot.GetComponentsInChildren<Collider2D>(true);
        foreach (Collider2D wallCollider in wallColliders)
        {
            if (wallCollider != null)
            {
                wallCollider.sharedMaterial = smoothPhysicsMaterial;
            }
        }

        materialWallRoot = wallRoot;
    }

    Collider2D GetCollisionCollider()
    {
        return keyCollider != null ? keyCollider : controlPointCollider;
    }

    Collider2D GetPointerCollider()
    {
        return controlPointCollider != null ? controlPointCollider : keyCollider;
    }

    Collider2D FindNamedChildCollider(string childName)
    {
        Transform child = FindChildByExactName(transform, childName);
        return child != null ? child.GetComponent<Collider2D>() : null;
    }

    Transform FindWallRoot()
    {
        GameObject keyWallRoot = FindSceneObjectByExactName(KeyWallRootName);
        return keyWallRoot != null ? keyWallRoot.transform : null;
    }

    bool IsOwnKeyCollider(Collider2D candidate)
    {
        return candidate != null
            && (candidate == keyCollider || candidate == controlPointCollider);
    }

    Collider2D GetLockCollider()
    {
        return lockCollider != null ? lockCollider : exitCollider;
    }

    static Transform FindChildByExactName(Transform parent, string objectName)
    {
        if (parent == null)
        {
            return null;
        }

        Transform[] children = parent.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child != null && child.name == objectName)
            {
                return child;
            }
        }

        return null;
    }

    static void SetLayerRecursively(Transform root, int layer)
    {
        if (root == null || layer < 0)
        {
            return;
        }

        Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform descendant in descendants)
        {
            descendant.gameObject.layer = layer;
        }
    }

    static GameObject FindSceneObjectByExactName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return null;
        }

        GameObject[] sceneObjects = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (GameObject sceneObject in sceneObjects)
        {
            if (sceneObject != null && sceneObject.name == objectName)
            {
                return sceneObject;
            }
        }

        return null;
    }

    void OnDrawGizmosSelected()
    {
        if (!drawControlPointGizmo)
        {
            return;
        }

        Collider2D dragCollider = controlPointCollider != null ? controlPointCollider : keyCollider;
        Vector3 center = dragCollider != null
            ? dragCollider.bounds.center
            : (controlPoint != null ? controlPoint.position : transform.position);
        float radius = dragCollider != null
            ? Mathf.Max(dragCollider.bounds.extents.x, dragCollider.bounds.extents.y)
            : Mathf.Max(0.05f, pickFallbackRadius);

        Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.9f);
        Gizmos.DrawWireSphere(center, Mathf.Max(0.05f, radius));
    }

    Vector3 GetPointerWorldPosition()
    {
        Vector2 pointerScreen = GetPointerScreenPosition();
        Camera activeCamera = worldCamera != null ? worldCamera : Camera.main;
        if (activeCamera == null)
        {
            return transform.position;
        }

        float depth = Mathf.Abs(transform.position.z - activeCamera.transform.position.z);
        Vector3 world = activeCamera.ScreenToWorldPoint(new Vector3(pointerScreen.x, pointerScreen.y, depth));
        world.z = transform.position.z;
        return world;
    }

    static Vector2 GetPointerScreenPosition()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            return mouse.position.ReadValue();
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.mousePosition;
#else
        return Vector2.zero;
#endif
    }

    static bool WasPointerPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButtonDown(0))
        {
            return true;
        }
#endif

        return false;
    }

    static bool IsPointerHeld()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.isPressed)
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButton(0))
        {
            return true;
        }
#endif

        return false;
    }

    static bool WasPointerReleasedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasReleasedThisFrame)
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButtonUp(0))
        {
            return true;
        }
#endif

        return false;
    }
}
