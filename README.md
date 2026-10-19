# Outbreak AR

An augmented-reality zombie survival shooter for Android and iOS, built with Unity 2022.3 and AR Foundation 5.1.

Scan your room, then hold off waves of zombies that claw up out of the floor around you. Turn to find them and shoot to survive.

<p align="center">
  <img src="docs/android-gameplay.png" alt="Outbreak AR running on a Redmi Note 10 Pro: a runner zombie charging at the camera with the red on-target crosshair, health bar, score, shotgun shells, and the grenade and swap buttons" width="300">
</p>
<p align="center"><em>Running on a Redmi Note 10 Pro (Android 13).</em></p>

## Campaign

| Chapter | Level | Objective |
|---|---|---|
| 1: Outbreak | 1. First Contact | Eliminate 6 walkers |
| | 2. They're Fast Now | Eliminate 10, runners appear |
| | 3. Hold the Line | Survive 60 seconds |
| | 4. The Brute | Boss: the Brute King |
| 2: Nightfall | 5. Nightfall | Eliminate 14, horde rushes |
| | 6. Surrounded | Survive 75 seconds |
| | 7. No Way Out | Eliminate 18 |
| | 8. The Abomination | Final boss |

Chapter 1 runs in `Level1.unity` and Chapter 2 in `Level2.unity`. Levels within a chapter follow on without reloading the scene, so you only scan the room once per chapter. All level data lives in `Assets/Scripts/LevelCatalog.cs`.

## Features

- **Enemies:** Walkers, fast Runners, armoured Brutes and bosses that summon reinforcements and enrage at half health.
- **Combat:** hitscan shooting with headshots, combo multipliers and a saved high score.
- **Two weapons:** pistol and a pump shotgun, unlocked on level 2. The shotgun fires 8 pellets, does more damage up close and uses shells. Tap SWAP to switch.
- **Grenades:** two per level, plus one from each ammo crate. Aim at the floor and tap GRENADE to lob one; the blast hits every zombie nearby, with a bonus for multi-kills.
- **Combat feedback:** floating damage numbers (gold for headshots), a crosshair that turns red over a target, blood pools where zombies fall, and an end-of-run card with kills, accuracy and score.
- **HUD:** health bar that shows each hit, ammo and grenade panel, and a dark backing so it stays readable over any camera image.
- **Daily missions and survivor level:** three new missions every day, XP from kills and missions, survivor levels 1-30 that unlock extra ammo, grenades and health, and a 7-day streak bonus. Open MISSIONS from the start or end screen.
- **Level select:** progress is saved on the device. Returning players can start from any unlocked level and see their best score for each.
- **Power-ups:** Rapid Fire, Hollow Points, Cryo Pulse and Shockwave.
- **Supply drops:** ammo crates and med kits. Shoot them or walk up to them to collect.
- **Danger cues:** off-screen threat arrows, a low-health heartbeat and vignette, and music that intensifies during boss fights and hordes.
- **Generated in code:** all sound effects, particle effects and HUD graphics, so they need no extra asset files.

## Getting started

1. Open the project in Unity **2022.3.15f1** with Android Build Support.
2. Open `Assets/Scenes/Level1.unity`.
3. Build for Android (ARCore) or iOS (ARKit), or test in the Editor with XR Simulation.

Large binary assets are stored with Git LFS. Run `git lfs install` before cloning.

Keep the project outside OneDrive, Dropbox and other synced folders. Syncing corrupts Unity's `Library` cache and produces broken builds.

### Android build

The Android build targets API 34 (Android 14) with a minimum of API 30. Unity 2022.3.15's bundled SDK stops at API 32, so copy `platforms/android-34` from an Android Studio SDK into the editor's `PlaybackEngines/AndroidPlayer/SDK/platforms` folder.

Build from the editor with **Build > Android APK**, or from the command line with Unity Hub signed in:

```
Unity.exe -batchmode -quit -projectPath . -buildTarget Android -executeMethod BuildAndroid.Build
```

The APK is written to `Builds/Android/OutbreakAR.apk`.

## Scripts

| Script | Role |
|---|---|
| `GameControllerScript` | Game flow, levels, spawning, shooting, score |
| `ZombieController` | Zombie types, movement, attacks, boss behaviour |
| `LevelCatalog` | Level definitions and saved progress (`GameProgress`) |
| `LevelSelectMenu` | Runtime level picker |
| `GameHUD` | Health and ammo panels, crosshair, threat arrows, damage numbers, boss bar, banners, results card |
| `Grenade` | Thrown grenade arc, fuse and blast callback |
| `GameFX` / `GameAudio` | Generated particles, textures and sound effects |
| `PowerUpPickup`, `IncreaseAmmoScript`, `IncreaseHealthScript` | Pickups |
| `Editor/BuildAndroid` | Command-line and menu Android build |

## Credits

The zombie models, the PT-9M pistol, the music and the gunshot sound are third-party assets owned by their respective creators.
