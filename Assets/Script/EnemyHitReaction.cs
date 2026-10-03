using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Animation keeps ownership of the visible skeleton. A separate jointed rig receives impulses.
[DefaultExecutionOrder(500)]
public class EnemyHitReaction : MonoBehaviour
{
    private sealed class Bone
    {
        public Rigidbody Source;
        public Rigidbody Physics;
        public Pose AnimatedPose;
        public Vector3 TargetVelocity;
        public float EndTime;
        public float FadeTime;
    }
    public bool IsReacting { get; private set; }
    private Animator animator;
    private GameObject physicsRoot;
    private readonly List<Bone> bones = new List<Bone>();
    private readonly Dictionary<Rigidbody, Bone> lookup = new Dictionary<Rigidbody, Bone>();
    private float supportStrength = 30f;
    private float visualBlend = 0.75f;
    private float maxAngle = 25f;

    public void Play(Animator animation, Rigidbody target, Vector3 point, Vector3 impulse,
        float physicsDuration, float blendDuration, float blend = 0.75f, float support = 30f, float angleLimit = 25f)
    {
        if (animation == null || !animation.enabled || target == null) return;
        if (animator != animation || physicsRoot == null) Build(animation);
        if (!lookup.TryGetValue(target, out Bone struck)) return;
        supportStrength = Mathf.Max(0f, support);
        visualBlend = Mathf.Clamp01(blend);
        maxAngle = Mathf.Max(0f, angleLimit);
        foreach (Bone bone in bones)
        {
            if (bone.Source == null || (bone.Source != target && !bone.Source.transform.IsChildOf(target.transform))) continue;
            if (bone.Physics.isKinematic)
            {
                bone.Physics.position = bone.AnimatedPose.position;
                bone.Physics.rotation = bone.AnimatedPose.rotation;
                bone.Physics.isKinematic = false;
                bone.Physics.linearVelocity = bone.TargetVelocity;
                bone.Physics.angularVelocity = Vector3.zero;
            }
            bone.EndTime = Time.time + Mathf.Max(0.02f, physicsDuration) + Mathf.Max(0.01f, blendDuration);
            bone.FadeTime = Mathf.Max(0.01f, blendDuration);
        }
        struck.Physics.AddForceAtPosition(impulse, point, ForceMode.Impulse);
        IsReacting = true;
    }

    private void Build(Animator source)
    {
        Cancel();
        if (physicsRoot != null) Destroy(physicsRoot);
        bones.Clear();
        lookup.Clear();
        animator = source;
        // Animated targets must keep updating even when the renderer leaves the camera view.
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        physicsRoot = new GameObject("Hit Reaction Physics") { hideFlags = HideFlags.HideAndDontSave };
        SceneManager.MoveGameObjectToScene(physicsRoot, gameObject.scene);
        foreach (Rigidbody sourceBody in animator.GetComponentsInChildren<Rigidbody>())
        {
            if (sourceBody.transform == transform) continue;
            GameObject proxy = new GameObject(sourceBody.name + " Physics");
            proxy.transform.SetParent(physicsRoot.transform, false);
            proxy.transform.SetPositionAndRotation(sourceBody.position, sourceBody.rotation);
            Rigidbody body = proxy.AddComponent<Rigidbody>();
            body.mass = sourceBody.mass;
            body.useGravity = false;
            body.isKinematic = true;
            body.linearDamping = 2f;
            body.angularDamping = 4f;
            body.maxAngularVelocity = 8f;
            body.solverIterations = 12;
            // No proxy colliders: bullets and world collisions use the original visible hitboxes.
            body.automaticInertiaTensor = false;
            body.inertiaTensor = sourceBody.inertiaTensor;
            body.inertiaTensorRotation = sourceBody.inertiaTensorRotation;
            Bone bone = new Bone { Source = sourceBody, Physics = body,
                AnimatedPose = new Pose(sourceBody.transform.position, sourceBody.transform.rotation) };
            bones.Add(bone);
            lookup[sourceBody] = bone;
        }
        foreach (Bone bone in bones)
        {
            Joint original = bone.Source.GetComponent<Joint>();
            if (original == null || original.connectedBody == null || !lookup.TryGetValue(original.connectedBody, out Bone parent)) continue;
            CharacterJoint joint = bone.Physics.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = parent.Physics;
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = Vector3.Scale(original.anchor, bone.Source.transform.lossyScale);
            joint.connectedAnchor = Vector3.Scale(original.connectedAnchor, parent.Source.transform.lossyScale);
            joint.axis = original.axis;
            joint.enableProjection = true;
            joint.enablePreprocessing = false;
            if (original is CharacterJoint character)
            {
                joint.swingAxis = character.swingAxis;
                joint.lowTwistLimit = character.lowTwistLimit;
                joint.highTwistLimit = character.highTwistLimit;
                joint.swing1Limit = character.swing1Limit;
                joint.swing2Limit = character.swing2Limit;
            }
        }
    }

