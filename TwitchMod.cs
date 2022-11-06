using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
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

        public const string pluginVerson = "1.1.4";

        public ConfigDefinition modEnableDef = new ConfigDefinition("_" + pluginName, "Enable/Disable Mod");
        public ConfigDefinition SendSelfDef = new ConfigDefinition("_" + pluginName, "Send To Self");
        public ConfigDefinition KeyDef = new ConfigDefinition("_" + pluginName, "Key");
        public ConfigDefinition AmountDef = new ConfigDefinition("_" + pluginName, "Streamer Amount");

        public ConfigEntry<bool> mEnabled;
        
        public ConfigEntry<string> mKey;

        public ConfigEntry<KeyboardShortcut> mSendSelf;

        public ConfigEntry<int> mAmount;
        public int CurrentAmount = 0;
        public bool handledAmount = true;

        public CashedStreamer currentStreamer = null;

        public List<CashedStreamer> streamers = new List<CashedStreamer>();

        public List<CashedStreamer> queue = new List<CashedStreamer>();


        public const string ClientID = "9bksp3cxt84auuicqxnnuk1y4mhrd6";
        public const string ClientSecret = "fvilbl2jku0xixwfmz5euqi4vnml7f";

        public bool SendingBridge = false;
        public bool FindingID = false;
        public bool GettingLayout = false;

        public string loadedBridgeHash = "";

        public string Key = "";

        public DateTime LastCharInput = DateTime.Now;
        public bool handledGetID = true;

        public static TwitchMain instance;

        void Awake()
        {
            if (instance == null) instance = this;
            repositoryUrl = "https://github.com/Bram2323/PB-Twitch-Mod/";
            authors = new string[] { "Bram2323" };

            int order = 0;

            mEnabled = Config.Bind(modEnableDef, true, new ConfigDescription("Controls if the mod should be enabled or disabled", null, new ConfigurationManagerAttributes { Order = order }));
            mEnabled.SettingChanged += onEnableDisable;
            order--;

            mSendSelf = Config.Bind(SendSelfDef, new KeyboardShortcut(KeyCode.None), new ConfigDescription("What button sends the bridge to yourself", null, new ConfigurationManagerAttributes { Order = order }));
            order--;

            mKey = Config.Bind(KeyDef, "", new ConfigDescription("Don't change this unless you know what you're doing! DON'T SHOW THIS TO ANYONE!", null, new ConfigurationManagerAttributes { Order = order , IsAdvanced = true}));
            order--;

            mAmount = Config.Bind(AmountDef, 1, new ConfigDescription("The amount of streamers you want to store", null, new ConfigurationManagerAttributes { Order = order }));
            mAmount.SettingChanged += onAmountChange;
            order--;

            handledAmount = false;

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

        public void onCharInput(object sender, EventArgs e)
        {
            LastCharInput = DateTime.Now;
            handledGetID = false;
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

        public bool CheckForCheating()
        {
            return mEnabled.Value && PolyTechMain.modEnabled.Value;
        }

        public bool SendingStuff()
        {
            return FindingID || SendingBridge || GettingLayout;
        }


        public void onAmountChange(object sender, EventArgs e)
        {
            if (CurrentAmount != mAmount.Value) handledAmount = false;
            else handledAmount = true;

            if (CurrentAmount != mAmount.Value && !SendingStuff())
            {
                for (int i = 0; i < CurrentAmount; i++)
                {
                    CashedStreamer streamer = streamers[i];
                    
                    string tName = streamer.mName.Value;
                    KeyboardShortcut tSend = streamer.mSendBridge.Value;
                    KeyboardShortcut tLoad = streamer.mLoadLayout.Value;
                    string tID = streamer.mID.Value;


                    Config.Remove(streamer.NameDef);
                    Config.Remove(streamer.SendBridgeDef);
                    Config.Remove(streamer.LoadLayoutDef);
                    Config.Remove(streamer.IDDef);


                    streamer.mName = Config.Bind(streamer.NameDef, "", new ConfigDescription("The name of the streamer", null, new ConfigurationManagerAttributes { Browsable = false }));
                    streamer.mName.Value = tName;

                    streamer.mSendBridge = Config.Bind(streamer.SendBridgeDef, new KeyboardShortcut(KeyCode.None), new ConfigDescription("Send to streamer", null, new ConfigurationManagerAttributes { Browsable = false }));
                    streamer.mSendBridge.Value = tSend;

                    streamer.mLoadLayout = Config.Bind(streamer.LoadLayoutDef, new KeyboardShortcut(KeyCode.None), new ConfigDescription("Load layout from streamer", null, new ConfigurationManagerAttributes { Browsable = false }));
                    streamer.mLoadLayout.Value = tLoad;

                    streamer.mID = Config.Bind(streamer.IDDef, "", new ConfigDescription("Don't change this!", null, new ConfigurationManagerAttributes { Browsable = false }));
                    streamer.mID.Value = tID;
                }
                streamers.Clear();
                queue.Clear();

                for (int i = 0; i < mAmount.Value; i++)
                {
                    CashedStreamer streamer = new CashedStreamer();
                    streamers.Add(streamer);

                    string Def = "Streamer " + (i + 1);

                    streamer.NameDef = new ConfigDefinition(Def, "Name");
                    streamer.SendBridgeDef = new ConfigDefinition(Def, "Send Bridge");
                    streamer.LoadLayoutDef = new ConfigDefinition(Def, "Load Layout");
                    streamer.IDDef = new ConfigDefinition(Def, "ID");


                    streamer.mName = Config.Bind(streamer.NameDef, "", new ConfigDescription("The name of the streamer", null, new ConfigurationManagerAttributes { Browsable = true, Order = 4 }));

                    streamer.mSendBridge = Config.Bind(streamer.SendBridgeDef, new KeyboardShortcut(KeyCode.None), new ConfigDescription("Send to streamer", null, new ConfigurationManagerAttributes { Browsable = true, Order = 3 }));

                    streamer.mLoadLayout = Config.Bind(streamer.LoadLayoutDef, new KeyboardShortcut(KeyCode.None), new ConfigDescription("Load layout from streamer", null, new ConfigurationManagerAttributes { Browsable = true, Order = 2 }));

                    streamer.mID = Config.Bind(streamer.IDDef, "", new ConfigDescription("Don't change this!", null, new ConfigurationManagerAttributes { Browsable = false }));

                    string tName = streamer.mName.Value;
                    KeyboardShortcut tSend = streamer.mSendBridge.Value;
                    KeyboardShortcut tLoad = streamer.mLoadLayout.Value;
                    string tID = streamer.mID.Value;

                    Config.Remove(streamer.NameDef);
                    Config.Remove(streamer.SendBridgeDef);
                    Config.Remove(streamer.LoadLayoutDef);
                    Config.Remove(streamer.IDDef);

                    
                    streamer.mName = Config.Bind(streamer.NameDef, "", new ConfigDescription("The name of the streamer", null, new ConfigurationManagerAttributes { Browsable = true, Order = 4 }));
                    streamer.mName.Value = tName;
                    streamer.mName.SettingChanged += onCharInput;

                    streamer.mSendBridge = Config.Bind(streamer.SendBridgeDef, new KeyboardShortcut(KeyCode.None), new ConfigDescription("Send to streamer", null, new ConfigurationManagerAttributes { Browsable = true, Order = 3 }));
                    streamer.mSendBridge.Value = tSend;

                    streamer.mLoadLayout = Config.Bind(streamer.LoadLayoutDef, new KeyboardShortcut(KeyCode.None), new ConfigDescription("Load layout from streamer", null, new ConfigurationManagerAttributes { Browsable = true, Order = 2 }));
                    streamer.mLoadLayout.Value = tLoad;

                    streamer.mID = Config.Bind(streamer.IDDef, "", new ConfigDescription("Don't change this!", null, new ConfigurationManagerAttributes { Browsable = false }));
                    streamer.mID.Value = tID;
                    if (string.IsNullOrWhiteSpace(streamer.mName.Value)) streamer.mID.Value = "";

                    streamer.LastName = streamer.mName.Value;

                    if (!string.IsNullOrWhiteSpace(streamer.mName.Value) && string.IsNullOrWhiteSpace(streamer.mID.Value))
                    {
                        queue.Add(streamer);
                        handledGetID = false;
                    }
                }

                CurrentAmount = mAmount.Value;
                handledAmount = true;
            }

            Config.Reload();
        }


        void Update()
        {
            if (!handledAmount && !SendingStuff())
            {
                onAmountChange(null, null);
            }

            if (!CheckForCheating()) return;
            

            if (!handledGetID && (DateTime.Now - LastCharInput).Seconds > 3)
            {
                foreach (CashedStreamer streamer in streamers)
                {
                    if (streamer.mName.Value != streamer.LastName) queue.Add(streamer);
                    streamer.LastName = streamer.mName.Value;
                }
                handledGetID = true;
            }

            if (queue.Count > 0 && !SendingStuff())
            {
                CashedStreamer streamer = queue[0];

                GetStreamerID(streamer);

                queue.Remove(streamer);
            }


            foreach (CashedStreamer streamer in streamers)
            {
                if (streamer.mSendBridge.Value.IsDown() && !SendingStuff())
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
                        PopUpMessage.Display("Do you want to send the bridge to " + streamer.mName.Value + "?", delegate { SendBridge(streamer); });
                    }
                }

                if (streamer.mLoadLayout.Value.IsDown() && !SendingStuff())
                {
                    PopUpMessage.Display("Do you want to load the layout of " + streamer.mName.Value + "?", delegate { GetLayout(streamer); });
                }
            }

            if (mSendSelf.Value.IsDown())
            {
                SendToSelf();
            }
        }


        public void GetStreamerID(CashedStreamer streamer)
        {
            if (streamer == null || SendingStuff())
            {
                return;
            }
            else if (string.IsNullOrWhiteSpace(streamer.mName.Value))
            {
                streamer.mID.Value = "";
                return;
            }

            FindingID = true;
            currentStreamer = streamer;

            if (Key.IsNullOrWhiteSpace())
            {
                GameUI.m_Instance.m_Status.Open("Getting streamer id (1/2)");
                GameUI.m_Instance.m_TopBar.m_MessageTopLeft.ShowMessage("Getting Streamer ID", 3);
                Debug.Log("Getting Twitch Key...");
                WebRequest.Post("https://id.twitch.tv/oauth2/token?client_id=" + ClientID + "&client_secret=" + ClientSecret + "&grant_type=client_credentials", PolyTwitch.m_Key).SendWebRequest().completed += OnPullKeyComplete;
            }
            else
            {
                GameUI.m_Instance.m_Status.Open("Getting streamer id (1/1)");
                GameUI.m_Instance.m_TopBar.m_MessageTopLeft.ShowMessage("Getting Streamer ID", 3);
                PullId();
            }
        }

        private void OnPullKeyComplete(UnityEngine.AsyncOperation asyncOperation)
        {
            Debug.Log("Completed");
            FindingID = false;
            GameUI.m_Instance.m_Status.Close();
            UnityWebRequestAsyncOperation unityWebRequestAsyncOperation = (UnityWebRequestAsyncOperation)asyncOperation;
            Debug.Log(unityWebRequestAsyncOperation.webRequest.downloadHandler.text);
            if (unityWebRequestAsyncOperation.webRequest.isNetworkError || unityWebRequestAsyncOperation.webRequest.isHttpError)
            {
                string errorMessage = WebRequest.GetErrorMessage(unityWebRequestAsyncOperation.webRequest);
                Debug.LogWarning("https://id.twitch.tv/oauth2/token?client_id=" + ClientID + "&client_secret=******************************&grant_type=client_credentials" + " failed with: " + errorMessage);
                GameUI.m_Instance.m_TopBar.m_MessageTopLeft.ShowMessage("Could not get ID: " + errorMessage, 3);
                PopUpWarning.Display("Could not get ID: " + errorMessage);
                currentStreamer.mID.Value = "";
            }
            else
            {
                TwitchResponse response = JsonUtility.FromJson<TwitchResponse>(unityWebRequestAsyncOperation.webRequest.downloadHandler.text);
                Key = response.access_token;
                FindingID = true;
                GameUI.m_Instance.m_Status.Open("Getting streamer id (2/2)");
                PullId();
            }
        }

        private void PullId()
        {
            Debug.Log("Getting streamer ID...");
            UnityWebRequest IDRequest = WebRequest.Get("https://api.twitch.tv/helix/users?login=" + currentStreamer.mName.Value, PolyTwitch.m_Key);
            IDRequest.SetRequestHeader("Authorization", "Bearer " + Key);
            IDRequest.SetRequestHeader("Client-ID", ClientID);
            IDRequest.SendWebRequest().completed += OnPullIdComplete;
        }

        private void OnPullIdComplete(UnityEngine.AsyncOperation asyncOperation)
        {
            Debug.Log("Completed");
            FindingID = false;
            GameUI.m_Instance.m_Status.Close();
            UnityWebRequestAsyncOperation unityWebRequestAsyncOperation = (UnityWebRequestAsyncOperation)asyncOperation;
            Debug.Log(unityWebRequestAsyncOperation.webRequest.downloadHandler.text);
            if (unityWebRequestAsyncOperation.webRequest.isNetworkError || unityWebRequestAsyncOperation.webRequest.isHttpError)
            {
                string errorMessage = WebRequest.GetErrorMessage(unityWebRequestAsyncOperation.webRequest);
                Debug.LogWarning(unityWebRequestAsyncOperation.webRequest.url + " failed with: " + errorMessage);
                GameUI.m_Instance.m_TopBar.m_MessageTopLeft.ShowMessage("Could not get ID: " + errorMessage, 3);
                PopUpWarning.Display("Could not get ID: " + errorMessage);
                currentStreamer.mID.Value = "";
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
                    GameUI.m_Instance.m_TopBar.m_MessageTopLeft.ShowMessage("Found ID: " + foundID, 3);
                    currentStreamer.mID.Value = foundID;
                }
                else
                {
                    Debug.LogWarning("ID not found");
                    GameUI.m_Instance.m_TopBar.m_MessageTopLeft.ShowMessage("Streamer doesn't exist!", 3);
                    PopUpWarning.Display("Streamer ID was not found!");
                    currentStreamer.mID.Value = "";
                }
            }
        }


        public void SendBridge(CashedStreamer streamer)
        {
            if (streamer == null || SendingStuff())
            {
                return;
            }
            else if (string.IsNullOrWhiteSpace(streamer.mID.Value))
            {
                PopUpWarning.Display("The ID of this streamer is not cashed or not valid!");
                return;
            }
            else if (string.IsNullOrWhiteSpace(mKey.Value))
            {
                PopUpWarning.Display("Make sure you linked your twitch acount with poly bridge before you send a bridge!");
                return;
            }

            foreach (BridgeEdge Edge in BridgeEdges.m_Edges)
            {
                if (Edge.m_IsBroken)
                {
                    PopUpWarning.Display("Can't send bridge with broken edge!");
                    return;
                }
            }

            currentStreamer = streamer;
            SendingBridge = true;
            GameUI.m_Instance.m_Status.Open("Sending bridge (1/3)");
            GameUI.m_Instance.m_TopBar.m_MessageTopLeft.ShowMessage("Sending bridge...", 3);
            Debug.Log("Connecting to streamer...");

            UnityWebRequest request = WebRequest.Post("https://api.t2.drycactus.com/v1/" + "viewer/stream/" + streamer.mID.Value + "/connect", mKey.Value);
            request.SetRequestHeader("Authorization", "Bearer " + mKey.Value);
            request.SetRequestHeader("accept", "application/json");
            request.SendWebRequest().completed += OnConnectComplete;
        }
        
        private void OnConnectComplete(UnityEngine.AsyncOperation asyncOperation)
        {
            Debug.Log("Completed");
            SendingBridge = false;
            GameUI.m_Instance.m_Status.Close();

            foreach (BridgeEdge Edge in BridgeEdges.m_Edges)
            {
                if (Edge.m_IsBroken)
                {
                    PopUpWarning.Display("Can't send bridge with broken edge!");
                    return;
                }
            }

            UnityWebRequestAsyncOperation unityWebRequestAsyncOperation = (UnityWebRequestAsyncOperation)asyncOperation;
            Debug.Log(unityWebRequestAsyncOperation.webRequest.downloadHandler.text);
            if (unityWebRequestAsyncOperation.webRequest.isNetworkError || unityWebRequestAsyncOperation.webRequest.isHttpError)
            {
                string errorMessage = WebRequest.GetErrorMessage(unityWebRequestAsyncOperation.webRequest);
                Debug.LogWarning(unityWebRequestAsyncOperation.webRequest.url + " failed with: " + errorMessage);
                PopUpWarning.Display("Could not send bridge: " + errorMessage);
            }
            else
            {
                SendingBridge = true;
                GameUI.m_Instance.m_Status.Open("Sending bridge (2/3)");
                Debug.Log("Getting level hash");
                UnityWebRequest BridgeRequest = WebRequest.Get("https://api.t2.drycactus.com/v1/" + "viewer/stream/" + currentStreamer.mID.Value + "/pull?delay=0", mKey.Value);
                BridgeRequest.SetRequestHeader("Authorization", "Bearer " + mKey.Value);
                BridgeRequest.SetRequestHeader("accept", "application/json");
                BridgeRequest.SendWebRequest().completed += PushBridge;
            }
        }

        private void PushBridge(UnityEngine.AsyncOperation asyncOperation)
        {
            Debug.Log("Completed");
            SendingBridge = false;
            GameUI.m_Instance.m_Status.Close();

            foreach (BridgeEdge Edge in BridgeEdges.m_Edges)
            {
                if (Edge.m_IsBroken)
                {
                    PopUpWarning.Display("Can't send bridge with broken edge!");
                    return;
                }
            }

            UnityWebRequestAsyncOperation unityWebRequestAsyncOperation = (UnityWebRequestAsyncOperation)asyncOperation;
            Debug.Log(unityWebRequestAsyncOperation.webRequest.downloadHandler.text);
            if (unityWebRequestAsyncOperation.webRequest.isNetworkError || unityWebRequestAsyncOperation.webRequest.isHttpError)
            {
                string errorMessage = WebRequest.GetErrorMessage(unityWebRequestAsyncOperation.webRequest);
                Debug.LogWarning(unityWebRequestAsyncOperation.webRequest.url + " failed with: " + errorMessage);
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

                try
                {
                    SendingBridge = true;
                    GameUI.m_Instance.m_Status.Open("Sending bridge (3/3)");

                    JSONObject json = new JSONObject(unityWebRequestAsyncOperation.webRequest.downloadHandler.text);

                    if (json == null)
                    {
                        Debug.LogWarning("Something went wrong while looking for level hash");
                        SendingBridge = false;
                        GameUI.m_Instance.m_Status.Close();
                        PopUpWarning.Display("Something went wrong while trying to send the bridge");
                        return;
                    }

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
                        PopUpWarning.Display("Something went wrong while trying to send the bridge");
                        return;
                    }

                    Debug.Log("Sending bridge...");

                    byte[] payloadBytes = GetLayoutBytesWithDetectionNode();

                    byte[] payloadCompressed = Utils.ZipPayload(payloadBytes);
                    string payload_md5 = Utils.MD5HashFor(payloadCompressed);

                    WWWForm wwwform = new WWWForm();
                    wwwform.AddBinaryData("payload", payloadCompressed, "file.zip", "zip");
                    wwwform.AddField("payload_hash", payload_md5);
                    wwwform.AddField("level_hash", LevelHash);
                    UnityWebRequest BridgeRequest = WebRequest.Post("https://api.t2.drycactus.com/v1/" + "viewer/stream/" + currentStreamer.mID.Value + "/push", mKey.Value, wwwform);
                    BridgeRequest.SetRequestHeader("Authorization", "Bearer " + mKey.Value);
                    BridgeRequest.SetRequestHeader("accept", "application/json");
                    BridgeRequest.SendWebRequest().completed += OnPushBridgeComplete;
                }
                catch
                {
                    Debug.LogWarning("Exception was thrown while sending bridge");
                    SendingBridge = false;
                    GameUI.m_Instance.m_Status.Close();
                    PopUpWarning.Display("Something went wrong while trying to send the bridge");
                }
            }
        }
        
        private void OnPushBridgeComplete(UnityEngine.AsyncOperation asyncOperation)
        {
            SendingBridge = false;
            GameUI.m_Instance.m_Status.Close();
            UnityWebRequestAsyncOperation unityWebRequestAsyncOperation = (UnityWebRequestAsyncOperation)asyncOperation;
            Debug.Log(unityWebRequestAsyncOperation.webRequest.downloadHandler.text);
            if (unityWebRequestAsyncOperation.webRequest.isNetworkError || unityWebRequestAsyncOperation.webRequest.isHttpError)
            {
                string errorMessage = WebRequest.GetErrorMessage(unityWebRequestAsyncOperation.webRequest);
                Debug.LogWarning(unityWebRequestAsyncOperation.webRequest.url + " failed with: " + errorMessage);
                PopUpWarning.Display("Could not send bridge: " + errorMessage);
                GameUI.m_Instance.m_TopBar.m_MessageTopLeft.ShowMessage(unityWebRequestAsyncOperation.webRequest.downloadHandler.text, 3);
            }
            else
            {
                GameUI.m_Instance.m_TopBar.m_MessageTopLeft.ShowMessage("The bridge was sent successfully!", 3);
                PopUpWarning.Display("The bridge was sent successfully!");
            }
        }
        
        
        public void GetLayout(CashedStreamer streamer)
        {
            if (streamer == null || SendingStuff())
            {
                return;
            }
            else if (string.IsNullOrWhiteSpace(streamer.mID.Value))
            {
                PopUpWarning.Display("The ID of this streamer is not cashed or not valid!");
                return;
            }
            else if (string.IsNullOrWhiteSpace(mKey.Value))
            {
                PopUpWarning.Display("Make sure you linked your twitch acount with poly bridge before you send a bridge!");
                return;
            }

            currentStreamer = streamer;
            GettingLayout = true;
            GameUI.m_Instance.m_Status.Open("Getting layout (1/3)");
            Debug.Log("Connecting to streamer...");

            UnityWebRequest request = WebRequest.Post("https://api.t2.drycactus.com/v1/" + "viewer/stream/" + streamer.mID.Value + "/connect", mKey.Value);
            request.SetRequestHeader("Authorization", "Bearer " + mKey.Value);
            request.SetRequestHeader("accept", "application/json");
            request.SendWebRequest().completed += OnConnectComplete2;
        }

        private void OnConnectComplete2(UnityEngine.AsyncOperation asyncOperation)
        {
            Debug.Log("Completed");
            GettingLayout = false;
            GameUI.m_Instance.m_Status.Close();
            UnityWebRequestAsyncOperation unityWebRequestAsyncOperation = (UnityWebRequestAsyncOperation)asyncOperation;
            Debug.Log(unityWebRequestAsyncOperation.webRequest.downloadHandler.text);
            if (unityWebRequestAsyncOperation.webRequest.isNetworkError || unityWebRequestAsyncOperation.webRequest.isHttpError)
            {
                string errorMessage = WebRequest.GetErrorMessage(unityWebRequestAsyncOperation.webRequest);
                Debug.LogWarning(unityWebRequestAsyncOperation.webRequest.url + " failed with: " + errorMessage);
                PopUpWarning.Display("Could not get layout: " + errorMessage);
            }
            else
            {
                GettingLayout = true;
                GameUI.m_Instance.m_Status.Open("Getting layout (2/3)");
                Debug.Log("Getting payload url");
                UnityWebRequest BridgeRequest = WebRequest.Get("https://api.t2.drycactus.com/v1/" + "viewer/stream/" + currentStreamer.mID.Value + "/pull?delay=0", mKey.Value);
                BridgeRequest.SetRequestHeader("Authorization", "Bearer " + mKey.Value);
                BridgeRequest.SetRequestHeader("accept", "application/json");
                BridgeRequest.SendWebRequest().completed += GetLayoutPayload;
            }
        }

        private void GetLayoutPayload(UnityEngine.AsyncOperation asyncOperation)
        {
            Debug.Log("Completed");
            GettingLayout = false;
            GameUI.m_Instance.m_Status.Close();
            UnityWebRequestAsyncOperation unityWebRequestAsyncOperation = (UnityWebRequestAsyncOperation)asyncOperation;
            Debug.Log(unityWebRequestAsyncOperation.webRequest.downloadHandler.text);
            if (unityWebRequestAsyncOperation.webRequest.isNetworkError || unityWebRequestAsyncOperation.webRequest.isHttpError)
            {
                string errorMessage = WebRequest.GetErrorMessage(unityWebRequestAsyncOperation.webRequest);
                Debug.LogWarning(unityWebRequestAsyncOperation.webRequest.url + " failed with: " + errorMessage);
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

        private void GenerateLayout(UnityEngine.AsyncOperation asyncOperation)
        {
            Debug.Log("Completed");
            GettingLayout = false;
            GameUI.m_Instance.m_Status.Close();
            UnityWebRequestAsyncOperation unityWebRequestAsyncOperation = (UnityWebRequestAsyncOperation)asyncOperation;
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
                    PopUpWarning.Display("Could not get layout");
                    return;
                }

                GameStateManager.SwitchToStateImmediate(GameState.BUILD);
                GameManager.SetGameMode(GameMode.SANDBOX);
                
                Sandbox.Clear();
                Sandbox.Load(layoutData.m_ThemeStubKey, layoutData, true);
                loadedBridgeHash = Utils.MD5HashFor(layoutData.m_Bridge.SerializeBinary());
            }
        }

        [HarmonyPatch(typeof(BridgeSave), "Serialize")]
        private static class patchSerialize
        {
            private static void Postfix(ref BridgeSaveData __result)
            {
                if (Utils.MD5HashesMatch(instance.loadedBridgeHash, Utils.MD5HashFor(__result.SerializeBinary())))
                {
                    __result.m_BridgeEdges.Clear();
                    __result.m_BridgeJoints.Clear();
                    __result.m_BridgeSprings.Clear();
                    __result.m_Pistons.Clear();
                }
            }
        }

        [HarmonyPatch(typeof(BridgeSaveData), "SerializeBinary", new Type[] { typeof(bool) })]
        private static class patchSerializeBinary
        {
            private static void Postfix(BridgeSaveData __instance, ref byte[] __result)
            {
                if (Utils.MD5HashesMatch(instance.loadedBridgeHash, Utils.MD5HashFor(__result)))
                {
                    List<byte> list = new List<byte>();
                    list.AddRange(ByteSerializer.SerializeInt(__instance.m_Version));
                    list.AddRange(ByteSerializer.SerializeInt(0));
                    list.AddRange(ByteSerializer.SerializeInt(0));
                    list.AddRange(ByteSerializer.SerializeInt(0));
                    list.AddRange(ByteSerializer.SerializeInt(0));
                    list.AddRange(__instance.m_HydraulicsController.SerializeBinary(true));
                    list.AddRange(ByteSerializer.SerializeInt(__instance.m_Anchors.Count));
                    foreach (BridgeJointProxy bridgeJointProxy2 in __instance.m_Anchors)
                    {
                        list.AddRange(bridgeJointProxy2.SerializeBinary());
                    }
                    __result = list.ToArray();
                }
            }
        }


        public void SendToSelf()
        {
            byte[] payloadBytes = GetLayoutBytesWithDetectionNode();
            string LevelHash = Sandbox.m_CurrentLayoutHash;

            ConsumeReply consumeReply = new ConsumeReply
            {
                id = "TwitchMod",
                owner = new Owner
                {
                    id = "TwitchMod",
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


        public byte[] GetLayoutBytesWithDetectionNode()
        {
            string guid = "Bram2323IsTheBest!"; //Had to think of a guid the game would never pick...

            BridgeSaveData payloadProxy = BridgeSave.Serialize();

            bool alreadyExists = false;
            foreach (BridgeJointProxy joint in payloadProxy.m_BridgeJoints)
            {
                if (joint.m_Guid == guid)
                {
                    alreadyExists = true;
                    break;
                }
            }

            if (!alreadyExists)
            {
                int jointIndex = -1;

                for (int i = 0; i < payloadProxy.m_BridgeJoints.Count; i++)
                {
                    BridgeJointProxy joint = payloadProxy.m_BridgeJoints[i];
                    if (!joint.m_IsSplit && !joint.m_IsAnchor)
                    {
                        jointIndex = i;
                        break;
                    }
                }

                if (jointIndex >= 0)
                {
                    string jointGuid = payloadProxy.m_BridgeJoints[jointIndex].m_Guid;
                    foreach (BridgeEdgeProxy edge in payloadProxy.m_BridgeEdges)
                    {
                        if (edge.m_NodeA_Guid == jointGuid) edge.m_NodeA_Guid = guid;
                        if (edge.m_NodeB_Guid == jointGuid) edge.m_NodeB_Guid = guid;
                    }
                    foreach (BridgeSpringProxy spring in payloadProxy.m_BridgeSprings)
                    {
                        if (spring.m_NodeA_Guid == jointGuid) spring.m_NodeA_Guid = guid;
                        if (spring.m_NodeB_Guid == jointGuid) spring.m_NodeB_Guid = guid;
                    }
                    foreach (PistonProxy piston in payloadProxy.m_Pistons)
                    {
                        if (piston.m_NodeA_Guid == jointGuid) piston.m_NodeA_Guid = guid;
                        if (piston.m_NodeB_Guid == jointGuid) piston.m_NodeB_Guid = guid;
                    }
                    payloadProxy.m_BridgeJoints[jointIndex].m_Guid = guid;
                }
                else
                {
                    List<byte> detectionNodeData = new List<byte>();
                    detectionNodeData.AddRange(ByteSerializer.SerializeVector3(new Vector3(0, 0, -1000)));
                    detectionNodeData.AddRange(ByteSerializer.SerializeBool(false));
                    detectionNodeData.AddRange(ByteSerializer.SerializeBool(false));
                    detectionNodeData.AddRange(ByteSerializer.SerializeString(guid));

                    int offset = 0;
                    BridgeJointProxy detectionNode = new BridgeJointProxy(1, detectionNodeData.ToArray(), ref offset);
                    payloadProxy.m_BridgeJoints.Add(detectionNode);
                }
            }
            else Debug.Log("Detection node already exists!");

            return payloadProxy.SerializeBinary();
        }


        [HarmonyPatch(typeof(PolyTwitch), "AuthorizeWithKey")]
        private static class patchAuthorize
        {
            private static void Postfix(string key)
            {
                instance.mKey.Value = key;
            }
        }
    }


    public class CashedStreamer
    {
        public ConfigDefinition NameDef;
        public ConfigEntry<string> mName;
        public string LastName = "";

        public ConfigDefinition SendBridgeDef;
        public ConfigEntry<KeyboardShortcut> mSendBridge;

        public ConfigDefinition LoadLayoutDef;
        public ConfigEntry<KeyboardShortcut> mLoadLayout;

        public ConfigDefinition IDDef;
        public ConfigEntry<string> mID;
    }


    public class TwitchResponse
    {
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
