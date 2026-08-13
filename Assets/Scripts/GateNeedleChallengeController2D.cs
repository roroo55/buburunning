using System;
using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class GateNeedleChallengeController2D : MonoBehaviour
{
    [Serializable]
    public class ItemRequirement
    {
        public string itemName;
        public bool required = true;
        [TextArea]
        public string missingMessage;
    }

    [Serializable]
    public class ExternalRequirement
    {
        public string conditionName;
        public bool required = true;
        public bool satisfied;
        [TextArea]
        public string missingMessage;
    }

    [Header("References")]
    public string playerObjectName = BubuRunningGame.PlayerRootName;
    public Transform player;
    public Collider2D playerCollider;
    public Collider2D gateBlocker;
    public Renderer[] gateVisuals = Array.Empty<Renderer>();
    public PenzaiSearchController2D itemInventory;
    public PrecisionNeedleChallenge2D needleChallenge;
    public GameplayMessageUI2D messageUI;

    [Header("Interaction")]
    [Min(0f)]
    public float interactionPadding = 0.8f;
    [Min(0f)]
    public float fallbackInteractionDistance = 2.6f;
#if ENABLE_INPUT_SYSTEM
    public Key interactionKey = Key.P;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
    public KeyCode legacyInteractionKey = KeyCode.P;
#endif

    [Header("Requirements To Start Needle Challenge")]
    public ItemRequirement[] challengeItemRequirements =
        Array.Empty<ItemRequirement>();

    [Header("Requirements To Open Gate")]
    public bool requireNeedleChallengeCompletion = true;
    public ItemRequirement[] gateItemRequirements =
        Array.Empty<ItemRequirement>();
    public ExternalRequirement[] externalRequirements =
        Array.Empty<ExternalRequirement>();

    [Header("Gate Result")]
    public bool disableGateBlockerWhenUnlocked = true;
    public bool hideGateVisualsWhenUnlocked = true;

    [Header("Editable Messages")]
    [TextArea]
    public string approachMessage =
        "Press P to use the doll for needle calibration.";
    [TextArea]
    public string missingChallengeItemMessage =
        "Required ritual items are missing.";
    [TextArea]
    public string pendingRequirementMessage =
        "Calibration complete, but another gate requirement is still missing.";
    [TextArea]
    public string gateOpenedMessage = "The gate is open.";

    [Header("Events")]
    public UnityEvent onChallengeRequested = new UnityEvent();
    public UnityEvent onRequirementsStillMissing = new UnityEvent();
    public UnityEvent onGateOpened = new UnityEvent();

    public bool IsGateOpen { get; private set; }

    bool approachPromptShown;

    void Awake()
    {
        CacheMissingReferences();
        ApplyGateState();

        if (needleChallenge != null)
        {
            needleChallenge.onChallengeCompleted.AddListener(
                HandleNeedleChallengeCompleted);
        }
    }

    void OnDestroy()
    {
        if (needleChallenge != null)
        {
            needleChallenge.onChallengeCompleted.RemoveListener(
                HandleNeedleChallengeCompleted);
        }
    }

    void Update()
    {
        if (IsGateOpen)
        {
            return;
        }

        CacheMissingReferences();
        bool playerIsNear = IsPlayerNearGate();
        if (!playerIsNear)
        {
            approachPromptShown = false;
            return;
        }

        if (needleChallenge != null && needleChallenge.IsActive)
        {
            return;
        }

        if (!approachPromptShown)
        {
            messageUI?.ShowMessage(approachMessage);
            approachPromptShown = true;
        }

        if (WasInteractionPressed())
        {
            TryStartNeedleChallenge();
        }
    }

    public bool TryStartNeedleChallenge()
    {
        CacheMissingReferences();
        if (IsGateOpen || !IsPlayerNearGate() || needleChallenge == null)
        {
            return false;
        }

        if (needleChallenge.IsCompleted)
        {
            return TryUnlockGate();
        }

        string missingMessage;
        if (!AreItemRequirementsSatisfied(
                challengeItemRequirements,
                out missingMessage))
        {
            messageUI?.ShowMessage(
                string.IsNullOrWhiteSpace(missingMessage)
                    ? missingChallengeItemMessage
                    : missingMessage);
            onRequirementsStillMissing?.Invoke();
            return false;
        }

        onChallengeRequested?.Invoke();
        return needleChallenge.StartChallenge();
    }

    public bool TryUnlockGate()
    {
        CacheMissingReferences();
        if (IsGateOpen)
        {
            return true;
        }

        if (requireNeedleChallengeCompletion
            && (needleChallenge == null || !needleChallenge.IsCompleted))
        {
            return false;
        }

        string missingMessage;
        if (!AreItemRequirementsSatisfied(gateItemRequirements, out missingMessage)
            || !AreExternalRequirementsSatisfied(out missingMessage))
        {
            messageUI?.ShowMessage(
                string.IsNullOrWhiteSpace(missingMessage)
                    ? pendingRequirementMessage
                    : missingMessage);
            onRequirementsStillMissing?.Invoke();
            return false;
        }

        IsGateOpen = true;
        ApplyGateState();
        messageUI?.ShowMessage(gateOpenedMessage);
        onGateOpened?.Invoke();
        return true;
    }

    public void SetExternalRequirementSatisfied(string conditionName)
    {
        if (string.IsNullOrWhiteSpace(conditionName)
            || externalRequirements == null)
        {
            return;
        }

        foreach (ExternalRequirement requirement in externalRequirements)
        {
            if (requirement != null
                && string.Equals(
                    requirement.conditionName,
                    conditionName,
                    StringComparison.OrdinalIgnoreCase))
            {
                requirement.satisfied = true;
            }
        }

        if (needleChallenge != null && needleChallenge.IsCompleted)
        {
            TryUnlockGate();
        }
    }

    public void ResetGate()
    {
        IsGateOpen = false;
        ApplyGateState();
        needleChallenge?.ResetChallenge();
    }

    void HandleNeedleChallengeCompleted()
    {
        TryUnlockGate();
    }

    bool AreItemRequirementsSatisfied(
        ItemRequirement[] requirements,
        out string missingMessage)
    {
        missingMessage = string.Empty;
        if (requirements == null)
        {
            return true;
        }

        foreach (ItemRequirement requirement in requirements)
        {
            if (requirement == null
                || !requirement.required
                || string.IsNullOrWhiteSpace(requirement.itemName))
            {
                continue;
            }

            if (itemInventory != null
                && itemInventory.HasItem(requirement.itemName))
            {
                continue;
            }

            missingMessage = requirement.missingMessage;
            return false;
        }

        return true;
    }

    bool AreExternalRequirementsSatisfied(out string missingMessage)
    {
        missingMessage = string.Empty;
        if (externalRequirements == null)
        {
            return true;
        }

        foreach (ExternalRequirement requirement in externalRequirements)
        {
            if (requirement == null
                || !requirement.required
                || requirement.satisfied)
            {
                continue;
            }

            missingMessage = requirement.missingMessage;
            return false;
        }

        return true;
    }

    bool IsPlayerNearGate()
    {
        if (gateBlocker != null
            && playerCollider != null
            && gateBlocker.enabled
            && playerCollider.enabled)
        {
            ColliderDistance2D distance = gateBlocker.Distance(playerCollider);
            if (distance.isOverlapped
                || distance.distance <= Mathf.Max(0f, interactionPadding))
            {
                return true;
            }
        }

        return player != null
            && Vector2.Distance(player.position, transform.position)
                <= Mathf.Max(0f, fallbackInteractionDistance);
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

        if (playerCollider == null && player != null)
        {
            playerCollider = player.GetComponentInChildren<Collider2D>(true);
        }

        if (gateBlocker == null)
        {
            gateBlocker = GetComponent<Collider2D>();
        }

        if (itemInventory == null)
        {
            itemInventory =
                FindAnyObjectByType<PenzaiSearchController2D>(
                    FindObjectsInactive.Include);
        }

        if (needleChallenge == null)
        {
            needleChallenge = GetComponent<PrecisionNeedleChallenge2D>();
        }

        if (messageUI == null)
        {
            messageUI =
                FindAnyObjectByType<GameplayMessageUI2D>(
                    FindObjectsInactive.Include);
        }

        if (gateVisuals == null || gateVisuals.Length == 0)
        {
            gateVisuals = GetComponentsInChildren<Renderer>(true);
        }
    }

    void ApplyGateState()
    {
        if (gateBlocker != null && disableGateBlockerWhenUnlocked)
        {
            gateBlocker.enabled = !IsGateOpen;
        }

        if (!hideGateVisualsWhenUnlocked || gateVisuals == null)
        {
            return;
        }

        foreach (Renderer gateVisual in gateVisuals)
        {
            if (gateVisual != null)
            {
                gateVisual.enabled = !IsGateOpen;
            }
        }
    }

    bool WasInteractionPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard[interactionKey].wasPressedThisFrame)
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
}
