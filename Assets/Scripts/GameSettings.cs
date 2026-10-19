using UnityEngine;
using UnityEngine.XR.ARFoundation;

public enum GraphicsQuality { Low, High }

// Player options saved on the device.
public static class GameSettings
{
    const string SoundKey = "Settings_Sound";
    const string VibrationKey = "Settings_Vibration";
    const string QualityKey = "Settings_Quality";

    // raised when the graphics quality changes, so effects already on screen can follow it
    public static event System.Action QualityChanged;

    public static bool Sound
    {
        get => PlayerPrefs.GetInt(SoundKey, 1) == 1;
        set
        {
            PlayerPrefs.SetInt(SoundKey, value ? 1 : 0);
            PlayerPrefs.Save();
            Apply();
        }
    }

    public static bool Vibration
    {
        get => PlayerPrefs.GetInt(VibrationKey, 1) == 1;
        set
        {
            PlayerPrefs.SetInt(VibrationKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    // Low: no bloom or film grain, and the frame rate follows the AR camera (30 on most phones).
    // High: every effect, rendered at 60. Phones start on Low.
    public static GraphicsQuality Quality
    {
        get => (GraphicsQuality)PlayerPrefs.GetInt(QualityKey, (int)(Application.isMobilePlatform ? GraphicsQuality.Low : GraphicsQuality.High));
        set
        {
            PlayerPrefs.SetInt(QualityKey, (int)value);
            PlayerPrefs.Save();
            Apply();
            QualityChanged?.Invoke();
        }
    }

    public static bool HighQuality => Quality == GraphicsQuality.High;

    public static void Apply()
    {
        AudioListener.volume = Sound ? 1f : 0f;
        ApplyFrameRate();
    }

    static void ApplyFrameRate()
    {
        QualitySettings.vSyncCount = 0;

        // while the AR session matches the camera's frame rate it overrides targetFrameRate itself
        ARSession session = Object.FindFirstObjectByType<ARSession>();
        if (session != null) session.matchFrameRateRequested = !HighQuality;
        Application.targetFrameRate = HighQuality ? 60 : 30;
    }
}
