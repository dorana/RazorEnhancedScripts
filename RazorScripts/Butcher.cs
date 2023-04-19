using System;
using System.Collections.Generic;
using RazorEnhanced;
using System.Linq;

namespace Razorscripts
{
    public class Butcher
    {
        private readonly bool TakeLeather = true; //should we keep leather
        private readonly bool TakeMeat = true; //should we keep meats
        private readonly bool TakeFeathers = false; //should we keep feathers
        private readonly bool TakeWool = false; //should we keep feathers

        private int _leather = Convert.ToInt32("0x1081", 16);
        private int _hide = Convert.ToInt32("0x1079", 16);
        private int _meat = Convert.ToInt32("0x09F1", 16);
        private int _rotwormMeat = Convert.ToInt32("0x2DB9", 16);
        private int _pultry = Convert.ToInt32("0x09B9", 16);
        private int _feathers = Convert.ToInt32("0x1BD1", 16);
        private int _wool = Convert.ToInt32("0x0DF8", 16);
        private int _lambLeg = Convert.ToInt32("0x1609", 16);

        private readonly List<int> _daggers =
            new[]
            {
                Convert.ToInt32("0x2D20", 16), //Harvesters Blade
                Convert.ToInt32("0x0F52", 16), //Dagger
                Convert.ToInt32("0x0EC4", 16), //Skinning Knife
                Convert.ToInt32("0x0EC3", 16), //Butchers Cleaver
                Convert.ToInt32("0x13F6", 16), //Butchers Knife
                Convert.ToInt32("0x13B6", 16), //Butchers Knife
            }.ToList();

        public void Run()
        {
            Item dagger = null;

            foreach (var dagId in _daggers)
            {
                dagger = DigDeep(Player.Backpack, dagId);
                if (dagger != null)
                {
                    break;
                }
            }
            

            if (dagger == null || dagger.ItemID != _daggers.First()) //we didn't find dagger in pack, or the dagger was not Harvester
            {
                var rHand = Player.GetItemOnLayer("RightHand");
                var lHand = Player.GetItemOnLayer("LeftHand");
                
                foreach (var dag in _daggers)
                {
                    if (rHand?.ItemID == dag)
                    {
                        dagger = rHand;
                        break;
                    }

                    if (lHand?.ItemID == dag)
                    {
                        dagger = lHand;
                        break;
                    }
                }
            }
            
            if (dagger == null)
            {
                Misc.SendMessage("Unable to locate preset dagger", 201);
                return;
            }

            var isHarvestersBlade = dagger.Name == "Harvester's Blade";

            var corpses = Items.ApplyFilter(new Items.Filter
            {
                IsCorpse = 1,
                RangeMax = 2,
                RangeMin = 0
            });

            if (corpses == null || !corpses.Any())
            {
                return;
            }

            foreach (var corpse in corpses)
            {
                Items.UseItem(dagger);
                Target.WaitForTarget(2000);
                Target.TargetExecute(corpse);
                Misc.Pause(500);

                if (isHarvestersBlade) continue;
                if (TakeFeathers)
                {
                    LootItems(corpse, _feathers);
                }

                if (TakeMeat)
                {
                    LootItems(corpse, _meat);
                    LootItems(corpse, _rotwormMeat);
                    LootItems(corpse, _pultry);
                    LootItems(corpse, _lambLeg);
                }

                if (TakeLeather)
                {
                    LootItems(corpse, _hide);
                }

                if (TakeWool)
                {
                    LootItems(corpse, _wool);
                }
            }

            if (isHarvestersBlade)
            {
                var dumperCorpse = corpses.First();

                if (!TakeFeathers)
                {
                    DumpItem(dumperCorpse, _feathers);
                    Misc.Pause(200);
                }

                if (!TakeLeather)
                {
                    DumpItem(dumperCorpse, _leather);
                    Misc.Pause(200);
                }

                if (!TakeMeat)
                {
                    DumpItem(dumperCorpse, _meat);
                    Misc.Pause(200);
                    DumpItem(dumperCorpse, _pultry);
                    Misc.Pause(200);
                    DumpItem(dumperCorpse, _lambLeg);
                    Misc.Pause(200);
                    DumpItem(dumperCorpse, _rotwormMeat);
                    Misc.Pause(200);
                }

                if (!TakeWool)
                {
                    DumpItem(dumperCorpse, _wool);
                    Misc.Pause(200);
                }
            }
        }

        private Item DigDeep(Item container, int itemId)
        {
            var found = container.Contains.FirstOrDefault(i => i.ItemID == itemId);
            if (found != null)
            {
                return found;
            }
            
            var subContainers = container.Contains.Where(c => c.IsContainer && c.Contains.Any() && c.Contains.First().Name != " (0000)").ToList();
            foreach (var subcont in subContainers)
            {
                return DigDeep(subcont, itemId);
            }

            return null;
        }

        private void LootItems(Item corpse, int itemId)
        {
            var stack = corpse.Contains.Where(i => i.ItemID == itemId).ToList();
            foreach (var feather in stack)
            {
                Items.Move(feather, Player.Backpack.Serial, feather?.Amount ?? int.MaxValue);
                Misc.Pause(100);
            }
        }

        private void DumpItem(Item corpse, int itemId)
        {
            var dumpThese = Player.Backpack.Contains.Where(i => i.ItemID == itemId).ToList();
            foreach (var dump in dumpThese)
            {
                Items.Move(dump, corpse, dump?.Amount ?? int.MaxValue);
            }
        }
    }
}
