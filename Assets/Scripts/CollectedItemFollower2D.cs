using UnityEngine;

[DisallowMultipleComponent]
public class CollectedItemFollower2D : MonoBehaviour
{
    [Header("Follow Target")]
    public string targetObjectName = BubuRunningGame.PlayerRootName;
    public Transform target;

    [Header("Follow Motion")]
    [Tooltip("X 是玩家朝右移动时的相对位置；玩家朝左移动时会自动镜像。")]
    public Vector3 followOffset = new Vector3(-1.1f, -0.15f, 0f);

    [Min(0.01f)]
    public float smoothTime = 0.18f;

    [Min(0f)]
    public float maximumFollowSpeed = 20f;

    [Min(0f)]
    [Tooltip("玩家瞬移超过该距离时，物品直接回到玩家身后，不会慢慢穿过整张地图。")]
    public float teleportSnapDistance = 5f;

    [Min(0f)]
    public float horizontalDirectionThreshold = 0.01f;

    [Header("Collected Object")]
    public bool detachFromSearchPointOnCollect = true;
    public bool enableRenderersOnCollect = true;
    public bool disableCollidersWhileFollowing = true;
    public bool overrideSortingOrder = true;
    public int sortingOrderWhileFollowing = 9;

    public bool IsFollowing { get; private set; }

    Vector3 followVelocity;
    Vector3 previousTargetPosition;
    float horizontalDirection = 1f;

    void Awake()
    {
        CacheTarget();
        if (target != null)
        {
            previousTargetPosition = target.position;
        }
    }

    void LateUpdate()
    {
        if (!IsFollowing)
        {
            return;
        }

        CacheTarget();
        if (target == null)
        {
            return;
        }

        Vector3 targetMovement = target.position - previousTargetPosition;
        if (Mathf.Abs(targetMovement.x) >= horizontalDirectionThreshold)
        {
            horizontalDirection = Mathf.Sign(targetMovement.x);
        }

        Vector3 desiredPosition = GetDesiredPosition();
        float snapDistance = Mathf.Max(0f, teleportSnapDistance);
        if (snapDistance > 0f
            && Vector3.Distance(transform.position, desiredPosition) >= snapDistance)
        {
            transform.position = desiredPosition;
            followVelocity = Vector3.zero;
        }
        else
        {
            transform.position =
                Vector3.SmoothDamp(
                    transform.position,
                    desiredPosition,
                    ref followVelocity,
                    Mathf.Max(0.01f, smoothTime),
                    maximumFollowSpeed <= 0f
                        ? Mathf.Infinity
                        : maximumFollowSpeed,
                    Time.deltaTime);
        }

        previousTargetPosition = target.position;
    }

    public void BeginFollowing()
    {
        CacheTarget();
        if (target == null)
        {
            Debug.LogWarning(
                "Collected item follower could not find target '"
                + targetObjectName
                + "'.");
            return;
        }

        if (detachFromSearchPointOnCollect)
        {
            transform.SetParent(null, true);
        }

        ConfigureCollectedObject();
        previousTargetPosition = target.position;
        transform.position = GetDesiredPosition();
        followVelocity = Vector3.zero;
        IsFollowing = true;
    }

    public void StopFollowing()
    {
        IsFollowing = false;
        followVelocity = Vector3.zero;
    }

    void CacheTarget()
    {
        if (target != null || string.IsNullOrWhiteSpace(targetObjectName))
        {
            return;
        }

        GameObject[] sceneObjects =
            FindObjectsByType<GameObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        foreach (GameObject sceneObject in sceneObjects)
        {
            if (sceneObject != null && sceneObject.name == targetObjectName)
            {
                target = sceneObject.transform;
                return;
            }
        }
    }

    Vector3 GetDesiredPosition()
    {
        Vector3 mirroredOffset = followOffset;
        mirroredOffset.x = followOffset.x * horizontalDirection;
        return target.position + mirroredOffset;
    }

    void ConfigureCollectedObject()
    {
        foreach (Renderer itemRenderer in GetComponentsInChildren<Renderer>(true))
        {
            if (enableRenderersOnCollect)
            {
                itemRenderer.enabled = true;
            }

            if (overrideSortingOrder)
            {
                itemRenderer.sortingOrder = sortingOrderWhileFollowing;
            }
        }

        if (!disableCollidersWhileFollowing)
        {
            return;
        }

        foreach (Collider2D itemCollider in GetComponentsInChildren<Collider2D>(true))
        {
            itemCollider.enabled = false;
        }

        foreach (Collider itemCollider in GetComponentsInChildren<Collider>(true))
        {
            itemCollider.enabled = false;
        }
    }
}
