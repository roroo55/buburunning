using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class PenzaiSearchController2D : MonoBehaviour
{
    [Serializable]
    public class HiddenItem
    {
        [Tooltip("用于游戏逻辑判断的唯一名称，例如 key。")]
        public string itemName;

        [TextArea]
        [Tooltip("找到该物品时显示的文字。")]
        public string foundMessage;

        [Tooltip("显示在找到物品文字正上方的图标。")]
        public Sprite itemIcon;

        [Tooltip("场景物体名称。没有直接指定物体时，会用此名称查找。")]
        public string itemObjectName;

        [Tooltip("可选：后续可直接拖入 Prefab，运行时会自动创建。")]
        public GameObject itemPrefab;

        [Tooltip("可选：场景中已经存在的物品对象。")]
        public GameObject itemObject;

        [Tooltip("找到物品时触发，可在 Inspector 里添加后续 UI 或其他逻辑。")]
        public UnityEvent onCollected = new UnityEvent();

        [HideInInspector]
        public Transform assignedSearchPoint;

        [HideInInspector]
        public bool collected;
    }

    public string playerObjectName = BubuRunningGame.PlayerRootName;
    public string searchPointPrefix = "penzai";

    [Header("Search Range And Prompt")]
    [Min(0f)]
    [InspectorName("P Search Radius")]
    public float interactionRadius = 1.5f;
    [Min(0f)]
    [Tooltip("Only the nearest searchable object inside this radius shows the prompt.")]
    public float promptDisplayRadius = 0.6f;
    [Min(0.001f)]
    [Tooltip("World-space size of the shared search prompt.")]
    public float promptWorldScale = 0.055f;
    public float promptVerticalOffset = 0.35f;
    public bool hidePromptAfterSearch = true;

    [Header("Search Debug")]
    public bool enableSearchDebug = true;
    public bool debugVisibleOnStart = true;

    [Header("Search Blocking")]
    [Tooltip("When enabled, the search prompt and P-key search are disabled while the maze is visible.")]
    public bool disableSearchWhileMazeActive = true;
    public MazePuzzleController mazePuzzleController;

    public bool randomizeItemsOnStart = true;
    public bool hideItemsOnStart = true;
    public bool refreshSearchPointsOnStart = true;

    [Header("Furniture Search Points")]
    public Transform searchPointRoot;
    public bool searchOnlyWithinRoot;
    public bool addTriggerCollidersToRootChildren = true;

    [Tooltip("These objects remain searchable, but hidden items will never be assigned to them.")]
    public List<Transform> excludedHiddenItemPoints = new List<Transform>();

    [Tooltip("关闭时，每件隐藏物品一定会分配到不同盆栽。")]
    public bool allowMultipleItemsPerSearchPoint;

    public bool logSearchResults = true;
    public GameplayMessageUI2D messageUI;
    public Transform player;
    public List<Transform> searchPoints = new List<Transform>();

    [Header("Search Audio")]
    [Tooltip("每次成功对一个盆栽执行搜索时播放。")]
    public AudioClip searchAudioClip;
    public AudioSource searchAudioSource;
    [Range(0f, 1f)]
    public float searchAudioVolume = 1f;

    [Tooltip("可在 Inspector 中继续添加任意隐藏物品，不需要修改本脚本。")]
    public HiddenItem[] hiddenItems = Array.Empty<HiddenItem>();

    readonly HashSet<string> collectedItemNames =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<Transform> exploredSearchPoints = new HashSet<Transform>();

    GameObject sharedSearchPrompt;
    GameObject searchPromptTemplate;

    public bool HasKey => HasItem("key");

    void Awake()
    {
        InitializeSearchState();
        EnsureSearchDebugOverlay();
    }

    void Update()
    {
        if (IsSearchSuppressed)
        {
            DestroySharedSearchPrompt();
            return;
        }

        UpdateSharedSearchPrompt();

        if (WasExplorePressed())
        {
            TryExploreNearestPoint();
        }
    }

    public bool HasItem(string itemName)
    {
        return !string.IsNullOrWhiteSpace(itemName) && collectedItemNames.Contains(itemName);
    }

    public bool GrantItem(
        string itemName,
        string foundMessage,
        Sprite itemIcon)
    {
        CacheMissingReferences();
        if (string.IsNullOrWhiteSpace(itemName)
            || collectedItemNames.Contains(itemName))
        {
            return false;
        }

        HiddenItem definition = null;
        if (hiddenItems != null)
        {
            foreach (HiddenItem item in hiddenItems)
            {
                if (item != null
                    && string.Equals(
                        item.itemName,
                        itemName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    definition = item;
                    break;
                }
            }
        }

        collectedItemNames.Add(itemName);
        if (definition != null)
        {
            definition.collected = true;
            HideItemObject(definition.itemObject);
            if (string.IsNullOrWhiteSpace(foundMessage))
            {
                foundMessage = GetFoundMessage(definition);
            }

            if (itemIcon == null)
            {
                itemIcon = GetItemIcon(definition);
            }

            definition.onCollected?.Invoke();
        }

        if (string.IsNullOrWhiteSpace(foundMessage))
        {
                foundMessage = "Item obtained.";
        }

        messageUI?.ShowItemMessage(foundMessage, itemIcon);
        if (logSearchResults)
        {
            Debug.Log("Player was granted item '" + itemName + "'.");
        }

        return true;
    }

    public void RefreshSearchPointsFromScene()
    {
        List<Transform> inspectorSearchPoints =
            searchPoints != null
                ? new List<Transform>(searchPoints)
                : new List<Transform>();

        GameObject promptTemplate = FindPromptTemplate(inspectorSearchPoints);

        if (searchPoints == null)
        {
            searchPoints = new List<Transform>();
        }
        else
        {
            searchPoints.Clear();
        }

        HashSet<Transform> uniquePoints = new HashSet<Transform>();
        if (searchPointRoot != null)
        {
            for (int index = 0; index < searchPointRoot.childCount; index++)
            {
                Transform child = searchPointRoot.GetChild(index);
                if (child == null)
                {
                    continue;
                }

                if (addTriggerCollidersToRootChildren)
                {
                    EnsureSearchPointCollider(child);
                }

                if (uniquePoints.Add(child))
                {
                    searchPoints.Add(child);
                }
            }
        }

        foreach (Transform point in inspectorSearchPoints)
        {
            if (point == null || !uniquePoints.Add(point))
            {
                continue;
            }

            EnsureSearchPointCollider(point);
            searchPoints.Add(point);
        }

        if (!searchOnlyWithinRoot)
        {
            GameObject[] sceneObjects =
                FindObjectsByType<GameObject>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            foreach (GameObject sceneObject in sceneObjects)
            {
                if (sceneObject == null
                    || !IsSearchPointName(sceneObject.name)
                    || !uniquePoints.Add(sceneObject.transform))
                {
                    continue;
                }

                searchPoints.Add(sceneObject.transform);
            }
        }

        searchPoints.Sort(
            (first, second) =>
                string.Compare(first.name, second.name, StringComparison.OrdinalIgnoreCase));

        SetupSharedSearchPrompt(promptTemplate);
    }

    public bool IsSearchSuppressed
    {
        get
        {
            if (!disableSearchWhileMazeActive)
            {
                return false;
            }

            if (mazePuzzleController == null)
            {
                mazePuzzleController = FindAnyObjectByType<MazePuzzleController>(FindObjectsInactive.Include);
            }

            return mazePuzzleController != null && mazePuzzleController.IsActive;
        }
    }

    public bool WasSearchPointExplored(Transform point)
    {
        return point != null && exploredSearchPoints.Contains(point);
    }

    void EnsureSearchDebugOverlay()
    {
        PenzaiSearchDebugOverlay2D overlay = GetComponent<PenzaiSearchDebugOverlay2D>();
        if (!enableSearchDebug)
        {
            if (overlay != null)
            {
                overlay.enabled = false;
            }

            return;
        }

        if (overlay == null)
        {
            overlay = gameObject.AddComponent<PenzaiSearchDebugOverlay2D>();
        }

        overlay.controller = this;
        overlay.SetVisible(debugVisibleOnStart);
        overlay.enabled = true;
    }

    static GameObject FindPromptTemplate(List<Transform> preferredPoints)
    {
        foreach (Transform point in preferredPoints)
        {
            if (point == null)
            {
                continue;
            }

            HandPromptTrigger2D prompt = point.GetComponent<HandPromptTrigger2D>();
            if (prompt != null && prompt.handObject != null)
            {
                return prompt.handObject;
            }
        }

        return null;
    }

    void SetupSharedSearchPrompt(GameObject template)
    {
        if (template != null)
        {
            searchPromptTemplate = template;
        }

        foreach (Transform point in searchPoints)
        {
            if (point == null)
            {
                continue;
            }

            HandPromptTrigger2D oldPrompt = point.GetComponent<HandPromptTrigger2D>();
            if (oldPrompt == null)
            {
                continue;
            }

            oldPrompt.SetHandVisible(false);
            oldPrompt.enabled = false;
        }

        if (!IsSearchSuppressed)
        {
            EnsureSharedSearchPrompt();
        }

        ApplyPromptSize();
        SetSharedPromptVisible(false);
    }

    void UpdateSharedSearchPrompt()
    {
        if (IsSearchSuppressed)
        {
            DestroySharedSearchPrompt();
            return;
        }

        EnsureSharedSearchPrompt();

        if (sharedSearchPrompt == null || player == null)
        {
            SetSharedPromptVisible(false);
            return;
        }

        Transform nearestPoint = GetNearestPromptPoint();
        if (nearestPoint == null)
        {
            SetSharedPromptVisible(false);
            return;
        }

        PositionSharedPrompt(nearestPoint);
        ApplyPromptSize();
        SetSharedPromptVisible(true);
    }

    void EnsureSharedSearchPrompt()
    {
        if (sharedSearchPrompt != null || searchPromptTemplate == null || IsSearchSuppressed)
        {
            return;
        }

        sharedSearchPrompt = Instantiate(searchPromptTemplate);
        sharedSearchPrompt.name = "Shared Search Prompt";
        sharedSearchPrompt.transform.SetParent(null, true);
        ApplyPromptSize();
        SetSharedPromptVisible(false);
    }

    void DestroySharedSearchPrompt()
    {
        if (sharedSearchPrompt == null)
        {
            return;
        }

        Destroy(sharedSearchPrompt);
        sharedSearchPrompt = null;
    }

    Transform GetNearestPromptPoint()
    {
        float radiusSqr = promptDisplayRadius * promptDisplayRadius;
        float nearestDistanceSqr = float.PositiveInfinity;
        Transform nearestPoint = null;
        Vector2 playerPosition = player.position;

        foreach (Transform point in searchPoints)
        {
            if (point == null
                || !point.gameObject.activeInHierarchy
                || (hidePromptAfterSearch && exploredSearchPoints.Contains(point)))
            {
                continue;
            }

            float distanceSqr = GetDistanceSqrToSearchPoint(point, playerPosition);
            if (distanceSqr <= radiusSqr && distanceSqr < nearestDistanceSqr)
            {
                nearestDistanceSqr = distanceSqr;
                nearestPoint = point;
            }
        }

        return nearestPoint;
    }

    void PositionSharedPrompt(Transform point)
    {
        Renderer renderer = point.GetComponentInChildren<Renderer>(true);
        Vector3 position = point.position + Vector3.up * promptVerticalOffset;
        if (renderer != null)
        {
            Bounds bounds = renderer.bounds;
            position = new Vector3(
                bounds.center.x,
                bounds.max.y + promptVerticalOffset,
                point.position.z - 0.1f);
        }

        sharedSearchPrompt.transform.position = position;
        sharedSearchPrompt.transform.rotation = Quaternion.identity;
    }

    void ApplyPromptSize()
    {
        if (sharedSearchPrompt == null)
        {
            return;
        }

        float scale = Mathf.Max(0.001f, promptWorldScale);
        sharedSearchPrompt.transform.localScale = new Vector3(scale, scale, 1f);
    }

    void SetSharedPromptVisible(bool visible)
    {
        if (sharedSearchPrompt != null && sharedSearchPrompt.activeSelf != visible)
        {
            sharedSearchPrompt.SetActive(visible);
        }
    }

    public bool TryExploreNearestPoint()
    {
        if (IsSearchSuppressed)
        {
            DestroySharedSearchPrompt();
            return false;
        }

        CacheMissingReferences();

        Transform point = GetNearestSearchPointInRange();
        if (point == null)
        {
            if (logSearchResults)
            {
                Debug.Log("No penzai search point is close enough to explore.");
            }

            return false;
        }

        return ExplorePoint(point);
    }

    void InitializeSearchState()
    {
        if (hiddenItems == null)
        {
            hiddenItems = Array.Empty<HiddenItem>();
        }

        ResetCollectedItems();

        if (refreshSearchPointsOnStart)
        {
            RefreshSearchPointsFromScene();
        }

        CacheMissingReferences();

        if (randomizeItemsOnStart)
        {
            AssignHiddenItemsToRandomSearchPoints();
        }

        if (hideItemsOnStart)
        {
            HideAllHiddenItems();
        }
    }

    void ResetCollectedItems()
    {
        collectedItemNames.Clear();
        exploredSearchPoints.Clear();
        foreach (HiddenItem item in hiddenItems)
        {
            if (item == null)
            {
                continue;
            }

            item.collected = false;
            item.assignedSearchPoint = null;
        }
    }

    void CacheMissingReferences()
    {
        if (player == null)
        {
            GameObject playerObject = FindSceneObjectByExactName(playerObjectName);
            if (playerObject != null)
            {
                player = playerObject.transform;
            }
        }

        if (messageUI == null)
        {
            messageUI = FindAnyObjectByType<GameplayMessageUI2D>(FindObjectsInactive.Include);
        }

        if (searchPoints == null || searchPoints.Count == 0)
        {
            RefreshSearchPointsFromScene();
        }

        foreach (HiddenItem item in hiddenItems)
        {
            if (item == null || item.itemObject != null)
            {
                continue;
            }

            item.itemObject = FindSceneObjectByExactName(item.itemObjectName);
            if (item.itemObject == null && item.itemPrefab != null)
            {
                item.itemObject = Instantiate(item.itemPrefab);
                item.itemObject.name = GetItemObjectName(item);
            }
        }
    }

    void AssignHiddenItemsToRandomSearchPoints()
    {
        List<Transform> availablePoints = GetAvailableSearchPoints();
        if (availablePoints.Count == 0)
        {
            Debug.LogWarning("Penzai search has no active search points for hidden items.");
            return;
        }

        Shuffle(availablePoints);
        int uniquePointIndex = 0;

        foreach (HiddenItem item in hiddenItems)
        {
            if (item == null)
            {
                continue;
            }

            Transform point;
            if (allowMultipleItemsPerSearchPoint)
            {
                point = availablePoints[UnityEngine.Random.Range(0, availablePoints.Count)];
            }
            else
            {
                if (uniquePointIndex >= availablePoints.Count)
                {
                    item.assignedSearchPoint = null;
                    Debug.LogWarning(
                        "There are more hidden items than penzai search points. Item '"
                        + item.itemName
                        + "' was not assigned.");
                    continue;
                }

                point = availablePoints[uniquePointIndex];
                uniquePointIndex++;
            }

            item.assignedSearchPoint = point;
            PlaceItemAtSearchPoint(item, point);

            if (logSearchResults)
            {
                Debug.Log("Hidden item '" + item.itemName + "' assigned to " + point.name + ".");
            }
        }
    }

    List<Transform> GetAvailableSearchPoints()
    {
        List<Transform> availablePoints = new List<Transform>();
        if (searchPoints == null)
        {
            return availablePoints;
        }

        foreach (Transform point in searchPoints)
        {
            if (point != null
                && point.gameObject.activeInHierarchy
                && (excludedHiddenItemPoints == null
                    || !excludedHiddenItemPoints.Contains(point)))
            {
                availablePoints.Add(point);
            }
        }

        return availablePoints;
    }

    static void Shuffle(List<Transform> points)
    {
        for (int index = points.Count - 1; index > 0; index--)
        {
            int swapIndex = UnityEngine.Random.Range(0, index + 1);
            Transform temporaryPoint = points[index];
            points[index] = points[swapIndex];
            points[swapIndex] = temporaryPoint;
        }
    }

    static void PlaceItemAtSearchPoint(HiddenItem item, Transform point)
    {
        if (item.itemObject == null || point == null)
        {
            return;
        }

        PenzaiSearchFeedback2D feedback = point.GetComponent<PenzaiSearchFeedback2D>();
        Transform spawnPoint =
            feedback != null && feedback.itemSpawnPoint != null
                ? feedback.itemSpawnPoint
                : point;

        Transform itemTransform = item.itemObject.transform;
        Vector3 configuredWorldScale = itemTransform.lossyScale;
        itemTransform.SetParent(spawnPoint, true);
        itemTransform.SetPositionAndRotation(
            spawnPoint.position,
            Quaternion.identity);
        SetWorldScale(itemTransform, configuredWorldScale);
    }

    static void SetWorldScale(Transform target, Vector3 worldScale)
    {
        if (target == null)
        {
            return;
        }

        Transform parent = target.parent;
        if (parent == null)
        {
            target.localScale = worldScale;
            return;
        }

        Vector3 parentScale = parent.lossyScale;
        target.localScale =
            new Vector3(
                SafeDivide(worldScale.x, parentScale.x),
                SafeDivide(worldScale.y, parentScale.y),
                SafeDivide(worldScale.z, parentScale.z));
    }

    static float SafeDivide(float value, float divisor)
    {
        return Mathf.Approximately(divisor, 0f) ? value : value / divisor;
    }

    Transform GetNearestSearchPointInRange()
    {
        if (player == null || searchPoints == null)
        {
            return null;
        }

        float radiusSqr = interactionRadius * interactionRadius;
        float nearestDistanceSqr = float.PositiveInfinity;
        Transform nearestPoint = null;
        Vector2 playerPosition = player.position;

        foreach (Transform point in searchPoints)
        {
            if (point == null || !point.gameObject.activeInHierarchy)
            {
                continue;
            }

            float distanceSqr =
                GetDistanceSqrToSearchPoint(point, playerPosition);
            if (distanceSqr <= radiusSqr && distanceSqr < nearestDistanceSqr)
            {
                nearestDistanceSqr = distanceSqr;
                nearestPoint = point;
            }
        }

        return nearestPoint;
    }

    static float GetDistanceSqrToSearchPoint(
        Transform point,
        Vector2 playerPosition)
    {
        Collider2D pointCollider = point.GetComponent<Collider2D>();
        Vector2 nearestPosition =
            pointCollider != null && pointCollider.enabled
                ? pointCollider.ClosestPoint(playerPosition)
                : (Vector2)point.position;
        return (nearestPosition - playerPosition).sqrMagnitude;
    }

    static void EnsureSearchPointCollider(Transform point)
    {
        if (point.GetComponent<Collider2D>() != null)
        {
            return;
        }

        BoxCollider2D collider = point.gameObject.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;

        SpriteRenderer spriteRenderer = point.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null || spriteRenderer.sprite == null)
        {
            return;
        }

        collider.offset = spriteRenderer.sprite.bounds.center;
        collider.size = spriteRenderer.sprite.bounds.size;
    }

    bool ExplorePoint(Transform point)
    {
        PlaySearchAudio();
        exploredSearchPoints.Add(point);

        List<string> foundMessages = new List<string>();
        Sprite foundIcon = null;

        foreach (HiddenItem item in hiddenItems)
        {
            if (item == null || item.collected || item.assignedSearchPoint != point)
            {
                continue;
            }

            item.collected = true;
            if (!string.IsNullOrWhiteSpace(item.itemName))
            {
                collectedItemNames.Add(item.itemName);
            }

            HideItemObject(item.itemObject);
            foundMessages.Add(GetFoundMessage(item));
            if (foundIcon == null)
            {
                foundIcon = GetItemIcon(item);
            }

            item.onCollected?.Invoke();

            if (logSearchResults)
            {
                Debug.Log("Player found item '" + item.itemName + "' at " + point.name + ".");
            }
        }

        bool foundItem = foundMessages.Count > 0;
        PenzaiSearchFeedback2D feedback = point.GetComponent<PenzaiSearchFeedback2D>();

        if (foundItem)
        {
            string combinedMessage = string.Join("\n", foundMessages);
            if (feedback != null)
            {
                feedback.ShowItemFound(combinedMessage, foundIcon);
            }
            else if (messageUI != null)
            {
                messageUI.ShowItemMessage(combinedMessage, foundIcon);
            }
        }
        else
        {
            if (logSearchResults)
            {
                Debug.Log("Player searched " + point.name + ", but found nothing.");
            }

            if (feedback != null)
            {
                feedback.ShowNothingFound();
            }
            else if (messageUI != null)
            {
                messageUI.ShowNothingFoundMessage();
            }
        }

        return foundItem;
    }

    void OnValidate()
    {
        interactionRadius = Mathf.Max(0f, interactionRadius);
        promptDisplayRadius = Mathf.Max(0f, promptDisplayRadius);
        promptWorldScale = Mathf.Max(0.001f, promptWorldScale);
        ApplyPromptSize();
    }

    public void PlaySearchAudio()
    {
        if (searchAudioClip == null)
        {
            return;
        }

        if (searchAudioSource == null)
        {
            searchAudioSource = GetComponent<AudioSource>();
        }

        if (searchAudioSource == null)
        {
            searchAudioSource = gameObject.AddComponent<AudioSource>();
            searchAudioSource.playOnAwake = false;
            searchAudioSource.loop = false;
            searchAudioSource.spatialBlend = 0f;
        }

        searchAudioSource.PlayOneShot(
            searchAudioClip,
            Mathf.Clamp01(searchAudioVolume));
    }

    static Sprite GetItemIcon(HiddenItem item)
    {
        if (item.itemIcon != null)
        {
            return item.itemIcon;
        }

        if (item.itemObject == null)
        {
            return null;
        }

        SpriteRenderer spriteRenderer =
            item.itemObject.GetComponentInChildren<SpriteRenderer>(true);
        return spriteRenderer != null ? spriteRenderer.sprite : null;
    }

    static string GetFoundMessage(HiddenItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.foundMessage))
        {
            return item.foundMessage;
        }

        return string.IsNullOrWhiteSpace(item.itemName)
                ? "Item found."
                : "Item found.";
    }

    static string GetItemObjectName(HiddenItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.itemObjectName))
        {
            return item.itemObjectName;
        }

        if (!string.IsNullOrWhiteSpace(item.itemName))
        {
            return item.itemName;
        }

        return item.itemPrefab != null ? item.itemPrefab.name : "Hidden Item";
    }

    void HideAllHiddenItems()
    {
        foreach (HiddenItem item in hiddenItems)
        {
            if (item != null)
            {
                HideItemObject(item.itemObject);
            }
        }
    }

    static void HideItemObject(GameObject itemObject)
    {
        if (itemObject == null)
        {
            return;
        }

        Renderer[] renderers = itemObject.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer itemRenderer in renderers)
        {
            itemRenderer.enabled = false;
        }

        Collider2D[] colliders2D = itemObject.GetComponentsInChildren<Collider2D>(true);
        foreach (Collider2D itemCollider in colliders2D)
        {
            itemCollider.enabled = false;
        }

        Collider[] colliders3D = itemObject.GetComponentsInChildren<Collider>(true);
        foreach (Collider itemCollider in colliders3D)
        {
            itemCollider.enabled = false;
        }
    }

    bool IsSearchPointName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName) || string.IsNullOrEmpty(searchPointPrefix))
        {
            return false;
        }

        return string.Equals(objectName, searchPointPrefix, StringComparison.OrdinalIgnoreCase)
            || objectName.StartsWith(searchPointPrefix + " (", StringComparison.OrdinalIgnoreCase);
    }

    static GameObject FindSceneObjectByExactName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        GameObject[] sceneObjects =
            FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (GameObject sceneObject in sceneObjects)
        {
            if (sceneObject != null && sceneObject.name == objectName)
            {
                return sceneObject;
            }
        }

        return null;
    }

    static bool WasExplorePressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.pKey.wasPressedThisFrame)
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.P))
        {
            return true;
        }
#endif

        return false;
    }
}
