using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using PixelCrushers;
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Tobey.ChainedEchoes.UltrawideSupport;
using UnityEngine.Experimental.Rendering.Universal;
[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class UltrawideSupport : BaseUnityPlugin
{
    private const float OriginalAspectRatio = 16f / 9f;
    private static float CurrentAspectRatio => (float)Screen.width / Screen.height;

    private static UltrawideSupport instance;
    private static ManualLogSource Log => instance.Logger;

    private Harmony harmony = new(PluginInfo.PLUGIN_GUID);

    private void Awake()
    {
        // enforce singleton
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(this);
            return;
        }
    }

    private void OnEnable()
    {
        harmony.PatchAll(typeof(UltrawideSupport));
        SceneManager.activeSceneChanged -= SceneManager_activeSceneChanged;
        SceneManager.activeSceneChanged += SceneManager_activeSceneChanged;
    }
    private void OnDisable()
    {
        harmony.UnpatchSelf();
        SceneManager.activeSceneChanged -= SceneManager_activeSceneChanged;
        SceneManager.sceneLoaded -= SceneManager_sceneLoaded;
    }

    private void SceneManager_activeSceneChanged(Scene from, Scene to)
    {
        if (to.name == "StartMenu")
        {
            SetupStartMenu();
            SceneManager.activeSceneChanged -= SceneManager_activeSceneChanged;
        }
    }

    private void SetupStartMenu()
    {
        // fix the aspect ratio
        var pp = Camera.main.GetComponent<PixelPerfectCamera>();
        if (pp != null)
        {
            pp.refResolutionX = Mathf.RoundToInt(pp.refResolutionX / OriginalAspectRatio * CurrentAspectRatio);
        }

        StartCoroutine(FixKeybindingsInfo());
        StartCoroutine(HidePartyField());
        StartCoroutine(FixFleeField());
        StartCoroutine(FixSkillNameBox());
        StartCoroutine(FixBorders());
        FixStartMenuSternenritt();

        SceneManager.sceneLoaded -= SceneManager_sceneLoaded;
        SceneManager.sceneLoaded += SceneManager_sceneLoaded;
    }

    private void SceneManager_sceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "StartMenu")
        {
            StartCoroutine(RemoveBorders());
        }

        switch (scene.name)
        {
            case "bf_1":
                FixTutorialBattleBasics();
                break;
        }
    }

    private IEnumerator FixBorders()
    {
        yield return new WaitWhile(() => PartyInfoBattle.instance == null);

        foreach (Transform child in PartyInfoBattle.instance.transform.root.Find("Border"))
        {
            child.localPosition = new(
                x: child.localPosition.x < 0 ? (float)Math.Truncate(child.localPosition.x) : Mathf.Round(child.localPosition.x),
                y: child.localPosition.y,
                z: child.localPosition.z);
        }
    }

    private IEnumerator RemoveBorders()
    {
        yield return new WaitWhile(() => PartyInfoBattle.instance == null);
        foreach (var image in PartyInfoBattle.instance.transform.root.Find("Border").GetComponentsInChildren<Image>())
        {
            image.enabled = false;
        }
    }

    private static IEnumerator BattleTriggered()
    {
        yield return ShowPartyField();
        yield return new WaitUntil(() => Battle.battleEnding);
        yield return new WaitWhile(() => Battle.battleEnding);
        yield return HidePartyField();
    }

    private static IEnumerator ShowPartyField()
    {
        yield return new WaitWhile(() => PartyInfoBattle.instance == null);
        PartyInfoBattle.instance.GetComponent<Canvas>().enabled = true;
    }

    private static IEnumerator HidePartyField()
    {
        yield return new WaitWhile(() => PartyInfoBattle.instance == null);
        PartyInfoBattle.instance.GetComponent<Canvas>().enabled = false;
    }
    private static IEnumerator FixFleeField()
    {
        yield return new WaitWhile(() => PartyInfoBattle.instance == null);

        var fleeFieldTransform = PartyInfoBattle.instance.transform.root.Find("FleeCanvas/FleeField") as RectTransform;
        fleeFieldTransform.anchorMin = new(
            x: 1 - (Screen.height * OriginalAspectRatio / Screen.width),
            y: fleeFieldTransform.anchorMin.y
        );
    }

    private static IEnumerator FixSkillNameBox()
    {
        yield return new WaitWhile(() => PartyInfoBattle.instance == null);
        var skillNameBoxTransform = PartyInfoBattle.instance.transform.Find("skillNameBox") as RectTransform;
        skillNameBoxTransform.anchorMin = new(
            x: (Screen.height * OriginalAspectRatio / Screen.width),
            y: skillNameBoxTransform.anchorMin.y
        );
    }

    private static IEnumerator FixStartMenuVignette()
    {
        yield return new WaitWhile(() => PartyInfoBattle.instance == null);
        var vignetteTransform = PartyInfoBattle.instance.transform.root.Find("StartMenu/StartMenuContainer/vignette") as RectTransform;
        vignetteTransform.localScale = new(
                x: Screen.width / (Screen.height * OriginalAspectRatio),
                y: vignetteTransform.localScale.y,
                z: vignetteTransform.localScale.z
            );
    }

    private static void FixStartMenuSternenritt()
    {
        if (SceneManager.GetActiveScene().name == "StartMenu")
        {
            GameObject.Find("__Environment/sr_sternenritt_U3")?.SetActive(false);
        }
    }

    private static IEnumerator FixKeybindingsInfo()
    {
        yield return new WaitWhile(() => PartyInfoBattle.instance == null);
        StretchImage(PartyInfoBattle.instance.transform.root.Find("KeybindingsInfo/Container"));
    }

    private void FixTutorialBattleBasics()
    {
        foreach (var tutorial in Resources.FindObjectsOfTypeAll<GameObject>().Where(x => x.name.StartsWith("Tutorial Battle Basics")))
        {
            var bg = tutorial.transform.Find("Container/Image");
            StretchImage(bg, true);
        }
    }

    private static void StretchImage(Transform container, bool square = false)
    {
        var clone = Instantiate(container.gameObject);
        clone.transform.SetParent(container.parent, false);
        foreach (Transform child in clone.transform)
        {
            Destroy(child.gameObject);
        }
        clone.transform.SetAsFirstSibling();

        clone.transform.localScale = new(
            x: container.localScale.x / OriginalAspectRatio * CurrentAspectRatio,
            y: square ? container.localScale.y / OriginalAspectRatio * CurrentAspectRatio : container.localScale.y,
            z: container.localScale.z);

        container.GetComponent<Image>().enabled = false;
    }

    [HarmonyPatch(typeof(SplashScreenAnimation), nameof(SplashScreenAnimation.AnimationFinished))]
    [HarmonyPatch(typeof(MainMenuSystem), nameof(MainMenuSystem.ExecuteSettings))]
    [HarmonyPostfix, HarmonyWrapSafe]
    public static void SetUltrawideResolution()
    {
        var resolution = Screen.resolutions.Where(r => r.height == Screen.height).Last();
        Screen.SetResolution(resolution.width, resolution.height, true, resolution.refreshRate);
    }

    [HarmonyPatch(typeof(BattleTrigger), nameof(BattleTrigger.MoveToPos))]
    [HarmonyPrefix, HarmonyWrapSafe]
    public static void BattleTrigger_Prefix() => instance.StartCoroutine(BattleTriggered());

    [HarmonyPatch(typeof(StartMenu), nameof(StartMenu.Start))]
    [HarmonyPostfix, HarmonyWrapSafe]
    public static void StartMenu_Postfix()
    {
        SetUltrawideResolution();
        instance.StartCoroutine(FixStartMenuVignette());
        FixStartMenuSternenritt();
    }

    [HarmonyPatch(typeof(UIPanel), nameof(UIPanel.Start))]
    [HarmonyPostfix, HarmonyWrapSafe]
    public static void Dialogue_UIPanel_Start_Postfix(UIPanel __instance)
    {
        if (__instance is PixelCrushers.Wrappers.UIPanel)
        {
            StretchImage(__instance.transform.Find("Main Panel"));
        }
    }
}
