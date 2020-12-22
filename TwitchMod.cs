using System;
using System.Reflection;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using UnityEngine;
using HarmonyLib;
using Poly.Math;
using BepInEx;
using BepInEx.Configuration;
using PolyTechFramework;
using UnityEngine.Networking;

namespace TwitchMod
{
    [BepInPlugin(pluginGuid, pluginName, pluginVerson)]
    [BepInProcess("Poly Bridge 2")]
    [BepInDependency(PolyTechMain.PluginGuid, BepInDependency.DependencyFlags.HardDependency)]
    public class TwitchMain : PolyTechMod
    {

        public const string pluginGuid = "polytech.twitchmod";

        public const string pluginName = "Twitch Mod";

        public const string pluginVerson = "1.0.0";

        public static ConfigDefinition modEnableDef = new ConfigDefinition(pluginName, "Enable/Disable Mod");
        public static ConfigDefinition NameDef = new ConfigDefinition(pluginName, "Streamer Name");
        public static ConfigDefinition SendBridgeDef = new ConfigDefinition(pluginName, "Send Bridge");
        public static ConfigDefinition LoadLayoutDef = new ConfigDefinition(pluginName, "Load Layout");
        public static ConfigDefinition KeyDef = new ConfigDefinition(pluginName, "Key");

        public static ConfigEntry<bool> mEnabled;

        public static ConfigEntry<string> mName;
        public static string LastName = "";
        
        public static ConfigEntry<string> mKey;

        public static ConfigEntry<KeyboardShortcut> mSendBridge;
        public static bool KeyIsDown = false;
        public static bool SendingBridge = false;

        public static ConfigEntry<KeyboardShortcut> mLoadLayout;
        public static bool LayoutKeyIsDown = false;
        public static bool GettingLayout = false;

        public static string ID = "";
        public static bool FindingID = false;

        public static string Key = "";

        public static TwitchMain instance;

        void Awake()
        {
            if (instance == null) instance = this;

            int order = 0;

            Config.Bind(modEnableDef, true, new ConfigDescription("Controls if the mod should be enabled or disabled", null, new ConfigurationManagerAttributes { Order = order }));
            mEnabled = (ConfigEntry<bool>)Config[modEnableDef];
            mEnabled.SettingChanged += onEnableDisable;
            order--;

            Config.Bind(NameDef, "", new ConfigDescription("Which streamer to send the bridge to", null, new ConfigurationManagerAttributes { Order = order }));
            mName = (ConfigEntry<string>)Config[NameDef];
            order--;

            mSendBridge = Config.Bind(SendBridgeDef, new KeyboardShortcut(KeyCode.Tab), new ConfigDescription("What button sends the bridge", null, new ConfigurationManagerAttributes { Order = order }));
            order--;

            mLoadLayout = Config.Bind(LoadLayoutDef, new KeyboardShortcut(KeyCode.LeftBracket), new ConfigDescription("What button loads the current layout the streamer has", null, new ConfigurationManagerAttributes { Order = order }));
            order--;

            Config.Bind(KeyDef, "", new ConfigDescription("Don't change this unless you know what you're doing!", null, new ConfigurationManagerAttributes { Order = order , IsAdvanced = true}));
            mKey = (ConfigEntry<string>)Config[KeyDef];
            order--;


            Config.SettingChanged += onSettingChanged;
            onSettingChanged(null, null);

            Harmony harmony = new Harmony(pluginGuid);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            isCheat = false;
            isEnabled = mEnabled.Value;

            PolyTechMain.registerMod(this);
        }

        public void onEnableDisable(object sender, EventArgs e)
        {
            isEnabled = mEnabled.Value;
        }

        public void onSettingChanged(object sender, EventArgs e)
        {

        }

