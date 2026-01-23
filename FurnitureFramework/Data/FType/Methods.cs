using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Objects;
using FurnitureFramework.Data.FType.Properties;
using HarmonyLib;

namespace FurnitureFramework.Data.FType
{
	using SVObject = StardewValley.Object;
	
	public partial class FType
	{
		public static void SetModData(Furniture furniture)
		{
			if (FPack.FPack.TryGetType(furniture, out FType? type))
			{
				furniture.modData["FF"] = "true";
				furniture.modData["FF.particle_timers"] = "[]";
				type.InitializeSlots(furniture);

				if (furniture is StorageFurniture)
				{
					furniture.modData["FF.storage_open_state"] = false.ToString();
					furniture.modData["FF.storage_anim_start"] = 0.ToString();
				}
			}
		}

		public void loadDisplayName(Furniture furniture, ref string result)
		{
			result = Variants[furniture.ItemId].GetVariantString(DisplayName, FPack.FPack.ContentPacks[ModID]);
		}

		public void loadDescription(Furniture furniture, ref string result)
		{
			if (Description == null || Description.Length == 0) return;

			result = Variants[furniture.ItemId].GetVariantString(Description, FPack.FPack.ContentPacks[ModID]);
		}

		string GetRot(Furniture furniture)
		{
			if (furniture.currentRotation.Value >= Rotations.Count)
				furniture.currentRotation.Set(0);
			return Rotations[furniture.currentRotation.Value];
		}

		#region Rotation

		public void rotate(Furniture furniture)
		{
			int rot = furniture.currentRotation.Value;
			rot = (rot + 1) % Rotations.Count;
			if (rot < 0) rot = 0;

			furniture.currentRotation.Value = rot;
			this.updateRotation(furniture); // ระบุ this เพื่อเรียก method ใน class นี้
		}

		public void updateRotation(Furniture furniture)
		{
			string rot = GetRot(furniture);
			Point pos = furniture.TileLocation.ToPoint() * Data.Utils.TILESIZE;

			furniture.boundingBox.Value = Collisions[rot].GetBoundingBox(pos);
			furniture.sourceRect.Value = Layers[rot][0].SourceRect;
		}

		#endregion

		#region Methods for Seats

		public void GetSeatPositions(Furniture furniture, ref List<Vector2> list)
		{
			string rot = GetRot(furniture);
			Vector2 tile_pos = furniture.boundingBox.Value.Location.ToVector2() / 64f;
			Seats[rot].GetSeatPositions(tile_pos, list);
		}

		public void GetSittingDirection(Furniture furniture, Farmer who, ref int sit_dir)
		{
			int seat_index = furniture.sittingFarmers[who.UniqueMultiplayerID];
			string rot = GetRot(furniture);

			int new_sit_dir = Seats[rot].GetSittingDirection(seat_index);
			if (new_sit_dir >= 0) sit_dir = new_sit_dir;
		}

		public void GetSittingDepth(Furniture furniture, Farmer who, ref float depth)
		{
			int seat_index = furniture.sittingFarmers[who.UniqueMultiplayerID];
			string rot = GetRot(furniture);

			float new_sit_depth = Seats[rot].GetSittingDepth(seat_index, furniture.boundingBox.Top);
			if (new_sit_depth >= 0) depth = new_sit_depth;
		}

		#endregion

		#region Methods for Collisions

		public void IntersectsForCollision(Furniture furniture, Rectangle rect, ref bool collides)
		{
			if (!collides) return;
			if (PlacementType == PlacementType.Rug)
			{
				collides = false;
				return;
			}

			string rot = GetRot(furniture);
			Point pos = furniture.boundingBox.Value.Location;
			collides = Collisions[rot].IsColliding(rect, pos);
		}

