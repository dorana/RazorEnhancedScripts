// this is a much older version of Lootmaster
// it is not recommended to use this version
//This is before the UI and many of it's mor advanced functions

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Text.RegularExpressions;
using RazorEnhanced;

namespace Razorscripts
{
    public class LootmasterOld
    {
        private readonly List<int> _gems = new List<int>();
        private readonly List<int> ignoreList = new List<int>();

        private Item _goldBag;
        private Item _gemBag;
        private Item _artifactsBag;
        private Item _specialRulesBag;
        private Item _quiver;
        private Dictionary<ItemProperty, int> _specialAttributeRules = new Dictionary<ItemProperty, int>();
        List<string> _specialNameRules = new List<string>();
        private bool _allSpecialPropertiesRequired;

        private bool _digDeep = false; //Leave off for now, setting to true will recursivly loot though all bags in the corpse, and thus will open all of them

        private Mobile _player;

        private readonly List<string> _lootLevels = new List<string>();
        private int _lootDelay = 300;
        Journal _journal = new Journal();

        public void Run()
        {
            Setup();

            while (true)
            {
                if (Misc.ReadSharedValue("LootmasterDirectContainer") is int directContainerSerial && directContainerSerial != 0)
                {
                    Misc.SendMessage($"Looting direct container {directContainerSerial}");
                    var directContainer = Items.FindBySerial(directContainerSerial);
                    if (directContainer != null)
                    {
                        var oldDigValue = _digDeep;
                        _digDeep = true;
                        LootCorpse(directContainer);
                        Misc.RemoveSharedValue("LootmasterDirectContainer");
                        Misc.Pause(500);
                        _digDeep = oldDigValue;
                        continue;
                    }
                }
                var corpses = Items.ApplyFilter(new Items.Filter
                {
                    IsCorpse = 1,
                    RangeMax = 2,
                    RangeMin = 0
                });

                foreach (var corpse in corpses)
                {
                    if (Target.HasTarget() || ignoreList.Contains(corpse.Serial))
                    {
                        continue;
                    }

                    var lastEntry = _journal.GetJournalEntry(null)?.FirstOrDefault();
                    LootCorpse(corpse);

                    var rows = _journal.GetJournalEntry(lastEntry);
                    if (rows == null) continue;
                    var filtered = rows.Where(r => r.Type == "System");
                    if (filtered.Any(r => r.Text == "You must wait to perform another action."))
                    {
                        _lootDelay += 10;
                        Misc.SendMessage($"Adjusting loot delay with 10ms, currently at {_lootDelay}");
                    }
                }

                Misc.Pause(50);
            }
        }

        private bool SetBag(LootContainer container, int itemSerial)
        {
            Item current;
            if (itemSerial == Player.Backpack.Serial)
                current = Player.Backpack;
            else
                current = FindBag(Player.Backpack, itemSerial);

            if (current == null)
            {
                Misc.SendMessage($"Unable to set bag for {container}");
                return false;
            }

            switch (container)
            {
                case LootContainer.Gold:
                    _goldBag = current;
                    break;
                case LootContainer.Gems:
                    _gemBag = current;
                    break;
                case LootContainer.Other:
                    _artifactsBag = current;
                    break;
                case LootContainer.Quiver:
                    _quiver = current;
                    break;
                case LootContainer.Special:
                    _specialRulesBag = current;
                    break;
            }

            return true;
        }

        private Item FindBag(Item bag, int target)
        {
            var found = bag.Contains.FirstOrDefault(c => c.Serial == target);
            return found ?? bag.Contains.Where(c => c.IsContainer).Select(sub => FindBag(sub, target)).FirstOrDefault();
        }

        private void LootCorpse(Item corpse)
        {
            Items.WaitForContents(corpse, 1000);
            Misc.Pause(_lootDelay);

            if (corpse.DistanceTo(_player) > 2)
            {
                return;
            }

            var sum = 1;
            while (sum != 0)
            {
                sum = LootGold(corpse);
                Misc.Pause(100);
            }

            sum = 1;
            while (sum != 0)
            {
                sum = LootGems(corpse);
                Misc.Pause(100);
            }
            sum = 1;
            while (sum != 0)
            {
                sum = LootObjects(corpse);
                Misc.Pause(100);
            }

            Misc.Pause(100);
            ignoreList.Add(corpse.Serial);
        }

        private int LootGems(Item container)
        {
            //Misc.SendMessage($"digging in {container.Serial}");
            if (_gemBag == null)
            {
                return 0;
            }

            var gems = container.Contains.Where(i => _gems.Contains(i.ItemID)).ToList();

            var sum = gems.Sum(i => i.Amount);
            foreach (var gem in gems)
            {
                MoveToBag(gem, _gemBag);
            }

            if (_digDeep)
            {
                var subContainers = container.Contains.Where(i => i.IsContainer).ToList();
                foreach (var sub in subContainers)
                {
                    Items.WaitForContents(sub, 1000);
                    Misc.Pause(_lootDelay);
                    sum += LootGems(sub);
                }
            }

            return sum;
        }