        private static void OnPullKeyComplete(UnityEngine.AsyncOperation asyncOperation)
        {
            Debug.Log("Completed");
            FindingID = false;
            GameUI.m_Instance.m_Status.Close();
            UnityWebRequestAsyncOperation unityWebRequestAsyncOperation = (UnityWebRequestAsyncOperation)asyncOperation;
            Debug.Log(unityWebRequestAsyncOperation.webRequest.downloadHandler.text);
            if (unityWebRequestAsyncOperation.webRequest.isNetworkError || unityWebRequestAsyncOperation.webRequest.isHttpError)
            {
                string errorMessage = WebRequest.GetErrorMessage(unityWebRequestAsyncOperation.webRequest);
                Debug.LogWarning("https://id.twitch.tv/oauth2/token?client_id=nhmpgqu2aehuztyb1r5tkx2cad060n&client_secret=w1dla4hhs4r4u5gmklc395nfiqjgsv&grant_type=client_credentials" + " failed with: " + errorMessage);
                GameUI.m_Instance.m_TopBar.m_MessageTopLeft.ShowMessage("Could not get ID: " + errorMessage, 3);
                PopUpWarning.Display("Could not get ID: " + errorMessage);
            }
            else
            {
                FindingID = true;
                GameUI.m_Instance.m_Status.Open("Getting streamer id (2/2)");
                TwitchResponse response = JsonUtility.FromJson<TwitchResponse>(unityWebRequestAsyncOperation.webRequest.downloadHandler.text);
                Key = response.access_token;
                Debug.Log("Getting streamer ID...");
                UnityWebRequest IDRequest = WebRequest.Get("https://api.twitch.tv/helix/users?login=" + mName.Value, PolyTwitch.m_Key);
                IDRequest.SetRequestHeader("Authorization", "Bearer " + Key);
                IDRequest.SetRequestHeader("Client-ID", "nhmpgqu2aehuztyb1r5tkx2cad060n");
                IDRequest.SendWebRequest().completed += OnPullIdComplete;
            }
        }

        private static void OnPullIdComplete(UnityEngine.AsyncOperation asyncOperation)
        {
            Debug.Log("Completed");
            FindingID = false;
            GameUI.m_Instance.m_Status.Close();
            UnityWebRequestAsyncOperation unityWebRequestAsyncOperation = (UnityWebRequestAsyncOperation)asyncOperation;
            Debug.Log(unityWebRequestAsyncOperation.webRequest.downloadHandler.text);
            if (unityWebRequestAsyncOperation.webRequest.isNetworkError || unityWebRequestAsyncOperation.webRequest.isHttpError)
            {
                string errorMessage = WebRequest.GetErrorMessage(unityWebRequestAsyncOperation.webRequest);
                Debug.LogWarning("https://api.twitch.tv/helix/users?login=" + mName.Value + " failed with: " + errorMessage);
                GameUI.m_Instance.m_TopBar.m_MessageTopLeft.ShowMessage("Could not get ID: " + errorMessage, 3);
                PopUpWarning.Display("Could not get ID: " + errorMessage);
            }
            else
            {
                char[] Text = unityWebRequestAsyncOperation.webRequest.downloadHandler.text.ToCharArray();

                string LastChars = "";
                bool idFound = false;
                int countdown = 2;
                string foundID = "";
                for (int i = 0; i < Text.Length; i++)
                {
                    char chr = Text[i];
                    if (idFound && countdown <= 0)
                    {
                        if (chr == '"')
                        {
                            idFound = false;
                        }
                        else
                        {
                            foundID += chr;
                        }
                    }
                    if (idFound) countdown--;

                    LastChars = "";
                    for (int j = 0; j < 4; j++)
                    {
                        if (i - j >= 0) LastChars += Text[i - j];
                    }
                    idFound = idFound || LastChars == "\"di\"";
                }
                if (foundID != "")
                {
                    Debug.Log("ID found! " + foundID);
                    if (GameStateManager.GetState() != GameState.MAIN_MENU)
                    {
                        GameUI.m_Instance.m_TopBar.m_MessageTopLeft.ShowMessage("Found ID: " + foundID, 3);
                    }
                    ID = foundID;
                }
                else
                {
                    Debug.LogWarning("ID not found");
                    if (GameStateManager.GetState() != GameState.MAIN_MENU)
                    {
                        GameUI.m_Instance.m_TopBar.m_MessageTopLeft.ShowMessage("Streamer doesn't exist!", 3);
                    }
                }
            }
        }



        public override void enableMod()
        {
            this.isEnabled = true;
            mEnabled.Value = true;
            onEnableDisable(null, null);
        }

        public override void disableMod()
        {
            this.isEnabled = false;
            mEnabled.Value = false;
            onEnableDisable(null, null);
        }

