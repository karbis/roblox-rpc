using robloxrpc.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using DiscordRPC;
using Microsoft.Win32;
using Newtonsoft.Json;
using Timer = System.Timers.Timer;

#pragma warning disable CA1416 // Validate platform compatibility
namespace robloxrpc {
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
		static GameInfo cachedGameInfo;
		static Dictionary<string, DateTime> timestamps = [];
		static string currentTimestamp = "";

		public MyCustomApplicationContext() {
			// Initialize Tray Icon

			var contextMenu = new ContextMenuStrip();
			ToolStripMenuItem titleThingy = new ToolStripMenuItem("Roblox Studio RPC", null, null, "Roblox Studio RPC");
			contextMenu.Items.Add(titleThingy);
			contextMenu.Items.Add(new ToolStripSeparator());
			contextMenu.Items.Add(new SettingsDropdown());
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
			timer.Elapsed += (_, _) => {
				Update();
			};
		}
		public static string GetUserId() {
			// since the installedplugins is located in a folder thats named by your user id,
			// we need to go inside of the account switcher data and get the account that has the lowest user id
			// as that is the one you are probably logged into

			using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Roblox\RobloxStudio\LoggedInUsersStore\https:\www.roblox.com")) {
				string value = ((string)key.GetValue("users")).Replace("};{",",");
				while (value.Substring(value.Length-1, 1) != "}") {
					value = value.Substring(0, value.Length - 1);
				}
				Dictionary<string, dynamic> users = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(value);
				List<long> indices = [];
				foreach (KeyValuePair<string, dynamic> pair in users) {
					indices.Add(Convert.ToInt64(pair.Key));
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
			public long Lines;
			public byte Type;
			public string Name;
			public long PlaceId;
			public bool Playtesting;
		}
		static string[] typeToolTips = ["Script","LocalScript","ModuleScript"];
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

			if (data.Status == Status.NotRunning || !Settings.Default.StudioEnabled) {
				if (cachedGameInfo != null) {
					cachedGameInfo.Dispose();
					cachedGameInfo = null;
				}
				timestamps.Clear();
				timestamps.TrimExcess();

				if (client == null) return;
				client.Dispose();
				client = null;
				return;
			}

			if (client == null) {
				client = new DiscordRpcClient("1109820127605686273");
				client.Initialize();
				timestamps["Editing"] = DateTime.UtcNow;
				currentTimestamp = "Editing";
			}

			if (data.Status == Status.NoScript) {
				string verb = (data.Playtesting) ? "Playtesting" : "Editing";
				if (verb != currentTimestamp && verb == "Playtesting") {
					timestamps["Playtesting"] = DateTime.UtcNow;
				}
				currentTimestamp = verb;

				if (cachedGameInfo == null) {
					cachedGameInfo = new GameInfo(data.PlaceId);
					cachedGameInfo.GameInfoRecieved += Update;
					UpdatePresence(verb, null);
				} else if (!cachedGameInfo.Ready) {
					UpdatePresence(verb, null);
				} else {
					string creator = (cachedGameInfo.Creator == null || !Settings.Default.StudioRevealUsername) ? null : $"By {cachedGameInfo.Creator}";
					string gameName = cachedGameInfo.GameName;
					string iconLink = cachedGameInfo.IconLink;
					if (!Settings.Default.StudioRevealGameName) {
						gameName = "a game";
						iconLink = null;
					}

					string smallAssetToolTip = (data.PlaceId == 0) ? null : gameName;

					UpdatePresence($"{verb} {gameName}", creator, iconLink, smallAssetToolTip, Settings.Default.StudioSwapIconAndLogo);
				}
			} else if (data.Status == Status.Active) {
				UpdatePresence($"Editing {data.Name}", $"{data.Lines} lines", $"scriptnewer{data.Type}", typeToolTips[data.Type]);
			}
		}

		public static void UpdatePresence(string details, string state, string smallAssetName = null, string smallAssetToolTip = null, bool swap = false) {
			RichPresence presence = client.CurrentPresence?.Clone() ?? new RichPresence();
			if (!presence.HasTimestamps()) {
				presence.Timestamps = new Timestamps() { Start = DateTime.UtcNow };
			}
			if (!presence.HasAssets()) {
				presence.Assets = new Assets() {
					LargeImageKey = "logo5",
					LargeImageText = "Roblox Studio"
				};
			} else {
				presence.Assets.LargeImageKey = "logo5";
				presence.Assets.LargeImageText = "Roblox Studio";
			}

			if (presence.State == state && presence.Details == details && (presence.Assets.SmallImageKey == smallAssetName || presence.Assets.LargeImageKey == smallAssetName)) return;
			presence.State = state;
			presence.Details = details;
			presence.Assets.SmallImageKey = smallAssetName;
			presence.Assets.SmallImageText = smallAssetToolTip;
			presence.Timestamps.Start = timestamps[currentTimestamp];
			if (swap) {
				presence.Assets = new Assets() {
					LargeImageKey = smallAssetName,
					LargeImageText = smallAssetToolTip,
					SmallImageKey = "logo5",
					SmallImageText = "Roblox Studio",
				};
			}

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