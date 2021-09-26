using Microsoft.Xna.Framework;
using System.Collections.Generic;
using StardewValley;

namespace CustomCommunityCentre
{
	public class BundleMetadata
	{
		private const LocalizedContentManager.LanguageCode DefaultLanguageCode
			= LocalizedContentManager.LanguageCode.en;

		public string AreaName;
		public Rectangle AreaBounds;
		public Point NoteTileLocation;
		public Vector2 JunimoOffsetFromNoteTileLocation;
		public int BundlesRequired;
		public string JunimoColourName;
		public Dictionary<string, string> AreaDisplayNames;
		public Dictionary<string, string> AreaRewardNames;
		public Dictionary<string, string> AreaCompletionMessage;
		public Dictionary<string, Dictionary<string, string>> BundleDisplayNames;


		public Color JunimoColour
		{
			get
			{
				System.Drawing.Color colour = System.Drawing.Color.FromName(this.JunimoColourName);
				return new Color(colour.R, colour.G, colour.B);
			}
		}

		public static string GetLocalisedString(
			Dictionary<string, string> dict,
			string defaultValue = null,
			LocalizedContentManager.LanguageCode? code = null)
		{
			code ??= LocalizedContentManager.CurrentLanguageCode;
			string key = code.ToString();
			return dict.TryGetValue(key, out string translation)
				? translation
				: dict.TryGetValue(BundleMetadata.DefaultLanguageCode.ToString(), out translation)
					? translation
					: defaultValue;
		}
	}
}
