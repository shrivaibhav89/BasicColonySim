using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SoundCue
{
    public SoundId id;
    public AudioClip[] clips;
    [Range(0f, 1f)] public float volume = 1f;
    [Range(0.1f, 3f)] public float minPitch = 1f;
    [Range(0.1f, 3f)] public float maxPitch = 1f;

    public AudioClip GetClip()
    {
        if (clips == null || clips.Length == 0)
        {
            return null;
        }

        return clips[UnityEngine.Random.Range(0, clips.Length)];
    }

    public float GetPitch()
    {
        return UnityEngine.Random.Range(minPitch, maxPitch);
    }
}

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("Music")]
    public AudioSource musicSource;
    public AudioClip backgroundMusic;
    [Range(0f, 1f)] public float musicVolume = 0.45f;
    public bool playMusicOnStart = true;

    [Header("SFX")]
    public AudioSource sfxSourcePrefab;
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Min(1)] public int initialSfxPoolSize = 12;
    [Min(1)] public int maxSfxPoolSize = 24;
    public SoundCue[] soundCues;

    private readonly Dictionary<SoundId, SoundCue> cueLookup = new Dictionary<SoundId, SoundCue>();
    private readonly List<AudioSource> sfxSourcePool = new List<AudioSource>();
    private int nextStealIndex;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureAudioSources();
        BuildCueLookup();
        PrewarmSfxPool();
    }

    void Start()
    {
        if (playMusicOnStart && backgroundMusic != null)
        {
            PlayMusic(backgroundMusic);
        }
    }

    public void PlayMusic(AudioClip clip)
    {
        if (clip == null || musicSource == null)
        {
            return;
        }

        musicSource.clip = clip;
        musicSource.loop = true;
        musicSource.volume = musicVolume;
        musicSource.Play();
    }

    public void StopMusic()
    {
        if (musicSource != null)
        {
            musicSource.Stop();
        }
    }

    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        if (musicSource != null)
        {
            musicSource.volume = musicVolume;
        }
    }

    public void SetSfxVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
    }

    public void PlaySfx(SoundId id)
    {
        PlaySfxAt(id, transform.position);
    }

    public void PlaySfxAt(SoundId id, Vector3 position)
    {
        if (!cueLookup.TryGetValue(id, out SoundCue cue))
        {
            return;
        }

        AudioClip clip = cue.GetClip();
        if (clip == null)
        {
            return;
        }

        AudioSource source = GetAvailableSfxSource();
        if (source == null)
        {
            return;
        }

        source.transform.position = position;
        source.clip = clip;
        source.volume = cue.volume * sfxVolume;
        source.pitch = cue.GetPitch();
        source.loop = false;
        source.Play();
    }

    private void EnsureAudioSources()
    {
        if (musicSource == null)
        {
            GameObject musicObject = new GameObject("MusicSource");
            musicObject.transform.SetParent(transform, false);
            musicSource = musicObject.AddComponent<AudioSource>();
        }

        musicSource.playOnAwake = false;
        musicSource.loop = true;

        if (sfxSourcePrefab == null)
        {
            GameObject sfxObject = new GameObject("SfxSourcePrefab");
            sfxObject.transform.SetParent(transform, false);
            sfxSourcePrefab = sfxObject.AddComponent<AudioSource>();
            sfxObject.SetActive(false);
        }

        sfxSourcePrefab.playOnAwake = false;
    }

    private void BuildCueLookup()
    {
        cueLookup.Clear();
        if (soundCues == null)
        {
            return;
        }

        foreach (SoundCue cue in soundCues)
        {
            if (cue == null)
            {
                continue;
            }

            cueLookup[cue.id] = cue;
        }
    }

    private AudioSource GetAvailableSfxSource()
    {
        foreach (AudioSource source in sfxSourcePool)
        {
            if (source != null && !source.isPlaying)
            {
                return source;
            }
        }

        if (sfxSourcePool.Count < Mathf.Max(1, maxSfxPoolSize))
        {
            return CreateSfxSource();
        }

        return StealOldestSfxSource();
    }

    private void PrewarmSfxPool()
    {
        int targetSize = Mathf.Clamp(initialSfxPoolSize, 1, Mathf.Max(1, maxSfxPoolSize));
        while (sfxSourcePool.Count < targetSize)
        {
            CreateSfxSource();
        }
    }

    private AudioSource CreateSfxSource()
    {
        AudioSource newSource = Instantiate(sfxSourcePrefab, transform);
        newSource.gameObject.name = "SfxSource";
        newSource.gameObject.SetActive(true);
        newSource.playOnAwake = false;
        sfxSourcePool.Add(newSource);
        return newSource;
    }

    private AudioSource StealOldestSfxSource()
    {
        if (sfxSourcePool.Count == 0)
        {
            return null;
        }

        nextStealIndex %= sfxSourcePool.Count;
        AudioSource source = sfxSourcePool[nextStealIndex];
        nextStealIndex = (nextStealIndex + 1) % sfxSourcePool.Count;

        if (source != null)
        {
            source.Stop();
        }

        return source;
    }
}
