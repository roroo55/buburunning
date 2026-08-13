using UnityEngine;

[DisallowMultipleComponent]
public class HandPromptTrigger2D : MonoBehaviour
{
    public string playerObjectName = BubuRunningGame.PlayerRootName;
    public Transform player;
    public Collider2D playerCollider;
    public GameObject handObject;
    [Min(0f)]
    public float triggerRadius = 0.6f;
    [Tooltip("在世界坐标中微调靠近判定中心，不会移动花盆或手形提示。")]
    public Vector2 triggerCenterOffset;
    public bool hideHandOnStart = true;
    public bool useObjectBounds = true;
    public bool requirePlayerTouch;

    Collider2D targetCollider;
    Renderer targetRenderer;
    bool handVisible;

    void Awake()
    {
        CacheReferences();

        if (hideHandOnStart)
        {
            SetHandVisible(false);
        }
    }

    void OnEnable()
    {
        CacheReferences();

        if (hideHandOnStart)
        {
            SetHandVisible(false);
        }
    }

    void Update()
    {
        RefreshHandVisibility();
    }

    public bool RefreshHandVisibility()
    {
        CacheReferences();
        bool shouldShow = IsPlayerNear();
        if (shouldShow != handVisible)
        {
            SetHandVisible(shouldShow);
        }

        return shouldShow;
    }

    public void SetHandVisible(bool visible)
    {
        handVisible = visible;

        if (handObject == null)
        {
            return;
        }

        if (handObject.activeSelf != visible)
        {
            handObject.SetActive(visible);
        }
    }

    bool IsPlayerNear()
    {
        if (player == null || handObject == null || !gameObject.activeInHierarchy)
        {
            return false;
        }

        float radius = Mathf.Max(0f, triggerRadius);
        if (requirePlayerTouch)
        {
            return IsPlayerTouchingTarget();
        }

        Vector2 playerPosition = player.position;
        Vector2 closestPoint = GetClosestPoint(playerPosition);
        return (closestPoint - playerPosition).sqrMagnitude <= radius * radius;
    }

    bool IsPlayerTouchingTarget()
    {
        if (player == null)
        {
            return false;
        }

        if (playerCollider != null && playerCollider.enabled)
        {
            if (targetCollider != null && targetCollider.enabled)
            {
                return targetCollider.Distance(playerCollider).isOverlapped;
            }

            if (targetRenderer != null)
            {
                return targetRenderer.bounds.Intersects(playerCollider.bounds);
            }
        }

        Vector2 playerPosition = player.position;
        if (targetCollider != null && targetCollider.enabled)
        {
            return targetCollider.OverlapPoint(playerPosition);
        }

        return targetRenderer != null && targetRenderer.bounds.Contains(player.position);
    }

    Vector2 GetClosestPoint(Vector2 playerPosition)
    {
        if (useObjectBounds)
        {
            if (targetCollider != null && targetCollider.enabled)
            {
                return targetCollider.ClosestPoint(playerPosition);
            }

            if (targetRenderer != null)
            {
                Bounds bounds = targetRenderer.bounds;
                return new Vector2(
                    Mathf.Clamp(playerPosition.x, bounds.min.x, bounds.max.x),
                    Mathf.Clamp(playerPosition.y, bounds.min.y, bounds.max.y));
            }
        }

        return (Vector2)transform.position + triggerCenterOffset;
    }

    void CacheReferences()
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

        if (targetCollider == null)
        {
            targetCollider = GetComponent<Collider2D>();
        }

        if (targetRenderer == null)
        {
            targetRenderer = GetComponentInChildren<Renderer>(true);
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
        Gizmos.color = new Color(1f, 0.78f, 0.15f, 0.35f);
        if (requirePlayerTouch && targetCollider != null)
        {
            Gizmos.DrawWireCube(targetCollider.bounds.center, targetCollider.bounds.size);
        }
        else
        {
            Gizmos.DrawWireSphere(
                (Vector2)transform.position + triggerCenterOffset,
                Mathf.Max(0f, triggerRadius));
        }
    }
}
