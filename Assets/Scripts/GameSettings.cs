using UnityEngine;

// Player options saved on the device.
public static class GameSettings
{
    const string SoundKey = "Settings_Sound";
    const string VibrationKey = "Settings_Vibration";

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

    public static void Apply()
    {
        AudioListener.volume = Sound ? 1f : 0f;
    }
}
