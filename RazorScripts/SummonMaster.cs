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
        private List<int> SummonsSerials = new List<int>();
        private Journal jurnal = new Journal();
        private Journal.JournalEntry _last = null;

        public void Run()
        {
            _last = jurnal.GetJournalEntry(null).OrderBy(j => j.Timestamp).LastOrDefault();
            UpdateGump();
            try
            {
                while (true)
                {
                    var entries = jurnal.GetJournalEntry(_last);
                    if (entries.Any(CheckSummons))
                    {
                        var precast = Mobiles.ApplyFilter(new Mobiles.Filter
                        {
                            RangeMax = 1,
                            RangeMin = 0,
                            Notorieties = new List<byte> { 1 },
                        });

                        WaitForSummons(precast);

                        _last = entries.OrderBy(e => e.Timestamp).LastOrDefault();
                    }

                    var runMobs = Mobiles.ApplyFilter(new Mobiles.Filter
                    {
                        RangeMax = 20,
                        RangeMin = 0,
                        Notorieties = new List<byte> { 1 },
                    }).Where(m => SummonsSerials.Contains(m.Serial)).ToList();
                    runMobs.ForEach(m => Mobiles.WaitForProps(m, 1000));
                    var runSums = runMobs.Where(m => m.Properties.Any(p => p.Number == 1049646)).ToList();
                    Summons = runSums;
                    var change = false;
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
                            case SumReply.Summon:
                                UpdateGump();
                                SummonFey();
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

                    Misc.Pause(100);
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

        private bool CheckSummons(Journal.JournalEntry entry)
        {
            if(entry.Name != Player.Name)
            {
                return false;
            }

            return entry.Text.ToLower().Contains("alalithra")
                   || entry.Text.ToLower().Contains("kal vas xen corp")
                   || entry.Text.ToLower().Contains("kal vas xen hur")
                   || entry.Text.ToLower().Contains("kal vas xen ylem")
                   || entry.Text.ToLower().Contains("kal vas xen flam")
                   || entry.Text.ToLower().Contains("kal vas xen an flam")
                   || entry.Text.ToLower().Contains("in jux por ylem")
                   || entry.Text.ToLower().Contains("kal vas cen Corp ylem")
                   || entry.Text.ToLower().Contains("kal xen");
        }

        private void SummonFey()
        {
            var precast = Mobiles.ApplyFilter(new Mobiles.Filter
            {
                RangeMax = 1,
                RangeMin = 0,
                Notorieties = new List<byte> { 1 },
            });
            Spells.CastSpellweaving("Summon Fey");
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
            }
        }

        private void ReleaseAll()
        {
            var filter = new Mobiles.Filter
            {
                RangeMax = 20,
                RangeMin = 0,
                Notorieties = new List<byte> { 1 }
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

        private void ReleaseSummon(int serial)
        {
            var mob = Mobiles.FindBySerial(serial);
            if (mob != null)
            {
                Misc.WaitForContext(mob, 500);
                Misc.ContextReply(mob, 5);
            }
        }
        private void WaitForSummons(List<Mobile> summons)
        {
            Misc.SendMessage("Waiting for Summons to appear");
            var rems = new List<int>();
            foreach (var serial in SummonsSerials.Where(serial => Summons.All(s => s.Serial != serial)))
            {
                rems.Add(serial);
            }
            rems.ForEach(s => SummonsSerials.Remove(s));

            var clockStart = DateTime.Now;
            while (DateTime.Now < clockStart.AddSeconds(5))
            {
                var postcast = Mobiles.ApplyFilter(new Mobiles.Filter
                {
                    RangeMax = 1,
                    RangeMin = 0,
                    Notorieties = new List<byte> { 1 },
                });
            
                //Find Mobiles only in postcase
                var newMobs = postcast.Where(m => summons.All(p => p.Serial != m.Serial)).ToList();
                if (newMobs.Any())
                {
                    newMobs.ForEach(m => SummonsSerials.Add(m.Serial));
                    break;
                }
                Misc.Pause(50);
            }
            
        }

        private void UpdateGump()
        {
            var sumGump = Gumps.CreateGump();
            sumGump.gumpId = Gumpid;
            sumGump.serial = (uint)Player.Serial;
            Gumps.AddBackground(ref sumGump,0,0,480,120,-1);
            
            Gumps.AddImage(ref sumGump,0,0,9400);
            for (var i = 1; i <= 40; i++)
            {
                Gumps.AddImage(ref sumGump,i*11,0,9401);
            }
            Gumps.AddImage(ref sumGump,451,0,9402);
            for (var row = 1; row <= 38; row++)
            {
                Gumps.AddImage(ref sumGump,0,row*2+11,9403);
            }
            
            Gumps.AddImage(ref sumGump,0,89,9406);
            for (var i = 1; i <= 40; i++)
            {
                Gumps.AddImage(ref sumGump,i*11,89,9407);
            }
            Gumps.AddImage(ref sumGump,451,89,9408);
            
            for (var row = 1; row <= 38; row++)
            {
                Gumps.AddImage(ref sumGump,451,row*2+11,9405);
            }

            for (var column = 1; column <= 28; column++)
            {
                for (var row = 1; row <= 38; row++)
                {
                    Gumps.AddImage(ref sumGump,column*16-5,row*2+11,9404);
                }
            }

            foreach (var sum in Summons)
            {
                var index = Summons.IndexOf(sum) + 1;
                Gumps.AddButton(ref sumGump,index*60-40,20,GetGumpKey(sum),GetGumpKey(sum),sum.Serial,1,1);
                Gumps.AddTooltip(ref sumGump,"Release " + sum.Name);
                if (sum.Properties.Any(p => p.Number == 1080078))
                {
                    Gumps.AddLabel(ref sumGump, index*60-40+5,65,0x35,"Guard");
                }
                if (sum.WarMode)
                {
                    Gumps.AddLabel(ref sumGump, index*60-40+2,80,0x25,"Combat");
                }
            }
            
            Gumps.AddButton(ref sumGump, 330,5,9721,9722,(int)SumReply.Summon,1,0);
            Gumps.AddButton(ref sumGump, 330,35,9721,9722,(int)SumReply.Release,1,0);
            Gumps.AddButton(ref sumGump, 330,65,9721,9722,(int)SumReply.Guard,1,0);
            Gumps.AddLabel(ref sumGump, 370,10,0x30,"Summon");
            Gumps.AddLabel(ref sumGump, 370,40,0x6D,"Release");
            Gumps.AddLabel(ref sumGump, 370,70,0x35,"Guard");
            
            
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
                default :
                    return 2279;
            }
        }
        
        private enum SumReply
        {
            Summon = 1,
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