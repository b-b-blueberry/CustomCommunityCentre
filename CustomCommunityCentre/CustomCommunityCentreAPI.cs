using StardewModdingAPI;

namespace CustomCommunityCentre
{
	public interface ICommunityCentreKitchenAPI
	{
		StardewValley.Locations.CommunityCenter GetCommunityCentre();
		bool IsCommunityCentreComplete();
		bool IsCommunityCentreKitchenActive();
		bool IsCommunityCentreKitchenComplete();
		bool HasOrWillReceiveCommunityCentreAreasCompletedMailForAllAreas();
		bool IsAbandonedJojaMartBundleAvailableOrComplete();
		StardewValley.Objects.Chest GetCommunityCentreFridge();
		bool IsMultiplayer();
		int GetNumberOfCabinsBuilt();
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

		public static bool IsCommunityCentreComplete()
		{
			return Bundles.IsCommunityCentreComplete(Bundles.CC);
		}

		public static bool IsCommunityCentreKitchenActive()
		{
			return Kitchen.IsKitchenLoaded(Bundles.CC);
		}

		public static bool IsCommunityCentreKitchenComplete()
		{
			return Kitchen.IsKitchenComplete(Bundles.CC);
		}

		public static bool HasOrWillReceiveCommunityCentreAreasCompletedMailForAllAreas()
		{
			return Bundles.HasOrWillReceiveAreaCompletedMailForAllCustomAreas();
		}

		public static bool IsAbandonedJojaMartBundleAvailable()
		{
			return Bundles.IsAbandonedJojaMartBundleAvailableOrComplete();
		}

		public static StardewValley.Objects.Chest GetCommunityCentreFridge()
		{
			return Kitchen.GetKitchenFridge(Bundles.CC);
		}

		public static bool IsMultiplayer()
		{
			return Bundles.IsMultiplayer();
		}

		public static int GetNumberOfCabinsBuilt()
		{
			return Bundles.GetNumberOfCabinsBuilt();
		}
	}
}
