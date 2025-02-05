using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using RazorEnhanced;
using Mobile = RazorEnhanced.Mobile;

namespace RazorScripts
{
    public class SummonMaster
    {
        private uint Gumpid = 98413566;
        private List<Mobile> Summons = new List<Mobile>();
        private List<MockMob> LastLoop = new List<MockMob>();
        private List<int> SerialsLog = new List<int>();
        Dictionary<int,DateTime> _timers = new Dictionary<int, DateTime>();
        System.Timers.Timer _timer = new System.Timers.Timer(5000);
        private Target _target = new Target();
        private string _version = "1.2.0";

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
                    var runSums = runMobs.Where(m => m.Properties.Any(p => p.Number == 1049646)).ToList();
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
                            default:
                                UpdateGump();
                                ReleaseSummon(reply.buttonid);
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

        private void GuardMode()
        {
            foreach (var mob in Summons)
            {
                Mobiles.WaitForProps(mob,1000);
                var sumProp = mob.Properties.FirstOrDefault(p => p.Number == 1049646);
                if (sumProp == null || !sumProp.ToString().Contains("summoned")) continue;
                Misc.WaitForContext(mob, 500);
                Misc.ContextReply(mob, 2);
                var timeprop = mob.Properties.FirstOrDefault(p => p.Number == 1060847);
                if(timeprop != null)
                {
                    var parts = timeprop.Args.Split('\t')[1].Split(':');
                    if (parts.Length == 2)
                    {
                        var time = DateTime.Now.AddMinutes(int.Parse(parts[0])).AddSeconds(int.Parse(parts[1]));
                        _timers[mob.Serial] = time;
                    }
                }
            }
        }

        private void ReleaseAll()
        {
            var filter = new Mobiles.Filter
            {
                RangeMax = 20,
                RangeMin = 0,
                Notorieties = new List<byte> { 1, 2 }
            };

            var mobs = Mobiles.ApplyFilter(filter);
            foreach (var mob in mobs)
            {
                Mobiles.WaitForProps(mob,1000);
                if (mob.Properties.Any(p => p.Number == 1049646))
                {
                    ReleaseSummon(mob.Serial);    
                }
            }
        }
        
        private void Attack(Mobile target)
        {
            var filter = new Mobiles.Filter
            {
                RangeMax = 20,
                RangeMin = 0,
                Notorieties = new List<byte> { 1, 2 }
            };

            var mobs = Mobiles.ApplyFilter(filter);
            foreach (var mob in mobs)
            {
                Mobiles.WaitForProps(mob,1000);
                if (mob.Properties.Any(p => p.Number == 1049646))
                {
                    Misc.WaitForContext(mob, 500);
                    Misc.ContextReply(mob, 0);
                    Target.WaitForTarget(1000);
                    Target.TargetExecute(target);
                }
            }
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

        private void UpdateGump()
        {
            var sumGump = Gumps.CreateGump();
            sumGump.gumpId = Gumpid;
            sumGump.serial = (uint)Player.Serial;
            Gumps.AddBackground(ref sumGump,0,0,480,120,1755);

            foreach (var sum in Summons)
            {
                var index = Summons.IndexOf(sum) + 1;
                Gumps.AddButton(ref sumGump,index*60-40,30,GetGumpKey(sum),GetGumpKey(sum),sum.Serial,1,1);
                Gumps.AddTooltip(ref sumGump,"Release " + sum.Name);
                if (sum.Properties.Any(p => p.Number == 1080078))
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
                    Gumps.AddLabel(ref sumGump, index*60-40+5,10,0x35,$"{diff.Minutes}:{diff.Seconds.ToString("##")}");
                }
            }
            
            Gumps.AddButton(ref sumGump, 330,10,9903,9904,(int)SumReply.Attack,1,0);
            Gumps.AddButton(ref sumGump, 330,40,9903,9904,(int)SumReply.Release,1,0);
            Gumps.AddButton(ref sumGump, 330,70,9903,9904,(int)SumReply.Guard,1,0);
            Gumps.AddLabel(ref sumGump, 355,10,0x30,"Attack");
            Gumps.AddLabel(ref sumGump, 355,40,0x6D,"Release");
            Gumps.AddLabel(ref sumGump, 355,70,0x35,"Guard");
            
            Gumps.AddLabel(ref sumGump,290,95,0x7b, "Summon Master");
            Gumps.AddLabel(ref sumGump,393,95,0x7b, "Version: " + _version);
            
            Gumps.CloseGump(Gumpid);
            Gumps.SendGump(sumGump,500,500);
        }

        private int GetGumpKey(Mobile mobile)
        {
            switch (mobile.MobileID)
            {
                case 0x0080:
                    return 23006;
                case 0x000D:
                    return 2299;
                case 0x000A:
                    return 2300;
                case 0x000E:
                    return 2301;
                case 0x000F:
                    return 2302;
                case 0x0010:
                    return 2303;
                case 0x033D:
                    return 24015;
                case 0x02B4:
                    return 24006;
                default :
                    return 2279;
            }
        }
        
        private enum SumReply
        {
            Attack = 1,
            Release = 2,
            Guard = 3
        }

        private class MockMob
        {
            public int Serial { get; set; }
            public List<Property> Properties { get; set; }
            public bool WarMode { get; set; }
        }
    }
}