		public void canBePlacedHere(Furniture furniture, GameLocation loc, Vector2 tile, CollisionMask collisionMask, ref bool result)
		{
			if (!loc.CanPlaceThisFurnitureHere(furniture)) return;

			if (!furniture.isGroundFurniture())
				tile.Y = furniture.GetModifiedWallTilePosition(loc, (int)tile.X, (int)tile.Y);

			CollisionMask passable_ignored = CollisionMask.Buildings | CollisionMask.Flooring | CollisionMask.TerrainFeatures;
			if (furniture.isPassable()) passable_ignored |= CollisionMask.Characters | CollisionMask.Farmers;
			collisionMask &= ~(CollisionMask.Furniture | CollisionMask.Objects);

			string rot = GetRot(furniture);
			if (!Collisions[rot].CanBePlacedHere(furniture, loc, tile.ToPoint(), collisionMask, passable_ignored))
			{
				result = false;
				return;
			}

			if (PlacementType == PlacementType.Mural)
			{
				Point point = tile.ToPoint();
				if (loc is not DecoratableLocation dec_loc)
				{
					result = false;
					return;
				}

				if (!((dec_loc.isTileOnWall(point.X, point.Y) && dec_loc.GetWallTopY(point.X, point.Y) == point.Y) ||
					(dec_loc.isTileOnWall(point.X, point.Y - 1) && dec_loc.GetWallTopY(point.X, point.Y) + 1 == point.Y)))
				{
					result = false;
					return;
				}
			}

			if (furniture.GetAdditionalFurniturePlacementStatus(loc, (int)tile.X * 64, (int)tile.Y * 64) != 0)
			{
				result = false;
				return;
			}

			result = true;
		}

		public static void AllowPlacementOnThisTile(Furniture furniture, int x, int y, ref bool allow)
		{
			if (allow) return;
			allow = !IsClicked(furniture, x * 64, y * 64);
		}

		#endregion

		#region Methods for Slots

		private void InitializeSlots(Furniture furniture, string rot = "")
		{
			if (rot == "") rot = GetRot(furniture);

			int slots_count = Slots[rot].Count;
			Point position = new(furniture.boundingBox.Left, furniture.boundingBox.Bottom);

			if (furniture.heldObject.Value is not Chest chest)
			{
				SVObject held = furniture.heldObject.Value;
				chest = new();
				chest.Items.Add(held);
				furniture.heldObject.Value = chest;

				if (slots_count > 0 && held != null)
					Slots[rot][0].SetBox(held, position);
			}

			while (chest.Items.Count > slots_count)
			{
				Item? item = chest.Items[slots_count];
				chest.Items.RemoveAt(slots_count);
				if (item is null) continue;
				Game1.createItemDebris(item, furniture.boundingBox.Center.ToVector2(), 0);
			}

			if (chest.Items.Count < slots_count)
			{
				chest.Items.AddRange(Enumerable.Repeat<Item?>(null, slots_count - chest.Items.Count).ToList());
			}
		}

		private Point GetRelPos(Furniture furniture, Point pos)
		{
			Point this_pos = furniture.boundingBox.Value.Location;
			this_pos.Y += furniture.boundingBox.Value.Height;
			return (pos - this_pos) / new Point(4);
		}

		public bool PlaceInSlot(Furniture furniture, Point pos, Farmer who, SVObject obj)
		{
			string rot = GetRot(furniture);
			if (furniture.heldObject.Value is not Chest chest) return false;

			int slot_index = Slots[rot].GetEmptySlot(GetRelPos(furniture, pos), chest, who, furniture, obj);
			if (slot_index < 0) return false;

			obj.Location = furniture.Location;
			Slots[rot][slot_index].SetBox(obj, new Point(furniture.boundingBox.Left, furniture.boundingBox.Bottom));
			chest.Items[slot_index] = obj;
			who.reduceActiveItemByOne();
			Game1.currentLocation.playSound("woodyStep");
			obj.performDropDownAction(who);

			return true;
		}

		public bool RemoveFromSlot(Furniture furniture, Point pos, Farmer who)
		{
			string rot = GetRot(furniture);
			if (furniture.heldObject.Value is not Chest chest) return false;

			int slot_index = Slots[rot].GetFilledSlot(GetRelPos(furniture, pos), chest, out SVObject? obj);
			if (slot_index < 0 || obj is null) return false;

			if (who.addItemToInventoryBool(obj))
			{
				obj.performRemoveAction();
				chest.Items[slot_index] = null;
				Game1.playSound("coin");
				return true;
			}
			return false;
		}

		public bool ActionInSlot(Furniture furniture, Point pos, Farmer who)
		{
			string rot = GetRot(furniture);
			if (furniture.heldObject.Value is not Chest chest) return false;

			int slot_index = Slots[rot].GetFilledSlot(GetRelPos(furniture, pos), chest, out SVObject? obj);
			if (slot_index < 0 || obj is not Furniture furn) return false;

			return furn.checkForAction(who);
		}

