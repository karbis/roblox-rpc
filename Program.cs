using robloxrpc.Properties;
using DiscordRPC;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;
using System.Resources.Extensions;
using System.Diagnostics;

namespace bruhshot {
    static class Program {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main() {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.Run(new MyCustomApplicationContext());
        }
    }


    public class MyCustomApplicationContext : ApplicationContext {
        private NotifyIcon trayIcon;
        static System.Timers.Timer lastTimer = null;
        static string currentPath = "";
        static bool gaming = false;
        static DiscordRpcClient client;
        static HttpClient httpClient;
        static int lastLineCount = 0;
        static string storedUsername = "";

        public MyCustomApplicationContext() {
            // Initialize Tray Icon

            var contextMenu = new ContextMenuStrip();
            ToolStripMenuItem titleThingy = new ToolStripMenuItem("Roblox Studio RPC", null, null, "Roblox Studio RPC");
            contextMenu.Items.Add(titleThingy);
            contextMenu.Items.Add(new ToolStripSeparator());
            var exitButton = new ToolStripMenuItem();
            contextMenu.Items.AddRange(new ToolStripItem[] { exitButton });
            exitButton.Text = "Exit";
            exitButton.Click += Exit;

            trayIcon = new NotifyIcon() {
                Icon = Resources.AppIcon,
                ContextMenuStrip = contextMenu,
                Visible = true
            };

            // we're gonna be spying on the logs directory cause the logs can display the current game id of the client
            string logsDirectory = @"C:\Users\" + Environment.UserName + @"\AppData\Local\Roblox\logs";
            var watcher = new FileSystemWatcher(logsDirectory);
            watcher.Created += FileCreated;
            watcher.EnableRaisingEvents = true;

            HttpClientHandler handler = new HttpClientHandler() {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            httpClient = new HttpClient(handler);
        }
        private static void FileCreated(object sender, FileSystemEventArgs e) {
            if (gaming) { return; };
            bool inPlayer = e.Name.Contains("Player");
            if (inPlayer) { return; };
            if (lastTimer != null) {
                lastTimer.Stop();
                lastTimer.Dispose();
            }
            currentPath = e.FullPath;
            lastTimer = new System.Timers.Timer(1000);
            lastTimer.AutoReset = true;
            lastTimer.Enabled = true;
            lastTimer.Elapsed += OnFileUpdateForStudio;
        }

        public static async Task<string[]> ReadAllLinesAsync(string path) {
            using (FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize: 4096, useAsync: true))
            using (StreamReader reader = new StreamReader(fileStream)) {
                var lines = new List<string>();
                string line;
                while ((line = await reader.ReadLineAsync()) != null) {
                    lines.Add(line);
                }
                return lines.ToArray();
            }
        }

        private static JObject QuickGet(string url) {
            HttpResponseMessage response = httpClient.GetAsync(url).Result;
            string jsonString = response.Content.ReadAsStringAsync().Result;
            JObject jsonObject = JObject.Parse(jsonString);
            return jsonObject;
        }

        private static dynamic[] GetGameInfo(string placeId) {
            if (placeId == "0") {
                dynamic[] returnValue2 = { "a Local File", "", "" };

                return returnValue2;
            }
            Settings.Default.Reload();
            JObject info = QuickGet("https://apis.roblox.com/universes/v1/places/" + placeId + "/universe"); // why does the games.roblox.com version require authentication
            string universeId = (string)info["universeId"];
            JObject moreInfo = QuickGet("https://games.roblox.com/v1/games?universeIds=" + universeId);
            string gameName = (string)moreInfo["data"][0]["name"];
            if (gameName.Length < 2) { gameName += " "; };

            if (!Settings.Default.StudioRevealGameInformation) { gameName = "a Game"; };

            dynamic[] returnValue = { gameName, "", "" };

            return returnValue;
        }

        static string lastData = "";
        private static void OnFileUpdateForStudio(Object source, ElapsedEventArgs e) {
            var lines = Task.Run(async () => await ReadAllLinesAsync(currentPath)).Result;
            if (lastLineCount == lines.Length) { return; };
            lastLineCount = lines.Length;
            Array.Reverse(lines);
            if (lines.Length > 200) {
                Array.Resize(ref lines, 200);
            }

            foreach (string line in lines) {
                if (line.Contains("RobloxIDEDoc::open - start")) {
                    if (gaming) { break; };
                    string gameId = Regex.Match(line, @"placeId: (\d+)").Groups[1].Value;
                    gaming = true;
                    var gameInfo = GetGameInfo(gameId);

                    client = new DiscordRpcClient("1109820127605686273");
                    client.Initialize();

                    RichPresence richPresence = new RichPresence() {
                        Details = "IDK",
                        Timestamps = new Timestamps { Start = DateTime.UtcNow },
                        Assets = new Assets() {
                            LargeImageKey = "logo3",
                            LargeImageText = "Roblox Studio"
                        }
                    };

                    client.SetPresence(richPresence);
                    break;
                } else if (line.Contains("RobloxIDEDoc::~RobloxIDEDoc - end")) {
                    if (!gaming) { break; }
                    gaming = false;
                    if (client != null) {
                        client.Dispose();
                    }
                    break;
                } else if (line.Contains("About to exit the application, doing cleanup.")) {
                    gaming = false;
                    lastTimer.Stop();
                    lastTimer.Dispose();
                    if (client != null) {
                        client.Dispose();
                    }
                    lastTimer = null;
                    break;
                } else if (line.Contains("[FLog::Output]") && gaming) {
                    string output = Regex.Match(line, @"\[FLog::Output\] (.*)").Groups[1].Value;
                    if (output == lastData) { break; }
                    lastData = output;
                    string[] data = output.Split('^');
                    if (data.Length != 4) { return; }
                    string newState = "Editing " + data[1];
                    if (client.CurrentPresence.Details != newState) {
                        client.UpdateDetails(newState);
                    }
                    string newState2 = data[2] + " lines";
                    if (client.CurrentPresence.State != newState2) {
                        client.UpdateState(newState2);
                    }
                    string newName = "script" + data[3];
                    if (client.CurrentPresence.Assets.SmallImageKey != newName) {
                        string toolTip = "Script";
                        if (data[3] == "1") { toolTip = "LocalScript";  }
                        if (data[3] == "2") { toolTip = "ModuleScript"; }
                        client.UpdateSmallAsset(newName, toolTip);
                    }
                    break;
                }
            }
        }

        void Exit(object sender, EventArgs e) {
            // Hide tray icon, otherwise it will remain shown until user mouses over it
            trayIcon.Visible = false;
            if (client != null) {
                client.Dispose();
            }
            Application.Exit();
        }
    }
}