# Outbreak AR

An augmented-reality zombie survival shooter for Android and iOS, built with Unity 2022.3 and AR Foundation 5.1.

Scan your room, then hold off waves of zombies that claw up out of the floor around you. Turn to find them and shoot to survive.

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
- **Power-ups:** Rapid Fire, Hollow Points, Cryo Pulse and Shockwave.
- **Supply drops:** ammo crates and med kits. Shoot them or walk up to them to collect.
- **Danger cues:** off-screen threat arrows, a low-health heartbeat and vignette, and music that intensifies during boss fights and hordes.
- **Generated in code:** all sound effects, particle effects and HUD graphics, so they need no extra asset files.

## Getting started

1. Open the project in Unity **2022.3.15f1**.
2. Open `Assets/Scenes/Level1.unity`.
3. Build for Android (ARCore) or iOS (ARKit), or test in the Editor with XR Simulation or AR Foundation Remote.

Large binary assets are stored with Git LFS. Run `git lfs install` before cloning.

## Scripts

| Script | Role |
|---|---|
| `GameControllerScript` | Game flow, levels, spawning, shooting, score |
| `ZombieController` | Zombie types, movement, attacks, boss behaviour |
| `LevelCatalog` | Level definitions |
| `GameHUD` | Crosshair, threat arrows, boss bar, banners |
| `GameFX` / `GameAudio` | Generated particles, textures and sound effects |
| `PowerUpPickup`, `IncreaseAmmoScript`, `IncreaseHealthScript` | Pickups |

## Credits

The zombie models, the PT-9M pistol, the music and the gunshot sound are third-party assets owned by their respective creators.
