﻿using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Experimental.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Tobey.ChainedEchoes.UltrawideSupport;
[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class UltrawideSupport : BaseUnityPlugin
{
    internal static UltrawideSupport Instance;
    internal static ManualLogSource Log => Instance.Logger;

    internal Harmony Harmony = new(PluginInfo.PLUGIN_GUID);

    private void Awake()
    {
        // enforce singleton
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(this);
            return;
        }
    }

    private void Start()
    {
        SceneManager.activeSceneChanged -= SceneManager_activeSceneChanged;
        SceneManager.activeSceneChanged += SceneManager_activeSceneChanged;
    }

    private void SceneManager_activeSceneChanged(Scene from, Scene to)
    {
        if (to.name != "StartMenu") return;
        SceneManager.activeSceneChanged -= SceneManager_activeSceneChanged;

        // fix the aspect ratio
        var pp = Camera.main.GetComponent<PixelPerfectCamera>();
        if (pp != null)
        {
            pp.refResolutionX = Convert.ToInt32(Convert.ToSingle(pp.refResolutionX) / (16f / 9f) * (Convert.ToSingle(Screen.width) / Screen.height));
        }

        StartCoroutine(RemoveBorders());
        StartCoroutine(HidePartyField());
        StartCoroutine(FixFleeField());
        StartCoroutine(FixSkillNameBox());
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

        var fleeFieldTransform = PartyInfoBattle.instance.transform.root.Find("FleeCanvas").Find("FleeField") as RectTransform;
        fleeFieldTransform.anchorMin = new(
            x: 1 - (Screen.height * (16f / 9f) / Screen.width),
            y: fleeFieldTransform.anchorMin.y
        );
    }

    private static IEnumerator FixSkillNameBox()
    {
        yield return new WaitWhile(() => PartyInfoBattle.instance == null);
        var skillNameBoxTransform = PartyInfoBattle.instance.transform.Find("skillNameBox") as RectTransform;
        skillNameBoxTransform.anchorMin = new(
            x: (Screen.height * (16f / 9f) / Screen.width),
            y: skillNameBoxTransform.anchorMin.y
        );
    }

    private static IEnumerator FixStartMenuVignette()
    {
        yield return new WaitWhile(() => PartyInfoBattle.instance == null);
        var vignetteTransform = PartyInfoBattle.instance.transform.root.Find("StartMenu/StartMenuContainer/vignette") as RectTransform;
        vignetteTransform.localScale = new(
                x: Screen.width / (Screen.height * (16f / 9f)),
                y: vignetteTransform.localScale.y,
                z: vignetteTransform.localScale.z
            );
    }

    private void OnEnable() => Harmony.PatchAll(typeof(UltrawideSupport));
    private void OnDisable() => Harmony.UnpatchSelf();

    [HarmonyPatch(typeof(SplashScreenAnimation), nameof(SplashScreenAnimation.AnimationFinished))]
    [HarmonyPatch(typeof(StartMenu), nameof(StartMenu.Start))]
    [HarmonyPatch(typeof(MainMenuSystem), nameof(MainMenuSystem.ExecuteSettings))]
    [HarmonyPostfix, HarmonyWrapSafe]
    public static void SetUltrawideResolution()
    {
        var resolution = Screen.resolutions.Where(r => r.height == Screen.height).Last();
        Screen.SetResolution(resolution.width, resolution.height, true, resolution.refreshRate);
    }

    [HarmonyPatch(typeof(BattleTrigger), nameof(BattleTrigger.MoveToPos))]
    [HarmonyPrefix, HarmonyWrapSafe]
    public static void BattleTrigger_Prefix() => Instance.StartCoroutine(BattleTriggered());
    
    [HarmonyPatch(typeof(StartMenu), nameof(StartMenu.Start))]
    [HarmonyPostfix, HarmonyWrapSafe]
    public static void StartMenu_Postfix() => Instance.StartCoroutine(FixStartMenuVignette());
}
