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
        var clip = sound.clips[UnityEngine.Random.Range(0, sound.clips.Length)];
        float finalSound = sound.volume * volume;
        instance.audioSource.PlayOneShot(clip , finalSound);
    }
}

