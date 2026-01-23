using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using StardewValley;
using StardewValley.Menus;
using System.Collections.Generic;

namespace FurnitureFramework.Data.FType.Properties
{
    [JsonConverter(typeof(SpaceRemover<TabProperty>))]
    public class TabProperty
    {
        public string ID;
        public string HoverText = "";
        public string? Condition;
        public string SourceImage;
        public Rectangle SourceRect;

        public static Dictionary<int, string> ActiveTabConditions = new Dictionary<int, string>();

        public void AddTab(ShopMenu shop_menu, string mod_id, int idx)
        {
            int tabID = 100000 + idx;

            if (Condition != null)
            {
                if (ActiveTabConditions.ContainsKey(tabID))
                    ActiveTabConditions[tabID] = Condition;
                else
                    ActiveTabConditions.Add(tabID, Condition);
            }

            shop_menu.tabButtons.Add(
                new ClickableTextureComponent(
                    new Rectangle(0, 0, 64, 64), 
                    ModEntry.GetHelper().GameContent.Load<Texture2D>($"FF/{mod_id}/{SourceImage}"), 
                    SourceRect, 
                    4f
                )
                {
                    myID = tabID,
                    upNeighborID = -99998,
                    downNeighborID = -99998,
                    rightNeighborID = 3546,
                    
#if !IS_ANDROID
					Filter = salable => Condition == null || (salable is Item item && GameStateQuery.CheckConditions(Condition, inputItem:item)),
#endif
                }
            );
        }
    }
}
