using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RazorEnhanced;
using Mobile = RazorEnhanced.Mobile;

namespace RazorScripts
{
    public class SummonMaster
    {
        private bool _compactMode = false;
        private bool _transparancyMode = false;

        private bool _optionShow = false;
        private uint Gumpid = 98413566;
        private List<Mobile> Summons = new List<Mobile>();
        private List<MockMob> LastLoop = new List<MockMob>();
        private List<int> SerialsLog = new List<int>();
        Dictionary<int,DateTime> _timers = new Dictionary<int, DateTime>();
        System.Timers.Timer _timer = new System.Timers.Timer(5000);
        private Target _target = new Target();
        private string _version = "1.4.0";
        Journal _journal = new Journal();
        private Journal.JournalEntry _lastJournalEntry = null;

        private List<string> _summonBenefitSpells = new List<string>
        {
            "In Mani",
            "In Vas Mani",
            "In Vas Mani Hur",
            "Vas An Nox",
            "An Nox",
            "Olorisstra",
            "Rel Sanct"
        };

        public void Run()
        {
            UpdateGump();
            try
            {
                _timer.Enabled = true;
                _timer.Elapsed += (sender, e) => UpdateGump();
                _timer.AutoReset = true;
                _timer.Start();
                
                while (true)
                {
                    var newSummons = false;

                    var runMobs = Mobiles.ApplyFilter(new Mobiles.Filter
                    {
                        RangeMax = 20,
                        RangeMin = 0,
                        Notorieties = new List<byte> { 1, 2 },
                    }).ToList();
                    
                    runMobs.ForEach(m => Mobiles.WaitForProps(m, 1000));
                    var runSums = runMobs.Where(m => m.Properties.Any(p => p.Number == (int)PropertyNumber.Summoned)).ToList();
                    var mySumms = FilterMySummons(runSums);
                    Summons = mySumms;
                    foreach (var summon in Summons)
                    {
                        if (SerialsLog.Contains(summon.Serial))
                        {
                            continue;
                        }
                        
                        SerialsLog.Add(summon.Serial);
                        newSummons = true;
                    }
                    var change = false;
                    if (newSummons)
                    {
                        GuardMode();
                    }

                    var reply = Gumps.GetGumpData(Gumpid);
                    if (reply.buttonid != -1)
                    {
                        switch ((SumReply)reply.buttonid)
                        {
                            case SumReply.Guard:
                                UpdateGump();
                                GuardMode();
                                reply.buttonid = -1;
                                break;
                            case SumReply.Release:
                                UpdateGump();
                                ReleaseAll();
                                reply.buttonid = -1;
                                break;
                            case SumReply.Follow:
                                UpdateGump();
                                Follow();
                                reply.buttonid = -1;
                                break;
                            case SumReply.Attack:
                                var targetSerial = _target.PromptTarget();
                                
                                if(targetSerial != 0)
                                {
                                    var mob = Mobiles.FindBySerial(targetSerial);
                                    if (mob != null)
                                    {
                                        Attack(mob);
                                    }
                                }
                                reply.buttonid = -1;
                                break;
                            case SumReply.SetCompact:
                                _compactMode = true;
                                break;
                            case SumReply.SetClassic:
                                _compactMode = false;
                                break;
                            case SumReply.ToggleTransparency:
                                _transparancyMode = !_transparancyMode;
                                break;
                            case SumReply.ToggleOptions:
                                _optionShow = !_optionShow;
                                break;
                            default:
                                if (Target.HasTarget())
                                {
                                    var lines = _journal.GetJournalEntry(_lastJournalEntry).OrderBy(j => j.Timestamp).ToList();
                                    if (lines.Any())
                                    {
                                        var lastSpell = lines.LastOrDefault(l =>
                                            l.Type.Equals("Spell", StringComparison.InvariantCultureIgnoreCase) &&
                                            l.Name.Equals(Player.Name, StringComparison.InvariantCultureIgnoreCase));
                                        if (lastSpell != null)
                                        {
                                            if (_summonBenefitSpells.Any(word =>
                                                    lastSpell.Text.Equals(word,
                                                        StringComparison.InvariantCultureIgnoreCase)))
                                            {
                                                Target.TargetExecute(reply.buttonid);
                                                UpdateGump();
                                                reply.buttonid = -1;
                                                _lastJournalEntry = lines.Last();
                                                break;
                                            }
                                        }
                                        _lastJournalEntry = lines.Last();
                                    }
                                }
                                ReleaseSummon(reply.buttonid);
                                UpdateGump();
                                reply.buttonid = -1;
                                break;
                        }

                        change = true;
                    }

                    if (Summons.Count != LastLoop.Count)
                        change = true;

                    //compare all Mobiles in Summons with LastLoop, Check Mobile.Warmode and Mobile.Properties for any changes
                    foreach (var sum in Summons)
                    {
                        var last = LastLoop.FirstOrDefault(m => m.Serial == sum.Serial);
                        if (last == null)
                        {
                            change = true;
                            break;
                        }

                        //check warmode
                        if (last.WarMode != sum.WarMode)
                        {
                            change = true;
                            break;
                        }

                        //check properties
                        if (sum.Properties.Count != last.Properties.Count)
                        {
                            change = true;
                            break;
                        }

                        foreach (var prop in sum.Properties)
                        {
                            var lastProp = last.Properties.FirstOrDefault(p => p.Number == prop.Number);
                            if (lastProp == null)
                            {
                                //new property
                                change = true;
                                break;
                            }
                        }
                    }

                    if (change)
                    {
                        UpdateGump();
                        LastLoop.Clear();
                        Summons.ForEach(s => LastLoop.Add(new MockMob
                        {
                            Serial = s.Serial,
                            WarMode = s.WarMode,
                            Properties = s.Properties.ToList()
                        }));
                    }
                    Misc.Pause(500);
                }
            }
            catch (ThreadAbortException)
            {
                //silent
            }
            catch (Exception e)
            {
                Misc.SendMessage(e.ToString());
            }
            finally
            {
                _timer.Stop();
                _timer.Dispose();
                Gumps.CloseGump(Gumpid);
            }

        }

