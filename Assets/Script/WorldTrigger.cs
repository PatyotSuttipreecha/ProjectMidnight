using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
[RequireComponent(typeof(PointNote))]
[AddComponentMenu("Gameplay/World Trigger")]
public class WorldTrigger : MonoBehaviour
{
    [Header("Activation")]
    public bool armed = true;
    public bool triggerOnce = true;
    [Min(0)] public float cooldown;
    [Tooltip("Retry while the player stays inside, e.g. when armed by another event. Otherwise activation requires entry.")]
    public bool retryWhileInside;
    public bool allowWhileGameplayBlocked;
    [Header("Events")]
    public UnityEvent onPlayerEnter = new UnityEvent();
    public UnityEvent onTriggered = new UnityEvent();
    public UnityEvent onPlayerExit = new UnityEvent();
    [Header("Scene View")]
    public bool showGizmos = true;
    public Color gizmoColor = new Color(0, .8f, 1, .25f);
    public bool HasTriggered { get; private set; }
    public PlayerController CurrentPlayer { get; private set; }
    private readonly HashSet<Collider> contacts = new HashSet<Collider>();
    private float nextAllowedTime;

    private void Reset()
    {
        ConfigureVolume();
    }
    private void Awake()
    {
        ConfigureVolume();
    }
    private void ConfigureVolume()
    {
        var volume = GetComponent<BoxCollider>(); volume.isTrigger = true;
        var body = GetComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
    }
    private PlayerController ResolvePlayer(Collider other)
    {
        var player = other.GetComponentInParent<PlayerController>();
        return player != null && player.isActiveAndEnabled && player.currentHealth > 0 ? player : null;
    }
    private void OnTriggerEnter(Collider other)
    {
        var player = ResolvePlayer(other);
        if (player == null || CurrentPlayer != null && player != CurrentPlayer || !contacts.Add(other)) return;
        if (contacts.Count != 1) return;
        CurrentPlayer = player;
        onPlayerEnter.Invoke();
        if (isActiveAndEnabled) TryActivate(player);
    }
    private void OnTriggerStay(Collider other)
    {
        if (retryWhileInside && contacts.Contains(other)) TryActivate(CurrentPlayer);
    }
    private void OnTriggerExit(Collider other)
    {
        if (!contacts.Remove(other) || contacts.Count != 0) return;
        CurrentPlayer = null;
        onPlayerExit.Invoke();
    }
    private void Update()
    {
        // Destroyed/disabled player colliders may not send OnTriggerExit.
        if (contacts.Count == 0) return;
        contacts.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
        if (contacts.Count == 0)
        {
            CurrentPlayer = null;
            onPlayerExit.Invoke();
        }
    }
    public bool TryActivate(PlayerController player)
    {
        if (!isActiveAndEnabled || !armed || triggerOnce && HasTriggered || Time.time < nextAllowedTime ||
            player == null || !player.isActiveAndEnabled || player.currentHealth <= 0 ||
            !allowWhileGameplayBlocked && player.IsGameplayInputBlocked) return false;
        HasTriggered = true;
        // Set state before callbacks so recursive UnityEvents cannot activate twice in the same frame.
        nextAllowedTime = Time.time + Mathf.Max(.001f, cooldown);
        onTriggered.Invoke();
        return true;
    }
    public void Activate() => TryActivate(CurrentPlayer);
    public void Arm() => armed = true;
    public void Disarm() => armed = false;
    public void ResetTrigger() { HasTriggered = false; nextAllowedTime = 0; }
    private void OnDisable() { contacts.Clear(); CurrentPlayer = null; }
    private void OnDrawGizmos()
    {
        if (!showGizmos) return;
        var volume = GetComponent<BoxCollider>(); if (volume == null) return;
        var previous = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Color color = !armed ? Color.gray : HasTriggered && triggerOnce ? Color.green : gizmoColor;
        color.a = .12f; Gizmos.color = color; Gizmos.DrawCube(volume.center, volume.size);
        color.a = 1; Gizmos.color = color; Gizmos.DrawWireCube(volume.center, volume.size);
        Gizmos.matrix = previous;
    }
}
