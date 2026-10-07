using System.Collections.Generic;
using UnityEngine;
using TMPro;

public interface IWorldInteractable
{
    MonoBehaviour InteractionOwner { get; }
    Vector3 InteractionPoint { get; }
    float InteractionRange { get; }
    bool RequiresAim { get; }
    bool InteractionAvailable { get; }
    string InteractionPrompt { get; }
    string Interact(PlayerController player);
}

public class PlayerInteraction : MonoBehaviour
{
    public KeyCode interactKey = KeyCode.F;
    [Min(.1f)] public float maxDistance = 3f;
    [Tooltip("Optional UI text. Empty uses a screen prompt.")]
    public TMP_Text promptText;
    public Camera interactionCamera;
    public LayerMask interactionMask = ~0;
    public bool showGizmos = true;
    private static readonly HashSet<IWorldInteractable> targets = new HashSet<IWorldInteractable>();
    private PlayerController player;
    private IWorldInteractable selected;
    private string feedback;
    private float feedbackUntil;
    public static void Register(IWorldInteractable target) => targets.Add(target);
    public static void Unregister(IWorldInteractable target) => targets.Remove(target);
    private void Awake() => player = GetComponent<PlayerController>();
    private void OnDisable() { selected = null; if (promptText != null) promptText.gameObject.SetActive(false); }
    private bool Allowed => player != null && player.isActiveAndEnabled && player.currentHealth > 0 && !player.IsGameplayInputBlocked && Time.timeScale > 0;
    private void Update()
    {
        selected = Allowed ? SelectTarget() : null;
        if (selected != null && Input.GetKeyDown(interactKey))
        {
            TryInteractSelected();
            // Keep the resulting message even when the collected object is disabled/destroyed.
            selected = SelectTarget();
        }
        if (promptText != null)
        {
            string message = Message;
            promptText.gameObject.SetActive(Allowed && !string.IsNullOrEmpty(message));
            promptText.text = message;
        }
    }
    public bool TryInteractSelected()
    {
        if (!Allowed) return false;
        selected = SelectTarget();
        if (selected == null) return false;
        feedback = selected.Interact(player);
        feedbackUntil = Time.time + 2;
        return true;
    }
    private string Message => Time.time < feedbackUntil && !string.IsNullOrEmpty(feedback) ? feedback :
        selected != null ? "[" + interactKey + "] " + selected.InteractionPrompt : "";
    private IWorldInteractable SelectTarget()
    {
        var camera = interactionCamera != null ? interactionCamera : Camera.main;
        if (camera == null) return null;
        var ray = camera.ViewportPointToRay(new Vector3(.5f, .5f));
        var aimHits = Physics.RaycastAll(ray, 100, interactionMask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(aimHits, (a, b) => a.distance.CompareTo(b.distance));
        IWorldInteractable aimed = null;
        Vector3 aimPoint = Vector3.zero;
        foreach (var hit in aimHits)
        {
            if (hit.collider.transform.IsChildOf(transform)) continue;
            foreach (var behaviour in hit.collider.GetComponentsInParent<MonoBehaviour>())
                if (behaviour is IWorldInteractable target) { aimed = target; aimPoint = hit.point; break; }
            break;
        }
        IWorldInteractable best = null; float bestScore = float.PositiveInfinity;
        foreach (var target in targets)
        {
            var owner = target.InteractionOwner;
            if (owner == null || !owner.isActiveAndEnabled || !target.InteractionAvailable) continue;
            if (target.RequiresAim && target != aimed) continue;
            Vector3 point = target == aimed ? aimPoint : target.InteractionPoint;
            float distance = Vector3.Distance(transform.position, point);
            if (distance > Mathf.Min(maxDistance, target.InteractionRange)) continue;
            Vector3 screen = camera.WorldToViewportPoint(point);
            if (screen.z <= 0 || screen.x < 0 || screen.x > 1 || screen.y < 0 || screen.y > 1) continue;
            if (Blocked(camera.transform.position, point, owner.transform)) continue;
            float score = target == aimed ? -1 : new Vector2(screen.x - .5f, screen.y - .5f).sqrMagnitude * 10 + distance * .1f;
            if (score < bestScore || (score == bestScore && best != null && owner.GetInstanceID() < best.InteractionOwner.GetInstanceID()))
            { best = target; bestScore = score; }
        }
        return best;
    }
    private bool Blocked(Vector3 origin, Vector3 point, Transform target)
    {
        Vector3 direction = point - origin; float distance = direction.magnitude;
        foreach (var hit in Physics.RaycastAll(origin, direction.normalized, distance, interactionMask, QueryTriggerInteraction.Ignore))
            if (!hit.collider.transform.IsChildOf(transform) && !hit.collider.transform.IsChildOf(target) && hit.distance < distance - .08f) return true;
        return false;
    }
    private void OnGUI()
    {
        if (promptText != null || !Allowed || string.IsNullOrEmpty(Message)) return;
        var style = new GUIStyle(GUI.skin.box) { fontSize = 16, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        GUI.Box(new Rect(Screen.width * .5f - 210, Screen.height * .8f, 420, 64), Message, style);
    }
    private void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;
        Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(transform.position, maxDistance);
        if (Application.isPlaying && selected != null && selected.InteractionOwner != null)
        { Gizmos.color = Color.green; Gizmos.DrawLine(transform.position + Vector3.up, selected.InteractionPoint); }
    }
}
