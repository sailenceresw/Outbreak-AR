using UnityEngine;

// The player's health: claws and acid, med kits, and the Adrenaline Shot that cheats death once.
public class PlayerHealth : GameSystem
{
    // health reached zero and no Adrenaline Shot was left
    public event System.Action Died;

    public float Current { get; private set; }
    // the inspector's max health plus the survivor level bonus
    public float Max { get; private set; }

    float nextScreenBloodTime;
    float nextHapticTime;

    public void Setup()
    {
        Max = Game.maxPlayerHealth + PlayerProfile.BonusMaxHealth;
    }

    public void Refill()
    {
        Current = Max;
    }

    public void Heal(float amount)
    {
        Current = Mathf.Min(Current + amount, Max);
    }

    // a med kit was picked up
    public void AddHealth(float amount)
    {
        Heal(amount);
        GameAudio.Play(GameAudio.Pickup, 0.7f, 1.2f);
        MissionSystem.Report(MissionType.CollectPickups);
        ShowMessage("+" + Mathf.RoundToInt(amount) + " health");
    }

    public void Damage(float amount)
    {
        if (!Flow.IsPlaying) return;

        Current = Mathf.Max(Current - amount, 0f);
        if (Hud != null) Hud.FlashDamage(0.45f);

        // claws land in bursts: blood hits the screen and the view jolts, at most every half second
        if (Time.time >= nextScreenBloodTime)
        {
            nextScreenBloodTime = Time.time + 0.9f;
            if (Hud != null) Hud.ScreenBlood(1);
            if (PostFX.Instance != null) PostFX.Instance.Jolt(0.3f);
        }

#if UNITY_ANDROID || UNITY_IOS
        if (GameSettings.Vibration && Time.time >= nextHapticTime)
        {
            Handheld.Vibrate();
            nextHapticTime = Time.time + 1f;
        }
#endif

        if (Current <= 0f)
        {
            // an Adrenaline Shot from the shop cheats death once
            if (Inventory.Use(ItemId.Adrenaline))
            {
                Current = Max * 0.5f;
                // the blast shoves nearby zombies back so the revive isn't wasted
                Pickups.ActivatePowerUp(PowerUpType.Shockwave);
                ShowMessage("ADRENALINE!\n<size=70%>Back on your feet</size>", 2f, UIStyle.Toxic);
                return;
            }
            Died?.Invoke();
        }
    }

    // a spitter's glob reached the player
    public void AcidHit(float damage)
    {
        if (!Flow.IsPlaying) return;
        if (Hud != null) Hud.ScreenSplatter(3, new Color(0.35f, 0.8f, 0.1f, 0.8f));
        ShowMessage("ACID!", 1f, new Color(0.55f, 1f, 0.2f));
        Damage(damage);
    }

    // the player stepped out of the way in time
    public void AcidDodged()
    {
        if (!Flow.IsPlaying) return;
        Flow.AddScore(25);
        ShowMessage("DODGED!", 0.8f, new Color(0.55f, 1f, 0.2f));
    }
}
