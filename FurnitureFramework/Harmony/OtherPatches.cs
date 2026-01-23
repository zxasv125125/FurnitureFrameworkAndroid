using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;
using FurnitureType = FurnitureFramework.Data.FType.FType;
using FurniturePack = FurnitureFramework.Data.FPack.FPack;

namespace FurnitureFramework.FFHarmony.Patches
{
	internal class FarmerPostfixes
	{
		#pragma warning disable 0414
		static readonly PatchType patch_type = PatchType.Postfix;
		static readonly Type base_type = typeof(Farmer);
		#pragma warning restore 0414

		internal static float getDrawLayer(
			float __result, Farmer __instance
		)
		{
			if (!__instance.IsSitting()) return __result;
			if (__instance.sittingFurniture is not Furniture furniture)
				return __result;

			try
			{
				// [FIX] ใช้ FurniturePack ที่ alias ไว้ด้านบนเพื่อเลี่ยงชื่อซ้ำ
				if (FurniturePack.TryGetType(furniture, out FurnitureType? type))
					type.GetSittingDepth(furniture, __instance, ref __result);
			}
			catch (Exception ex)
			{
				ModEntry.Log($"Failed in {nameof(getDrawLayer)}:\n{ex}", LogLevel.Error);
			}

			return __result;
		}
	}

	internal class Game1Transpiler
	{
		#pragma warning disable 0414
		static readonly PatchType patch_type = PatchType.Transpiler;
		static readonly Type base_type = typeof(Game1);
		#pragma warning restore 0414

		#region DrawLighting

		static IEnumerable<CodeInstruction> DrawLighting(
			IEnumerable<CodeInstruction> instructions
		)
		{
			List<CodeInstruction> to_replace = new()
			{
				new CodeInstruction(
					OpCodes.Ldsfld,
					AccessTools.DeclaredField(
						typeof(Game1),
						"spriteBatch"
					)
				),
				new CodeInstruction(
					OpCodes.Callvirt,
					AccessTools.DeclaredMethod(
						typeof(SpriteBatch),
						"End",
						Array.Empty<Type>()
					)
				)
			};
			List<CodeInstruction> to_write = new()
			{
				new CodeInstruction(
					OpCodes.Ldsfld,
					AccessTools.DeclaredField(
						typeof(Game1),
						"spriteBatch"
					)
				),
				new CodeInstruction(
					OpCodes.Call,
					AccessTools.DeclaredMethod(
						typeof(FurnitureType),
						"DrawLighting",
						new Type[] {typeof(SpriteBatch) }
					)
				),
				new CodeInstruction(
					OpCodes.Ldsfld,
					AccessTools.DeclaredField(
						typeof(Game1),
						"spriteBatch"
					)
				),
				new CodeInstruction(
					OpCodes.Callvirt,
					AccessTools.DeclaredMethod(
						typeof(SpriteBatch),
						"End",
						Array.Empty<Type>()
					)
				)
			};

			return Transpiler.ReplaceInstructions(instructions, to_replace, to_write);
		}

		#endregion
	}

	internal class UtilityTranspiler
	{
		#pragma warning disable 0414
		static readonly PatchType patch_type = PatchType.Transpiler;
		static readonly Type base_type = typeof(Utility);
		#pragma warning restore 0414

		#region canGrabSomethingFromHere

		static IEnumerable<CodeInstruction> canGrabSomethingFromHere(
			IEnumerable<CodeInstruction> instructions
		)
		{
			List<CodeInstruction> to_replace = new()
			{
				new(OpCodes.Ldfld, AccessTools.Field(
					typeof(StardewValley.Object),
					"heldObject"
				)),
				new(OpCodes.Callvirt, AccessTools.Method(
					typeof(Netcode.NetRef<StardewValley.Object>),
					"get_Value"
				))
			};
			List<CodeInstruction> to_write = new()
			{
				new(OpCodes.Call, AccessTools.Method(
					typeof(FurnitureType),
					"HasHeldObject"
				))
			};

			return Transpiler.ReplaceInstructions(instructions, to_replace, to_write);

		}

		#endregion
	}

	internal class ShopMenuPostfixes
	{
		#pragma warning disable 0414
		static readonly PatchType patch_type = PatchType.Postfix;
		static readonly Type base_type = typeof(ShopMenu);
		#pragma warning restore 0414

		#region setUpStoreForContext

		internal static void setUpStoreForContext(ShopMenu __instance, ref bool ____isStorageShop)
		{
			try
			{
				if (FurniturePack.TryGetType(__instance, out FurnitureType? type))
					type.setUpStoreForContext(__instance, ref ____isStorageShop);

				if (__instance.ShopId == "leroymilo.FF.debug_catalog")
				{
#if IS_ANDROID
					if (__instance.tabButtons == null) 
						__instance.tabButtons = new List<ClickableTextureComponent>();

					__instance.tabButtons.Add(new ClickableTextureComponent(
						new Rectangle(0, 0, 64, 64), Game1.mouseCursors, new Rectangle(20, 20, 16, 16), 4f));
					__instance.tabButtons.Add(new ClickableTextureComponent(
						new Rectangle(0, 0, 64, 64), Game1.mouseCursors, new Rectangle(36, 20, 16, 16), 4f));
					__instance.tabButtons.Add(new ClickableTextureComponent(
						new Rectangle(0, 0, 64, 64), Game1.mouseCursors, new Rectangle(52, 20, 16, 16), 4f));
					
					__instance.repositionTabs();
#else
					__instance.UseFurnitureCatalogueTabs();
#endif
				}
			}
			catch (Exception ex)
			{
				ModEntry.Log($"Failed in {nameof(setUpStoreForContext)}:\n{ex}", LogLevel.Error);
			}
		}

		#endregion

		#region highlightItemToSell

		internal static bool highlightItemToSell(bool __result, ShopMenu __instance, Item i)
		{
			try
			{
				if (FurniturePack.TryGetType(__instance, out FurnitureType? type))
					return type.highlightItemToSell(i);
			}
			catch (Exception ex)
			{
				ModEntry.Log($"Failed in {nameof(highlightItemToSell)}:\n{ex}", LogLevel.Error);
			}
			return __result;
		}

		#endregion
	}
}
