using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class IncenseShoeCutInteraction2D : MonoBehaviour
{
    [Header("References")]
    public string playerObjectName = BubuRunningGame.PlayerRootName;
    public Transform player;
    public PenzaiSearchController2D inventory;
    public GameplayMessageUI2D messageUI;
    public ShoeCutMiniGame2D miniGame;

    [Header("Interaction")]
    public Vector2 interactionCenterOffset;
    [Min(0f)]
    public float interactionRadius = 2.2f;
#if ENABLE_INPUT_SYSTEM
    public Key interactionKey = Key.P;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
    public KeyCode legacyInteractionKey = KeyCode.P;
#endif

    [Header("Required Items")]
    public string shoeItemName = "xiuhuaxie";
    public string scissorsItemName = "scissors";
    public string rewardItemName = "key";

    [Header("Editable Messages")]
    [TextArea]
    public string readyMessage =
        "Press P at the incense burner to cut open the embroidered shoe.";
    [TextArea]
    public string missingBothMessage =
        "You need both the embroidered shoe and the scissors.";
    [TextArea]
    public string missingShoeMessage =
        "The embroidered shoe is missing.";
    [TextArea]
    public string missingScissorsMessage = "The scissors are missing.";
    [TextArea]
    public string alreadyCompletedMessage =
        "The embroidered shoe has already been cut open.";

    [Header("Events")]
    public UnityEvent onChallengeRequested = new UnityEvent();
    public UnityEvent onRequirementsMissing = new UnityEvent();
    public UnityEvent onShoeCutCompleted = new UnityEvent();

    public bool IsCompleted { get; private set; }

    bool approachPromptShown;

    void Awake()
    {
        CacheReferences();
        IsCompleted =
            inventory != null
            && inventory.HasItem(rewardItemName);
    }

    void Update()
    {
        CacheReferences();
        if (miniGame != null && miniGame.IsActive)
        {
            return;
        }

        bool playerIsNear = IsPlayerNear();
        if (!playerIsNear)
        {
            approachPromptShown = false;
            return;
        }

        bool hasRequirements = HasRequiredItems();
        if (!IsCompleted && hasRequirements && !approachPromptShown)
        {
            messageUI?.ShowMessage(readyMessage);
            approachPromptShown = true;
        }

        if (WasInteractionPressed())
        {
            TryInteract();
        }
    }

    public bool TryInteract()
    {
        CacheReferences();
        if (!IsPlayerNear())
        {
            return false;
        }

        if (IsCompleted
            || (inventory != null && inventory.HasItem(rewardItemName)))
        {
            IsCompleted = true;
            messageUI?.ShowMessage(alreadyCompletedMessage);
            return false;
        }

        bool hasShoe =
            inventory != null && inventory.HasItem(shoeItemName);
        bool hasScissors =
            inventory != null && inventory.HasItem(scissorsItemName);
        if (!hasShoe || !hasScissors)
        {
            string message =
                !hasShoe && !hasScissors
                    ? missingBothMessage
                    : !hasShoe
                        ? missingShoeMessage
                        : missingScissorsMessage;
            messageUI?.ShowMessage(message);
            onRequirementsMissing?.Invoke();
            return false;
        }

        if (miniGame == null)
        {
            return false;
        }

        onChallengeRequested?.Invoke();
        return miniGame.StartChallenge();
    }

    public void NotifyShoeCutCompleted()
    {
        if (IsCompleted)
        {
            return;
        }

        IsCompleted = true;
        approachPromptShown = true;
        onShoeCutCompleted?.Invoke();
    }

    public bool HasRequiredItems()
    {
        return inventory != null
            && inventory.HasItem(shoeItemName)
            && inventory.HasItem(scissorsItemName);
    }

    public bool IsPlayerNear()
    {
        if (player == null)
        {
            return false;
        }

        Vector2 center =
            (Vector2)transform.position + interactionCenterOffset;
        return Vector2.Distance(player.position, center)
            <= Mathf.Max(0f, interactionRadius);
    }

    void CacheReferences()
    {
        if (player == null)
        {
            GameObject playerObject =
                FindSceneObjectByExactName(playerObjectName);
            if (playerObject != null)
            {
                player = playerObject.transform;
            }
        }

        if (inventory == null)
        {
            inventory =
                FindAnyObjectByType<PenzaiSearchController2D>(
                    FindObjectsInactive.Include);
        }

        if (messageUI == null)
        {
            messageUI =
                FindAnyObjectByType<GameplayMessageUI2D>(
                    FindObjectsInactive.Include);
        }

        if (miniGame == null)
        {
            miniGame =
                FindAnyObjectByType<ShoeCutMiniGame2D>(
                    FindObjectsInactive.Include);
        }
    }

    bool WasInteractionPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null
            && keyboard[interactionKey].wasPressedThisFrame)
        {
            return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(legacyInteractionKey))
        {
            return true;
        }
#endif
        return false;
    }

    static GameObject FindSceneObjectByExactName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        GameObject[] sceneObjects =
            FindObjectsByType<GameObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
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
        Gizmos.color = new Color(0.95f, 0.65f, 0.2f, 0.4f);
        Gizmos.DrawWireSphere(
            (Vector2)transform.position + interactionCenterOffset,
            Mathf.Max(0f, interactionRadius));
    }
}