		public static bool HasHeldObject(Furniture furniture)
		{
			SVObject held_obj = furniture.heldObject.Value;
			if (held_obj == null) return false;
			if (held_obj is Chest chest)
			{
				foreach (Item? item in chest.Items)
					if (item != null) return true;
				return false;
			}
			return true;
		}

		#endregion

		#region Methods for Placement Type

		public void isGroundFurniture(ref bool is_ground_f) => is_ground_f = PlacementType != PlacementType.Mural;
		public void isPassable(ref bool is_passable) => is_passable = PlacementType == PlacementType.Rug;

		#endregion

		#region Methods for Special Furniture

		#region TV
		public void getScreenPosition(TV furniture, ref Vector2 position)
		{
			Rectangle bounding_box = furniture.boundingBox.Value;
			position = bounding_box.Location.ToVector2();
			position.Y += bounding_box.Height;
			position += ScreenPosition[GetRot(furniture)].ToVector2() * 4f;
		}

		public void getScreenSizeModifier(ref float scale) => scale = ScreenScale;
		#endregion

		public void GetBedSpot(BedFurniture furniture, ref Point spot) => spot = furniture.TileLocation.ToPoint() + BedSpot;

		public void GetTankBounds(FishTankFurniture furniture, ref Rectangle result)
		{
			string rot = GetRot(furniture);
			Rectangle bounding_box = furniture.boundingBox.Value;
			Rectangle source_rect = Layers[rot][0].SourceRect;

			Point position = new(bounding_box.X, bounding_box.Y + bounding_box.Height);
			Point size = source_rect.Size * new Point(4);
			Rectangle area = FishArea[rot];

			if (area.IsEmpty)
			{
				position.Y -= source_rect.Height * 4;
				position += Layers[rot][0].DrawPos * new Point(4);
				result = new Rectangle(position + new Point(4, 64), size - new Point(8, 92));
			}
			else
			{
				result = new Rectangle(position + area.Location * new Point(4), area.Size * new Point(4));
			}
		}

		#region Storage

		public void setUpStoreForContext(ShopMenu shop_menu, ref bool _isStorageShop)
		{
			if (SpecialType != SpecialType.FFStorage) return;
			shop_menu.purchaseSound = null;
			shop_menu.purchaseRepeatSound = null;
			_isStorageShop = true;

#if IS_ANDROID
			shop_menu.tabButtons = new List<ClickableTextureComponent>();
			
			var tab1 = new ClickableTextureComponent(new Rectangle(0, 0, 64, 64), Game1.mouseCursors, new Rectangle(20, 20, 16, 16), 4f);
			var tab2 = new ClickableTextureComponent(new Rectangle(0, 0, 64, 64), Game1.mouseCursors, new Rectangle(36, 20, 16, 16), 4f);
			var tab3 = new ClickableTextureComponent(new Rectangle(0, 0, 64, 64), Game1.mouseCursors, new Rectangle(52, 20, 16, 16), 4f);

			switch (StoragePreset)
			{
				case StoragePreset.Dresser:
					shop_menu.ShopId = "Dresser";
					shop_menu.tabButtons.Add(tab1);
					shop_menu.setUpStoreForContext();
					shop_menu.applyTab();
					return;
				case StoragePreset.Catalogue:
					shop_menu.ShopId = "Catalogue";
					shop_menu.tabButtons.Add(tab2);
					shop_menu.setUpStoreForContext();
					shop_menu.applyTab();
					return;
				case StoragePreset.FurnitureCatalogue:
					string curID = this.Variants.Keys.FirstOrDefault() ?? "";
					if (curID.Contains("JojaFurnitureCatalogue") || curID.Contains("WizardFurnitureCatalogue") || 
						curID.Contains("JunimoFurnitureCatalogue") || curID.Contains("RetroFurnitureCatalogue") || 
						curID.Contains("TrashFurnitureCatalogue"))
						shop_menu.ShopId = curID;
					else
						shop_menu.ShopId = "Furniture Catalogue";
					
					shop_menu.tabButtons.Add(tab3);
					shop_menu.setUpStoreForContext();
					shop_menu.applyTab();
					return;
			}

			try {
				foreach ((TabProperty tab_prop, int idx) in StorageTabs.Select((value, index) => (value, index)))
					tab_prop.AddTab(shop_menu, ModID, idx);
			} catch (Exception ex) {
				ModEntry.Log($"[Error] Failed to create custom tabs: {ex.Message}", StardewModdingAPI.LogLevel.Error);
			}
			shop_menu.repositionTabs();
#else
			shop_menu.tabButtons = new();
			switch (StoragePreset)
			{
				case StoragePreset.Dresser: shop_menu.UseDresserTabs(); return;
				case StoragePreset.Catalogue: shop_menu.UseCatalogueTabs(); return;
				case StoragePreset.FurnitureCatalogue: shop_menu.UseFurnitureCatalogueTabs(); return;
			}

			foreach ((TabProperty tab_prop, int idx) in StorageTabs.Select((value, index) => (value, index)))
				tab_prop.AddTab(shop_menu, ModID, idx);
			shop_menu.repositionTabs();
#endif
		}

