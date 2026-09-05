using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Windows;

namespace SpeedrunMod
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public static class PluginInfo
        {
            public const string PLUGIN_GUID = "com.sialala.speedrun";
            public const string PLUGIN_NAME = "Speedrun Mod";
            public const string PLUGIN_VERSION = "1.0.0";
        }

        // TIMER
        public static bool IsTimerRunning = false;
        public static float ElapsedTime = 0f;
        public static TextMeshProUGUI TimerText;
        public static TextMeshProUGUI DeltaText;
        private static GameObject UiCanvasObject;

        // SPLITS
        public static string CurrentLevelName = "UnknownLevel";
        public static List<float> CurrentSplits = new List<float>();
        public static int CurrentSplitIndex = 0;
        public static bool HasFinishedSplit = false;

        // SPLITS MEMORY
        public static List<float> LoadedPBSplits = new List<float>();
        public static List<float> LoadedBestSegments = new List<float>();

        // CONFIG
        public static ConfigEntry<bool> ConfigEnableTimer;
        public static ConfigEntry<bool> ConfigEnableDelta;
        public static ConfigEntry<bool> ConfigEnableQuickReset;
        public static ConfigEntry<KeyCode> ConfigQuickResetKey;
        public static ConfigEntry<bool> ConfigShowMenuTime;

        public static ConfigEntry<string> ConfigTimerColor;
        public static ConfigEntry<string> ConfigDeltaColorNegative;
        public static ConfigEntry<string> ConfigDeltaColorPositive;
        public static ConfigEntry<string> ConfigDeltaColorGold;
        public static ConfigEntry<string> ConfigMenuTimeColor;

        public static ConfigEntry<int> ConfigTimerFontSize;
        public static ConfigEntry<int> ConfigDeltaFontSize;

        private void Awake()
        {
            ConfigEnableTimer = Config.Bind("General",
                "EnableTimer",
                true,
                "Enables or disables the main speedrun timer.");

            ConfigEnableDelta = Config.Bind("General",
                "EnableDeltaTimer",
                true,
                "Enables or disables live delta comparisons.");

            ConfigEnableQuickReset = Config.Bind("General",
                "EnableQuickReset",
                true,
                "Enables quick reset shortcut.");

            ConfigQuickResetKey = Config.Bind("General",
                "QuickResetKey",
                KeyCode.F9,
                "The key used for quick reset (returns to lobby or restarts run).");

            ConfigShowMenuTime = Config.Bind("General",
                "ShowMenuTime",
                true,
                "Enables or disables displaying mod PB time in the level selection menu.");

            ConfigTimerColor = Config.Bind("UI.Colors",
                "TimerColor",
                "#FFFFFF",
                "Hex color code for the main timer.");

            ConfigDeltaColorNegative = Config.Bind("UI.Colors",
                "DeltaColorNegative",
                "#00FF00",
                "Hex color code for negative delta (time save).");

            ConfigDeltaColorPositive = Config.Bind("UI.Colors",
                "DeltaColorPositive",
                "#FF0000",
                "Hex color code for positive delta (time loss).");

            ConfigDeltaColorGold = Config.Bind("UI.Colors",
                "DeltaColorGold",
                "#FFD700",
                "Hex color code for a gold split (best segment).");

            ConfigMenuTimeColor = Config.Bind("UI.Colors",
                "MenuTimeColor",
                "#9544B7",
                "Hex color code for the PB time displayed in the level selection menu.");

            ConfigTimerFontSize = Config.Bind("UI.Sizes",
                "TimerFontSize",
                70,
                "Font size for the main timer.");

            ConfigDeltaFontSize = Config.Bind("UI.Sizes",
                "DeltaFontSize",
                50,
                "Font size for the live delta timer.");

            SceneManager.sceneLoaded += OnSceneLoaded;
            Harmony harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            harmony.PatchAll();

            Logger.LogInfo($"Mod {PluginInfo.PLUGIN_NAME} loaded and Harmony injected");
        }

        public static string FormatTime(float timeInSeconds)
        {
            TimeSpan ts = TimeSpan.FromSeconds(timeInSeconds);
            return string.Format("{0:00}:{1:00}:{2:00}",
                                  Math.Floor(ts.TotalMinutes),
                                  ts.Seconds,
                                  ts.Milliseconds / 10);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ElapsedTime = 0f;
            IsTimerRunning = false;
            CurrentSplitIndex = 0;
            HasFinishedSplit = false;

            CurrentSplits.Clear();
            LoadedPBSplits.Clear();
            LoadedBestSegments.Clear();

            if (UiCanvasObject != null)
            {
                if (TimerText != null)
                {
                    TimerText.text = "00:00:00";
                    TimerText.gameObject.SetActive(false);
                }
                if (DeltaText != null)
                {
                    DeltaText.text = "";
                    DeltaText.gameObject.SetActive(false);
                }
                return;
            }

            UiCanvasObject = new GameObject("ModTimerCanvas");
            Canvas canvas = UiCanvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;

            UiCanvasObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            UiCanvasObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            UiCanvasObject.AddComponent<TimerUpdater>();

            DontDestroyOnLoad(UiCanvasObject);

            // Main Timer
            GameObject textObj = new GameObject("ModTimerText");
            textObj.transform.SetParent(UiCanvasObject.transform, false);

            TimerText = textObj.AddComponent<TextMeshProUGUI>();
            TimerText.text = "00:00:00";
            TimerText.fontSize = Plugin.ConfigTimerFontSize.Value;
            ColorUtility.TryParseHtmlString(Plugin.ConfigTimerColor.Value, out Color parsedColor);
            TimerText.color = parsedColor;
            TimerText.alignment = TextAlignmentOptions.BottomLeft;
            TimerText.enableWordWrapping = false;

            RectTransform rect = TimerText.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(0, 0);
            rect.pivot = new Vector2(0, 0);
            rect.anchoredPosition = new Vector2(20, 20);
            rect.sizeDelta = new Vector2(500, Plugin.ConfigTimerFontSize.Value + 20);

            TimerText.gameObject.SetActive(false);

            // Delta Timer
            GameObject deltaObj = new GameObject("ModDeltaText");
            deltaObj.transform.SetParent(UiCanvasObject.transform, false);

            DeltaText = deltaObj.AddComponent<TextMeshProUGUI>();
            DeltaText.text = "";
            DeltaText.fontSize = Plugin.ConfigDeltaFontSize.Value;
            DeltaText.alignment = TextAlignmentOptions.BottomLeft;
            DeltaText.enableWordWrapping = false;

            RectTransform deltaRect = DeltaText.GetComponent<RectTransform>();
            deltaRect.anchorMin = new Vector2(0, 0);
            deltaRect.anchorMax = new Vector2(0, 0);
            deltaRect.pivot = new Vector2(0, 0);
            float dynamicDeltaY = 20f + Plugin.ConfigTimerFontSize.Value + 10f;
            deltaRect.anchoredPosition = new Vector2(20, dynamicDeltaY);
            deltaRect.sizeDelta = new Vector2(500, Plugin.ConfigDeltaFontSize.Value + 20);

            DeltaText.gameObject.SetActive(false);

            Debug.Log("[SpeedrunMod] Timer UI successfully injected into the scene.");
        }
    }

    public class TimerUpdater : MonoBehaviour
    {
        private void Update()
        {
            if (Plugin.TimerText != null && !Plugin.ConfigEnableTimer.Value)
            {
                if (Plugin.TimerText.gameObject.activeSelf) Plugin.TimerText.gameObject.SetActive(false);
                if (Plugin.DeltaText != null && Plugin.DeltaText.gameObject.activeSelf) Plugin.DeltaText.gameObject.SetActive(false);
            }

            if (Plugin.IsTimerRunning && Plugin.TimerText != null)
            {
                Plugin.ElapsedTime += Time.deltaTime;
                Plugin.TimerText.text = Plugin.FormatTime(Plugin.ElapsedTime);

                if (Plugin.ConfigEnableTimer.Value && Plugin.ConfigEnableDelta.Value)
                {
                    if (!Plugin.HasFinishedSplit && Plugin.DeltaText != null && Plugin.LoadedPBSplits != null && Plugin.CurrentSplitIndex < Plugin.LoadedPBSplits.Count)
                    {
                        float pbSplitTime = Plugin.LoadedPBSplits[Plugin.CurrentSplitIndex];

                        float bestSegmentTime = Plugin.LoadedBestSegments.Count > Plugin.CurrentSplitIndex
                            ? Plugin.LoadedBestSegments[Plugin.CurrentSplitIndex]
                            : float.MaxValue;

                        float currentSegmentTime = Plugin.CurrentSplitIndex == 0
                            ? Plugin.ElapsedTime
                            : Plugin.ElapsedTime - Plugin.CurrentSplits[Plugin.CurrentSplitIndex - 1];

                        float liveDelta = Plugin.ElapsedTime - pbSplitTime;

                        if (Plugin.ElapsedTime > pbSplitTime)
                        {
                            Plugin.DeltaText.text = "+" + Plugin.FormatTime(Math.Abs(liveDelta));
                            if (ColorUtility.TryParseHtmlString(Plugin.ConfigDeltaColorPositive.Value, out Color posColor))
                            {
                                Plugin.DeltaText.color = posColor;
                            }
                            if (!Plugin.DeltaText.gameObject.activeSelf)
                                Plugin.DeltaText.gameObject.SetActive(true);
                        }
                        else if (currentSegmentTime > bestSegmentTime)
                        {
                            Plugin.DeltaText.text = "-" + Plugin.FormatTime(Math.Abs(liveDelta));
                            if (ColorUtility.TryParseHtmlString(Plugin.ConfigDeltaColorNegative.Value, out Color negColor))
                            {
                                Plugin.DeltaText.color = negColor;
                            }
                            if (!Plugin.DeltaText.gameObject.activeSelf)
                                Plugin.DeltaText.gameObject.SetActive(true);
                        }
                        else
                        {
                            if (Plugin.DeltaText.gameObject.activeSelf)
                                Plugin.DeltaText.gameObject.SetActive(false);
                        }
                    }
                }
                else
                {
                    if (Plugin.DeltaText != null && Plugin.DeltaText.gameObject.activeSelf)
                        Plugin.DeltaText.gameObject.SetActive(false);
                }
            }

            // ZAMIAST: if (Plugin.ConfigEnableQuickReset.Value && UnityEngine.Input.GetKeyDown(KeyCode.F9))
            if (Plugin.ConfigEnableQuickReset.Value && UnityEngine.Input.GetKeyDown(Plugin.ConfigQuickResetKey.Value))
            {
                if (GameUtil.isLobby)
                {
                    GameManager.NextRun();
                    Debug.Log($"[SpeedrunMod] Quick start from Lobby ({Plugin.ConfigQuickResetKey.Value})!");
                }
                else
                {
                    GameManager.Next(GameNextType.ServerLobby);
                    Debug.Log($"[SpeedrunMod] Run aborted! Safe return to Lobby ({Plugin.ConfigQuickResetKey.Value}).");
                }
            }

            // FOR DEBUGING
            //if (UnityEngine.Input.GetKeyDown(KeyCode.F8))
            //{
            //    var shiftManager = Aggro.Core.Networking.NetworkAggroManagerBase<ShiftManager>.instance;
            //    if (shiftManager != null)
            //    {
            //        var winMethod = HarmonyLib.AccessTools.Method(typeof(ShiftManager), "CmdShiftDevCmdWinShift");
            //        if (winMethod != null)
            //        {
            //            winMethod.Invoke(shiftManager, new object[] { ContractScore.S });
            //            Debug.Log("[SpeedrunMod] Dev command triggered: Instant win (Rank S)!");
            //        }
            //    }
            //}
        }
    }

    public static class SplitsManager
    {
        public static string SaveFilePath => Path.Combine(Paths.PluginPath, "SpeedrunSplits.json");

        public static SplitsSaveFile LoadSplits()
        {
            if (!File.Exists(SaveFilePath))
            {
                Debug.Log("[SpeedrunMod] No existing splits file found. Creating a new one.");
                return new SplitsSaveFile();
            }

            try
            {
                string json = File.ReadAllText(SaveFilePath);
                return JsonConvert.DeserializeObject<SplitsSaveFile>(json) ?? new SplitsSaveFile();
            }
            catch (Exception e)
            {
                Debug.LogError($"[SpeedrunMod] Error loading splits: {e.Message}");
                return new SplitsSaveFile();
            }
        }

        public static void SaveSplits(SplitsSaveFile data)
        {
            try
            {
                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(SaveFilePath, json);
                Debug.Log("[SpeedrunMod] Splits saved successfully.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SpeedrunMod] Failed to save splits: {e.Message}");
            }
        }

        public static void LoadLevelDataIntoMemory(string levelName)
        {
            Plugin.LoadedPBSplits.Clear();
            Plugin.LoadedBestSegments.Clear();

            SplitsSaveFile fileData = LoadSplits();
            SplitRecord record = fileData.records.Find(r => r.levelName == levelName);

            if (record != null)
            {
                if (record.pbSplits != null) Plugin.LoadedPBSplits = new List<float>(record.pbSplits);
                if (record.bestSegments != null) Plugin.LoadedBestSegments = new List<float>(record.bestSegments);
            }

            if (record != null)
            {
                Debug.Log($"[SpeedrunMod] Loaded PB data for {levelName}. Total time to beat: {Plugin.FormatTime(record.totalTime)}");
            }
            else
            {
                Debug.Log($"[SpeedrunMod] No previous PB found for {levelName}. This is a new run!");
            }
        }

        public static void ProcessAndSaveRun(string levelName, List<float> runSplits, bool isRunCompleted)
        {
            if (runSplits.Count == 0 || string.IsNullOrEmpty(levelName) || levelName == "UnknownLevel") return;

            SplitsSaveFile fileData = LoadSplits();
            SplitRecord record = fileData.records.Find(r => r.levelName == levelName);
            bool needsSaving = false;

            if (record == null)
            {
                record = new SplitRecord { levelName = levelName, totalTime = float.MaxValue };
                fileData.records.Add(record);
                needsSaving = true;
            }

            if (record.bestSegments == null) record.bestSegments = new List<float>();
            if (record.pbSplits == null) record.pbSplits = new List<float>();

            for (int i = 0; i < runSplits.Count; i++)
            {
                float segmentTime = runSplits[i];
                if (i > 0)
                {
                    segmentTime -= runSplits[i - 1];
                }

                if (record.bestSegments.Count <= i)
                {
                    record.bestSegments.Add(segmentTime);
                    needsSaving = true;
                }
                else if (segmentTime < record.bestSegments[i])
                {
                    record.bestSegments[i] = segmentTime;
                    needsSaving = true;
                    Debug.Log($"[SpeedrunMod] GOLD SPLIT! Shift {i + 1} completed in {segmentTime:F2}s");
                }
            }

            if (isRunCompleted)
            {
                float finalTime = runSplits[runSplits.Count - 1];
                if (finalTime < record.totalTime)
                {
                    record.totalTime = finalTime;
                    record.pbSplits = new List<float>(runSplits);
                    needsSaving = true;
                    Debug.Log($"[SpeedrunMod] NEW PB! Total time: {finalTime:F2}s");
                }
            }

            if (needsSaving)
            {
                SaveSplits(fileData);
            }
        }
    }

    [HarmonyPatch(typeof(ShiftManager))]
    public class ShiftManagerPatches
    {
        [HarmonyPatch("ShiftChanged")]
        [HarmonyPostfix]
        public static void Postfix_ShiftChanged(ShiftPhase phase, int shift, int outboundsRequired)
        {
            if (Plugin.TimerText != null && !Plugin.TimerText.gameObject.activeSelf && Plugin.ConfigEnableTimer.Value)
            {
                Plugin.TimerText.gameObject.SetActive(true);
            }

            if (phase == ShiftPhase.Shift)
            {
                Plugin.HasFinishedSplit = false;
                if (Plugin.DeltaText != null) Plugin.DeltaText.gameObject.SetActive(false);

                if (shift == 1 && !Plugin.IsTimerRunning && Plugin.ElapsedTime < 0.1f)
                {
                    if (GameUtil.contract != null)
                    {
                        Plugin.CurrentLevelName = GameUtil.contract.name;
                        SplitsManager.LoadLevelDataIntoMemory(Plugin.CurrentLevelName);
                        Debug.Log($"[SpeedrunMod] Started: {Plugin.CurrentLevelName}");
                    }
                }
                Plugin.IsTimerRunning = true;
            }
            else
            {
                Plugin.IsTimerRunning = false;
            }
        }

        private static void RecordSplit(byte shiftCount, bool isRunCompleted)
        {
            Debug.Log($"[SpeedrunMod] Shift {shiftCount} completed at {Plugin.FormatTime(Plugin.ElapsedTime)}");
            Plugin.IsTimerRunning = false;
            int splitIndex = Plugin.CurrentSplits.Count;
            Plugin.CurrentSplits.Add(Plugin.ElapsedTime);

            bool canShowDelta = Plugin.ConfigEnableTimer.Value && Plugin.ConfigEnableDelta.Value;

            if (canShowDelta && Plugin.LoadedPBSplits != null && splitIndex < Plugin.LoadedPBSplits.Count)
            {
                float pbTime = Plugin.LoadedPBSplits[splitIndex];
                float delta = Plugin.ElapsedTime - pbTime;

                float currentSegmentTime = splitIndex == 0 ? Plugin.ElapsedTime : Plugin.ElapsedTime - Plugin.CurrentSplits[splitIndex - 1];
                bool isGold = false;

                if (Plugin.LoadedBestSegments != null && splitIndex < Plugin.LoadedBestSegments.Count)
                {
                    if (currentSegmentTime < Plugin.LoadedBestSegments[splitIndex]) isGold = true;
                }

                string prefix = delta > 0 ? "+" : "-";
                Plugin.DeltaText.text = prefix + Plugin.FormatTime(Math.Abs(delta));

                if (isGold)
                {
                    if (ColorUtility.TryParseHtmlString(Plugin.ConfigDeltaColorGold.Value, out Color goldColor))
                    {
                        Plugin.DeltaText.color = goldColor;
                    }
                }
                else if (delta <= 0)
                {
                    if (ColorUtility.TryParseHtmlString(Plugin.ConfigDeltaColorNegative.Value, out Color negColor))
                    {
                        Plugin.DeltaText.color = negColor;
                    }
                }
                else
                {
                    if (ColorUtility.TryParseHtmlString(Plugin.ConfigDeltaColorPositive.Value, out Color posColor))
                    {
                        Plugin.DeltaText.color = posColor;
                    }
                }

                Plugin.DeltaText.gameObject.SetActive(true);
                Plugin.HasFinishedSplit = true;
            }
            else
            {
                if (Plugin.DeltaText != null)
                {
                    Plugin.DeltaText.gameObject.SetActive(false);
                }
            }

            Plugin.CurrentSplitIndex = Plugin.CurrentSplits.Count;
            SplitsManager.ProcessAndSaveRun(Plugin.CurrentLevelName, Plugin.CurrentSplits, isRunCompleted);
        }

        [HarmonyPatch("UserCode_RpcTransitionShiftToShiftWon__Byte__ContractScore__Vector3")]
        [HarmonyPostfix]
        public static void StopTimer_ShiftWon(byte shiftCount) { RecordSplit(shiftCount, false); }

        [HarmonyPatch("UserCode_RpcTransitionShiftToShiftWonPhase1__Byte__ContractScore__Vector3")]
        [HarmonyPostfix]
        public static void StopTimer_ShiftWonPhase1(byte shiftCount) { RecordSplit(shiftCount, false); }

        [HarmonyPatch("UserCode_RpcTransitionShiftToGameWon__Byte__ContractScore__ContractScore__Int32__Vector3__PlayerResult[]")]
        [HarmonyPostfix]
        public static void StopTimer_GameWon(byte shiftCount) { RecordSplit(shiftCount, true); }

        [HarmonyPatch("UserCode_RpcTransitionShiftToGameLost__Byte__Int32__Vector3__PlayerResult[]")]
        [HarmonyPostfix]
        public static void StopTimer_GameLost(byte shiftCount)
        {
            Plugin.IsTimerRunning = false;
            Debug.Log("[SpeedrunMod] Run lost! Timer stopped. Splits ignored.");
        }
    }

    [HarmonyPatch(typeof(ContractSelectionUI))]
    public class ContractSelectionUIPatches
    {
        [HarmonyPatch("SetUp")]
        [HarmonyPostfix]
        public static void Postfix_SetUp(ContractSelectionUI __instance)
        {
            if (!Plugin.ConfigShowMenuTime.Value)
            {
                return;
            }

            SplitsSaveFile fileData = SplitsManager.LoadSplits();

            for (int i = 0; i < __instance._contracts.Count; i++)
            {
                ContractObject contract = __instance._contracts[i];

                if (i < __instance.contractGroup.childCount)
                {
                    ContractUI ui = __instance.contractGroup.GetChild(i).GetComponent<ContractUI>();

                    if (ui != null)
                    {
                        SplitRecord record = fileData.records.Find(r => r.levelName == contract.name);

                        if (record != null && record.totalTime < float.MaxValue)
                        {
                            string pbText = Plugin.FormatTime(record.totalTime);

                            Transform parent;
                            if (contract.type == ContractType.Random)
                            {
                                parent = ui.transform;
                            }
                            else
                            {
                                parent = ui.bestTimeText.transform.parent;
                            }
                            Transform existingModText = parent.Find("ModSpeedrunText");
                            TextMeshProUGUI modText;

                            if (existingModText == null)
                            {
                                GameObject modObj = new GameObject("ModSpeedrunText");
                                modObj.transform.SetParent(parent, false);

                                RectTransform modRect = modObj.AddComponent<RectTransform>();
                                modText = modObj.AddComponent<TextMeshProUGUI>();

                                RectTransform origRect = ui.bestTimeText.GetComponent<RectTransform>();
                                modRect.anchorMin = origRect.anchorMin;
                                modRect.anchorMax = origRect.anchorMax;
                                modRect.pivot = origRect.pivot;
                                modRect.sizeDelta = new Vector2(origRect.sizeDelta.x * 1.5f, 150f);

                                if (contract.type == ContractType.Random)
                                {
                                    modRect.anchoredPosition = origRect.anchoredPosition + new Vector2(95, -350f);
                                }
                                else
                                {
                                    modRect.anchoredPosition = origRect.anchoredPosition + new Vector2(65, -110f);
                                }

                                modText.font = ui.bestTimeText.font;
                                modText.fontSize = 60;
                                modText.alignment = TextAlignmentOptions.TopLeft;
                                modText.enableAutoSizing = false;
                                modText.overflowMode = TextOverflowModes.Overflow;
                            }
                            else
                            {
                                modText = existingModText.GetComponent<TextMeshProUGUI>();
                            }

                            modText.text = $"<color={Plugin.ConfigMenuTimeColor.Value}><b>{pbText}</b></color>";
                        }
                    }
                }
            }
        }
    }

    [Serializable]
    public class SplitRecord
    {
        public string levelName;
        public float totalTime;
        public List<float> pbSplits = new List<float>();
        public List<float> bestSegments = new List<float>();
    }

    [Serializable]
    public class SplitsSaveFile
    {
        public List<SplitRecord> records = new List<SplitRecord>();
    }
}