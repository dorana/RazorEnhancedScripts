using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using RazorEnhanced;

namespace RazorScripts
{
    public class WeaponMaster
    {
        private Item _weaponContainer { get; set; }
        private readonly List<Weapon> _slayerWeapons = new List<Weapon>();
        private readonly List<Weapon> _areaWeapons = new List<Weapon>();
        private readonly List<Weapon> _extras = new List<Weapon>();
        private BaseSkill _skill;
        private static string _version = "1.0.1";

        private Func<Item, bool> _slayerSearchFilter;
        private Func<Item, bool> _areaSearchFilter;
        private readonly List<WeaponIcon> _slayerList = new List<WeaponIcon>
        {
            WeaponIcon.None,
            WeaponIcon.RepondSlayer,
            WeaponIcon.UndeadSlayer,
            WeaponIcon.ReptileSlayer,
            WeaponIcon.DragonSlayer,
            WeaponIcon.ArachnidSlayer,
            WeaponIcon.ElementalSlayer,
            WeaponIcon.AirElementalSlayer,
            WeaponIcon.FireElementalSlayer,
            WeaponIcon.WaterElementalSlayer,
            WeaponIcon.EarthElementalSlayer,
            WeaponIcon.BloodElementalSlayer,
            WeaponIcon.DemonSlayer,
            WeaponIcon.FeySlayer,
        };
        
        private TextInfo _tinfo;

        public void Run()
        {
            _tinfo = CultureInfo.CurrentCulture.TextInfo;
            _skill = TryFindSkill();
            
            if (_skill == BaseSkill.Magery)
            {
                _slayerSearchFilter = i => IsSpellBook(i)
                                      && (i.Properties.Any(p => p.ToString().ToLower().Contains("slayer"))
                                      || i.Properties.Any(p => p.ToString().ToLower().Contains("silver")));
            }
            else
            {
                _slayerSearchFilter = i => (i.Properties.Any(p => p.ToString().ToLower().Contains("slayer"))
                                      || i.Properties.Any(p => p.ToString().ToLower().Contains("silver")))
                                      && i.Properties.Any(p => p.ToString().ToLower().Contains(_tinfo.ToTitleCase(_skill.ToString()).ToLower()));
            }
            
            
            if (TryFindWeaponBag(Player.Backpack))
            {
                SetWeapons(_weaponContainer);
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
                    
                    EquipSlayer(_slayerWeapons.FirstOrDefault(i => i.Slayer == (WeaponIcon)bar.buttonid));
                    
                    UpdateBar(); 
                }
                    
                

                Misc.Pause(100);
            }
        }
        
        private void EquipSlayer(Weapon book)
        {
            var held = GetEquippedWeapon();
            var resolved = _slayerWeapons.FirstOrDefault(b => b.Serial == held?.Serial);
            if (_weaponContainer == null)
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
                Items.Move(gameEquipped, _weaponContainer, 1,(index-offset)*20+45, offset == 0 ? 95 : 125);
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
            var booksSorted = _slayerWeapons.OrderBy(b => _slayerList.IndexOf(b.Slayer)).ToList();
            var resolved = _slayerWeapons.FirstOrDefault(b => b.Serial == held?.Serial);
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
            Gumps.AddBackground(ref bar, 0, 0, (_slayerWeapons.Count*60-5), 55, 1755);
            var slayersSorted = _slayerWeapons.OrderBy(b => _slayerList.IndexOf(b.Slayer)).ToList();
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

        private bool TryFindWeaponBag(Item container)
        {
            if (_weaponContainer != null)
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
                
                if (_slayerSearchFilter(item))
                {
                    potentials.Add(item);
                }
                
            }
            
            slayersFound += potentials.Count;

            if (slayersFound > 1)
            {
                _weaponContainer = container;
                return true;
            }

            foreach (var subContainer in container.Contains.Where(c => c.IsContainer && !c.IsBagOfSending))
            {
                var found = TryFindWeaponBag(subContainer);
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

        private void SetWeapons(Item container)
        {
            _slayerWeapons.Clear();
            var compoundList = container.Contains.ToList();
            var held = GetEquippedWeapon();
            if (held != null)
            {
                compoundList.Add(held);
            }
            
            foreach (var extra in _extras)
            {
                var exAdd = Items.FindBySerial(extra.Serial);
                if (exAdd != null)
                {
                    _slayerWeapons.Add( new Weapon
                    {
                        Name = _tinfo.ToTitleCase(exAdd.Name),
                        Serial = exAdd.Serial,
                        Slayer = WeaponIcon.None
                    });
                }
            }
            
            foreach (var item in compoundList.Where(_slayerSearchFilter))
            {
                var prop = item.Properties.FirstOrDefault(p => p.ToString().Contains("slayer")) ?? item.Properties.FirstOrDefault(p => p.Number == 1071451);
                var slayerString = prop.ToString().ToLower();
                if (slayerString == "silver")
                {
                    slayerString = "undead slayer";
                }
                
                
                
                _slayerWeapons.Add( new Weapon
                {
                    Name = _tinfo.ToTitleCase(prop.ToString()),
                    Serial = item.Serial,
                    Slayer = _slayerList.Any(s => _tinfo.ToTitleCase(slayerString).Equals(SplitCamelCase(s.ToString()))) ? _slayerList.First(s => _tinfo.ToTitleCase(slayerString).Equals(SplitCamelCase(s.ToString()))) : WeaponIcon.UnKnown
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
        
        public class Weapon
        {
            public int Serial { get; set; }
            public string Name { get; set; }
            public WeaponIcon Slayer { get; set; }
        }
        
        
        public enum WeaponIcon
        {
            None = 20744,
            RepondSlayer = 2277,
            UndeadSlayer = 20486,
            ReptileSlayer = 21282,
            ArachnidSlayer = 20994,
            ElementalSlayer = 24014,
            AirElementalSlayer = 2299,
            FireElementalSlayer = 2302,
            WaterElementalSlayer = 2303,
            EarthElementalSlayer = 2301,
            BloodElementalSlayer = 20993,
            DemonSlayer = 2300,
            DragonSlayer = 21010,
            FeySlayer = 23006,
            UnKnown = 24015,
            
            //Hit Area
            HitAreaFire = 2267,
            HitAreaLightning = 2288,
            HitAreaEnergy = 2289,
            HitAreaCold = 23010,
            HitAreaPoison = 2285,
            
            //Selectable
            SwordFlame = 21015,
            SwordSerpent = 21019,
            SwordQuick = 21004,
            SwordHeart = 21000,
            SwordSpin = 21006,
            SwordDouble = 20998,
            SwordDrop = 20996,
            SwordPierce = 20992,
            Sword5Point = 20741,
            SwordMagic = 20483,
            PoisonStrike = 20489,
            LighteningArrow = 21017,
            BowQuick = 21001,
            Crossbow = 21013,
            
            Shuriken = 21021,
            JumpKill = 21293,
            
            BrokenSword = 2305,
            BrokenShield = 2304,
            BloodSkull = 2237,
            
            Swirl = 2297,
            FlameStrike = 2290,
            BladeSpirit = 2272,
            FireBall = 2257,
            Poison = 2259,
            Needles = 2251,
            MagicArrow = 2244,
            Heal = 2243,
            GreaterHeal = 2268,
            
            
            
        }
        
        public string SplitCamelCase(string str)
        {
            return Regex.Replace(
                Regex.Replace(
                    str,
                    @"(\P{Ll})(\P{Ll}\p{Ll})",
                    "$1 $2"
                ),
                @"(\p{Ll})(\P{Ll})",
                "$1 $2"
            );
        }
    }
}