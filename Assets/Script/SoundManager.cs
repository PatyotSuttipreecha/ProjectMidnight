using System;
using UnityEngine;

[RequireComponent(typeof(AudioSource)), ExecuteInEditMode]
public class SoundManager : MonoBehaviour
{
    private static SoundManager instance;
    [SerializeField] private AudioSource audioSource;
    private void Awake()
    {
        instance = this;
        DontDestroyOnLoad(this.gameObject);
    }
    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }
    public static void PlaySound(SoundSO sound, float volume)
    {
        
        if(instance == null)
        {
            Debug.LogWarning("⚠️ No SoundManager instance found in the scene!");
            return;
        }
        if (sound == null || sound.clips == null || sound.clips.Length == 0)
        {
            Debug.LogWarning("⚠️ SoundSO is missing or has no clips!");
            return;
        }
        var clip = sound.clips[UnityEngine.Random.Range(0, sound.clips.Length)];
        float finalSound = sound.volume * volume;
        if (clip!=null)
        {
            instance.audioSource.PlayOneShot(clip, finalSound);
        }
        
    }
}

