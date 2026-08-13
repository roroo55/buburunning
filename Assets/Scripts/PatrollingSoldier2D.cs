using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class PatrollingSoldier2D : MonoBehaviour
{
    const string FailureRangeName = "Soldier Failure Range";

    public float patrolSpeed = 2.4f;
    public float patrolHighestOffsetY = 2.3f;
    public float patrolLowestOffsetY = -2.3f;
    public bool startMovingUp = true;
    public Vector2 failureRangePadding = new Vector2(0.8f, 0.8f);
    public Vector2 failureRangeOffset = Vector2.zero;

    Vector3 patrolCenter;
    float lockedX;
    int verticalDirection;
    bool restarting;
    BoxCollider2D bodyCollider;
    BoxCollider2D failureRangeCollider;

    void Awake()
    {
        patrolCenter = transform.position;
        lockedX = patrolCenter.x;
        verticalDirection = startMovingUp ? 1 : -1;
        EnsureFailureRange();
        RefreshFailureRange();
        MoveToStartingPatrolEdge();
        ClampCurrentPositionIntoPatrolRange();
    }

    void Update()
    {
        if (restarting)
        {
            return;
        }

        float lowerY = GetLowerY();
        float upperY = GetUpperY();
        Vector3 position = transform.position;
        position.x = lockedX;
        position.y += verticalDirection * patrolSpeed * Time.deltaTime;

        if (position.y >= upperY)
        {
            position.y = upperY;
            verticalDirection = -1;
        }
        else if (position.y <= lowerY)
        {
            position.y = lowerY;
            verticalDirection = 1;
        }

        transform.position = position;
        RefreshFailureRange();
    }

    void OnValidate()
    {
        patrolSpeed = Mathf.Max(0f, patrolSpeed);
        failureRangePadding = new Vector2(Mathf.Max(0f, failureRangePadding.x), Mathf.Max(0f, failureRangePadding.y));

        Transform range = transform.Find(FailureRangeName);
        if (range != null && range.TryGetComponent(out BoxCollider2D rangeCollider))
        {
            failureRangeCollider = rangeCollider;
            RefreshFailureRange();
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        RestartIfPlayer(collision.collider, collision.rigidbody);
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        RestartIfPlayer(collision.collider, collision.rigidbody);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        RestartIfPlayer(other, other.attachedRigidbody);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        RestartIfPlayer(other, other.attachedRigidbody);
    }

    void OnDrawGizmosSelected()
    {
        Vector3 center = Application.isPlaying ? patrolCenter : transform.position;
        float lowerY = center.y + Mathf.Min(patrolLowestOffsetY, patrolHighestOffsetY);
        float upperY = center.y + Mathf.Max(patrolLowestOffsetY, patrolHighestOffsetY);
        Vector3 top = new Vector3(center.x, upperY, center.z);
        Vector3 bottom = new Vector3(center.x, lowerY, center.z);

        Gizmos.color = new Color(0.1f, 0.8f, 1f, 1f);
        Gizmos.DrawLine(bottom, top);
        Gizmos.DrawWireSphere(top, 0.12f);
        Gizmos.DrawWireSphere(bottom, 0.12f);

        Bounds rangeBounds = GetFailureRangeWorldBounds();
        if (rangeBounds.size != Vector3.zero)
        {
            Gizmos.color = new Color(1f, 0.15f, 0.1f, 1f);
            Gizmos.DrawWireCube(rangeBounds.center, rangeBounds.size);
        }
    }

    void ClampCurrentPositionIntoPatrolRange()
    {
        Vector3 position = transform.position;
        position.x = lockedX;
        position.y = Mathf.Clamp(position.y, GetLowerY(), GetUpperY());
        transform.position = position;
    }

    void MoveToStartingPatrolEdge()
    {
        Vector3 position = transform.position;
        position.x = lockedX;
        position.y = startMovingUp ? GetLowerY() : GetUpperY();
        transform.position = position;
    }

    float GetLowerY()
    {
        return patrolCenter.y + Mathf.Min(patrolLowestOffsetY, patrolHighestOffsetY);
    }

    float GetUpperY()
    {
        return patrolCenter.y + Mathf.Max(patrolLowestOffsetY, patrolHighestOffsetY);
    }

    void EnsureFailureRange()
    {
        Transform range = transform.Find(FailureRangeName);
        if (range == null)
        {
            GameObject rangeObject = new GameObject(FailureRangeName);
            rangeObject.transform.SetParent(transform, false);
            range = rangeObject.transform;
        }

        failureRangeCollider = range.GetComponent<BoxCollider2D>();
        if (failureRangeCollider == null)
        {
            failureRangeCollider = range.gameObject.AddComponent<BoxCollider2D>();
        }

        failureRangeCollider.isTrigger = true;

        SoldierFailureRange2D failureRange = range.GetComponent<SoldierFailureRange2D>();
        if (failureRange == null)
        {
            failureRange = range.gameObject.AddComponent<SoldierFailureRange2D>();
        }

        failureRange.owner = this;
    }

    void RefreshFailureRange()
    {
        if (failureRangeCollider == null)
        {
            return;
        }

        bodyCollider = GetComponent<BoxCollider2D>();
        Vector2 baseSize = bodyCollider != null ? bodyCollider.size : Vector2.one;
        Vector2 baseOffset = bodyCollider != null ? bodyCollider.offset : Vector2.zero;
        Vector2 rangeSize = baseSize + failureRangePadding * 2f;

        Transform range = failureRangeCollider.transform;
        range.localRotation = Quaternion.identity;
        range.localScale = Vector3.one;
        range.localPosition = Vector3.zero;

        failureRangeCollider.offset = baseOffset + failureRangeOffset;
        failureRangeCollider.size = new Vector2(Mathf.Max(0.01f, rangeSize.x), Mathf.Max(0.01f, rangeSize.y));
    }

    Bounds GetFailureRangeWorldBounds()
    {
        Transform range = transform.Find(FailureRangeName);
        if (range != null && range.TryGetComponent(out BoxCollider2D rangeCollider))
        {
            return rangeCollider.bounds;
        }

        return new Bounds(Vector3.zero, Vector3.zero);
    }

    public void RestartIfPlayer(Collider2D otherCollider, Rigidbody2D otherRigidbody)
    {
        if (restarting || !IsPlayer(otherCollider, otherRigidbody))
        {
            return;
        }

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

    bool IsPlayer(Collider2D otherCollider, Rigidbody2D otherRigidbody)
    {
        if (otherCollider != null && IsPlayerTransform(otherCollider.transform))
        {
            return true;
        }

        return otherRigidbody != null && IsPlayerTransform(otherRigidbody.transform);
    }

    bool IsPlayerTransform(Transform target)
    {
        while (target != null)
        {
            if (target.name == BubuRunningGame.PlayerRootName || target.name == BubuRunningGame.LegacyPlayerRootName)
            {
                return true;
            }

            target = target.parent;
        }

        return false;
    }
}
