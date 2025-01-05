using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace robloxrpc {
	internal class GameInfo: IDisposable {
		public string Creator;
		public string IconLink;
		public string GameName;
		public bool Invalidated = false;
		public delegate void OnGameInfoRecieved();
		public event OnGameInfoRecieved GameInfoRecieved;
		HttpClient httpClient;
		public long PlaceId;
		public bool Ready = false;

		public GameInfo(long placeId) {
			httpClient = new HttpClient();
			PlaceId = placeId;

			Task.Run(getInfo);
		}

		private const string VERIFIED_BADGE = " ✔";
		private void getInfo() {
			bool isLocalFile = PlaceId == 0;
			if (isLocalFile) {
				GameName = "Local File";
			} else {
				GameDetails details = GetAsync<GameDetails>($"https://economy.roblox.com/v2/assets/{PlaceId}/details");
				if (details == null) return;
				GameName = details.Name;
				Creator = $"{(details.Creator.CreatorType == CreatorType.User ? "@" : "")}{details.Creator.Name}{(details.Creator.HasVerifiedBadge ? VERIFIED_BADGE : "")}";
			}

			long gameId = (isLocalFile) ? 95206881 : PlaceId;
			IconDetails icon = GetAsync<IconDetails>($"https://thumbnails.roblox.com/v1/places/gameicons?placeIds={gameId}&format=Png&size=256x256");
			if (icon == null || Invalidated) return;
			IconLink = icon.data[0].imageUrl;
			Ready = true;
			GameInfoRecieved?.Invoke();
		}

		public T GetAsync<T>(string url) {
			if (Invalidated) return default(T);
			try {
				using HttpResponseMessage message = httpClient.GetAsync(url).Result;
				string json = message.Content.ReadAsStringAsync().Result;
				return JsonConvert.DeserializeObject<T>(json);
			} catch {
				return default(T);
			}
		}

		public void Dispose() {
			if (Invalidated) return;
			Invalidated = true;
			httpClient.Dispose();

			foreach(OnGameInfoRecieved e in GameInfoRecieved.GetInvocationList()) {
				GameInfoRecieved -= e;
			}
		}
	}

	public enum CreatorType {
		User,
		Group
	}
	class GameDetails {
		public class CreatorData {
			public string Name;
			public bool HasVerifiedBadge;
			public CreatorType CreatorType = CreatorType.User;
		}
		public string Name;
		public CreatorData Creator;
	}

	class IconDetails {
		public class Icon {
			public string imageUrl;
		}
		public List<Icon> data;
	}
}
