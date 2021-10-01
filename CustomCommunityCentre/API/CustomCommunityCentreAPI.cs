using StardewModdingAPI;

namespace CustomCommunityCentre.API
{
	// TODO: Repopulate API methods

	public interface ICustomCommunityCentreAPI
	{
		StardewValley.Locations.CommunityCenter GetCommunityCentre();
	}

	public class CustomCommunityCentreAPI
	{
		private readonly IReflectionHelper Reflection;

		public CustomCommunityCentreAPI(IReflectionHelper reflection)
		{
			this.Reflection = reflection;
		}

		public static StardewValley.Locations.CommunityCenter GetCommunityCentre()
		{
			return Bundles.CC;
		}
	}
}
