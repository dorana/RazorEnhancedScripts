//Add 2 scripts that alter the Shared Value for SampMaster to the 2 values below so that you can keybind these.
//Misc.SetSharedValue("SampMaster:Targets","Multi")
//Misc.SetSharedValue("SampMaster:Targets","Single")
//This is simply to allow swapping stance from keybinds


using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Threading;
using RazorEnhanced;

namespace RazorScripts
{
    public class SampMaster
    {
        private static uint GumppId = 126542315;
        private bool Multi = false;
        private bool UseDoubleStrike = false;

        private List<uint> AoeSpecials = new List<uint>
        {
            21004,
            21006
        };
        
        public void Run()
        {
            var defensives = new List<string>
            {
                "Evasion",
                "Counter Attack",
                //"Confidence"
            };
            
            var offensivesSingle = new List<string>
            {
                "Lightning Strike",
                "Honorable Execution",
                "Stagger"
            };
            
            var offensivesMulti = new List<string>
            {
                
                "Whirlwind Attack",
                "Momentum Strike",
                "Frenzied Whirlwind",
            };
            try
            {
                UpdateGump();
                while (true)
                {
                    var gd = Gumps.GetGumpData(GumppId);
                    if(gd.buttonid == 2)
                    {
                        Misc.SetSharedValue("SampMaster:Targets","Single");
                    }
                    else if(gd.buttonid == 1)
                    {
                        Misc.SetSharedValue("SampMaster:Targets","Multi");
                    }
                    var oldTarg = Multi;
                    Multi = IsMulti();
                    var targChanged = oldTarg != Multi;
                    if (targChanged)
                    {
                        UpdateGump();
                    }
                    
                    if (Player.WarMode && Player.Mana > 10)
                    {
                        if (!Player.Buffs.Any(b => defensives.Contains(b)))
                        {
                            
                            Spells.CastBushido("Counter Attack");
                            Misc.Pause(200);
                        }

                        else
                        {
                            if (Multi)
                            {
                                if (!Player.Buffs.Any(b => offensivesMulti.Contains(b)) && !Player.HasSpecial)
                                {
                                    var force = false;
                                    // var targs = Mobiles.ApplyFilter(new Mobiles.Filter
                                    // {
                                    //     RangeMin = 0,
                                    //     RangeMax = 1,
                                    //     Warmode = 1
                                    // }).ToList();
                                    // targs.ForEach(m => Mobiles.WaitForStats(m, 200));
                                    // var primeTarget = targs.OrderBy(m => m.Hits).FirstOrDefault();
                                    //
                                    // if (primeTarget != null && targs.Count > 1 && targs.Count < 3)
                                    // {
                                    //     if (primeTarget.Hits < 30)
                                    //     {
                                    //         force = true;
                                    //         Player.Attack(primeTarget);
                                    //     }
                                    // }
                                    
                                    PrimeMulti(force);
                                    Misc.Pause(200);
                                }
                            }
                            else
                            {
                                if (!offensivesSingle.Any(Player.SpellIsEnabled))
                                {
                                    if (UseDoubleStrike && Player.PrimarySpecial == 20998)
                                    {
                                        Player.WeaponPrimarySA();
                                    }
                                    else
                                    {
                                        Spells.CastBushido("Lightning Strike");
                                    }
                                    
                                    Misc.Pause(200);
                                }
                            } 
                        }
                        
                        
                    }

                    Misc.Pause(500);
                }
            }
            catch (Exception e)
            {
                if (e.GetType() != typeof(ThreadAbortException))
                {
                    Misc.SendMessage(e.ToString());
                }
            }
        }


        private void PrimeMulti(bool forceMomentum = false)
        {
            if (!forceMomentum && AoeSpecials.Contains(GetMultiIcon("primary")))
            {
                Player.WeaponPrimarySA();
            }
            else if (!forceMomentum && AoeSpecials.Contains(GetMultiIcon("secondary")))
            {
                Player.WeaponSecondarySA();
            }
            
            else
            {
                Spells.CastBushido("Momentum Strike");
            }
        }

        private bool IsMulti()
        {
            var val = Misc.ReadSharedValue("SampMaster:Targets");
            var stance = (val is string) ? !string.IsNullOrEmpty(val.ToString()) ? val.ToString() : "Single" : "Single";
            if (string.IsNullOrEmpty(stance))
            {
                stance = "Single";
            }
            
            return stance == "Multi";
        }

        private bool IsDefensive()
        {
            var val = Misc.ReadSharedValue("SampMaster:Stance");
            var stance = (val is string) ? !string.IsNullOrEmpty(val.ToString()) ? val.ToString() : "Offensive" : "Offensive";
            if (string.IsNullOrEmpty(stance))
            {
                stance = "Offensive";
            }
            
            
            
            return stance == "Defensive";
        }
        
        private void UpdateGump()
        {
            var gump = Gumps.CreateGump();
            gump.gumpId = GumppId;
            gump.serial = (uint)Player.Serial;
            Gumps.AddBackground(ref gump,0,0,55,55,1755);

            if (!Multi)
            {
                // Gumps.AddImage(ref gump,5,5,21540);
                Gumps.AddButton(ref gump, 5,5,21540,21540,1,1,0);
                Gumps.AddTooltip(ref gump, "Single Target");
            }
            else
            {
                Gumps.AddButton(ref gump, 5,5,(int)GetMultiIcon(),(int)GetMultiIcon(),2,1,0);
                Gumps.AddTooltip(ref gump, "Multi Target");
            }

            Gumps.CloseGump(GumppId);
            Gumps.SendGump(gump,500,500);
        }
        
        private uint GetMultiIcon(string slot = null)
        {
            switch (slot)
            {
                case "primary":
                {
                    if (AoeSpecials.Contains(Player.PrimarySpecial))
                    {
                        return Player.PrimarySpecial;
                    }

                    break;
                }
                case "secondary":
                {
                    if (AoeSpecials.Contains(Player.SecondarySpecial))
                    {
                        return Player.SecondarySpecial;
                    }

                    break;
                }
                default:
                {
                    if (AoeSpecials.Contains(Player.PrimarySpecial))
                    {
                        return Player.PrimarySpecial;
                    }

                    if (AoeSpecials.Contains(Player.SecondarySpecial))
                    {
                        return Player.SecondarySpecial;
                    }

                    break;
                }
            }

            return 21541;
        }
    }
}