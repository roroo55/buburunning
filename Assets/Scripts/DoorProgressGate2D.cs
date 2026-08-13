using UnityEngine;

[DisallowMultipleComponent]
public class DoorProgressGate2D : MonoBehaviour
{
    public bool blocksPlayerProgress = true;
    public bool puzzleSolved = false;
    public bool disableColliderWhenSolved = true;
    public float stopPadding = 0.02f;

    [Header("Door Open Audio")]
    public AudioClip openDoorAudioClip;
    public AudioSource openDoorAudioSource;
    [Range(0f, 1f)]
    public float openDoorAudioVolume = 1f;
    public bool playOpenAudioOnlyOnce = true;

    public bool IsBlocking => blocksPlayerProgress && !puzzleSolved;
    public int OpenDoorAudioPlayCount { get; private set; }

    bool previousBlockingState;
    bool stateInitialized;
    bool openAudioPlayed;

    void Awake()
    {
        previousBlockingState = IsBlocking;
        stateInitialized = true;
        ApplyColliderState();
    }

    void Update()
    {
        bool currentlyBlocking = IsBlocking;
        ApplyColliderState();
        if (stateInitialized
            && previousBlockingState
            && !currentlyBlocking)
        {
            PlayOpenDoorAudio();
        }

        previousBlockingState = currentlyBlocking;
    }

    public void PlayOpenDoorAudio()
    {
        if (openDoorAudioClip == null
            || (playOpenAudioOnlyOnce && openAudioPlayed))
        {
            return;
        }

        if (openDoorAudioSource == null)
        {
            openDoorAudioSource = GetComponent<AudioSource>();
        }

        if (openDoorAudioSource == null)
        {
            openDoorAudioSource = gameObject.AddComponent<AudioSource>();
            openDoorAudioSource.playOnAwake = false;
            openDoorAudioSource.loop = false;
            openDoorAudioSource.spatialBlend = 0f;
        }

        openDoorAudioSource.PlayOneShot(
            openDoorAudioClip,
            Mathf.Clamp01(openDoorAudioVolume));
        openAudioPlayed = true;
        OpenDoorAudioPlayCount++;
    }

    public void SetPuzzleSolved(bool solved)
    {
        bool wasBlocking = IsBlocking;
        puzzleSolved = solved;
        bool currentlyBlocking = IsBlocking;
        ApplyColliderState();
        if (wasBlocking && !currentlyBlocking)
        {
            PlayOpenDoorAudio();
        }

        previousBlockingState = currentlyBlocking;
        stateInitialized = true;
    }

    public float GetBlockedPlayerCenterMaxX(float playerRightOffset)
    {
        return GetDoorLeftEdgeX() - Mathf.Max(0f, playerRightOffset) - Mathf.Max(0f, stopPadding);
    }

    public float GetDoorLeftEdgeX()
    {
        Collider2D doorCollider = GetComponent<Collider2D>();
        if (doorCollider != null)
        {
            return doorCollider.bounds.min.x;
        }

        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            return spriteRenderer.bounds.min.x;
        }

        return transform.position.x;
    }

    void OnValidate()
    {
        stopPadding = Mathf.Max(0f, stopPadding);
        ApplyColliderState();
    }

    void ApplyColliderState()
    {
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
        foreach (Collider2D doorCollider in colliders)
        {
            if (doorCollider == null)
            {
                continue;
            }

            if (IsBlocking)
            {
                doorCollider.enabled = true;
                doorCollider.isTrigger = false;
            }
            else if (disableColliderWhenSolved)
            {
                doorCollider.enabled = false;
            }
            else
            {
                doorCollider.enabled = true;
                doorCollider.isTrigger = true;
            }
        }
    }
}
