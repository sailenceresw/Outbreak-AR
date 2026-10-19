using UnityEngine;

// Containment traps: thrown at the zombie under the crosshair, which may break free or be caught for the dex.
public class CaptureSystem : GameSystem
{
    // a zombie was caught; it leaves the fight like a kill
    public event System.Action<ZombieController> ZombieCaptured;

    float nextTrapTime;

    // the catch chance under the crosshair, like a capture ring
    public void UpdateHint()
    {
        if (Hud == null) return;
        ZombieController aimed = Weapons.AimedZombie;
        string hint = string.Empty;
        if (aimed != null && !aimed.IsBeingCaptured && Inventory.Count(ItemId.Trap) > 0)
        {
            if (ZombieDex.Get(aimed.Type).catchable)
            {
                int percent = Mathf.RoundToInt(100f * ZombieDex.CatchChance(aimed.Type, aimed.IsMutant, aimed.HealthFraction));
                hint = (aimed.IsMutant ? "<color=#C080FF>MUTANT</color>  " : string.Empty) + "TRAP " + percent + "%";
            }
        }
        Hud.SetCatchHint(hint);
    }

    public void ThrowTrap()
    {
        if (!Flow.IsPlaying || MainCamera == null || Time.time < nextTrapTime) return;
        if (Inventory.Count(ItemId.Trap) <= 0)
        {
            GameAudio.Play(GameAudio.DryFire, 0.8f, 1.3f);
            ShowMessage("No traps left! Get more in the shop");
            return;
        }

        ZombieController target = Weapons.AimedZombie;
        if (target == null || target.IsDead || target.IsBeingCaptured)
        {
            ShowMessage("Aim at a zombie, then throw");
            return;
        }
        if (!ZombieDex.Get(target.Type).catchable)
        {
            ShowMessage("Too strong to trap!", 1.2f, new Color(1f, 0.4f, 0.3f));
            return;
        }

        Inventory.Use(ItemId.Trap);
        nextTrapTime = Time.time + 1.2f;
        float chance = ZombieDex.CatchChance(target.Type, target.IsMutant, target.HealthFraction);
        Transform view = MainCamera.transform;
        CaptureTrap.Throw(view.position + view.forward * 0.3f - view.up * 0.15f, target, chance, OnTrapResult);
    }

    void OnTrapResult(ZombieController zombie, bool caught)
    {
        if (zombie == null) return;
        if (caught)
        {
            OnZombieCaptured(zombie);
            return;
        }

        if (zombie.IsDead)
        {
            // the target died before the trap landed; don't waste it
            Inventory.Add(ItemId.Trap, 1);
            return;
        }
        zombie.BreakFree();
        ShowMessage("BROKE FREE!\n<size=70%>Weaken it more first</size>", 1.4f, new Color(1f, 0.4f, 0.3f));
    }

    void OnZombieCaptured(ZombieController zombie)
    {
        zombie.CompleteCapture(zombie.transform.position);
        ZombieCaptured?.Invoke(zombie);

        bool first = ZombieDex.RecordCaught(zombie.Type, zombie.IsMutant);
        int xp = ZombieDex.CatchXP(zombie.Type, zombie.IsMutant);

        LifetimeStats.Add(Stat.Catches);
        if (zombie.IsMutant) LifetimeStats.Add(Stat.MutantsCaught);
        MissionSystem.Report(MissionType.CatchZombies);
        if (zombie.IsMutant || zombie.Type == ZombieType.Spitter || zombie.Type == ZombieType.Brute)
        {
            MissionSystem.Report(MissionType.CatchRare);
        }
        Progression.AwardXP(xp);

        GameAudio.Play(GameAudio.Fanfare, 0.7f, 1.3f);
        string title = first ? (zombie.IsMutant ? "NEW MUTANT!" : "NEW DEX ENTRY!") : "CAPTURED!";
        ShowMessage(title + "\n<size=70%>" + zombie.DisplayName + "  +" + xp + " XP</size>", 2.2f,
            zombie.IsMutant ? ZombieDex.MutantColor : new Color(0.4f, 0.95f, 1f));
    }
}