        private List<Mobile> FilterMySummons(List<Mobile> summons)
        {
            var result = new List<Mobile>();
            foreach (var mob in summons)
            {
                if(mob.CanRename)
                {
                    result.Add(mob);
                }
            }

            return result;
        }

        private Task GuardMode()
        {
            foreach (var mob in Summons)
            {
                Misc.WaitForContext(mob, 500);
                Misc.ContextReply(mob, 2);
                var timeprop = mob.Properties.FirstOrDefault(p => p.Number == (int)PropertyNumber.TimeRemaining);
                if(timeprop != null)
                {
                    var propString = timeprop.Args.Split('\t');
                    if(propString.Length >= 2)
                    {
                        var parts = propString[1].Split(':');
                        if (parts.Length == 2)
                        {
                            var time = DateTime.Now.AddMinutes(int.Parse(parts[0])).AddSeconds(int.Parse(parts[1]));
                            _timers[mob.Serial] = time;
                        }
                    }
                    
                }
            }
            
            return Task.CompletedTask;
        }

        private Task Follow()
        {
            foreach (var mob in Summons)
            {
                Misc.WaitForContext(mob, 500);
                Misc.ContextReply(mob, 1);
                Target.WaitForTarget(1000);
                Target.TargetExecute(Player.Serial);
            }

            return Task.CompletedTask;
        }

        private Task ReleaseAll()
        {
            foreach (var mob in Summons)
            {
                ReleaseSummon(mob.Serial);
            }
            
            return Task.CompletedTask;
        }
        
        private Task Attack(Mobile target)
        {
            foreach (var mob in Summons)
            {
                Misc.WaitForContext(mob, 500);
                Misc.ContextReply(mob, 0);
                Target.WaitForTarget(1000);
                Target.TargetExecute(target);
            }
            
            return Task.CompletedTask;
        }

        private void ReleaseSummon(int serial)
        {
            var mob = Mobiles.FindBySerial(serial);
            if (mob != null)
            {
                Misc.WaitForContext(mob, 500);
                Misc.ContextReply(mob, 5);
            }
        }

        private void HandleOptionPanel(Gumps.GumpData gump, int width)
        {
            var baseX = width+15;
            var indentX = baseX+5;
            var optionsWidth = 110;
            if (_optionShow)
            {
                Gumps.AddBackground(ref gump,width,0,optionsWidth,140,1755);
                if (_transparancyMode)
                {
                    Gumps.AddAlphaRegion(ref gump, width, 0, optionsWidth, 140);
                }
                
                Gumps.AddLabel(ref gump,baseX, 15,0x75, "Mode");
                Gumps.AddButton(ref gump,indentX, 40, 5601, 5601, (int)SumReply.SetCompact, 1, 1);
                Gumps.AddLabel(ref gump,indentX+20, 40, _compactMode ? 72 : 0x7b, "Compact");
                Gumps.AddButton(ref gump,indentX, 60, 5601, 5601, (int)SumReply.SetClassic, 1, 1);
                Gumps.AddLabel(ref gump,indentX+20, 60,!_compactMode ? 72 : 0x7b, "Classic");
                
                Gumps.AddLabel(ref gump,baseX, 85,0x75, "Transparency");
                Gumps.AddButton(ref gump,indentX, 110, 5601, 5601, (int)SumReply.ToggleTransparency, 1, 1);
                Gumps.AddLabel(ref gump,indentX+20, 110, _transparancyMode ? 72 : 0x7b, "Ghost");
                
                Gumps.AddButton(ref gump, width+optionsWidth-25, 10, 9781, 9781, (int)SumReply.ToggleOptions, 1, 1);
                Gumps.AddTooltip(ref gump, "Hide Options");
            }
            else
            {
                Gumps.AddButton(ref gump, width-25, 10, 9780, 9780, (int)SumReply.ToggleOptions, 1, 1);
                Gumps.AddTooltip(ref gump, "Show Options");
            }
            
        }

