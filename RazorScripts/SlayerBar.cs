using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RazorEnhanced;

namespace RazorScripts
{
    public class SlayerBar
    {
        private Item _SlayerBag { get; set; }
        private readonly List<SlayerItem> _slayerItems = new List<SlayerItem>();
        private BaseSkill _skill;
        private int _nonSlayerSerial = -1;

        private Func<Item, bool> _searchFilter;
        private readonly List<SlayerType> _slayerList = new List<SlayerType>
        {
            SlayerType.None,
            SlayerType.Repond,
            SlayerType.Undead,
            SlayerType.Reptile,
            SlayerType.Dragon,
            SlayerType.Arachnid,
            SlayerType.Elemental,
            SlayerType.AirElemental,
            SlayerType.FireElemental,
            SlayerType.WaterElemental,
            SlayerType.EarthElemental,
            SlayerType.BloodElemental,
            SlayerType.Demon,
        };
        
        private TextInfo _tinfo;

        public void Run()
        {
            _tinfo = CultureInfo.CurrentCulture.TextInfo;
            _skill = TryFindSkill();
            
            if (_skill == BaseSkill.Magery)
            {
                _searchFilter = i => IsSpellBook(i)
                                      && (i.Properties.Any(p => p.ToString().ToLower().Contains("slayer"))
                                      || i.Properties.Any(p => p.ToString().ToLower().Contains("silver")));
            }
            else
            {
                _searchFilter = i => (i.Properties.Any(p => p.ToString().ToLower().Contains("slayer"))
                                      || i.Properties.Any(p => p.ToString().ToLower().Contains("silver")))
                                      && i.Properties.Any(p => p.ToString().ToLower().Contains(_tinfo.ToTitleCase(_skill.ToString()).ToLower()));
            }
            
            
            if (TryFindSlayerBag(Player.Backpack))
            {
                SetSlayers(_SlayerBag);
            }
            else
            {
                Misc.SendMessage("Unable to find Slayer weapon container, please open the container and try again");
                return;
            }
            
            UpdateBar();
            
            while (true)
            {
                var bar = Gumps.GetGumpData(788435749);
                if (bar.buttonid != -1 && bar.buttonid != 0)
                {
                    UpdateBar(); 
                    
                    EquipSlayer(_slayerItems.FirstOrDefault(i => i.Slayer == (SlayerType)bar.buttonid));
                    
                    UpdateBar(); 
                }
                    
                

                Misc.Pause(100);
            }
        }
        
        private void EquipSlayer(SlayerItem book)
        {
            var held = GetEquippedWeapon();
            var resolved = _slayerItems.FirstOrDefault(b => b.Serial == held?.Serial);
            if (_SlayerBag == null)
            {
                Misc.SendMessage("Unable to find book binder");
            }
            
            if (held != null && resolved != null)
            {
                if (resolved.Serial == book.Serial)
                {
                    return;
                }
                
                var gameEquipped = Items.FindBySerial(resolved.Serial);
                var index = _slayerList.IndexOf(resolved.Slayer);
                var offset = index > 5 ? 6 : 0;
                Items.Move(gameEquipped, _SlayerBag, 1,(index-offset)*20+45, offset == 0 ? 95 : 125);
                Misc.Pause(600);
            }
            else
            {
                if (held != null)
                {
                    Items.Move(held, Player.Backpack.Serial,held.Amount);
                    Misc.Pause(600);
                }
            }
            
            var gameTarget = Items.FindBySerial(book.Serial);
            Player.EquipItem(gameTarget);
            Misc.Pause(200);
        }
        
        private int GetActiveBookIndex()
        {
            var held = GetEquippedWeapon();
            var booksSorted = _slayerItems.OrderBy(b => _slayerList.IndexOf(b.Slayer)).ToList();
            var resolved = _slayerItems.FirstOrDefault(b => b.Serial == held?.Serial);
            if (resolved != null)
            {
                return booksSorted.IndexOf(resolved);
            }

            return -1;
        }
        
        private void UpdateBar()
        {
            var activeBookIndex = GetActiveBookIndex();
            var bar = Gumps.CreateGump();
            bar.buttonid = -1;
            bar.gumpId = 788435749;
            bar.serial = (uint)Player.Serial;
            bar.x = 500;
            bar.y = 500;
            Gumps.AddBackground(ref bar, 0, 0, (_slayerItems.Count*60-5), 55, 1755);
            var slayersSorted = _slayerItems.OrderBy(b => _slayerList.IndexOf(b.Slayer)).ToList();
            foreach (var slayerItem in slayersSorted)
            {
                var index = slayersSorted.IndexOf(slayerItem);
                var x = index * 60 + 5;
                var y = 5;
                Gumps.AddButton(ref bar, x,y,(int)slayerItem.Slayer,(int)slayerItem.Slayer,(int)slayerItem.Slayer,1,0);
                Gumps.AddTooltip(ref bar, slayerItem.Name);
            }

            if (activeBookIndex != -1)
            {
                Gumps.AddImage(ref bar, (60 * activeBookIndex-17),0,30071);
            }
            
            Gumps.CloseGump(788435749);
            Gumps.SendGump(bar, 500,500);
        }

