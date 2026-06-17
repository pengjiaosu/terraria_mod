using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.UI;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.GameContent.Achievements;
using Terraria.DataStructures;
using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace armor
{
    public class ExtraArmorPlayer : ModPlayer
    {
        // 修改点：数组大小改为 9 (3组 * 3件)
        public Item[] extraArmor = new Item[9];

        // ... (保留中间的属性引用代码) ...
        internal ref float armorPenetration => ref base.Player.GetArmorPenetration(DamageClass.Generic);
        internal ref float allCrit => ref base.Player.GetCritChance(DamageClass.Generic);
        internal ref float meleeCrit => ref base.Player.GetCritChance(DamageClass.Melee);
        internal ref float magicCrit => ref base.Player.GetCritChance(DamageClass.Magic);
        internal ref float rangedCrit => ref base.Player.GetCritChance(DamageClass.Ranged);
        internal ref StatModifier allDamage => ref base.Player.GetDamage(DamageClass.Generic);
        internal ref StatModifier meleeDamage => ref base.Player.GetDamage(DamageClass.Melee);
        internal ref StatModifier magicDamage => ref base.Player.GetDamage(DamageClass.Magic);
        internal ref StatModifier rangedDamage => ref base.Player.GetDamage(DamageClass.Ranged);
        private ref StatModifier arrowDamageAdditiveStack => ref base.Player.arrowDamage;
        internal ref StatModifier minionDamage => ref base.Player.GetDamage(DamageClass.Summon);
        internal ref float minionKB => ref base.Player.GetKnockback(DamageClass.Summon).Base;
        internal ref float meleeSpeed => ref base.Player.GetAttackSpeed(DamageClass.Melee);

        public override void Initialize()
        {
            // 初始化 9 个槽位
            for (int i = 0; i < 9; i++)
            {
                extraArmor[i] = new Item();
                extraArmor[i].SetDefaults(0);
            }
        }

        public override void PostUpdateEquips()
        {
            var config = ModContent.GetInstance<ArmorConfig>();
            string originalBonus = Player.setBonus;
            List<string> extraBonusLines = new List<string>();



            

            // 按照配置的组数进行循环处理
            for (int g = 0; g < config.ArmorGroupCount; g++)
            {
                int baseIdx = g * 3;
                Item head = extraArmor[baseIdx];
                Item body = extraArmor[baseIdx + 1];
                Item legs = extraArmor[baseIdx + 2];

                if (head.IsAir && body.IsAir && legs.IsAir) continue;

                // 1. 防御力计算
                int groupDefense = 0;
                if (!head.IsAir) groupDefense += head.defense;
                if (!body.IsAir) groupDefense += body.defense;
                if (!legs.IsAir) groupDefense += legs.defense;
                Player.statDefense += groupDefense;

                

                // 3. 套装文字抓取逻辑
                Player.setBonus = "";

                // Mod 套装检测
                for (int i = 0; i < 3; i++)
                {
                    Item curr = extraArmor[baseIdx + i];
                    if (!curr.IsAir && curr.ModItem != null)
                    {
                        if (curr.ModItem.IsArmorSet(head, body, legs))
                            curr.ModItem.UpdateArmorSet(Player);
                        curr.ModItem.UpdateEquip(Player);
                    }
                    else if (!curr.IsAir)
                    {
                        Player.GrantArmorBenefits(curr);
                    }
                }
            }   
            string modBonus = Player.setBonus;

            // 原版套装检测 (伪装 ID)
            int oldH = Player.head, oldB = Player.body, oldL = Player.legs;
            Item head1 = extraArmor[0];
            Item body1 = extraArmor[1];
            Item legs1 = extraArmor[2];
            Player.head = head1.headSlot;
            Player.body = body1.bodySlot;
            Player.legs = legs1.legSlot;
            Player.UpdateArmorSets(Player.whoAmI);
            string oriBonus = Player.setBonus;

            // 还原环境
            Player.head = oldH; Player.body = oldB; Player.legs = oldL;
            //for (int g = 0; g < config.ArmorGroupCount; g++)
            //{
            //    // 收集文字
            //    string finalGroupBonus = !string.IsNullOrEmpty(modBonus) ? modBonus : oriBonus;
            //    if (!string.IsNullOrEmpty(finalGroupBonus))
            //    {
            //        extraBonusLines.Add($"[栏位{g + 1}]: " + finalGroupBonus);
            //    }
            //}


            //// 4. 恢复主装备状态并合并文字
            Player.UpdateArmorSets(Player.whoAmI);
            //if (extraBonusLines.Count > 0)
            //{
            //    string combinedExtra = string.Join("\n", extraBonusLines);
            //    if (string.IsNullOrEmpty(originalBonus))
            //        Player.setBonus = combinedExtra;
            //    else
            //        Player.setBonus = originalBonus + "\n" + combinedExtra;
            //}
        }

        public override void SaveData(TagCompound tag) => tag["extraArmor"] = extraArmor.ToList();
        public override void LoadData(TagCompound tag)
        {
            if (tag.ContainsKey("extraArmor"))
            {
                var list = tag.Get<List<Item>>("extraArmor");
                for (int i = 0; i < Math.Min(list.Count, 9); i++) extraArmor[i] = list[i];
            }
        }
    }

    public class ExtraArmorSlot : UIElement
    {
        public int GroupIndex; // 组索引 0-2
        public int SlotType;   // 部位 0-2
        public ExtraArmorPlayer ModPlayer => Main.LocalPlayer.GetModPlayer<ExtraArmorPlayer>();

        public ExtraArmorSlot(int group, int type)
        {
            GroupIndex = group;
            SlotType = type;
            Width.Set(44, 0);
            Height.Set(44, 0);
        }

        public override void LeftClick(UIMouseEvent evt)
        {
            Item invItem = Main.mouseItem;
            int itemIdx = GroupIndex * 3 + SlotType;
            bool canPlace = invItem.IsAir ||
                           (SlotType == 0 && invItem.headSlot >= 0) ||
                           (SlotType == 1 && invItem.bodySlot >= 0) ||
                           (SlotType == 2 && invItem.legSlot >= 0);

            if (canPlace)
            {
                Utils.Swap(ref ModPlayer.extraArmor[itemIdx], ref Main.mouseItem);
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Grab);
            }
        }

        public override void RightClick(UIMouseEvent evt)
        {
            int itemIdx = GroupIndex * 3 + SlotType;
            if (!ModPlayer.extraArmor[itemIdx].IsAir && Main.mouseItem.IsAir)
            {
                Main.mouseItem = ModPlayer.extraArmor[itemIdx].Clone();
                ModPlayer.extraArmor[itemIdx].SetDefaults(0);
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Grab);
            }
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            // 如果组索引超过配置数量，不进行绘制
            if (GroupIndex >= ModContent.GetInstance<ArmorConfig>().ArmorGroupCount) return;

            CalculatedStyle dimensions = GetInnerDimensions();
            float oldScale = Main.inventoryScale;
            Main.inventoryScale = 0.85f;

            int itemIdx = GroupIndex * 3 + SlotType;
            ItemSlot.Draw(spriteBatch, ref ModPlayer.extraArmor[itemIdx], ItemSlot.Context.EquipArmor, dimensions.Position());

            if (IsMouseHovering)
            {
                Main.LocalPlayer.mouseInterface = true;
                Item item = ModPlayer.extraArmor[itemIdx];
                if (!item.IsAir)
                {
                    Main.HoverItem = item.Clone();
                    Main.hoverItemName = item.Name;
                }
            }
            Main.inventoryScale = oldScale;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            var config = ModContent.GetInstance<ArmorConfig>();

            // 动态更新坐标：每增加一组，向左偏移 50 像素
            float horizontalOffset = config.OffsetX - (GroupIndex * 50);
            Left.Set(horizontalOffset, 1f);
            Top.Set(config.OffsetY + (SlotType * 46), 0f);

            Recalculate();
        }
    }

    public class ExtraArmorUI : UIState
    {
        public override void OnInitialize()
        {
            // 初始化全部 9 个槽位（通过 Slot 内部的配置检查来决定显示）
            for (int g = 0; g < 3; g++)
            {
                for (int i = 0; i < 3; i++)
                {
                    Append(new ExtraArmorSlot(g, i));
                }
            }
        }
    }

    public class ArmorConfig : ModConfig
    {
        public override ConfigScope Mode => ConfigScope.ClientSide;

        [Header("UIPositionSettings")]
        [DefaultValue(-265)]
        [Range(-2000, 2000)]
        public int OffsetX;

        [DefaultValue(456)]
        [Range(0, 2000)]
        public int OffsetY;

        // 新增配置：控制开启的盔甲栏组数
        [DefaultValue(1)]
        [Range(1, 3)]
        [Slider]
        public int ArmorGroupCount;
    }

    public class ExtraArmorSystem : ModSystem
    {
        private UserInterface _interface;
        internal ExtraArmorUI _ui;
        public override void Load() { if (!Main.dedServ) { _interface = new UserInterface(); _ui = new ExtraArmorUI(); _ui.Activate(); _interface.SetState(_ui); } }
        public override void UpdateUI(GameTime gameTime) { if (Main.playerInventory) _interface?.Update(gameTime); }
        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int index = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Inventory"));
            if (index != -1)
            {
                layers.Insert(index, new LegacyGameInterfaceLayer("ExtraArmor: Slots", () =>
                {
                    if (Main.playerInventory) _interface.Draw(Main.spriteBatch, new GameTime());
                    return true;
                }, InterfaceScaleType.UI));
            }
        }
    }
}