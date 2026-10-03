using System.Collections.Generic;
using UnityEngine;

public static class EnemyRagdollBuilder
{
    private static readonly HumanBodyBones[] Bones = {
        HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.Head,
        HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm,
        HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm,
        HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg,
        HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg
    };

    public static bool Build(Animator animator)
    {
        if (animator == null || !animator.isHuman) return false;
        List<Rigidbody> bodies = new List<Rigidbody>();
        foreach (HumanBodyBones boneId in Bones)
        {
            Transform bone = animator.GetBoneTransform(boneId);
            if (bone == null) continue;
            Rigidbody body = bone.GetComponent<Rigidbody>();
            if (body == null) body = bone.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = true;
            body.mass = boneId == HumanBodyBones.Hips ? 12f : boneId == HumanBodyBones.Head ? 4f : 5f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            bodies.Add(body);
        }
        foreach (Rigidbody body in bodies)
        {
            // Existing child hitbox colliders now belong to their nearest bone rigidbody.
            bool hasCollider = false;
            foreach (Collider collider in body.GetComponentsInChildren<Collider>())
                if (collider.attachedRigidbody == body && !collider.isTrigger) hasCollider = true;
            if (!hasCollider)
            {
                CapsuleCollider collider = body.gameObject.AddComponent<CapsuleCollider>();
                collider.radius = 0.08f;
                collider.height = 0.25f;
                collider.center = new Vector3(0f, 0.1f, 0f);
            }
            Transform parent = body.transform.parent;
            Rigidbody connected = null;
            while (parent != null && parent != animator.transform)
            {
                connected = parent.GetComponent<Rigidbody>();
                if (connected != null) break;
                parent = parent.parent;
            }
            if (connected == null || body.GetComponent<Joint>() != null) continue;
            CharacterJoint joint = body.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = connected;
            joint.autoConfigureConnectedAnchor = true;
            joint.enablePreprocessing = false;
            joint.enableProjection = true;
            joint.lowTwistLimit = new SoftJointLimit { limit = -25f };
            joint.highTwistLimit = new SoftJointLimit { limit = 25f };
            joint.swing1Limit = new SoftJointLimit { limit = 45f };
            joint.swing2Limit = new SoftJointLimit { limit = 25f };
        }
        List<Collider> colliders = new List<Collider>();
        foreach (Collider collider in animator.GetComponentsInChildren<Collider>())
            if (bodies.Contains(collider.attachedRigidbody)) colliders.Add(collider);
        // Hitboxes can overlap. Avoid explosive self-collision when animation releases them.
        for (int i = 0; i < colliders.Count; i++)
            for (int j = i + 1; j < colliders.Count; j++) Physics.IgnoreCollision(colliders[i], colliders[j]);
        return bodies.Count >= 3;
    }
}
