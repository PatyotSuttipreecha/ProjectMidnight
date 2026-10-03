using Unity.Cinemachine;
using UnityEngine;

// Applied in Cinemachine's pipeline so camera follow, aim and recoil retain control.
[DisallowMultipleComponent]
public class BreathingCameraSway : CinemachineExtension
{
    [System.NonSerialized] public Vector2 RotationDegrees;

    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Noise) return;
        state.OrientationCorrection *= Quaternion.Euler(-RotationDegrees.y, RotationDegrees.x, 0f);
    }
}
