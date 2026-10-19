using UnityEngine;

// Survivor XP and level-ups, daily missions and the mission board.
public class ProgressionManager : GameSystem
{
    // raised whenever the survivor's XP or level may have changed
    public event System.Action XPChanged;

    public bool MissionBoardOpen { get; private set; }

    // called once when the scene starts; returns the day-streak XP bonus (0 if already played today)
    public int BeginSession()
    {
        int streakBonus = PlayerProfile.RegisterPlayToday();
        MissionSystem.MissionCompleted += OnMissionCompleted;
        return streakBonus;
    }

    void OnDestroy()
    {
        MissionSystem.MissionCompleted -= OnMissionCompleted;
    }

    public void ShowStreakBonus(int streakBonus)
    {
        if (streakBonus > 0)
        {
            ShowMessage(PlayerProfile.Streak + "-DAY STREAK!  +" + streakBonus + " XP", 3f, new Color(1f, 0.75f, 0.15f));
        }
    }

    public void AwardXP(int amount)
    {
        int levels = PlayerProfile.AddXP(amount);
        if (levels > 0) OnLevelsGained(levels);
        RefreshXP();
    }

    public void RefreshXP()
    {
        XPChanged?.Invoke();
    }

    void OnLevelsGained(int levels)
    {
        int level = PlayerProfile.Level;
        string reward = PlayerProfile.RewardFor(level);
        GameAudio.Play(GameAudio.Fanfare, 0.9f, 1.1f);
        ShowMessage("LEVEL UP!  SURVIVOR LV " + level +
            (reward.Length > 0 ? "\n<size=70%>Unlocked: " + reward + "</size>" : string.Empty),
            2.5f, new Color(0.35f, 0.75f, 1f));
        RefreshXP();
    }

    void OnMissionCompleted(MissionSystem.Mission mission)
    {
        GameAudio.Play(GameAudio.Pickup, 0.9f, 1.3f);
        ShowMessage("MISSION COMPLETE!\n<size=70%>" + mission.description + "  -  claim on the mission board</size>",
            2.5f, new Color(0.4f, 0.95f, 0.4f));
    }

    public void OpenMissionBoard()
    {
        Canvas canvas = Game.playerHealthText != null ? Game.playerHealthText.canvas : null;
        if (canvas == null || MissionBoardOpen) return;

        MissionBoardOpen = true;
        GameAudio.Play(GameAudio.Pickup, 0.6f);
        MissionBoard.Show(canvas.rootCanvas, Game.playerHealthText, OnLevelsGained, () =>
        {
            MissionBoardOpen = false;
            RefreshXP();
            UI.RefreshHomeScreen();
        });
    }
}
