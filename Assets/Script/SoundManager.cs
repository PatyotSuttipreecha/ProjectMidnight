using System;
using UnityEngine;

[RequireComponent(typeof(AudioSource)), ExecuteInEditMode]
public class SoundManager : MonoBehaviour
{
    //[SerializeField] private SoundList[] soundList;
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
    public static void PlaySound(SoundSO sound, float volume = 1)
    {
        var clip = sound.clips[UnityEngine.Random.Range(0, sound.clips.Length)];
        instance.audioSource.PlayOneShot(clip);
    }
//#if UNITY_EDITOR
//    private void OnEnable()
//    {
//        string[] name = Enum.GetNames(typeof(PlayerSoundType));
//        Array.Resize(ref soundList, name.Length);
//        for (int i = 0; i < soundList.Length; i++)
//        {
//            soundList[i].name = name[i];
//        }
//    }
//#endif
}
//[Serializable]
//public struct SoundList
//{
//    [HideInInspector]public string name;
//    [SerializeField] public AudioClip[] sounds;
//}
//public enum PlayerSoundType
//{
//    Walk,
//    Run,
//    PistolAim,
//    PistolShoot,
//    PistolReload,
//    PistolEmptyMag,
//    Hurt,
//    Dead,

//}

