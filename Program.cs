using robloxrpc.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using DiscordRPC;
using Microsoft.Win32;
using Newtonsoft.Json;
using Timer = System.Timers.Timer;

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
        static DiscordRpcClient client;
        static string userId;

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

            userId = GetUserId();
            Timer timer = new Timer();
            timer.Interval = 1000;
            timer.AutoReset = true;
            timer.Start();
            timer.Elapsed += (_, _2) => {
                Update();
			};
        }
        public static string GetUserId() {
            // since the installedplugins is located in a folder thats named by your user id,
            // we need to go inside of the account switcher data and get the account that has the lowest user id
            // as that is the one you are probably logged into

            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Roblox\RobloxStudio\LoggedInUsersStore\https:\www.roblox.com")) {
                string value = (string)key.GetValue("users");
				Dictionary<string, dynamic> users = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(value.Substring(0,value.Length-1));
                long[] indices = new long[users.Count];
                foreach (KeyValuePair<string, dynamic> pair in users) {
                    indices[indices.Length - 1] = Convert.ToInt64(pair.Key);
                }
                long userId = 9999999999999999;
                foreach (long id in indices) {
                    userId = Math.Min(id, userId);
                }
                return userId.ToString();
            }
        }

        enum Status {
            NotRunning,
            Active,
            NoScript
        }
        class RpcData {
            public Status Status = Status.Active;
            public long Lines = 0;
            public byte Type = 0;
            public string Name = "";
        }
        public static void Update() {
            string path = $@"C:\Users\{Environment.UserName}\AppData\Local\Roblox\{userId}\InstalledPlugins\0\settings.json";
            if (!File.Exists(path)) return;
            string contents = "";
            try {
                contents = File.ReadAllText(path);
            } catch {
                return;
            }

            Dictionary<string, dynamic> settings = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(contents);
            if (!settings.ContainsKey("RpcData")) return;
            RpcData data = settings["RpcData"].ToObject<RpcData>();

            if (data.Status == Status.NotRunning) {
                if (client == null) return;
                client.Dispose();
                client = null;
                return;
            }

            if (client == null) {
                client = new DiscordRpcClient("1109820127605686273");
                client.Initialize();
			}
            
            if (data.Status == Status.NoScript) {
                UpdatePresence($"Editing {data.Name}", null);
            } else if (data.Status == Status.Active) {
                string smallAssetToolTip = (data.Type == 0) ? "Script" : (data.Type == 1) ? "LocalScript" : (data.Type == 2) ? "ModuleScript" : "";
				UpdatePresence($"Editing {data.Name}", $"{data.Lines} lines", $"script{data.Type}", smallAssetToolTip);
			}
		}

        public static void UpdatePresence(string details, string state, string smallAssetName = null, string smallAssetToolTip = null) {
            RichPresence presence = client.CurrentPresence?.Clone() ?? new RichPresence();
            if (!presence.HasTimestamps()) {
                presence.Timestamps = new Timestamps() { Start = DateTime.UtcNow };
			}
            if (!presence.HasAssets()) {
                presence.Assets = new Assets() {
					LargeImageKey = "logo3",
					LargeImageText = "Roblox Studio"
				};
            }

			if (presence.State == state && presence.Details == details && (presence.Assets.SmallImageKey == smallAssetName)) return;
			presence.State = state;
            presence.Details = details;
            presence.Assets.SmallImageKey = smallAssetName;
            presence.Assets.SmallImageText = smallAssetToolTip;
            client.SetPresence(presence);
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