using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AudioManager : MonoBehaviour
{
    [Header("Audio Settings")]
    [SerializeField] private AudioClip _defaultClickSound;
    [SerializeField] private AudioClip _defaultHoverSound;
    public static AudioManager Instance { get; private set; }
    private AudioSource _audioSource;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
        }
    }

    public void PlayClickSound(AudioClip customClip = null)
    {
        AudioClip clipToPlay = customClip != null ? customClip : _defaultClickSound;

        if (clipToPlay != null)
        {
            _audioSource.PlayOneShot(clipToPlay);
        }
    }

    public void PlayHoverSound(AudioClip customClip = null)
    {
        AudioClip clipToPlay = customClip != null ? customClip : _defaultHoverSound;

        if (clipToPlay != null)
        {
            _audioSource.PlayOneShot(clipToPlay);
        }
    }
}