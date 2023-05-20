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
            if (!e.Name.Contains("Player")) { return; } // filter out studio logs, might add support later tho
            if (lastTimer != null) {
                lastTimer.Stop();
                lastTimer.Dispose();
            }
            currentPath = e.FullPath;
            lastTimer = new System.Timers.Timer(1000);
            lastTimer.Elapsed += OnFileUpdate;
            lastTimer.AutoReset = true;
            lastTimer.Enabled = true;
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

            dynamic[] returnValue = { gameName, creator, iconLink };

            return returnValue;
        }

        private static void OnFileUpdate(Object source, ElapsedEventArgs e) {
            var lines = Task.Run(async () => await ReadAllLinesAsync(currentPath)).Result;

            foreach (string line in lines) {
                if (line.Contains(@"https://assetgame.roblox.com/Game/Join") && !gaming) {
                    Console.WriteLine("OK");
                    string gameId = Regex.Match(line, @"placeId%3d(\d+)%26").Groups[1].Value;
                    string userName = Regex.Match(line, @"UserName%22%3a%22([^%]+)%22").Groups[1].Value;
                    gaming = true;
                    var gameInfo = GetGameInfo(gameId);

                    DiscordRPC.Button[] buttons = { new DiscordRPC.Button() { Label = "Game Link", Url = "https://www.roblox.com/games/" + gameId + "/redirect" } };

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
                            SmallImageText = "@" + userName
                        }
                    });
                    break;
                } else if (line.Contains("Fmod Closed.") && gaming) {
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