        private void UpdateGump()
        {
            var sumGump = Gumps.CreateGump();
            var width = (_compactMode ? (Summons.Count * 58) + 100 : 400)+10;
            sumGump.gumpId = Gumpid;
            sumGump.serial = (uint)Player.Serial;
            Gumps.AddBackground(ref sumGump,0,0, width,140,1755);
            if (_transparancyMode)
            {
                Gumps.AddAlphaRegion(ref sumGump, 0, 0, width, 140);
            }
            
            HandleOptionPanel(sumGump, width);

            foreach (var sum in Summons)
            {
                var index = Summons.IndexOf(sum) + 1;
                
                
                var healthFraction = (double)sum.Hits / sum.HitsMax;
                var healthVal = (int)Math.Floor(healthFraction * 44);
                Gumps.AddImageTiled(ref sumGump, index*60-48,30,8,44,9740);
                if(sum.Poisoned)
                {
                    Gumps.AddImageTiled(ref sumGump, index*60-48,74-healthVal,8,healthVal,9742);
                }
                else if(sum.YellowHits)
                {
                    Gumps.AddImageTiled(ref sumGump, index*60-48,74-healthVal,8,healthVal,9743);
                }
                else
                {
                    Gumps.AddImageTiled(ref sumGump, index*60-48,74-healthVal,8,healthVal,9741);
                }
                
                Gumps.AddButton(ref sumGump,index*60-40,30,GetGumpKey(sum),GetGumpKey(sum),sum.Serial,1,1);
                Gumps.AddTooltip(ref sumGump,"Release " + sum.Name);
                if (sum.Properties.Any(p => p.Number == (int)PropertyNumber.Guarding))
                {
                    Gumps.AddLabel(ref sumGump, index*60-40+5,75,0x35,"Guard");
                }
                if (sum.WarMode)
                {
                    Gumps.AddLabel(ref sumGump, index*60-40+2,90,0x25,"Combat");
                }
                if(_timers.ContainsKey(sum.Serial))
                {
                    var time = _timers[sum.Serial];
                    var diff = time - DateTime.Now;
                    Gumps.AddLabel(ref sumGump, index*60-40+5,10,0x35,$"{diff.Minutes.ToString("D2")}:{diff.Seconds.ToString("D2")}");
                }
            }
            
            Gumps.AddButton(ref sumGump, width - 90, 10, 9903, 9904, (int)SumReply.Attack, 1, 0);
            Gumps.AddButton(ref sumGump, width - 90, 40, 9903, 9904, (int)SumReply.Release, 1, 0);
            Gumps.AddButton(ref sumGump, width - 90, 70, 9903, 9904, (int)SumReply.Guard, 1, 0);
            Gumps.AddButton(ref sumGump, width - 90, 100, 9903, 9904, (int)SumReply.Follow, 1, 0);
            Gumps.AddLabel(ref sumGump, width - 70, 10, 0x30, "Attack");
            Gumps.AddLabel(ref sumGump, width - 70, 40, 0x6D, "Release");
            Gumps.AddLabel(ref sumGump, width - 70, 70, 0x35, "Guard");
            Gumps.AddLabel(ref sumGump, width - 70, 100, 0x55, "Follow");

            if (!_compactMode || Summons.Count >= 2)
            {
                Gumps.AddLabel(ref sumGump, 15, 115, 0x7b, "SummonMaster");
                Gumps.AddLabel(ref sumGump, 100, 115, 0x7b, _version);
            }

            Gumps.CloseGump(Gumpid);
            Gumps.SendGump(sumGump,500,500);
        }

        private int GetGumpKey(Mobile mobile)
        {
            switch ((KnownSummon)mobile.MobileID)
            {
                case KnownSummon.Fey:
                    return 23006;
                case KnownSummon.AirElemental:
                    return 2299;
                case KnownSummon.Daemon:
                    return 2300;
                case KnownSummon.EarthElemental:
                    return 2301;
                case KnownSummon.FireElemental:
                    return 2302;
                case KnownSummon.WaterElemental:
                    return 2303;
                case KnownSummon.Colossus:
                    return 24015;
                case KnownSummon.AnimateWeapon:
                    return 24006;
                default :
                    return 2279;
            }
        }
        
        private enum PropertyNumber
        {
            Summoned = 1049646,
            Guarding = 1080078,
            TimeRemaining = 1060847
        }
        
        private enum KnownSummon
        {
            BladeSpirits = 0x023E,
            EnergyVortex = 0x00A4,
            Fey = 0x0080,
            AnimateWeapon = 0x02B4,
            EarthElemental = 0x000E,
            AirElemental = 0x000D,
            FireElemental = 0x000F,
            WaterElemental = 0x0010,
            Daemon = 0x000A,
            Colossus = 0x033D
        }
        
        private enum SumReply
        {
            Attack = 1,
            Release = 2,
            Guard = 3,
            Follow = 4,
            SetCompact = 5,
            SetClassic = 6,
            ToggleTransparency = 7,
            ToggleOptions = 8
        }

        private class MockMob
        {
            public int Serial { get; set; }
            public List<Property> Properties { get; set; }
            public bool WarMode { get; set; }
        }
    }
}