		public bool highlightItemToSell(Item item)
		{
			if (SpecialType != SpecialType.FFStorage) return false;
			switch (StoragePreset)
			{
				case StoragePreset.Dresser: return new List<int>(){ -95, -100, -97, -96 }.Contains(item.Category);
				case StoragePreset.Catalogue: return item is Wallpaper;
				case StoragePreset.FurnitureCatalogue: return item is Furniture;
			}
			if (StorageCondition == null) return true;
#if IS_ANDROID
			return true; 
#else
			return GameStateQuery.CheckConditions(StorageCondition, inputItem:item);
#endif
		}
		#endregion
		#endregion

		#region Methods for Transpilers

		public static bool IsClicked(Furniture furniture, int x, int y)
		{
			if (!FPack.FPack.TryGetType(furniture, out FType? type) || type.PlacementType == PlacementType.Rug)
				return furniture.boundingBox.Value.Contains(x, y);

			Rectangle rect = new(x, y, 1, 1);
			bool clicks = furniture.boundingBox.Value.Intersects(rect);
			type.IntersectsForCollision(furniture, rect, ref clicks);
			return clicks;
		}

		public static bool IsClicked(Furniture furniture, Point pos) => IsClicked(furniture, pos.X, pos.Y);

		public static void DrawLighting(SpriteBatch sprite_batch)
		{
			foreach (Furniture furniture in Game1.currentLocation.furniture)
			{
				if (FPack.FPack.TryGetType(furniture, out FType? type))
					type.DrawLights(furniture, sprite_batch);
				else if (furniture.heldObject.Value is Furniture held_furn && FPack.FPack.TryGetType(held_furn, out FType? held_type))
					held_type.DrawLights(held_furn, sprite_batch);
			}
		}

		public static float GetScreenDepth(Furniture furniture, bool overlay = false)
		{
			float depth;
			if (FPack.FPack.TryGetType(furniture, out FType? type))
			{
				depth = type.ScreenDepth[type.GetRot(furniture)].GetValue(furniture.GetBoundingBox().Top);
				depth = MathF.BitIncrement(depth);
			}
			else depth = (furniture.boundingBox.Bottom - 1) / 10000f + 1E-05f;
			if (overlay) depth = MathF.BitIncrement(depth);
			return depth;
		}

		#endregion

		public static void DayUpdate(Furniture furniture)
		{
			if (Game1.IsMasterGame && Game1.season == Season.Winter && Game1.dayOfMonth == 25 && furniture.heldObject.Value is Chest chest)
			{
				foreach ((Item item, int index) in chest.Items.Select((value, index) => (value, index)))
				{
					if (item.QualifiedItemId == "(O)223" && !Game1.player.mailReceived.Contains("CookiePresent_year" + Game1.year))
					{
						chest.Items[index] = ItemRegistry.Create<SVObject>("(O)MysteryBox");
						Game1.player.mailReceived.Add("CookiePresent_year" + Game1.year);
					}
					else if (item.Category == -6 && !Game1.player.mailReceived.Contains("MilkPresent_year" + Game1.year))
					{
						chest.Items[index] = ItemRegistry.Create<SVObject>("(O)MysteryBox");
						Game1.player.mailReceived.Add("MilkPresent_year" + Game1.year);
					}
				}
			}
		}