        private int LootGold(Item container)
        {
            if (_goldBag == null)
            {
                return 0;
            }

            var goldStacks = container.Contains.Where(i => i.ItemID == 3821).ToList();

            var sum = goldStacks.Sum(g => g.Amount);

            foreach (var goldStack in goldStacks)
            {
                MoveToBag(goldStack, _goldBag);
            }

            if (_digDeep)
            {
                var subContainers = container.Contains.Where(i => i.IsContainer).ToList();
                foreach (var sub in subContainers)
                {
                    Misc.Pause(_lootDelay);
                    Items.WaitForContents(sub, 1000);
                    Misc.Pause(_lootDelay);
                    sum += LootGold(sub);
                }
            }


            return sum;
        }

        private int LootObjects(Item container)
        {
            var sum = 0;
            foreach (var obj in container.Contains)
            {
                if (_quiver != null && (obj.ItemID == 3903 || obj.ItemID == 7163)) //Bolts and arrows
                {
                    MoveToBag(obj, _quiver);
                    sum++;
                    continue;
                }
                if (obj.ItemID == 3968) //Demon Bones
                {
                    MoveToBag(obj, _goldBag);
                    continue;
                }

                if (_specialRulesBag != null)
                {
                    var nameMatch = false;
                    foreach (var specialName in _specialNameRules)
                    {
                        if (obj.Name.ToLower().Contains(specialName.ToLower()))
                        {
                            MoveToBag(obj, _specialRulesBag);
                            nameMatch = true;
                            break;
                        }
                    }
                    
                    if (nameMatch)
                    {
                        continue;
                    }
                    
                    if (CheckSpecialProps(obj))
                    {
                        MoveToBag(obj, _specialRulesBag);
                        continue;
                    }
                }

                if (_artifactsBag != null)
                {
                    if (CheckProps(obj))
                    {
                        MoveToBag(obj, _artifactsBag);
                        sum++;
                    }
                }
            }

            if (_digDeep)
            {
                var subContainers = container.Contains.Where(i => i.IsContainer).ToList();
                foreach (var sub in subContainers)
                {
                    Items.WaitForContents(sub, 1000);
                    Misc.Pause(_lootDelay);
                    sum += LootObjects(sub);
                }
            }

            return sum;
        }

        private bool CheckSpecialProps(Item obj)
        {
            var checks = _specialAttributeRules.ToDictionary(k => k.Key, v => false);
            var re = new Regex(@"\d+");
            Items.WaitForProps(obj, 500);
            var take = false;

            foreach (var prop in obj.Properties)
            {
                var stringVal = prop.ToString().Replace("%","");
                var reMatch = re.Match(stringVal);
                var numIndex = reMatch.Success ? reMatch.Index : stringVal.Length;
                foreach (var rule in _specialAttributeRules)
                {
                    var propString = ResolvePropertyName(rule.Key);
                    if (propString.Equals(stringVal.Substring(0,numIndex).Trim(), StringComparison.InvariantCultureIgnoreCase))
                    {
                        var numstring = stringVal.Substring(numIndex);
                        int.TryParse(numstring, out var parseValue);
                        if (parseValue >= rule.Value || !reMatch.Success)
                        {
                            checks[rule.Key] = true;
                            break;
                        }
                    }
                }
            }

            if (_allSpecialPropertiesRequired)
            {
                checks.All(c => c.Value);
            }

            return checks.Any(c => c.Value);
        }

        private bool CheckProps(Item obj)
        {
            Items.WaitForProps(obj, 50);
            var take = false;
            if (obj.Weight == 50)
            {
                return false;
            }

            foreach (var prop in obj.Properties)
            {

                foreach (var str in _lootLevels)
                {
                    take = prop.Args.Contains(str);
                    if (take) break;
                }

                if (take) break;
            }

            return take;
        }

        private string ResolvePropertyName(ItemProperty prop)
        {
            switch (prop)
            {
                case ItemProperty.SpellDamageIncrease:
                    return "Spell Damage Increase";
                case ItemProperty.EnhancePotions:
                    return "Enhance Potions";
                case ItemProperty.EnhancePoisons:
                    return "Enhance Poisons";
                case ItemProperty.CastRecovery:
                    return "Cast Recovery";
                case ItemProperty.CastSpeed:
                    return "Cast Speed";
                case ItemProperty.LowerManaCost:
                    return "Lower Mana Cost";
                case ItemProperty.LowerReagentCost:
                    return "Lower Reagent Cost";
                case ItemProperty.EnhanceMagery:
                    return "Enhance Magery";
                case ItemProperty.EnhanceMeditation:
                    return "Enhance Meditation";
                case ItemProperty.NightSight:
                    return "Night Sight";
                case ItemProperty.ReactiveParalyze:
                    return "Reactive Paralyze";
                case ItemProperty.ReactiveFireball:
                    return "Reactive Fireball";
                case ItemProperty.ReactiveCurse:
                    return "Reactive Curse";
                case ItemProperty.ReactiveLightning:
                    return "Reactive Lightning";
                case ItemProperty.ReactiveManaDrain:
                    return "Reactive Mana Drain";
                case ItemProperty.LowerAttackChance:
                    return "Lower Attack Chance";
                case ItemProperty.LowerDefendChance:
                    return "Lower Defend Chance";
                case ItemProperty.ReflectPhysicalDamage:
                    return "Reflect Physical Damage";
                case ItemProperty.EnhanceDamage:
                    return "Enhance Damage";
                case ItemProperty.EnhanceDefense:
                    return "Enhance Defense";
                case ItemProperty.BonusStr:
                    return "Bonus Str";
                case ItemProperty.BonusDex:
                    return "Bonus Dex";
                case ItemProperty.BonusInt:
                    return "Bonus Int";
                case ItemProperty.BonusHits:
                    return "Bonus Hits";
                case ItemProperty.BonusStam:
                    return "Bonus Stam";
                case ItemProperty.BonusMana:
                    return "Bonus Mana";
                case ItemProperty.SpellChanneling:
                    return "Spell Channeling";
                case ItemProperty.DamageIncrease:
                    return "Damage Increase";
                case ItemProperty.Luck:
                    return "Luck";
                case ItemProperty.SwingSpeedIncrease:
                    return "Swing Speed Increase";
                case ItemProperty.HitChanceIncrease:
                    return "Hit Chance Increase";
                case ItemProperty.DefenseChanceIncrease:
                    return "Defense Chance Increase";
            }

            return null;
        }