    private void FixedUpdate()
    {
        foreach (Bone bone in bones)
        {
            if (bone.Source == null || bone.Physics == null || bone.Physics.isKinematic) continue;
            Vector3 acceleration = (bone.AnimatedPose.position - bone.Physics.position) * supportStrength
                + (bone.TargetVelocity - bone.Physics.linearVelocity) * 6f;
            bone.Physics.AddForce(Vector3.ClampMagnitude(acceleration, 40f), ForceMode.Acceleration);
            Quaternion error = bone.AnimatedPose.rotation * Quaternion.Inverse(bone.Physics.rotation);
            if (error.w < 0f) error = new Quaternion(-error.x, -error.y, -error.z, -error.w);
            error.ToAngleAxis(out float angle, out Vector3 axis);
            if (axis.sqrMagnitude < 0.001f || float.IsNaN(axis.x)) continue;
            Vector3 torque = axis.normalized * (Mathf.DeltaAngle(0f, angle) * Mathf.Deg2Rad * supportStrength)
                - bone.Physics.angularVelocity * 6f;
            bone.Physics.AddTorque(Vector3.ClampMagnitude(torque, 40f), ForceMode.Acceleration);
        }
    }

    private void LateUpdate()
    {
        if (animator == null || !animator.enabled) return;
        // Capture every animation target before applying any physical rotation to its parents.
        foreach (Bone bone in bones)
        {
            if (bone.Source == null) continue;
            Transform visible = bone.Source.transform;
            bone.TargetVelocity = Time.deltaTime > 0f
                ? Vector3.ClampMagnitude((visible.position - bone.AnimatedPose.position) / Time.deltaTime, 10f) : Vector3.zero;
            bone.AnimatedPose = new Pose(visible.position, visible.rotation);
        }
        IsReacting = false;
        foreach (Bone bone in bones)
        {
            if (bone.Source == null || bone.Physics == null) continue;
            float remaining = bone.EndTime - Time.time;
            if (remaining <= 0f)
            {
                bone.Physics.isKinematic = true;
                bone.Physics.position = bone.AnimatedPose.position;
                bone.Physics.rotation = bone.AnimatedPose.rotation;
                continue;
            }
            IsReacting = true;
            float weight = Mathf.SmoothStep(0f, 1f, remaining / bone.FadeTime) * visualBlend;
            Quaternion physical = Quaternion.RotateTowards(bone.AnimatedPose.rotation, bone.Physics.rotation, maxAngle);
            // Keep animated bone positions and lengths intact; only blend the struck chain's rotations.
            bone.Source.transform.rotation = Quaternion.Slerp(bone.AnimatedPose.rotation, physical, weight);
        }
    }

    public void Cancel()
    {
        foreach (Bone bone in bones)
        {
            if (IsReacting && bone.Source != null) bone.Source.transform.rotation = bone.AnimatedPose.rotation;
            if (bone.Physics != null) bone.Physics.isKinematic = true;
            bone.EndTime = 0f;
        }
        IsReacting = false;
    }
    public void SyncMasses()
    {
        foreach (Bone bone in bones)
        {
            if (bone.Source == null || bone.Physics == null) continue;
            bone.Physics.mass = bone.Source.mass;
            bone.Physics.inertiaTensor = bone.Source.inertiaTensor;
            bone.Physics.inertiaTensorRotation = bone.Source.inertiaTensorRotation;
        }
    }
    private void OnDisable() => Cancel();
    private void OnDestroy() { if (physicsRoot != null) Destroy(physicsRoot); }
}