        private BaseSkill TryFindSkill()
        {
            var dict = new Dictionary<BaseSkill, double>();
            foreach (var baseSkill in Enum.GetValues(typeof(BaseSkill)).Cast<BaseSkill>())
            {
                var skillString = _tinfo.ToTitleCase(baseSkill.ToString());
                var value = Player.GetSkillValue(skillString);
                dict.Add(baseSkill, value);
            }

            //Return key of highest value
            return dict.Aggregate((l, r) => l.Value > r.Value ? l : r).Key;
        }

        private Item GetEquippedWeapon()
        {
            var item = Player.GetItemOnLayer("RightHand");
            if (item != null)
            {
                return item;
            }
            item = Player.GetItemOnLayer("LeftHand");
            if (item != null)
            {
                if (item.Properties.Any(p => p.ToString().ToLower().Contains("two-handed")))
                {
                    return item;
                }
            }

            return null;
        }

        private bool TryFindSlayerBag(Item container)
        {
            if (_SlayerBag != null)
            {
                return true;
            }

            var slayersFound = 0;
            var held = GetEquippedWeapon();
            if (held != null)
            {
                slayersFound++;
            }



            List<Item> potentials = new List<Item>();
            foreach (var item in container.Contains)
            {
                Items.WaitForProps(item, 1000);
                
                if (_searchFilter(item))
                {
                    potentials.Add(item);
                }
                
            }
            
            slayersFound += potentials.Count;

            if (slayersFound > 1)
            {
                _SlayerBag = container;
                return true;
            }

            foreach (var subContainer in container.Contains.Where(c => c.IsContainer && !c.IsBagOfSending))
            {
                var found = TryFindSlayerBag(subContainer);
                if (found)
                {
                    return true;
                }
            }

            return false;
        }
        
        private bool IsSpellBook(Item item)
        {
            return item.Name.ToLower().Contains("spellbook") || item.Name.Equals("Scrapper's Compendium", StringComparison.InvariantCultureIgnoreCase);
        }

        private void SetSlayers(Item container)
        {
            _slayerItems.Clear();
            var compoundList = container.Contains.ToList();
            var held = GetEquippedWeapon();
            if (held != null)
            {
                compoundList.Add(held);
            }
            
            if (_skill == BaseSkill.Magery)
            {
                if (_nonSlayerSerial != -1)
                {
                    var nonSlayer = Items.FindBySerial(_nonSlayerSerial);
                    if (nonSlayer != null)
                    {
                        _slayerItems.Add( new SlayerItem
                        {
                            Name = _tinfo.ToTitleCase(nonSlayer.Name),
                            Serial = nonSlayer.Serial,
                            Slayer = SlayerType.None
                        });
                    }
                }
                else
                {
                    var nonSlayer = compoundList.FirstOrDefault(i => i.Name.Equals("Scrapper's Compendium", StringComparison.InvariantCultureIgnoreCase));
                    if (nonSlayer != null)
                    {
                        _slayerItems.Add( new SlayerItem
                        {
                            Name = _tinfo.ToTitleCase(nonSlayer.Name),
                            Serial = nonSlayer.Serial,
                            Slayer = SlayerType.None
                        });
                    }
                }
            }
            else
            {
                if (_nonSlayerSerial != -1)
                {
                    var nonSlayer = Items.FindBySerial(_nonSlayerSerial);
                    if (nonSlayer != null)
                    {
                        _slayerItems.Add( new SlayerItem
                        {
                            Name = _tinfo.ToTitleCase(nonSlayer.Name),
                            Serial = nonSlayer.Serial,
                            Slayer = SlayerType.None
                        });
                    }
                }
            }
            
            foreach (var item in compoundList.Where(_searchFilter))
            {
                var prop = item.Properties.FirstOrDefault(p => p.ToString().Contains("slayer")) ?? item.Properties.FirstOrDefault(p => p.Number == 1071451);
                var slayerString = prop.ToString().ToLower();
                if (slayerString == "silver")
                {
                    slayerString = "undead";
                }
                
                _slayerItems.Add( new SlayerItem
                {
                    Name = _tinfo.ToTitleCase(prop.ToString()),
                    Serial = item.Serial,
                    Slayer = _slayerList.Any(s => slayerString.Contains(s.ToString().ToLower())) ? _slayerList.First(s => slayerString.Contains(s.ToString().ToLower())) : SlayerType.UnKnown
                });
                
            }
        }

        private enum BaseSkill
        {
            Swordsmanship = 0,
            MaceFighting = 1,
            Fencing = 2,
            Archery = 3,
            Throwing = 4,
            Magery = 5,
        }
        
        public class SlayerItem
        {
            public int Serial { get; set; }
            public string Name { get; set; }
            public SlayerType Slayer { get; set; }
        }
        
        public enum SlayerType
        {
            None = 20744,
            Repond = 2277,
            Undead = 20486,
            Reptile = 21282,
            Arachnid = 20994,
            Elemental = 24014,
            AirElemental = 2299,
            FireElemental = 2302,
            WaterElemental = 2303,
            EarthElemental = 2301,
            BloodElemental = 20993,
            Demon = 2300,
            Dragon = 21010,
            UnKnown = 24015
        }
    }
}