        public override string getSettings()
        {
            return "";
        }

        public override void setSettings(string st)
        {
            return;
        }

        private static bool CheckForCheating()
        {
            return mEnabled.Value && PolyTechMain.modEnabled.Value;
        }



        [HarmonyPatch(typeof(Main), "Update")]
        private static class patchUpdate
        {
            private static void Postfix()
            {
                if (!CheckForCheating()) return;


                if (LastName != mName.Value && !FindingID && !SendingBridge && !GettingLayout)
                {
                    FindingID = true;
                    LastName = mName.Value;
                    GameUI.m_Instance.m_Status.Open("Getting streamer id");

                    GameUI.m_Instance.m_TopBar.m_MessageTopLeft.ShowMessage("Getting Streamer ID (1/2)", 3);
                    Debug.Log("Getting Twitch Key...");
                    WebRequest.Post("https://id.twitch.tv/oauth2/token?client_id=nhmpgqu2aehuztyb1r5tkx2cad060n&client_secret=w1dla4hhs4r4u5gmklc395nfiqjgsv&grant_type=client_credentials", PolyTwitch.m_Key).SendWebRequest().completed += OnPullKeyComplete;
                }

                if (mSendBridge.Value.IsPressed() && !KeyIsDown && !SendingBridge && !FindingID && !GettingLayout)
                {
                    GameUI.m_Instance.m_TopBar.OnPauseSim();
                    bool Broken = false;
                    foreach (BridgeEdge Edge in BridgeEdges.m_Edges)
                    {
                        Broken = Edge.m_IsBroken || Broken;
                    }
                        

                    if (Broken)
                    {
                        PopUpWarning.Display("Can't send bridge with broken edge!");
                    }
                    else
                    {
                        PopUpMessage.Display("Do you want to send the bridge?", instance.SendBridge);
                    }
                }
                KeyIsDown = mSendBridge.Value.IsPressed();


                if (mLoadLayout.Value.IsPressed() && !LayoutKeyIsDown && !SendingBridge && !FindingID && !GettingLayout)
                {
                    PopUpMessage.Display("Do you want to load the layout?", instance.GetLayout);
                }
                LayoutKeyIsDown = mLoadLayout.Value.IsPressed();
            }
        }



        public void SendBridge()
        {
            foreach (BridgeEdge Edge in BridgeEdges.m_Edges)
            {
                if (Edge.m_IsBroken)
                {
                    PopUpWarning.Display("Can't send bridge with broken edge!");
                    return;
                }
            }

            SendingBridge = true;
            GameUI.m_Instance.m_Status.Open("Sending bridge (1/3)");
            GameUI.m_Instance.m_TopBar.m_MessageTopLeft.ShowMessage("Sending bridge...", 3);
            Debug.Log("Connecting to streamer...");

            UnityWebRequest request = WebRequest.Post("https://api.t2.drycactus.com/v1/" + "viewer/stream/" + ID + "/connect", mKey.Value);
            request.SetRequestHeader("Authorization", "Bearer " + mKey.Value);
            request.SetRequestHeader("accept", "application/json");
            request.SendWebRequest().completed += OnConnectComplete;
        }
        
        private static void OnConnectComplete(UnityEngine.AsyncOperation asyncOperation)
        {
            Debug.Log("Completed");
            SendingBridge = false;
            GameUI.m_Instance.m_Status.Close();
            UnityWebRequestAsyncOperation unityWebRequestAsyncOperation = (UnityWebRequestAsyncOperation)asyncOperation;
            Debug.Log(unityWebRequestAsyncOperation.webRequest.downloadHandler.text);
            if (unityWebRequestAsyncOperation.webRequest.isNetworkError || unityWebRequestAsyncOperation.webRequest.isHttpError)
            {
                string errorMessage = WebRequest.GetErrorMessage(unityWebRequestAsyncOperation.webRequest);
                Debug.LogWarning("https://api.t2.drycactus.com/v1/" + "viewer/stream/" + ID + "/connect failed with: " + errorMessage);
                PopUpWarning.Display("Could not send bridge: " + errorMessage);
            }
            else
            {
                SendingBridge = true;
                GameUI.m_Instance.m_Status.Open("Sending bridge (2/3)");
                Debug.Log("Getting level hash");
                UnityWebRequest BridgeRequest = WebRequest.Get("https://api.t2.drycactus.com/v1/" + "viewer/stream/" + ID + "/pull?delay=0", mKey.Value);
                BridgeRequest.SetRequestHeader("Authorization", "Bearer " + mKey.Value);
                BridgeRequest.SetRequestHeader("accept", "application/json");
                BridgeRequest.SendWebRequest().completed += PushBridge;
            }
        }

