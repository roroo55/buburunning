using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class DoorMazePuzzleTrigger2D : MonoBehaviour
{
    public string playerObjectName = BubuRunningGame.PlayerRootName;
    public string requiredItemName = "key";
    public float interactionPadding = 0.12f;
    public float fallbackInteractionDistance = 1.5f;
    public bool requireKey = true;
    public bool logInteractionResults = true;
    public Transform player;
    public Collider2D doorCollider;
    public Collider2D playerCollider;
    public PenzaiSearchController2D searchController;
    public MazePuzzleController mazePuzzleController;
    public GameplayMessageUI2D messageUI;

    bool missingKeyPromptShownForCurrentApproach;

    void Awake()
    {
        CacheMissingReferences();
    }

    void Update()
    {
        CacheMissingReferences();
        UpdateMissingKeyPrompt();

        if (!WasInteractPressed())
        {
            return;
        }

        TryOpenMazeAtDoor();
    }

    public bool TryOpenMazeAtDoor()
    {
        CacheMissingReferences();

        if (!IsPlayerAtDoor())
        {
            return false;
        }

        if (requireKey && !PlayerHasRequiredItem())
        {
            messageUI?.ShowMissingKeyMessage();

            if (logInteractionResults)
            {
                Debug.Log("Door maze puzzle needs item '" + requiredItemName + "' before it can open.");
            }

            return false;
        }

        if (mazePuzzleController == null)
        {
            if (logInteractionResults)
            {
                Debug.LogWarning("Door maze puzzle trigger could not find a MazePuzzleController.");
            }

            return false;
        }

        mazePuzzleController.ShowMaze();
        if (logInteractionResults)
        {
            Debug.Log("Door maze puzzle opened.");
        }

        return true;
    }

    void CacheMissingReferences()
    {
        if (doorCollider == null)
        {
            doorCollider = GetComponent<Collider2D>();
        }

        if (player == null)
        {
            GameObject playerObject = FindSceneObjectByExactName(playerObjectName);
            if (playerObject != null)
            {
                player = playerObject.transform;
            }
        }

        if (playerCollider == null && player != null)
        {
            playerCollider = player.GetComponentInChildren<Collider2D>();
        }

        if (searchController == null)
        {
            searchController = FindAnyObjectByType<PenzaiSearchController2D>(FindObjectsInactive.Include);
        }

        if (mazePuzzleController == null)
        {
            mazePuzzleController = FindAnyObjectByType<MazePuzzleController>(FindObjectsInactive.Include);
        }

        if (messageUI == null)
        {
            messageUI = FindAnyObjectByType<GameplayMessageUI2D>(FindObjectsInactive.Include);
        }
    }

    void UpdateMissingKeyPrompt()
    {
        bool playerAtDoor = IsPlayerAtDoor();
        bool shouldShowMissingKey = playerAtDoor && requireKey && !PlayerHasRequiredItem();

        if (shouldShowMissingKey && !missingKeyPromptShownForCurrentApproach)
        {
            messageUI?.ShowMissingKeyMessage();
            missingKeyPromptShownForCurrentApproach = true;
        }
        else if (!playerAtDoor)
        {
            missingKeyPromptShownForCurrentApproach = false;
        }
    }

    bool PlayerHasRequiredItem()
    {
        return searchController != null && searchController.HasItem(requiredItemName);
    }

    bool IsPlayerAtDoor()
    {
        if (doorCollider != null && playerCollider != null && doorCollider.enabled && playerCollider.enabled)
        {
            ColliderDistance2D distance = doorCollider.Distance(playerCollider);
            return distance.isOverlapped || distance.distance <= Mathf.Max(0f, interactionPadding);
        }

        if (player == null)
        {
            return false;
        }

        return Vector2.Distance(player.position, transform.position) <= Mathf.Max(0f, fallbackInteractionDistance);
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

    static bool WasInteractPressed()
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
