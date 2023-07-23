using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using RazorEnhanced;
using RazorEnhanced.UOScript;

namespace Razorscripts
{
    public class Debugger
    {
        internal class XYPos
        {
            public int X { get; set; }
            public int Y { get; set; }

            public XYPos(int x, int y)
            {
                X = x;
                Y = y;

                
            }
            
            public decimal GetDistance()
            {
                var a = Math.Abs(Player.Position.X - X);
                var b = Math.Abs(Player.Position.Y - Y);
                var c = Math.Sqrt(Math.Pow(a, 2) + Math.Pow(b, 2));
                return (decimal) c;
            }
        }
        public void Run()
        {
            var c1 = Mobiles.FindBySerial(0x000061AB);
            var c1field = GetAvailablePosition(c1.Position);
            var cloststc1 = GetClosestPosition(c1field);
            PathFinding.PathFindTo(cloststc1.X, cloststc1.Y);



            // var tar = new Target();
            // var ser = tar.PromptTarget();
            // var itm = Items.FindBySerial(ser);
            // Items.WaitForProps(itm, 1000);
            // foreach (var prop in itm.Properties)
            // {
            //     Misc.SendMessage($"{prop.Number} {prop.ToString()}");
            // }

            //FocusStrike();
            //PullWeeds();
            // TrainChiv();
            // TrainNecro();
            // TrainBushido();
        }

        private XYPos GetClosestPosition(List<XYPos> field)
        {
            return field.OrderBy(p => p.GetDistance()).First();
        }

        private List<XYPos> GetAvailablePosition(Point3D pos)
        {
            var result = new List<XYPos>();
            result.Add(new XYPos(pos.X, pos.Y));
            result.Add(new XYPos(pos.X, pos.Y+1));
            result.Add(new XYPos(pos.X, pos.Y-1));
            result.Add(new XYPos(pos.X+1, pos.Y));
            result.Add(new XYPos(pos.X+1, pos.Y+1));
            result.Add(new XYPos(pos.X+1, pos.Y-1));
            result.Add(new XYPos(pos.X-1, pos.Y));
            result.Add(new XYPos(pos.X-1, pos.Y+1));
            result.Add(new XYPos(pos.X-1, pos.Y-1));

            return result;
        }

        private int? ExampleGetMobId()
        {
            
            var mobs = Mobiles.ApplyFilter(new Mobiles.Filter
            {
                RangeMax = 12,
                RangeMin = 0,
            });

            return mobs.FirstOrDefault(m => m.Name == "MyPetname" && m.MobileID == 0x000)?.Serial;
        }

        private void PullWeeds()
        {
            while (true)
            {
                var weeds = Items.ApplyFilter(new Items.Filter
                {
                    RangeMin = 0,
                    RangeMax = 1,
                    Name = "Creepy weeds"
                });
                foreach (var weed in weeds)
                {
                    Items.UseItem(weed);
                    Misc.Pause(100);
                }
            }
        }

        private void TrainChiv()
        {
            var skill = Player.GetRealSkillValue("Chivalry");
            while (skill < 100)
            {
                if (Player.Hits < 20)
                {
                    WaitTillHealed();
                }
                if (skill <= 50)
                {
                    Spells.CastChivalry("Consecrate Weapon");
                }
                else if (skill <= 70)
                {
                    Spells.CastChivalry("Enemy Of One");
                }
                else if (skill <= 90)
                {
                    Spells.CastChivalry("Holy Light");
                }
                else
                {
                    Spells.CastChivalry("Noble Sacrifice");
                }
                
                Misc.Pause(4000);
                skill = Player.GetRealSkillValue("Chivalry");
            }
        }

        private void WaitTillHealed()
        {
            while (Player.Hits < Player.HitsMax)
            {
                Misc.Pause(5000);
            }
        }

        private void TrainNecro()
        {
            var skill = Player.GetRealSkillValue("Necromancy");
            var player = Mobiles.FindBySerial(Player.Serial);
            while (skill < 100)
            {
                if (Player.Hits < 20)
                {
                    while (Player.Buffs.Any(b => b.Equals("Lich Form", StringComparison.OrdinalIgnoreCase)))
                    {
                        Spells.CastNecro("Lich Form");
                        Misc.Pause(5000);
                    }
                    
                    WaitTillHealed();
                }
                
                if (skill < 50)
                {
                    Spells.CastNecro("Pain Spike");
                    Target.WaitForTarget(2000);
                    Target.TargetExecute(player);
                }
                else if (skill < 70)
                {
                    Spells.CastNecro("Horrific Beast");
                }
                else if (skill == 70 && Player.Buffs.Any(b => b.ToLower().Contains("beast")))
                {
                    // Spells.CastNecro("Horrific Beast");
                }
                else if (skill < 90)
                {
                    Spells.CastNecro("Wither");
                }
                else
                {
                    Spells.CastNecro("Lich Form");
                }
                
                Misc.Pause(5000);
                skill = Player.GetRealSkillValue("Necromancy");
            }
        }

        private void FocusStrike()
        {
            while (true)
            {
                if (!Player.SpellIsEnabled("Focus Attack") && Player.Mana >= 10)
                {
                    Spells.CastNinjitsu("Focus Attack");
                }
                Misc.Pause(200);
            }

        }
        
        private void TrainBushido()
        {
            var skill = Player.GetRealSkillValue("Bushido");
            while (skill < 100)
            {
                if (Player.Hits < 20)
                {
                    WaitTillHealed();
                }
                if (skill <= 60)
                {
                    Spells.CastBushido("Confidence");
                }
                else if (skill <= 75)
                {
                    Spells.CastBushido("Counter Attack");
                }
                else
                {
                    Spells.CastBushido("Evasion");
                    Misc.Pause(16000);
                }
                
                Misc.Pause(4000);
                skill = Player.GetRealSkillValue("Bushido");
            }
        }

        private void Search(Item container)
        {
            Items.WaitForContents(container,2000);
            foreach (var i in container.Contains)
            {
                if (i.Name.ToLower().Contains("staff"))
                {
                    Misc.SendMessage(i.Name);
                }

                if (i.IsContainer && !i.IsBagOfSending)
                {
                    Search(i);
                }
            }
        }
        
    }
}