        private static void PushBridge(UnityEngine.AsyncOperation asyncOperation)
        {
            Debug.Log("Completed");
            SendingBridge = false;
            GameUI.m_Instance.m_Status.Close();
            UnityWebRequestAsyncOperation unityWebRequestAsyncOperation = (UnityWebRequestAsyncOperation)asyncOperation;
            Debug.Log(unityWebRequestAsyncOperation.webRequest.downloadHandler.text);
            if (unityWebRequestAsyncOperation.webRequest.isNetworkError || unityWebRequestAsyncOperation.webRequest.isHttpError)
            {
                string errorMessage = WebRequest.GetErrorMessage(unityWebRequestAsyncOperation.webRequest);
                Debug.LogWarning("https://api.t2.drycactus.com/v1/" + "viewer/stream/" + ID + "/pull?delay=0 failed with: " + errorMessage);
                PopUpWarning.Display("Could not send bridge: " + errorMessage);
                GameUI.m_Instance.m_TopBar.m_MessageTopLeft.ShowMessage(unityWebRequestAsyncOperation.webRequest.downloadHandler.text, 3);
            }
            else
            {

                foreach (BridgeEdge Edge in BridgeEdges.m_Edges)
                {
                    if (Edge.m_IsBroken)
                    {
                        PopUpWarning.Display("Can't send bridge with broken edge!");
                        return;
                    }
                }

                SendingBridge = true;
                GameUI.m_Instance.m_Status.Open("Sending bridge (3/3)");

                JSONObject json = new JSONObject(unityWebRequestAsyncOperation.webRequest.downloadHandler.text);

                string LevelHash = "";
                if (json.HasField("level_hash"))
                {
                    json.GetField(ref LevelHash, "level_hash");
                }


                if (LevelHash != "")
                {
                    Debug.Log("Level Hash found! " + LevelHash);
                }
                else
                {
                    Debug.LogWarning("Level Hash not found");
                    SendingBridge = false;
                    GameUI.m_Instance.m_Status.Close();
                    return;
                }

                Debug.Log("Sending bridge...");

                byte[] payloadBytes = BridgeSave.SerializeBinary();

                payloadBytes = payloadBytes.AddToArray<byte>(255);
                payloadBytes = payloadBytes.AddToArray<byte>(255);
                payloadBytes = payloadBytes.AddToArray<byte>(255);
                payloadBytes = payloadBytes.AddToArray<byte>(255);
                
                byte[] payloadCompressed = Utils.ZipPayload(payloadBytes);
                string payload_md5 = Utils.MD5HashFor(payloadCompressed);

                if (mName.Value.ToLower() == Profile.m_TwitchUsername.ToLower())
                {
                    ConsumeReply consumeReply = new ConsumeReply
                    {
                        id = ID,
                        owner = new Owner
                        {
                            id = ID,
                            username = Profile.m_TwitchUsername
                        },
                        level_hash = LevelHash,
                        twitch_bits_used = 0,
                    };

                    string id = consumeReply.id;
                    string username = consumeReply.owner.username;
                    string id2 = consumeReply.owner.id;
                    string level_hash = consumeReply.level_hash;
                    int twitch_bits_used = consumeReply.twitch_bits_used;
                    if (!Utils.MD5HashesMatch(level_hash, Sandbox.m_CurrentLayoutHash))
                    {
                        return;
                    }
                    if (twitch_bits_used > 0)
                    {
                        InterfaceAudio.Play("ui_twitch_bits");
                    }
                    string bridgeHash = Utils.MD5HashFor(payloadBytes);
                    if (PolyTwitchSuggestions.SuggestionFromSameOwnerExists(id2, bridgeHash))
                    {
                        PolyTwitchSuggestions.UpdateSuggestionTimeAndBits(id2, bridgeHash, twitch_bits_used);
                        return;
                    }
                    int num = 0;
                    BridgeSaveData bridgeSaveData = new BridgeSaveData();
                    bridgeSaveData.DeserializeBinary(payloadBytes, ref num);
                    PolyTwitchSuggestions.Create(username, id2, id, bridgeSaveData, bridgeHash, level_hash, PolyTwitchSuggestionTag.NONE, twitch_bits_used);
                }


                WWWForm wwwform = new WWWForm();
                wwwform.AddBinaryData("payload", payloadCompressed, "file.zip", "zip");
                wwwform.AddField("payload_hash", payload_md5);
                wwwform.AddField("level_hash", LevelHash);
                UnityWebRequest BridgeRequest = WebRequest.Post("https://api.t2.drycactus.com/v1/" + "viewer/stream/" + ID + "/push", mKey.Value, wwwform);
                BridgeRequest.SetRequestHeader("Authorization", "Bearer " + mKey.Value);
                BridgeRequest.SetRequestHeader("accept", "application/json");
                BridgeRequest.SendWebRequest().completed += OnPushBridgeComplete;
            }
        }
        
