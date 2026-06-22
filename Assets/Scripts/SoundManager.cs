using System;
using Sirenix.OdinInspector;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    [TitleGroup("Refs"), SerializeField] private AudioSource bgMusicASfx;
    [TitleGroup("Refs"), SerializeField] private AudioSource playBtnSfx;
    [TitleGroup("Refs"), SerializeField] private AudioSource btnClickSfx;
    [TitleGroup("Refs"), SerializeField] private AudioSource backBtnSfx;
    [TitleGroup("Refs"), SerializeField] private AudioSource completedSfx;
    [TitleGroup("Refs"), SerializeField] private AudioSource failedSfx;

    public AudioSource BgMusicASfx => bgMusicASfx;
    public AudioSource PlayBtnSfx => playBtnSfx;
    public AudioSource BtnClickSfx => btnClickSfx;
    public AudioSource BackBtnSfx => backBtnSfx;
    public AudioSource CompletedSfx => completedSfx;
    public AudioSource FailedSfx => failedSfx;

    public static SoundManager Instance { get; set; }

    private void Awake()
    {
        if (!Instance)
        {
            Instance = this;
        }
    }

    public void PlayAudio(AudioSource audioSource, bool loop = false)
    {
        audioSource.loop = loop;
        if (loop)
        {
            audioSource.Play();
        }
        else
        {
            audioSource.PlayOneShot(audioSource.clip);
        }
    }

    public void StopAudioSource(AudioSource audioSource)
    {
        audioSource.Stop();
    }
}