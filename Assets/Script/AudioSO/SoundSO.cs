using UnityEngine;

[CreateAssetMenu(fileName = "SoundSO", menuName = "Scriptable Objects/SoundSO")]
public class SoundSO : ScriptableObject
{
    public string soundName;
    public AudioClip[] clips;
    [Range(0f, 1f)] public float volume = 1f;
}