        private static void OnPushBridgeComplete(UnityEngine.AsyncOperation asyncOperation)
        {
            SendingBridge = false;
            GameUI.m_Instance.m_Status.Close();
            UnityWebRequestAsyncOperation unityWebRequestAsyncOperation = (UnityWebRequestAsyncOperation)asyncOperation;
            Debug.Log(unityWebRequestAsyncOperation.webRequest.downloadHandler.text);
            if (unityWebRequestAsyncOperation.webRequest.isNetworkError || unityWebRequestAsyncOperation.webRequest.isHttpError)
            {
                string errorMessage = WebRequest.GetErrorMessage(unityWebRequestAsyncOperation.webRequest);
                Debug.LogWarning("https://api.t2.drycactus.com/v1/" + "viewer/stream/" + ID + "/push" + " failed with: " + errorMessage);
                PopUpWarning.Display("Could not send bridge: " + errorMessage);
                GameUI.m_Instance.m_TopBar.m_MessageTopLeft.ShowMessage(unityWebRequestAsyncOperation.webRequest.downloadHandler.text, 3);
            }
            else
            {
                GameUI.m_Instance.m_TopBar.m_MessageTopLeft.ShowMessage("The bridge was sent successfully!", 3);
                PopUpWarning.Display("The bridge was sent successfully!");
            }
        }
        
        
        public void GetLayout()
        {
            GettingLayout = true;
            GameUI.m_Instance.m_Status.Open("Getting layout (1/3)");
            Debug.Log("Connecting to streamer...");

            UnityWebRequest request = WebRequest.Post("https://api.t2.drycactus.com/v1/" + "viewer/stream/" + ID + "/connect", mKey.Value);
            request.SetRequestHeader("Authorization", "Bearer " + mKey.Value);
            request.SetRequestHeader("accept", "application/json");
            request.SendWebRequest().completed += OnConnectComplete2;
        }

        private static void OnConnectComplete2(UnityEngine.AsyncOperation asyncOperation)
        {
            Debug.Log("Completed");
            GettingLayout = false;
            GameUI.m_Instance.m_Status.Close();
            UnityWebRequestAsyncOperation unityWebRequestAsyncOperation = (UnityWebRequestAsyncOperation)asyncOperation;
            Debug.Log(unityWebRequestAsyncOperation.webRequest.downloadHandler.text);
            if (unityWebRequestAsyncOperation.webRequest.isNetworkError || unityWebRequestAsyncOperation.webRequest.isHttpError)
            {
                string errorMessage = WebRequest.GetErrorMessage(unityWebRequestAsyncOperation.webRequest);
                Debug.LogWarning("https://api.t2.drycactus.com/v1/" + "viewer/stream/" + ID + "/connect failed with: " + errorMessage);
                PopUpWarning.Display("Could not get layout: " + errorMessage);
            }
            else
            {
                GettingLayout = true;
                GameUI.m_Instance.m_Status.Open("Getting layout (2/3)");
                Debug.Log("Getting payload url");
                UnityWebRequest BridgeRequest = WebRequest.Get("https://api.t2.drycactus.com/v1/" + "viewer/stream/" + ID + "/pull?delay=0", mKey.Value);
                BridgeRequest.SetRequestHeader("Authorization", "Bearer " + mKey.Value);
                BridgeRequest.SetRequestHeader("accept", "application/json");
                BridgeRequest.SendWebRequest().completed += GetLayoutPayload;
            }
        }