        private void Setup()
            {
                var tar = new Target();
            
                var target = tar.PromptTarget("Pick loot bag for Gold");
                SetBag(LootContainer.Gold, target);
                target = tar.PromptTarget("Pick loot bag for Gems");
                SetBag(LootContainer.Gems, target);
                target = tar.PromptTarget("Pick loot bag for Artifacts");
                SetBag(LootContainer.Other, target);
                target = tar.PromptTarget("Pick loot bag for Special Rules Items");
                SetBag(LootContainer.Special, target);
                if (Player.GetSkillValue("Archery") > 30)
                {
                    target = tar.PromptTarget("Pick Quiver or arrow bag");
                    SetBag(LootContainer.Quiver, target);
                }
                
                Misc.RemoveSharedValue("LootmasterDirectContainer");
                _player = Mobiles.FindBySerial(Player.Serial);
                
                var gems = Enum.GetValues(typeof(Gem)).Cast<Gem>().ToList();

                foreach (var gem in gems) _gems.Add((int)gem);

                _lootLevels.AddRange(new[]
                {
                    "Artifact",
                    "Major Magic Item"
                });
                
                SetSpecialRules();
                SetUpSpecialNameRules();
            }

            private void SetSpecialRules()
            {
                _allSpecialPropertiesRequired = false; //This indicates if all of the special rules needs to be on the item, if false, it will work as ANY of the rules
                _specialAttributeRules.Clear();
                //Add special rules by adding a line such as
                //_specialAttributeRules.Add(ItemProperty.SpellDamageIncrease, 18); where SpellDamageIncrease is based on the ItemProperty seen below
                //and the number is the minumum value you are looking for
                _specialAttributeRules.Add(ItemProperty.ReactiveParalyze, 10);
            }
            
            private void SetUpSpecialNameRules()
            {
                _specialNameRules.Clear();
                //Add special rules by adding a line such as
                //_specialNameRules.Add("Name of Item"); where Name of Item is the name of the item you are looking for
                _specialNameRules.Add("Raptor Teeth");
            }


            private void MoveToBag(Item item, Item destinationBag)
            {
                Items.Move(item, destinationBag, item.Amount);
                Misc.Pause(_lootDelay);
            }

        private enum LootContainer
        {
            Gold = 0,
            Gems = 1,
            Other = 2,
            Quiver = 3,
            Special = 4
        }

        private enum Gem
        {
            StarSapphire = 3855,
            Ruby = 3859,
            Emerald = 3856,
            Sapphire = 3857,
            Citrine = 3861,
            Amethyst = 3862,
            Tourmaline = 3864,
            Amber = 3877,
            Diamond = 3878
        }

        private enum ItemProperty
        {
            SpellDamageIncrease = 1,
            EnhancePotions = 2,
            EnhancePoisons = 3,
            CastRecovery = 4,
            CastSpeed = 5,
            LowerManaCost = 6,
            LowerReagentCost = 7,
            EnhanceMagery = 8,
            EnhanceMeditation = 9,
            NightSight = 10,
            ReactiveParalyze = 11,
            ReactiveFireball = 12,
            ReactiveCurse = 13,
            ReactiveLightning = 14,
            ReactiveManaDrain = 15,
            LowerAttackChance = 16,
            LowerDefendChance = 17,
            ReflectPhysicalDamage = 18,
            EnhanceDamage = 19,
            EnhanceDefense = 20,
            BonusStr = 21,
            BonusDex = 22,
            BonusInt = 23,
            BonusHits = 24,
            BonusStam = 25,
            BonusMana = 26,
            //WeaponDamage = 27,
            //WeaponSpeed = 28,
            SpellChanneling = 29,
            DamageIncrease = 30,
            Luck = 31,
            SwingSpeedIncrease = 32,
            HitChanceIncrease = 33,
            DefenseChanceIncrease = 34,
        }
    }
}