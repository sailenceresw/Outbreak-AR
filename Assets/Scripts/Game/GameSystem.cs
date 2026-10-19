using UnityEngine;

// Base for the parts the game controller is split into. GameControllerScript adds them at runtime
// and keeps every inspector setting, so the scenes didn't change when the code was split.
// Parts call each other for commands and reads, and raise C# events for things others react to.
public abstract class GameSystem : MonoBehaviour
{
    protected GameControllerScript Game { get; private set; }

    public void Bind(GameControllerScript game)
    {
        Game = game;
        OnBind();
    }

    // every part exists by now, so this is the place to subscribe to their events
    protected virtual void OnBind() { }

    protected Camera MainCamera => Game.MainCamera;
    protected GameHUD Hud => Game.UI.Hud;
    protected LevelFlowManager Flow => Game.Flow;
    protected SpawnManager Spawner => Game.Spawner;
    protected PickupManager Pickups => Game.Pickups;
    protected PlayerHealth Player => Game.Player;
    protected WeaponSystem Weapons => Game.Weapons;
    protected CaptureSystem Capture => Game.Capture;
    protected UIController UI => Game.UI;
    protected ProgressionManager Progression => Game.Progression;
    protected SceneFlow Scenes => Game.Scenes;

    protected void ShowMessage(string message, float duration = 1.5f)
    {
        Game.UI.ShowMessage(message, duration);
    }

    protected void ShowMessage(string message, float duration, Color color)
    {
        Game.UI.ShowMessage(message, duration, color);
    }

    // where the player stands, at floor height
    protected Vector3 FloorUnderPlayer()
    {
        Vector3 position = MainCamera.transform.position;
        position.y = ARWorld.FloorY;
        return position;
    }

    protected static void SetActiveSafe(GameObject target, bool active)
    {
        if (target != null) target.SetActive(active);
    }
}