		public void updateWhenCurrentLocation(Furniture furniture)
		{
			string rot = GetRot(furniture);
			long ms_time = (long)Game1.currentGameTime.TotalGameTime.TotalMilliseconds;
			Particles[rot].UpdateTimer(furniture, ms_time, ModID);

			if (SpecialType == SpecialType.Bed)
			{
				Rectangle bed_col;
				if (BedArea == null)
				{
					Point bed_size = Collisions[rot].GameSize;
					Point area_size = new(Math.Max(64, bed_size.X - 128), Math.Max(64, bed_size.Y - 128));
					bed_col = new Rectangle((bed_size - area_size) / new Point(2), area_size);
				}
				else bed_col = new Rectangle(BedArea.Value.Location * new Point(4), BedArea.Value.Size * new Point(4));

				bed_col.Location += furniture.boundingBox.Value.Location;
				bool contains = bed_col.Contains(Game1.player.GetBoundingBox());

				if (!furniture.modData.ContainsKey("FF.checked_bed_tile"))
					furniture.modData["FF.checked_bed_tile"] = contains.ToString().ToLower();

				if (contains)
				{
					if (furniture.modData["FF.checked_bed_tile"] != "true" && !Game1.newDay && Game1.shouldTimePass() && Game1.player.hasMoved && !Game1.player.passedOut)
					{
						furniture.modData["FF.checked_bed_tile"] = "true";
						furniture.Location.createQuestionDialogue(Game1.content.LoadString("Strings\\Locations:FarmHouse_Bed_GoToSleep"), furniture.Location.createYesNoResponses(), "Sleep", null);
					}
				}
				else furniture.modData["FF.checked_bed_tile"] = "false";
			}
		}

		public void checkForAction(Furniture furniture, Farmer who, bool justCheckingForActivity, ref bool had_action)
		{
			if (justCheckingForActivity) return;
			string rot = GetRot(furniture);
			if (ShopId != null && Utility.TryOpenShopMenu(ShopId, Game1.currentLocation)) had_action = true;

			if (Toggle)
			{
				ToggleFurn(furniture);
				if (ModEntry.GetConfig().toggle_carry_to_slot && furniture.heldObject.Value is Chest held_chest)
					foreach (Item item in held_chest.Items)
						if (item is Furniture furn && FPack.FPack.TryGetType(furn, out FType? f_type)) f_type.ToggleFurn(furn);
				had_action = true;
			}
			else had_action |= Sounds.Play(furniture.Location);

			if (Seats[rot].Count > 0)
			{
				int sit_count = furniture.GetSittingFarmerCount();
				who.BeginSitting(furniture);
				if (furniture.GetSittingFarmerCount() > sit_count) had_action = true;
			}
		}

		public void ToggleFurn(Furniture furniture)
		{
			furniture.IsOn = !furniture.IsOn;
			Sounds.Play(furniture.Location, furniture.IsOn);
			Particles[GetRot(furniture)].Burst(furniture, ModID);
		}

		public static void OnRemoved(Furniture furniture)
		{
			furniture.modData["FF.particle_timers"] = "[]";
			furniture.heldObject.Value = null;
		}

		public void OnPlaced(Furniture furniture) => Particles[GetRot(furniture)].Burst(furniture, ModID);
	}

#if IS_ANDROID
    [HarmonyPatch(typeof(ShopMenu), "applyTab")]
    public static class ShopMenuPatch
    {
        public static void Postfix(ShopMenu __instance)
        {
            int currentTabID = (int)AccessTools.Field(typeof(ShopMenu), "currentTab").GetValue(__instance);
		
            if (TabProperty.ActiveTabConditions.TryGetValue(currentTabID, out string condition))
            {
                __instance.forSale.Clear();
                foreach (ISalable item in __instance.itemPriceAndStock.Keys)
                    if (CheckCondition(item, condition)) __instance.forSale.Add(item);
            }
        }

        private static bool CheckCondition(ISalable item, string condition)
        {
            if (item is not Item stardewItem) return false;
            if (condition.Contains("ITEM_CATEGORY") && condition.Contains("-96") && stardewItem.Category == -96) return true;
            if (condition.Contains("ITEM_TYPE"))
            {
                if (condition.Contains("(W)") && stardewItem is StardewValley.Tools.MeleeWeapon) return true;
                if (condition.Contains("(TR)") && stardewItem.Category == -96) return true;
            }
            if (stardewItem is StardewValley.Tools.MeleeWeapon w)
            {
                if (condition.Contains("WEAPON_TYPE Input 0 3") && (w.type.Value == 0 || w.type.Value == 3)) return true;
                if (condition.Contains("WEAPON_TYPE Input 2") && w.type.Value == 2) return true;
                if (condition.Contains("WEAPON_TYPE Input 1") && w.type.Value == 1) return true;
            }
            return true;
        }
    }
#endif
}
