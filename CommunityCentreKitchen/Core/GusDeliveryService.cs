using CustomCommunityCentre;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;
using StardewValley.Util;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CommunityCentreKitchen
{
	public class GusDeliveryService
	{
		private static IModHelper Helper => ModEntry.Instance.Helper;
		private static IReflectionHelper Reflection => ModEntry.Instance.Helper.Reflection;

		public static string DeliveryTextureAssetKey = CommunityCentreKitchen.AssetManager.GameDeliverySpritesPath;
		public static Lazy<Texture2D> DeliveryTexture;

		// Saloon delivery service
		public const string ShopOwner = "Gus";
		public static readonly string ItemDeliveryModDataKey = $"{ModEntry.Instance.ModManifest.UniqueID}.ItemDelivery";
		public static bool IsDeliveryInProgress;

		public static int DeliveryStartTime;
		public static int DeliveryEndTime;
		public const int DeliveryTimeDelta = 1;
		public const int SaloonOpeningTime = 1200;
		public const int SaloonClosingTime = 2400;

		public static bool IsSaloonDeliverySurchargeActive => !Bundles.IsCommunityCentreComplete(Bundles.CC);
		public const int SaloonDeliverySurcharge = 50;

		public static Vector2 DummyDeliveryChestTilePosition => new Vector2(CustomCommunityCentre.ModEntry.DummyId);
		public const string DummyDeliveryChestLocation = "Farm";

		// Mail data
		public static string MailSaloonDeliverySurchargeWaived => $"{ModEntry.Instance.ModManifest.UniqueID}.saloonDeliverySurchargeWaived";


		internal static void RegisterEvents()
		{
			Helper.Events.GameLoop.DayStarted += GusDeliveryService.GameLoop_DayStarted;
			Helper.Events.GameLoop.TimeChanged += GusDeliveryService.GameLoop_TimeChanged;
			Helper.Events.Input.ButtonPressed += GusDeliveryService.Input_ButtonPressed;
			Helper.Events.Display.MenuChanged += GusDeliveryService.Display_MenuChanged;
		}

		internal static void AddConsoleCommands(string cmd)
		{
			Helper.ConsoleCommands.Add(
				name: cmd + "delivery",
				documentation: $"Open a new Saloon delivery menu.",
				callback: (string s, string[] args) =>
				{
					GusDeliveryService.OpenDeliveryMenu();
				});
			Helper.ConsoleCommands.Add(
				name: cmd + "gus",
				documentation: $"Create a new {nameof(GusOnABike)} on the farm.",
				callback: (string s, string[] args) =>
				{
					GusOnABike.Create();
				});
		}

		private static void GameLoop_DayStarted(object sender, DayStartedEventArgs e)
		{
			if (Bundles.IsCommunityCentreComplete(Bundles.CC))
			{
				// Add mail on Community Centre completion for Saloon delivery service surcharge fee waived
				if (!Game1.player.hasOrWillReceiveMail(GusDeliveryService.MailSaloonDeliverySurchargeWaived))
				{
					Game1.addMail(GusDeliveryService.MailSaloonDeliverySurchargeWaived);
				}
			}
		}

		private static void GameLoop_TimeChanged(object sender, TimeChangedEventArgs e)
		{
			if (GusDeliveryService.IsDeliveryInProgress
				&& (e.NewTime < GusDeliveryService.SaloonOpeningTime
					|| e.NewTime > GusDeliveryService.SaloonClosingTime
					|| e.NewTime >= GusDeliveryService.DeliveryEndTime))
			{
				bool isGusHere = Game1.getFarm().critters.All(c => !(c is GusOnABike));
				if (isGusHere)
				{
					if (Game1.currentLocation.isFarmBuildingInterior())
					{
						GusOnABike.Honk(isOnFarm: false);
					}
					else if (Game1.currentLocation is Farm)
					{
						GusOnABike.Create();
					}
				}
			}
		}

		private static void Input_ButtonPressed(object sender, ButtonPressedEventArgs e)
		{
			// In-game interactions
			if (!Game1.game1.IsActive || Game1.currentLocation == null || !Context.IsWorldReady)
				return;

			// . . .

			// World interactions
			if (!Context.CanPlayerMove)
				return;

			if (e.Button.IsActionButton())
			{
				// Object actions
				Game1.currentLocation.Objects.TryGetValue(e.Cursor.GrabTile, out StardewValley.Object o);
				if (o != null)
				{
					// Open Saloon delivery giftbox chests
					if (o is Chest c)
					{
						GusDeliveryService.TryOpenDeliveryChest(location: Game1.currentLocation, chest: c, button: e.Button);
					}
				}
			}
		}

		private static void Display_MenuChanged(object sender, MenuChangedEventArgs e)
		{
			if (e.OldMenu is TitleMenu || e.NewMenu is TitleMenu || Game1.currentLocation == null || Game1.player == null)
				return;

			// Handle Saloon Delivery telephone dialogue menu
			const string questionKey = "telephone";
			if (e.NewMenu is DialogueBox dialogueBox
				&& Game1.currentLocation.lastQuestionKey == questionKey
				&& dialogueBox.characterDialogue?.speaker?.Name == GusDeliveryService.ShopOwner
				&& (Kitchen.HasOrWillReceiveKitchenCompletedMail() || Kitchen.IsKitchenComplete(Bundles.CC)))
			{
				GusDeliveryService.TryOpenDeliveryMenu();

				return;
			}
		}

		public static void ResetTexture()
		{
			GusDeliveryService.DeliveryTexture = new Lazy<Texture2D>(delegate
			{
				Texture2D texture = Game1.content.Load<Texture2D>(GusDeliveryService.DeliveryTextureAssetKey);
				return texture;
			});
		}

		internal static void TryOpenDeliveryMenu()
		{
			bool isNotReady = GameLocation.AreStoresClosedForFestival()
				|| Game1.timeOfDay < GusDeliveryService.SaloonOpeningTime || Game1.timeOfDay >= GusDeliveryService.SaloonClosingTime
				|| (Game1.getCharacterFromName(GusDeliveryService.ShopOwner)?.dayScheduleName.Value == "fall_4" && Game1.timeOfDay >= 1700);
			if (isNotReady)
				return;

			// Add question responses after dialogue
			List<Response> responses = new List<Response>
			{
				new Response(
					responseKey: $"{ModEntry.Instance.ModManifest.UniqueID}_Telephone_delivery",
					responseText: ModEntry.i18n.Get($"response.phone.delivery{(GusDeliveryService.IsSaloonDeliverySurchargeActive ? "_surcharge" : "")}")),
				new Response(
					responseKey: "HangUp",
					responseText: Game1.content.LoadString(@"Strings/Characters:Phone_HangUp"))
			};
			Game1.afterFadeFunction phoneDialogue = delegate
			{
				Game1.currentLocation.createQuestionDialogue(
					question: Game1.content.LoadString(@"Strings/Characters:Phone_SelectOption"),
					answerChoices: responses.ToArray(),
					afterDialogueBehavior: delegate (Farmer who, string questionAndAnswer)
					{
						static string getAnswer(string questionAndAnswer)
						{
							return questionAndAnswer.Split(new char[] { '_' }, 2).Last();
						}
						string[] answers = responses
							.Take(responses.Count - 1)
							.Select(r => getAnswer(r.responseKey))
							.ToArray();
						string answer = getAnswer(questionAndAnswer);
						// is 'answer' even a word? what the
						if (answer == answers[0])
						{
							// Saloon Delivery

							GusDeliveryService.OpenDeliveryMenu();
						}
						/*
						else if (answer == answers[1])
						{
							// Daily Dish

							// Load new phone dialogue
							const string messageRoot = @"Strings/Characters:Phone_Gus_Open";
							string message = Game1.dishOfTheDay != null
								? Game1.content.LoadString(messageRoot + ((Game1.random.NextDouble() < 0.01) ? "_Rare" : ""),
									Game1.dishOfTheDay.DisplayName)
								: Game1.content.LoadString(messageRoot + "_NoDishOfTheDay");
							Game1.drawDialogue(dialogueBox.characterDialogue.speaker, message);
						}
						*/
					});
			};
			Game1.afterDialogues = (Game1.afterFadeFunction)Delegate.Combine(Game1.afterDialogues, phoneDialogue);
		}

		internal static void TryOpenDeliveryChest(GameLocation location, Chest chest, SButton button)
		{
			if (chest.modData.TryGetValue(GusDeliveryService.ItemDeliveryModDataKey, out string s) && long.TryParse(s, out long id))
			{
				if (id != Game1.player.UniqueMultiplayerID)
				{
					Game1.drawObjectDialogue(Game1.content.LoadString("Strings\\Objects:ParsnipSeedPackage_SomeoneElse"));
				}
				else
				{
					Game1.currentLocation.playSound("Ship");
					foreach (Item item in chest.items)
					{
						if (item != null)
						{
							Game1.createItemDebris(item, chest.TileLocation * Game1.tileSize, -1, location);
						}
					}
					chest.items.Clear();
					chest.clearNulls();

					TemporaryAnimatedSprite sprite = new TemporaryAnimatedSprite(
						textureName: @"LooseSprites/Giftbox",
						sourceRect: new Rectangle(
							0,
							chest.giftboxIndex.Value * StardewValley.Object.spriteSheetTileSize * 2,
							StardewValley.Object.spriteSheetTileSize,
							StardewValley.Object.spriteSheetTileSize * 2),
						animationInterval: 80f,
						animationLength: 11,
						numberOfLoops: 1,
						position: (chest.TileLocation * Game1.tileSize) - new Vector2(0f, Game1.tileSize - 12f),
						flicker: false,
						flipped: false,
						layerDepth: chest.TileLocation.Y / 10000f,
						alphaFade: 0f,
						color: Color.White,
						scale: Game1.pixelZoom,
						scaleChange: 0f,
						rotation: 0f,
						rotationChange: 0f)
					{
						destroyable = false,
						holdLastFrame = true
					};
					if (location.netObjects.ContainsKey(chest.TileLocation) && location.netObjects[chest.TileLocation] == chest)
					{
						CustomCommunityCentre.ModEntry.Instance.GetMultiplayer().broadcastSprites(location, sprite);
						location.removeObject(chest.TileLocation, showDestroyedObject: false);
					}
					else
					{
						location.temporarySprites.Add(sprite);
					}
				}

				Helper.Input.Suppress(button);
			}
		}

		internal static void OpenDeliveryMenu()
		{
			if (GusDeliveryService.DeliveryTexture == null)
			{
				// Set first-time lazy texture
				GusDeliveryService.ResetTexture();
			}

			// Open a limited-stock saloon shop for the player
			Dictionary<ISalable, int[]> itemPriceAndStock = Utility.getSaloonStock()
				.Where(pair => pair.Key is Item i && (!(i is StardewValley.Object o) || !o.IsRecipe))
				.ToDictionary(pair => pair.Key, pair => pair.Value);

			ShopMenuNoInventory shopMenu = new ShopMenuNoInventory(
				itemPriceAndStock: itemPriceAndStock,
				currency: 0,
				who: GusDeliveryService.ShopOwner,
				on_purchase: delegate (ISalable item, Farmer farmer, int amount)
				{
					Game1.player.team.synchronizedShopStock.OnItemPurchased(
						shop: SynchronizedShopStock.SynchedShop.Saloon,
						item: item,
						amount: amount);

					// Vanish the item and add it to the dummy delivery chest
					((ShopMenuNoInventory)Game1.activeClickableMenu).heldItem = null;
					Item i = ((Item)item).getOne();
					i.Stack = amount;
					i.modData[GusDeliveryService.ItemDeliveryModDataKey] = farmer.UniqueMultiplayerID.ToString();
					((ShopMenuNoInventory)Game1.activeClickableMenu).AddToOrderDisplay(item: i);
					Chest dummyChest = GusDeliveryService.GetDummyDeliveryChest();
					dummyChest.addItem(i);

					// Raise flags for delivery
					GusDeliveryService.IsDeliveryInProgress = true;

					return false;
				});
			shopMenu.exitFunction = delegate
			{
				if (shopMenu.DeliveryItemsAndCounts.Any())
				{
					if (GusDeliveryService.IsSaloonDeliverySurchargeActive)
					{
						Game1.player.Money -= GusDeliveryService.SaloonDeliverySurcharge;
					}

					GusDeliveryService.DeliveryStartTime = Game1.timeOfDay;
					int deliveryEndTime = Utility.ModifyTime(
						timestamp: GusDeliveryService.DeliveryStartTime,
						minutes_to_add: GusDeliveryService.DeliveryTimeDelta * 10);
					GusDeliveryService.DeliveryEndTime = deliveryEndTime;

					Item item = shopMenu.DeliveryItemsAndCounts.Keys.ToArray()[Game1.random.Next(0, shopMenu.DeliveryItemsAndCounts.Count)];

					const string key = "dialogue.phone.delivery.";
					int count = ModEntry.i18n.GetTranslations()
						.Count(t => t.Key.ToString().StartsWith(key));
					int whichMessage = Game1.random.Next(0, count);
					string message = ModEntry.i18n.Get(
						key: $"{key}{whichMessage}",
						tokens: new { ItemName = item.DisplayName });
					Game1.drawDialogue(speaker: shopMenu.portraitPerson, dialogue: message);
				}
			};
			Game1.activeClickableMenu = shopMenu;
		}

		internal static Chest GetDummyDeliveryChest()
		{
			GameLocation where = Game1.getLocationFromName(GusDeliveryService.DummyDeliveryChestLocation);
			Vector2 tilePosition = GusDeliveryService.DummyDeliveryChestTilePosition;
			if (!where.Objects.TryGetValue(tilePosition, out StardewValley.Object o))
			{
				Chest chest = new Chest(playerChest: true, tileLocation: tilePosition);
				where.Objects.Add(tilePosition, chest);
			}
			return where.Objects[tilePosition] as Chest;
		}

		internal static void AddDeliveryChests()
		{
			Farm farm = Game1.getFarm();

			Chest dummyChest = GusDeliveryService.GetDummyDeliveryChest();
			Dictionary<Farmer, List<Item>> farmersAndItems = Game1.getAllFarmers().ToDictionary(
				keySelector: farmer => farmer,
				elementSelector: farmer => dummyChest.items
					.Where(i => farmer.UniqueMultiplayerID == long.Parse(i.modData[GusDeliveryService.ItemDeliveryModDataKey]))
					.ToList());
			foreach (Farmer farmer in farmersAndItems.Keys.Where(f => farmersAndItems[f].Any()))
			{
				Vector2 mailboxPosition = Utility.PointToVector2(farmer.getMailboxPosition());
				Vector2 chestPosition = CustomCommunityCentre.ModEntry.FindFirstPlaceableTileAroundPosition(
					location: farm,
					tilePosition: mailboxPosition,
					o: new Chest(),
					maxIterations: 100);
				Chest deliveryChest = new Chest(coins: 0, items: farmersAndItems[farmer], location: chestPosition, giftbox: true);
				deliveryChest.modData[GusDeliveryService.ItemDeliveryModDataKey] = farmer.UniqueMultiplayerID.ToString();
				farm.Objects.Add(chestPosition, deliveryChest);

				Bundles.BroadcastPuffSprites(
					multiplayer: null,
					location: farm,
					tilePosition: chestPosition);
			}

			GusDeliveryService.ResetDelivery();
		}

		internal static void ResetDelivery()
		{
			GusDeliveryService.IsDeliveryInProgress = false;
			GusDeliveryService.DeliveryStartTime = GusDeliveryService.DeliveryEndTime = -1;

			GameLocation where = Game1.getLocationFromName(GusDeliveryService.DummyDeliveryChestLocation);
			where.Objects.Remove(GusDeliveryService.DummyDeliveryChestTilePosition);
		}
	}
}
