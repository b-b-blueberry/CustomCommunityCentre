using StardewModdingAPI;

namespace CustomCommunityCentre
{
	public interface ICommunityCentreKitchenAPI
	{
		StardewValley.Locations.CommunityCenter GetCommunityCentre();
	}

	public class CommunityCentreKitchenAPI
	{
		private readonly IReflectionHelper Reflection;

		public CommunityCentreKitchenAPI(IReflectionHelper reflection)
		{
			this.Reflection = reflection;
		}

		public static StardewValley.Locations.CommunityCenter GetCommunityCentre()
		{
			return Bundles.CC;
		}
	}
}
