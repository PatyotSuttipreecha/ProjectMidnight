using UnityEngine;
using UnityEngine.Events;

public class DoorController : MonoBehaviour, IWorldInteractable
{
    [System.Serializable]
    public class DoorLeaf
    {
        public Transform hinge;
        public Vector3 openRotation = new Vector3(0, 90, 0);
    }
    [Tooltip("When populated, these leaves replace the single Hinge settings below.")]
    public DoorLeaf[] leaves = new DoorLeaf[0];
    public string doorName = "Door";
    [Tooltip("Pivot at the hinge; mesh and solid collider must be children of this transform.")]
    public Transform hinge;
    public bool startsLocked;
    public ItemSO requiredKey;
    public bool consumeKey;
    public bool openAfterUnlock = true;
    public bool startsOpen;
    public Vector3 openRotation = new Vector3(0, 90, 0);
    [Min(.05f)] public float duration = .8f;
    [Min(.1f)] public float interactionDistance = 2.5f;

    public AudioSource audioSource;
    public AudioClip openSound, closeSound, unlockSound, lockedSound;
    public UnityEvent onUnlocked = new UnityEvent();
    public UnityEvent onOpened = new UnityEvent();
    public UnityEvent onClosed = new UnityEvent();
    public bool IsLocked { get; private set; }
    public bool IsOpen { get; private set; }
    private DoorLeaf[] activeLeaves;
    private Quaternion[] closedRotations;
    public bool HasValidHinges
    {
        get
        {
            if (leaves == null || leaves.Length == 0) return hinge != null;
            var seen = new System.Collections.Generic.HashSet<Transform>();
            foreach (var leaf in leaves)
                if (leaf == null || leaf.hinge == null || !seen.Add(leaf.hinge)) return false;
            return true;
        }
    }
    private float progress;
    private string feedback;
    private float feedbackUntil;

    private void Awake()
    {
        IsLocked = startsLocked;
        IsOpen = startsOpen && !IsLocked;
        if (!HasValidHinges) { Debug.LogError("Assign a unique hinge for every door leaf.", this); enabled = false; return; }
        activeLeaves = leaves != null && leaves.Length > 0 ? leaves : new[] { new DoorLeaf { hinge = hinge, openRotation = openRotation } };
        closedRotations = new Quaternion[activeLeaves.Length];
        for (int i = 0; i < activeLeaves.Length; i++) closedRotations[i] = activeLeaves[i].hinge.localRotation;
        progress = IsOpen ? 1 : 0;
        ApplyPose();
    }
    private void Update()
    {
        progress = Mathf.MoveTowards(progress, IsOpen ? 1 : 0, Time.deltaTime / Mathf.Max(.05f, duration));
        ApplyPose();

    }
    private void ApplyPose()
    {
        for (int i = 0; i < activeLeaves.Length; i++)
            if (activeLeaves[i].hinge != null)
                activeLeaves[i].hinge.localRotation = Quaternion.Slerp(closedRotations[i],
                    closedRotations[i] * Quaternion.Euler(activeLeaves[i].openRotation), Mathf.SmoothStep(0, 1, progress));
    }

    public bool TryInteract(InventoryManager inventory)
    {
        if (!HasValidHinges) return false;
        if (IsLocked)
        {
            if (inventory == null || !inventory.TryUseKey(requiredKey, consumeKey))
            {
                feedback = requiredKey != null ? "Requires: " + requiredKey.itemName : "Locked";
                feedbackUntil = Time.time + 2; Play(lockedSound); return false;
            }
            IsLocked = false;
            feedback = "Unlocked"; feedbackUntil = Time.time + 2;
            Play(unlockSound); onUnlocked.Invoke();
            if (!openAfterUnlock) return true;
        }
        IsOpen = !IsOpen;
        Play(IsOpen ? openSound : closeSound);
        if (IsOpen) onOpened.Invoke(); else onClosed.Invoke();
        return true;
    }
    private void Play(AudioClip clip) { if (audioSource != null && clip != null) audioSource.PlayOneShot(clip); }
    private void OnEnable() => PlayerInteraction.Register(this);
    private void OnDisable() => PlayerInteraction.Unregister(this);
    public MonoBehaviour InteractionOwner => this;
    public Vector3 InteractionPoint => hinge != null ? hinge.position + Vector3.up : transform.position + Vector3.up;
    public float InteractionRange => interactionDistance;
    public bool RequiresAim => true;
    public bool InteractionAvailable => HasValidHinges;
    public string InteractionPrompt => (IsLocked ? "Unlock " : IsOpen ? "Close " : "Open ") + doorName +
        (IsLocked && requiredKey != null ? " | Requires: " + requiredKey.itemName : "");
    public string Interact(PlayerController player)
    {
        if (!TryInteract(InventoryManager.Instance)) return requiredKey != null ? "Requires: " + requiredKey.itemName : "Locked";
        return IsOpen ? "Opened " + doorName : "Closed / unlocked " + doorName;
    }
    private void OnDrawGizmosSelected()
    {
        var pivot = hinge != null ? hinge : transform;
        Gizmos.color = startsLocked ? new Color(1, .45f, .15f) : Color.cyan;
        Gizmos.DrawWireSphere(pivot.position, interactionDistance);
        if (leaves != null && leaves.Length > 0)
        {
            foreach (var leaf in leaves) if (leaf != null && leaf.hinge != null) DrawLeafGizmo(leaf.hinge, leaf.openRotation);
        }
        else DrawLeafGizmo(pivot, openRotation);
    }
    private static void DrawLeafGizmo(Transform pivot, Vector3 rotation)
    {
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(pivot.position, .06f);
        Gizmos.DrawLine(pivot.position, pivot.position + pivot.rotation * Quaternion.Euler(rotation) * Vector3.right * 1.2f);
    }
}