        private static void GetLayoutPayload(UnityEngine.AsyncOperation asyncOperation)
        {
            Debug.Log("Completed");
            GettingLayout = false;
            GameUI.m_Instance.m_Status.Close();
            UnityWebRequestAsyncOperation unityWebRequestAsyncOperation = (UnityWebRequestAsyncOperation)asyncOperation;
            Debug.Log(unityWebRequestAsyncOperation.webRequest.downloadHandler.text);
            if (unityWebRequestAsyncOperation.webRequest.isNetworkError || unityWebRequestAsyncOperation.webRequest.isHttpError)
            {
                string errorMessage = WebRequest.GetErrorMessage(unityWebRequestAsyncOperation.webRequest);
                Debug.LogWarning("https://api.t2.drycactus.com/v1/" + "viewer/stream/" + ID + "/pull?delay=0 failed with: " + errorMessage);
                PopUpWarning.Display("Could not get layout: " + errorMessage);
                GameUI.m_Instance.m_TopBar.m_MessageTopLeft.ShowMessage(unityWebRequestAsyncOperation.webRequest.downloadHandler.text, 3);
            }
            else
            {
                JSONObject json = new JSONObject(unityWebRequestAsyncOperation.webRequest.downloadHandler.text);
                if (json.HasField("payload") && json.HasField("level_hash"))
                {
                    string LevelHash = "";
                    json.GetField(ref LevelHash, "level_hash");
                    if (LevelHash == "mainMenu")
                    {
                        PopUpWarning.Display("Streamer is in the main menu");
                        return;
                    }

                    GettingLayout = true;
                    GameUI.m_Instance.m_Status.Open("Getting layout (3/3)");
                    Debug.Log("Getting layout payload");

                    string URL = "";
                    json.GetField(ref URL, "payload");
                    UnityWebRequest BridgeRequest = WebRequest.Get(URL.Replace("\\", ""), mKey.Value);
                    BridgeRequest.SetRequestHeader("Authorization", "Bearer " + mKey.Value);
                    BridgeRequest.SetRequestHeader("accept", "arraybuffer");
                    BridgeRequest.SendWebRequest().completed += GenerateLayout;
                }
                else
                {
                    PopUpWarning.Display("Could not get layout");
                }
            }
        }

        private static void GenerateLayout(UnityEngine.AsyncOperation asyncOperation)
        {
            Debug.Log("Completed");
            GettingLayout = false;
            GameUI.m_Instance.m_Status.Close();
            UnityWebRequestAsyncOperation unityWebRequestAsyncOperation = (UnityWebRequestAsyncOperation)asyncOperation;
            Debug.Log(unityWebRequestAsyncOperation.webRequest.downloadHandler.data);
            if (unityWebRequestAsyncOperation.webRequest.isNetworkError || unityWebRequestAsyncOperation.webRequest.isHttpError)
            {
                string errorMessage = WebRequest.GetErrorMessage(unityWebRequestAsyncOperation.webRequest);
                Debug.LogWarning(unityWebRequestAsyncOperation.webRequest.url + " failed with: " + errorMessage);
                PopUpWarning.Display("Could not get layout: " + errorMessage);
                GameUI.m_Instance.m_TopBar.m_MessageTopLeft.ShowMessage("Could not get layout: " + errorMessage, 3);
            }
            else
            {
                SandboxLayoutData layoutData = new SandboxLayoutData();

                try
                {
                    byte[] arrayBuffer = unityWebRequestAsyncOperation.webRequest.downloadHandler.data;
                    byte[] result = Utils.UnZipPayload(arrayBuffer);
                    int offset = 0;

                    layoutData.DeserializeBinary(result, ref offset);
                }
                catch
                {
                    PopUpWarning.Display("Could not ");
                }

                GameStateManager.SwitchToStateImmediate(GameState.BUILD);
                GameManager.SetGameMode(GameMode.SANDBOX);
                
                Sandbox.Clear();
                Sandbox.Load(layoutData.m_ThemeStubKey, layoutData, true);
            }
        }



