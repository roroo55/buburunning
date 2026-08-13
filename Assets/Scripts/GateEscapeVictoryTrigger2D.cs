using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class GateEscapeVictoryTrigger2D : MonoBehaviour
{
    public string playerObjectName = BubuRunningGame.PlayerRootName;
    public bool requireGateOpen = true;
    public bool triggerOnlyOnce = true;
    public GateNeedleChallengeController2D gateController;
    public VictoryPresentation2D victoryPresentation;
    public UnityEvent onPlayerEscaped = new UnityEvent();

    bool triggered;

    void Awake()
    {
        CacheReferences();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryTriggerVictory(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        TryTriggerVictory(other);
    }

    public bool TryTriggerVictory(Collider2D other)
    {
        if ((triggerOnlyOnce && triggered)
            || other == null
            || !IsPlayer(other))
        {
            return false;
        }

        CacheReferences();
        if (requireGateOpen
            && (gateController == null || !gateController.IsGateOpen))
        {
            return false;
        }

        if (victoryPresentation == null
            || !victoryPresentation.TriggerVictory())
        {
            return false;
        }

        triggered = true;
        onPlayerEscaped?.Invoke();
        return true;
    }

    void CacheReferences()
    {
        if (gateController == null)
        {
            gateController =
                FindAnyObjectByType<GateNeedleChallengeController2D>(
                    FindObjectsInactive.Include);
        }

        if (victoryPresentation == null)
        {
            victoryPresentation =
                FindAnyObjectByType<VictoryPresentation2D>(
                    FindObjectsInactive.Include);
        }
    }

    bool IsPlayer(Collider2D other)
    {
        Transform current = other.transform;
        while (current != null)
        {
            if (current.name == playerObjectName)
            {
                return true;
            }

            current = current.parent;
        }

        Rigidbody2D body = other.attachedRigidbody;
        return body != null && body.gameObject.name == playerObjectName;
    }
}
