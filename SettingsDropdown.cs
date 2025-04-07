using robloxrpc.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace robloxrpc {
	internal class SettingsDropdown : ToolStripMenuItem {
		public static Setting[] SettingsList = [
			new Setting("StudioRevealGameName", "Show game name"),
			new Setting("StudioRevealUsername", "Show creator"),
			new Setting("StudioSwapIconAndLogo", "Large game icon"),
			new Setting("StudioEnabled", "Enabled"),
		];

		[SupportedOSPlatform("windows7.0")]
		public SettingsDropdown() {
			Text = "Settings";

			foreach (Setting setting in SettingsList) {
				ToolStripMenuItem item = new ToolStripMenuItem(setting.DisplayName);
				item.Checked = (bool)Settings.Default[setting.Name];
				DropDownItems.Add(item);

				item.Click += (_, _) => {
					item.Checked = !item.Checked;
					Settings.Default[setting.Name] = item.Checked;
					Settings.Default.Save();
					MyCustomApplicationContext.Update();
				};
			}

			DropDown.Closing += (object _, ToolStripDropDownClosingEventArgs args) => {
				if (args.CloseReason != ToolStripDropDownCloseReason.ItemClicked) return;
				args.Cancel = true;
			};
		}
	}

	struct Setting {
		public string Name;
		public string DisplayName;

		public Setting(string name, string displayName) {
			Name = name;
			DisplayName = displayName;
		}
	}
}