        [HarmonyPatch(typeof(PolyTwitch), "AuthorizeWithKey")]
        private static class patchAuthorize
        {
            private static void Postfix(string key)
            {
                mKey.Value = key;
            }
        }
    }



    public class TwitchResponse
    {
        //{"access_token":"mwre5kk0jwgutslfqstlyu6ovekd37","expires_in":5047347,"token_type":"bearer"}
        public string access_token = "";
        public int expires_in = 0;
        public string token_type = "";
    }




    /// <summary>
    /// Class that specifies how a setting should be displayed inside the ConfigurationManager settings window.
    /// 
    /// Usage:
    /// This class template has to be copied inside the plugin's project and referenced by its code directly.
    /// make a new instance, assign any fields that you want to override, and pass it as a tag for your setting.
    /// 
    /// If a field is null (default), it will be ignored and won't change how the setting is displayed.
    /// If a field is non-null (you assigned a value to it), it will override default behavior.
    /// </summary>
    /// 
    /// <example> 
    /// Here's an example of overriding order of settings and marking one of the settings as advanced:
    /// <code>
    /// // Override IsAdvanced and Order
    /// Config.AddSetting("X", "1", 1, new ConfigDescription("", null, new ConfigurationManagerAttributes { IsAdvanced = true, Order = 3 }));
    /// // Override only Order, IsAdvanced stays as the default value assigned by ConfigManager
    /// Config.AddSetting("X", "2", 2, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 1 }));
    /// Config.AddSetting("X", "3", 3, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 2 }));
    /// </code>
    /// </example>
    /// 
    /// <remarks> 
    /// You can read more and see examples in the readme at https://github.com/BepInEx/BepInEx.ConfigurationManager
    /// You can optionally remove fields that you won't use from this class, it's the same as leaving them null.
    /// </remarks>
#pragma warning disable 0169, 0414, 0649
    internal sealed class ConfigurationManagerAttributes
    {
        /// <summary>
        /// Should the setting be shown as a percentage (only use with value range settings).
        /// </summary>
        public bool? ShowRangeAsPercent;

        /// <summary>
        /// Custom setting editor (OnGUI code that replaces the default editor provided by ConfigurationManager).
        /// See below for a deeper explanation. Using a custom drawer will cause many of the other fields to do nothing.
        /// </summary>
        public System.Action<BepInEx.Configuration.ConfigEntryBase> CustomDrawer;

        /// <summary>
        /// Show this setting in the settings screen at all? If false, don't show.
        /// </summary>
        public bool? Browsable;

        /// <summary>
        /// Category the setting is under. Null to be directly under the plugin.
        /// </summary>
        public string Category;

        /// <summary>
        /// If set, a "Default" button will be shown next to the setting to allow resetting to default.
        /// </summary>
        public object DefaultValue;

        /// <summary>
        /// Force the "Reset" button to not be displayed, even if a valid DefaultValue is available. 
        /// </summary>
        public bool? HideDefaultButton;

        /// <summary>
        /// Force the setting name to not be displayed. Should only be used with a <see cref="CustomDrawer"/> to get more space.
        /// Can be used together with <see cref="HideDefaultButton"/> to gain even more space.
        /// </summary>
        public bool? HideSettingName;

        /// <summary>
        /// Optional description shown when hovering over the setting.
        /// Not recommended, provide the description when creating the setting instead.
        /// </summary>
        public string Description;

        /// <summary>
        /// Name of the setting.
        /// </summary>
        public string DispName;

        /// <summary>
        /// Order of the setting on the settings list relative to other settings in a category.
        /// 0 by default, higher number is higher on the list.
        /// </summary>
        public int? Order;

        /// <summary>
        /// Only show the value, don't allow editing it.
        /// </summary>
        public bool? ReadOnly;

        /// <summary>
        /// If true, don't show the setting by default. User has to turn on showing advanced settings or search for it.
        /// </summary>
        public bool? IsAdvanced;

        /// <summary>
        /// Custom converter from setting type to string for the built-in editor textboxes.
        /// </summary>
        public System.Func<object, string> ObjToStr;

        /// <summary>
        /// Custom converter from string to setting type for the built-in editor textboxes.
        /// </summary>
        public System.Func<string, object> StrToObj;
    }
}
