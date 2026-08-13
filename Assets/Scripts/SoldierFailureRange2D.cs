using UnityEngine;

[DisallowMultipleComponent]
public class SoldierFailureRange2D : MonoBehaviour
{
    public PatrollingSoldier2D owner;

    void Awake()
    {
        if (owner == null)
        {
            owner = GetComponentInParent<PatrollingSoldier2D>();
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        owner?.RestartIfPlayer(other, other.attachedRigidbody);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        owner?.RestartIfPlayer(other, other.attachedRigidbody);
    }
}
