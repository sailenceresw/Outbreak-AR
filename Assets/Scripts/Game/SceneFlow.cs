using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Scene loads behind a fade, and what survives a scene load: a run carried into the next chapter,
// a level picked that lives in the other chapter's scene, or a map site fight waiting to start.
public class SceneFlow : GameSystem
{
    // carried from one chapter scene to the next
    static int carriedScore;
    static int carriedAmmo;
    static int carriedShells;
    static int carriedGrenades;
    static int carriedKills, carriedShotsFired, carriedShotsHit;
    static bool continuingChapter;
    // level picked in the level select that lives in another scene
    static int pendingStartLevel = -1;
    // a fight started from an outbreak site on the map, picked up by the next scene load
    static SiteEncounter pendingEncounter;

    RawImage fadeRawImage;

    // how this scene was entered; read once when it starts
    public struct StartRequest
    {
        public SiteEncounter encounter;
        public int startLevel;
        public bool fromLevelSelect;
        // the previous chapter's run continues here
        public bool continuingChapter;
    }

    protected override void OnBind()
    {
        if (Game.fadeImage != null)
        {
            fadeRawImage = Game.fadeImage.GetComponent<RawImage>();
        }
    }

    public StartRequest ConsumeStartRequest(int firstLevel)
    {
        StartRequest request = new StartRequest();

        // a map site fight replaces the campaign level, at the player's current campaign strength
        request.encounter = pendingEncounter;
        pendingEncounter = null;
        if (request.encounter != null)
        {
            pendingStartLevel = Mathf.Max(GameProgress.UnlockedLevel, firstLevel);
            continuingChapter = false;
        }

        request.fromLevelSelect = pendingStartLevel >= 0;
        request.startLevel = request.fromLevelSelect ? pendingStartLevel : firstLevel;
        request.continuingChapter = continuingChapter && !request.fromLevelSelect;
        pendingStartLevel = -1;
        continuingChapter = false;
        return request;
    }

    public void RestoreCarriedRun(int startLevel)
    {
        Flow.RestoreRun(carriedScore, carriedKills, carriedShotsFired, carriedShotsHit);
        Weapons.RestoreCarried(carriedAmmo, carriedShells, carriedGrenades, startLevel);
    }

    public void CarryRunToNextChapter()
    {
        carriedScore = Flow.Score;
        carriedAmmo = Weapons.AmmoCount;
        carriedShells = Weapons.ShellCount;
        carriedGrenades = Weapons.GrenadeCount;
        carriedKills = Flow.RunKills;
        carriedShotsFired = Flow.ShotsFired;
        carriedShotsHit = Flow.ShotsHit;
        continuingChapter = true;
    }

    static void ClearCarriedRun()
    {
        carriedScore = 0;
        carriedAmmo = 0;
        carriedShells = 0;
        carriedGrenades = 0;
        carriedKills = carriedShotsFired = carriedShotsHit = 0;
        continuingChapter = false;
    }

    // back to the welcome screen; the run is abandoned
    public void GoHome()
    {
        ClearCarriedRun();
        pendingStartLevel = -1;
        GameProgress.SubmitHighScore(Flow.Score);
        LoadScene(0);
    }

    // after beating the game, start the campaign over
    public void RestartCampaign()
    {
        ClearCarriedRun();
        SetActiveSafe(Game.youWonPrompt, false);
        LoadScene(0);
    }

    // start a fight at an outbreak site: reload the scene so it opens straight on the room scan
    public void StartEncounter(SiteEncounter siteEncounter)
    {
        pendingEncounter = siteEncounter;
        LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // a level that lives in the other chapter scene is opened there, straight on the scan step.
    // Returns false when the level is in this scene.
    public bool OpenLevelInItsScene(int index)
    {
        int scene = LevelCatalog.SceneForLevel(index);
        if (scene == SceneManager.GetActiveScene().buildIndex || scene >= SceneManager.sceneCountInBuildSettings) return false;

        pendingStartLevel = index;
        LoadScene(scene);
        return true;
    }

    public bool HasNextChapterScene => SceneManager.GetActiveScene().buildIndex + 1 < SceneManager.sceneCountInBuildSettings;

    public IEnumerator LoadNextChapter()
    {
        return LoadSceneWithFade(SceneManager.GetActiveScene().buildIndex + 1);
    }

    public void LoadScene(int buildIndex)
    {
        StartCoroutine(LoadSceneWithFade(buildIndex));
    }

    IEnumerator LoadSceneWithFade(int buildIndex)
    {
        // the fade runs on game time, so it must not start frozen or in slow motion
        Time.timeScale = 1f;
        yield return Fade(0f, 1f);
        SceneManager.LoadScene(buildIndex);
    }

    public IEnumerator Fade(float from, float to)
    {
        if (fadeRawImage == null) yield break;

        GameObject fadeImage = Game.fadeImage;
        float fadeDuration = Game.fadeDuration;
        fadeImage.SetActive(true);
        // block taps while the screen is fading
        fadeRawImage.raycastTarget = true;

        for (float t = 0f; t < fadeDuration; t += Time.deltaTime)
        {
            fadeRawImage.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, t / fadeDuration));
            yield return null;
        }
        fadeRawImage.color = new Color(0f, 0f, 0f, to);

        if (to <= 0f) fadeImage.SetActive(false);
    }
}
