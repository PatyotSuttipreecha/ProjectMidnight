using UnityEngine;

public class PlaySoundEnter : StateMachineBehaviour
{
    [SerializeField] private SoundSO soundType;
    [SerializeField, Range(0, 1)] private float volume = 1f;


    override public void OnStateEnter(Animator animator , AnimatorStateInfo stateInfo , int layerIndex)
    {
        SoundManager.PlaySound(soundType, volume);
    }
}
