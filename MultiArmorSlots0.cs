
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
        // 对应原 Mod 的 armor[3] 数组
        public Item[] extraArmor = new Item[3];

        private bool vortexStealthActive;

        private int shadowDodgeTime;

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

        internal ref StatModifier allKB => ref base.Player.GetKnockback(DamageClass.Generic);

        internal ref float minionKB => ref base.Player.GetKnockback(DamageClass.Summon).Base;

        internal ref float meleeSpeed => ref base.Player.GetAttackSpeed(DamageClass.Melee);

        internal float inverseMeleeSpeed => 1f / base.Player.GetTotalAttackSpeed(DamageClass.Melee);

        internal ref float summonerWeaponSpeedBonus => ref base.Player.GetAttackSpeed(DamageClass.SummonMeleeSpeed);
        public int extraDefense;

        public override void Initialize()
        {
            for (int i = 0; i < 3; i++)
            {
                extraArmor[i] = new Item();
                extraArmor[i].SetDefaults(0);
            }
        }

        // 相当于原 Mod 的 UpdateAccessory 逻辑，但在 Player 层面每帧执行
        public override void PostUpdateEquips()
        {
            // 1. 计算防御力 (参考原 Mod 的 DefenseMultiplier 逻辑)
            // 这里假设倍率为 1.0f，你可以根据需要调整
            int totalDefense = 0;
            foreach (var item in extraArmor)
            {
                if (!item.IsAir) totalDefense += item.defense;
            }
            Player.statDefense += totalDefense;

            // 如果没穿装备，直接跳过后续套装逻辑
            if (extraArmor.All(i => i.IsAir)) return;

            // 2. 准备检测套装
            Item head = extraArmor[0];
            Item body = extraArmor[1];
            Item legs = extraArmor[2];

            // 备份当前玩家的 setBonus，防止覆盖主装备效果
            string originalBonus = Player.setBonus;
            Player.setBonus = ""; // 暂时清空以获取额外栏位的奖励文本

            ArmorBagPlayer aPlayer = Player.GetModPlayer<ArmorBagPlayer>();
            aPlayer.ArmorBags.Add(new ArmorBagPlayer.ArmorBagInfo
             {
                 HeadSlot = ((extraArmor[0] == null) ? (-1) : extraArmor[0].headSlot),
                 BodySlot = ((extraArmor[1] == null) ? (-1) : extraArmor[1].bodySlot),
                 LegSlot = ((extraArmor[2] == null) ? (-1) : extraArmor[2].legSlot)
             });
            aPlayer.PostUpdateEquips();



            

            // 3. 手动触发 Mod 套装逻辑 (这是原 Mod 成功的关键)
            // 遍历三件装备，只要有一件满足 IsArmorSet 条件，就激活 UpdateArmorSet
            for (int i = 0; i < 3; i++)
            {
                if (!extraArmor[i].IsAir && extraArmor[i].ModItem != null)
                {
                    // 调用 ModItem 的 IsArmorSet 钩子
                    if (extraArmor[i].ModItem.IsArmorSet(head, body, legs))
                    {
                        // 激活套装效果 (每帧调用)
                        extraArmor[i].ModItem.UpdateArmorSet(Player);
                    }

                    // 如果不是“仅套装奖励”，则应用单件装备的 UpdateEquip
                    extraArmor[i].ModItem.UpdateEquip(Player);
                }
                else if (!extraArmor[i].IsAir)
                {
                    // 处理原版装备的单件属性叠加
                    
                    Player.GrantArmorBenefits(extraArmor[i]);
                }
            }
            string extraBonusTextmod = Player.setBonus;
            // 4. 处理原版套装 (如木甲、神圣甲等)
            // 由于原版套装检测依赖 Player.head/body/legs 的 ID，我们需要临时伪装
            int oldH = Player.head, oldB = Player.body, oldL = Player.legs;
            Player.head = head.headSlot;
            Player.body = body.bodySlot;
            Player.legs = legs.legSlot;

            // 强制调用原版检测函数
            Player.UpdateArmorSets(Player.whoAmI);

            // 记录额外栏位生成的奖励文字
            string extraBonusTextori = Player.setBonus;

            // 5. 环境还原与文字合并
            Player.head = oldH; Player.body = oldB; Player.legs = oldL;

            // 重新运行一次主装备检测，确保主槽位 Boolean (如 setSolar) 状态正确
            Player.UpdateArmorSets(Player.whoAmI);

            // 合并文字：让玩家在 UI 上能看到奖励
            if (!string.IsNullOrEmpty(originalBonus))
            {
                if (string.IsNullOrEmpty(extraBonusTextmod)&& string.IsNullOrEmpty(extraBonusTextori))
                {
                    Player.setBonus = originalBonus;
                }
                else if (!string.IsNullOrEmpty(extraBonusTextmod))
                {
                    Player.setBonus = originalBonus + "\n[额外栏位]: " + extraBonusTextmod;
                }
                else if (!string.IsNullOrEmpty(extraBonusTextori))
                {
                    Player.setBonus = originalBonus + "\n[额外栏位]: " + extraBonusTextori; ;
                }
                else { }
            }
        }

        public override void SaveData(TagCompound tag) => tag["extraArmor"] = extraArmor.ToList();
        public override void LoadData(TagCompound tag)
        {
            if (tag.ContainsKey("extraArmor"))
            {
                var list = tag.Get<List<Item>>("extraArmor");
                for (int i = 0; i < 3; i++) extraArmor[i] = list[i];
            }
        }

        
    }

    // --- UI 部分保持不变，确保物品能正确放入 ---
        public class ExtraArmorSlot : UIElement
    {
        public int SlotType; // 0:头, 1:身, 2:腿
        public ExtraArmorPlayer ModPlayer => Main.LocalPlayer.GetModPlayer<ExtraArmorPlayer>();


        public ExtraArmorSlot(int type)
        {
            SlotType = type;
            Width.Set(44, 0);
            Height.Set(44, 0);
        }

        public override void LeftClick(UIMouseEvent evt)
        {
            Item invItem = Main.mouseItem;
            // 严谨的部位检查
            bool canPlace = invItem.IsAir ||
                           (SlotType == 0 && invItem.headSlot >= 0) ||
                           (SlotType == 1 && invItem.bodySlot >= 0) ||
                           (SlotType == 2 && invItem.legSlot >= 0);

            if (canPlace)
            {
                Utils.Swap(ref ModPlayer.extraArmor[SlotType], ref Main.mouseItem);
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Grab);
            }
        }

        public override void RightClick(UIMouseEvent evt)
        {
            if (!ModPlayer.extraArmor[SlotType].IsAir && Main.mouseItem.IsAir)
            {
                Main.mouseItem = ModPlayer.extraArmor[SlotType].Clone();
                ModPlayer.extraArmor[SlotType].SetDefaults(0);
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Grab);
            }
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            CalculatedStyle dimensions = GetInnerDimensions();
            float oldScale = Main.inventoryScale;
            Main.inventoryScale = 0.85f;

            // 使用原版的盔甲槽背景绘制
            ItemSlot.Draw(spriteBatch, ref ModPlayer.extraArmor[SlotType], ItemSlot.Context.EquipArmor, dimensions.Position());

            if (IsMouseHovering)
            {
                Main.LocalPlayer.mouseInterface = true;
                Item item = ModPlayer.extraArmor[SlotType];
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

            // 从配置类获取当前设置的值
            var config = ModContent.GetInstance<ArmorConfig>();

            // 动态更新坐标
            // 1f 代表从屏幕右侧向左偏移，0f 代表从顶部向下偏移
            Left.Set(config.OffsetX, 1f);
            Top.Set(config.OffsetY + (SlotType * 46), 0f);

            // 必须调用 Recalculate() 确保 UI 更改生效
            Recalculate();
        }
    }

    // UI 系统的注册 (保持不变，已微调坐标)
    public class ExtraArmorUI : UIState
    {
        public override void OnInitialize()
        {
            for (int i = 0; i < 3; i++)
            {
                // 现在的逻辑移到了 Slot 的 Update 里
                var slot = new ExtraArmorSlot(i);
                Append(slot);
            }
        }
    }

    public class ArmorConfig : ModConfig
    {
        // 这样配置会保存在客户端本地
        public override ConfigScope Mode => ConfigScope.ClientSide;

        [Header("UIPositionSettings")]

        [DefaultValue(-265)]
        [Range(-2000, 2000)]
        public int OffsetX;

        [DefaultValue(456)]
        [Range(0, 2000)]
        public int OffsetY;
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