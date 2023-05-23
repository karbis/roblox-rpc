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

namespace bruhshot
{
    static class Program
    {
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


    public class MyCustomApplicationContext : ApplicationContext
    {
        private NotifyIcon trayIcon;
        static System.Timers.Timer lastTimer = null;
        static string currentPath = "";
        static bool gaming = false;
        static DiscordRpcClient client;
        static HttpClient httpClient;
        static bool inPlayer = true;
        static int lastLineCount = 0;
        static string storedUsername = "";

        public MyCustomApplicationContext() {
            // Initialize Tray Icon

            var contextMenu = new ContextMenuStrip();
            ToolStripMenuItem titleThingy = new ToolStripMenuItem("Roblox RPC", null, null, "Roblox RPC");
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
            inPlayer = e.Name.Contains("Player");
            if (lastTimer != null) {
                lastTimer.Stop();
                lastTimer.Dispose();
            }
            currentPath = e.FullPath;
            lastTimer = new System.Timers.Timer(1000);
            lastTimer.AutoReset = true;
            lastTimer.Enabled = true;
            if (inPlayer) {
                lastTimer.Elapsed += OnFileUpdateForPlayer;
            } else {
                lastTimer.Elapsed += OnFileUpdateForStudio;
            }
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
                dynamic[] returnValue2 = { "a Local File","","" };

                return returnValue2;
            }
            Settings.Default.Reload();
            JObject info = QuickGet("https://apis.roblox.com/universes/v1/places/" + placeId + "/universe"); // why does the games.roblox.com version require authentication
            string universeId = (string)info["universeId"];
            JObject moreInfo = QuickGet("https://games.roblox.com/v1/games?universeIds=" + universeId);
            string gameName = (string) moreInfo["data"][0]["name"];
            string creator = (string)moreInfo["data"][0]["creator"]["name"];
            if ((bool)moreInfo["data"][0]["creator"]["hasVerifiedBadge"]) { creator += " ✔"; };
            if ((string)moreInfo["data"][0]["creator"]["type"] == "User") { creator = "@" + creator; };
            if (gameName.Length < 2) { gameName += " "; };
            creator = "By " + creator;
            JObject iconData = QuickGet("https://thumbnails.roblox.com/v1/games/icons?universeIds=" + universeId + "&returnPolicy=PlaceHolder&size=512x512&format=Png&isCircular=false");
            string iconLink = (string)iconData["data"][0]["imageUrl"];

            if (creator == "By @Templates" && !inPlayer) { creator = ""; };
            if (!Settings.Default.StudioRevealGameInformation && !inPlayer) { creator = ""; gameName = "a Game"; iconLink = ""; };
            if (!Settings.Default.PlayerRevealGameInformation && inPlayer) { creator = ""; gameName = "Playing a Game"; iconLink = ""; };

            dynamic[] returnValue = { gameName, creator, iconLink };

            return returnValue;
        }

