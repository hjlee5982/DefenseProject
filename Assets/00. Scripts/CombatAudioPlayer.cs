using System;
using UnityEngine;

/// <summary>
/// Plays combat sound effects:
///  - Swing: once per character attack (shared by every element).
///  - Hit: per-element projectile hit. Hit (base) and AdditionalHit (grade 4/5 extra attack).
/// Clips are assigned in the scene; sources are pooled, calls are throttled and every clip is
/// cut at <see cref="maxPlaySeconds"/> so long, silence-padded files do not hog voices.
/// </summary>
public class CombatAudioPlayer : MonoBehaviour
{
    [Serializable]
    public class ElementSfx
    {
        public Item.ItemCategory category;
        [Tooltip("Base hit sound.")]
        public AudioClip hit;
        [Range(0f, 1f)] public float hitVolume = 0.8f;
        [Tooltip("Hit sound of the extra attack unlocked at grade 4/5. Falls back to the base hit when empty.")]
        public AudioClip additionalHit;
        [Range(0f, 1f)] public float additionalHitVolume = 0.8f;
    }

    [Header("Swing (character attack)")]
    [SerializeField] private AudioClip swing;
    [SerializeField, Range(0f, 1f)] private float swingVolume = 0.5f;
    [SerializeField, Min(0f)] private float minSwingInterval = 0.08f;

    [Header("Hit")]
    [SerializeField] private ElementSfx[] elements;
    [SerializeField, Min(0f)] private float minHitInterval = 0.045f;

    [Header("Mixer")]
    [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
    [SerializeField, Min(1)] private int voiceCount = 24;
    [SerializeField, Min(0.1f)] private float maxPlaySeconds = 1.6f;
    [SerializeField, Range(0f, 0.3f)] private float pitchJitter = 0.05f;

    private const int KindHit = 0;
    private const int KindAdditionalHit = 1;

    private AudioSource[] voices;
    private int nextVoice;
    private float[] lastHitTime;
    private float lastSwingTime = float.NegativeInfinity;

    public static CombatAudioPlayer Instance { get; private set; }

    /// <summary>Total number of clips actually played (useful for play-mode verification).</summary>
    public int PlayedCount { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;

        voices = new AudioSource[voiceCount];
        for (int i = 0; i < voices.Length; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            voices[i] = source;
        }

        int categoryCount = Enum.GetValues(typeof(Item.ItemCategory)).Length;
        lastHitTime = new float[categoryCount * 2];
        for (int i = 0; i < lastHitTime.Length; i++)
            lastHitTime[i] = float.NegativeInfinity;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static void PlaySwing()
    {
        if (Instance != null)
            Instance.PlaySwingInternal();
    }

    public static void PlayHit(Item.ItemCategory category, bool additional)
    {
        if (Instance != null)
            Instance.PlayHitInternal(category, additional);
    }

    private void PlaySwingInternal()
    {
        if (voices == null || swing == null) return;

        float now = Time.unscaledTime;
        if (now - lastSwingTime < minSwingInterval) return;
        lastSwingTime = now;

        PlayClip(swing, swingVolume);
    }

    private void PlayHitInternal(Item.ItemCategory category, bool additional)
    {
        if (voices == null || category == Item.ItemCategory.Buff) return;

        ElementSfx sfx = FindElement(category);
        if (sfx == null) return;

        AudioClip clip = sfx.hit;
        float volume = sfx.hitVolume;
        int kind = KindHit;
        if (additional && sfx.additionalHit != null)
        {
            clip = sfx.additionalHit;
            volume = sfx.additionalHitVolume;
            kind = KindAdditionalHit;
        }

        if (clip == null) return;

        int throttleIndex = (int)category * 2 + kind;
        float now = Time.unscaledTime;
        if (now - lastHitTime[throttleIndex] < minHitInterval) return;
        lastHitTime[throttleIndex] = now;

        PlayClip(clip, volume);
    }

    private void PlayClip(AudioClip clip, float volume)
    {
        AudioSource source = voices[nextVoice];
        nextVoice = (nextVoice + 1) % voices.Length;

        source.Stop();
        source.clip = clip;
        source.volume = volume * masterVolume;
        source.pitch = 1f + UnityEngine.Random.Range(-pitchJitter, pitchJitter);
        source.Play();

        // Source files can be padded with long silence; cut them so the voice is freed early.
        if (clip.length > maxPlaySeconds)
            source.SetScheduledEndTime(AudioSettings.dspTime + maxPlaySeconds);

        PlayedCount++;
    }

    private ElementSfx FindElement(Item.ItemCategory category)
    {
        if (elements == null) return null;

        for (int i = 0; i < elements.Length; i++)
        {
            if (elements[i] != null && elements[i].category == category)
                return elements[i];
        }

        return null;
    }
}
