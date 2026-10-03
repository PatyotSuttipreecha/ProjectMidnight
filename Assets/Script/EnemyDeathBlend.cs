using System.Collections.Generic;
using UnityEngine;

// A transform-only animation target. No renderers, gameplay scripts or colliders are cloned.
public sealed class EnemyDeathBlend
{
    private readonly GameObject targetRoot;
    private readonly Animator targetAnimator;
    private readonly Dictionary<Transform, Transform> targets = new Dictionary<Transform, Transform>();
    private readonly Rigidbody[] bodies;
    private readonly float duration;
    private readonly float strength;
    private float elapsed;

    public EnemyDeathBlend(Animator source, Rigidbody[] ragdollBodies, float blendDuration, float driveStrength)
    {
        bodies = ragdollBodies;
        duration = Mathf.Max(0.01f, blendDuration);
        strength = Mathf.Max(0f, driveStrength);
        targetRoot = new GameObject("Death Animation Target") { hideFlags = HideFlags.HideAndDontSave };
        targetRoot.SetActive(false);
        targetRoot.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        targetRoot.transform.localScale = source.transform.lossyScale;
        CopyChildren(source.transform, targetRoot.transform);
        targetAnimator = targetRoot.AddComponent<Animator>();
        targetAnimator.avatar = source.avatar;
        targetAnimator.runtimeAnimatorController = source.runtimeAnimatorController;
        targetAnimator.applyRootMotion = false;
        targetAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        targetAnimator.enabled = false;
        targetRoot.SetActive(true);
        AnimatorStateInfo state = source.GetCurrentAnimatorStateInfo(0);
        targetAnimator.Play(state.fullPathHash, 0, state.normalizedTime);
        targetAnimator.Update(0f);
    }

    private void CopyChildren(Transform source, Transform parent)
    {
        foreach (Transform child in source)
        {
            Transform copy = new GameObject(child.name).transform;
            copy.SetParent(parent, false);
            copy.localPosition = child.localPosition;
            copy.localRotation = child.localRotation;
            copy.localScale = child.localScale;
            targets[child] = copy;
            CopyChildren(child, copy);
        }
    }

    public bool Step(float deltaTime)
    {
        elapsed += deltaTime;
        if (elapsed >= duration) return false;
        targetAnimator.Update(deltaTime);
        float weight = 1f - Mathf.SmoothStep(0f, 1f, elapsed / duration);
        float damping = 2f * Mathf.Sqrt(strength);
        foreach (Rigidbody body in bodies)
        {
            if (body == null || body.isKinematic || !targets.TryGetValue(body.transform, out Transform target)) continue;
            // Pull only the anchor horizontally. Driving every limb's world position fights
            // joints and ground contacts, especially once the animated pose reaches the floor.
            if (body.GetComponent<Joint>() == null)
            {
                Vector3 offset = Vector3.ProjectOnPlane(target.position - body.position, Vector3.up);
                Vector3 velocity = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
                Vector3 acceleration = Vector3.ClampMagnitude(offset, 0.25f) * strength - velocity * damping;
                body.AddForce(Vector3.ClampMagnitude(acceleration, 8f) * weight, ForceMode.Acceleration);
            }
            Quaternion error = target.rotation * Quaternion.Inverse(body.rotation);
            if (error.w < 0f) error = new Quaternion(-error.x, -error.y, -error.z, -error.w);
            error.ToAngleAxis(out float angle, out Vector3 axis);
            if (axis.sqrMagnitude < 0.001f || float.IsNaN(axis.x)) continue;
            Vector3 torque = axis.normalized * (Mathf.DeltaAngle(0f, angle) * Mathf.Deg2Rad * strength)
                - body.angularVelocity * damping;
            body.AddTorque(Vector3.ClampMagnitude(torque, 20f) * weight, ForceMode.Acceleration);
        }
        return true;
    }

    public void Dispose()
    {
        if (targetRoot != null) Object.Destroy(targetRoot);
    }
}