        private static void OnFileUpdateForPlayer(Object source, ElapsedEventArgs e) {
            var lines = Task.Run(async () => await ReadAllLinesAsync(currentPath)).Result;
            if (lastLineCount == lines.Length) { return; };
            lastLineCount = lines.Length;
            Array.Reverse(lines);
            if (lines.Length > 200) {
                Array.Resize(ref lines, 200);
            }

            foreach (string line in lines) {
                if (line.Contains(@"https://assetgame.roblox.com/Game/Join")) {
                    if (gaming) { break; };
                    Console.WriteLine("OK");
                    string gameId = Regex.Match(line, @"placeId%3d(\d+)%26").Groups[1].Value;
                    string userName = Regex.Match(line, @"UserName%22%3a%22([^%]+)%22").Groups[1].Value;
                    storedUsername = userName;
                    gaming = true;
                    var gameInfo = GetGameInfo(gameId);

                    DiscordRPC.Button[] buttons = { };
                    if (Settings.Default.PlayerGameLinkButton) {
                        buttons = buttons.Append(new DiscordRPC.Button() { Label = "Game Link", Url = "https://www.roblox.com/games/" + gameId + "/redirect" }).ToArray();
                    }
                    if (Settings.Default.PlayerJoinServerButton) {
                        buttons = buttons.Append(new DiscordRPC.Button() { Label = "Join Server", Url = "roblox://experiences/start?placeId=" + gameId + "&gameInstanceId=" + Regex.Match(line, "\"jobId\":\"([^\"]+)\"").Groups[1].Value }).ToArray();
                    }
                    userName = "@" + userName;
                    if (!Settings.Default.PlayerRevealUsername) {
                        userName = "Roblox";
                    }

                    client = new DiscordRpcClient("1109427796494798949");
                    client.Initialize();
                    client.SetPresence(new RichPresence() {
                        Details = gameInfo[0],
                        State = gameInfo[1],
                        Timestamps = new Timestamps { Start = DateTime.UtcNow },
                        Buttons = buttons,
                        Assets = new Assets() {
                            LargeImageKey = gameInfo[2],
                            LargeImageText = gameInfo[0],
                            SmallImageKey = "logo3",
                            SmallImageText = userName
                        }
                    });
                    break;
                } else if (line.Contains("unregisterMemoryPrioritizationCallback")) {
                    gaming = false;
                    lastTimer.Stop();
                    lastTimer.Dispose();
                    client.Dispose();
                    lastTimer = null;
                    break;
                } else if (line.Contains("[FLog::WindowsLuaApp] Application did receive notification")) {
                    if (!gaming) { break; };
                    gaming = false;
                    client.Dispose();
                    break;
                }
            }
        }

        private static void OnFileUpdateForStudio(Object source, ElapsedEventArgs e) {
            var lines = Task.Run(async () => await ReadAllLinesAsync(currentPath)).Result;
            if (lastLineCount == lines.Length) { return;  };
            lastLineCount = lines.Length;
            Array.Reverse(lines);
            if (lines.Length > 200) {
                Array.Resize(ref lines, 200);
            }

            foreach (string line in lines) {
                if (line.Contains("RobloxIDEDoc::open - start")) {
                    if (gaming) { break; };
                    Console.WriteLine("!");
                    string gameId = Regex.Match(line, @"placeId: (\d+)").Groups[1].Value;
                    gaming = true;
                    var gameInfo = GetGameInfo(gameId);

                    DiscordRPC.Button[] buttons = { };
                    if (Settings.Default.StudioGameLinkButton) {
                        buttons = buttons.Append(new DiscordRPC.Button() { Label = "Game Link", Url = "https://www.roblox.com/games/" + gameId + "/redirect" }).ToArray();
                    }

                    client = new DiscordRpcClient("1109820127605686273");
                    client.Initialize();

                    RichPresence richPresence = new RichPresence() {
                        Details = "Editing " + gameInfo[0],
                        Timestamps = new Timestamps { Start = DateTime.UtcNow },
                        Buttons = buttons,
                        Assets = new Assets() {
                            LargeImageKey = "logo3",
                            LargeImageText = "Roblox Studio"
                        }
                    };

                    if (gameInfo[1] != "") {
                        richPresence.State = gameInfo[1];
                    };
                    if (gameInfo[2] != "") {
                        richPresence.Assets.SmallImageKey = gameInfo[2];
                        richPresence.Assets.SmallImageText = gameInfo[0];
                    };
                    if (storedUsername != "" && Settings.Default.StudioRevealUsername) {
                        richPresence.Assets.LargeImageText = "@" + storedUsername;
                    }

                    client.SetPresence(richPresence);
                    break;
                } else if (line.Contains("RobloxIDEDoc::~RobloxIDEDoc - end")) {
                    if (!gaming) { break; }
                    gaming = false;
                    client.Dispose();
                    break;
                } else if (line.Contains("About to exit the application, doing cleanup.")) {
                    gaming = false;
                    lastTimer.Stop();
                    lastTimer.Dispose();
                    client.Dispose();
                    lastTimer = null;
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
