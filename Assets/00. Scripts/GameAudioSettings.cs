using System;
using UnityEngine;

/// <summary>
/// 전역 사운드 설정. PlayerPrefs에 저장되며 BGM/SFX 켜짐 여부를 관리한다.
/// </summary>
public static class GameAudioSettings
{
    private const string BgmEnabledKey = "Audio.BgmEnabled";
    private const string SfxEnabledKey = "Audio.SfxEnabled";
    // 이전 슬라이더 볼륨 키(마이그레이션용).
    private const string LegacyBgmVolumeKey = "Audio.BgmVolume";
    private const string LegacySfxVolumeKey = "Audio.SfxVolume";

    private static bool loaded;
    private static bool bgmEnabled = true;
    private static bool sfxEnabled = true;

    public static event Action Changed;

    public static bool BgmEnabled
    {
        get
        {
            EnsureLoaded();
            return bgmEnabled;
        }
        set => SetBgmEnabled(value);
    }

    public static bool SfxEnabled
    {
        get
        {
            EnsureLoaded();
            return sfxEnabled;
        }
        set => SetSfxEnabled(value);
    }

    /// <summary>BGM 채널 볼륨(켜짐 1, 꺼짐 0).</summary>
    public static float BgmVolume => BgmEnabled ? 1f : 0f;

    /// <summary>SFX 채널 볼륨(켜짐 1, 꺼짐 0).</summary>
    public static float SfxVolume => SfxEnabled ? 1f : 0f;

    public static float EffectiveBgmVolume => BgmVolume;

    public static float EffectiveSfxVolume => SfxVolume;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureLoaded();
        Apply();
    }

    public static void SetBgmEnabled(bool enabled)
    {
        EnsureLoaded();
        if (bgmEnabled == enabled)
            return;

        bgmEnabled = enabled;
        PlayerPrefs.SetInt(BgmEnabledKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
        Apply();
        Changed?.Invoke();
    }

    public static void SetSfxEnabled(bool enabled)
    {
        EnsureLoaded();
        if (sfxEnabled == enabled)
            return;

        sfxEnabled = enabled;
        PlayerPrefs.SetInt(SfxEnabledKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
        Apply();
        Changed?.Invoke();
    }

    public static void ToggleBgm() => SetBgmEnabled(!BgmEnabled);

    public static void ToggleSfx() => SetSfxEnabled(!SfxEnabled);

    public static void Apply()
    {
        EnsureLoaded();
        AudioListener.volume = 1f;
    }

    private static void EnsureLoaded()
    {
        if (loaded)
            return;

        if (PlayerPrefs.HasKey(BgmEnabledKey))
            bgmEnabled = PlayerPrefs.GetInt(BgmEnabledKey, 1) != 0;
        else if (PlayerPrefs.HasKey(LegacyBgmVolumeKey))
            bgmEnabled = PlayerPrefs.GetFloat(LegacyBgmVolumeKey, 1f) > 0.001f;
        else
            bgmEnabled = true;

        if (PlayerPrefs.HasKey(SfxEnabledKey))
            sfxEnabled = PlayerPrefs.GetInt(SfxEnabledKey, 1) != 0;
        else if (PlayerPrefs.HasKey(LegacySfxVolumeKey))
            sfxEnabled = PlayerPrefs.GetFloat(LegacySfxVolumeKey, 1f) > 0.001f;
        else
            sfxEnabled = true;

        loaded = true;
    }
}
