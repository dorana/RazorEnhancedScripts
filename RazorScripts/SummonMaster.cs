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
        private Dictionary<int, MockMob> LastLoop = new Dictionary<int, MockMob>();
        Dictionary<int, DateTime> _timers = new Dictionary<int, DateTime>();
        System.Timers.Timer _timer = new System.Timers.Timer(5000);
        private Target _target = new Target();
        private string _version = "1.3.2";
        Journal _journal = new Journal();
        private Journal.JournalEntry _lastJournalEntry = null;
        // Adding constants for friendly adjust if constants from server change.
        const int SummonedPropNum = 1049646;
        const int GuardingPropNum = 1080078;
        const int TimeRemainingPropNum = 1060847;
        const int energyVortexMobileId = 0x00A4;
        const int bladeSpiritMobileId = 0x023E;

        private List<string> _summonBenefitSpells = new List<string>
        {
            "In Mani",
            "In Vas Mani",
            "In Vas Mani Hur",
            "Vas An Nox",
            "An Nox",
            "Olorisstra",
            "Rel Sanct",
        };

        public void Run()
        {
            UpdateGump();
            try
            {
                _timer.Enabled = false;
                _timer.Elapsed += (sender, e) => UpdateGump();
                _timer.AutoReset = true;
                _timer.Start();

                while (true)
                {
                    //Player.Pets will check Rename ability we just need summoned prop lookup.
                    var runMobs = Player.Pets.Concat(GetEnergyVortextOrBladeSpirit()).Where(m => m.Properties.Any(p => p.Number == SummonedPropNum)).ToList();
                    var newSummons = runMobs.Exists(r => !Summons.Exists(s => s.Serial == r.Serial));
                    runMobs.ForEach(m => Mobiles.WaitForProps(m, 1000));
                    Summons = runMobs;
                    if (newSummons)
                    {
                        GuardMode();
                    }
                    var change = false;
                    var reply = Gumps.GetGumpData(Gumpid);
                    if (reply.buttonid != -1)
                    {
                        switch ((SumReply)reply.buttonid)
                        {
                            case SumReply.Guard:
                                GuardMode();
                                break;
                            case SumReply.Release:
                                ReleaseAll();
                                break;
                            case SumReply.Attack:
                                UpdateGump();
                                if (_target.PromptTarget() is int targetSerial
                                    && targetSerial != 0
                                    && Mobiles.FindBySerial(targetSerial) is Mobile mob)
                                {
                                    Attack(mob);
                                }
                                break;
                            case SumReply.Follow:
                                Follow();
                                break;
                            default:
                                if (!Target.HasTarget())
                                {
                                    ReleaseSummon(reply.buttonid);
                                    break;
                                }
                                var lines = _journal.GetJournalEntry(_lastJournalEntry).OrderBy(j => j.Timestamp).ToList();
                                if (!lines.Any())
                                {
                                    break;
                                }
                                var lastSpell = lines.LastOrDefault(l =>
                                    l.Type.Equals("Spell", StringComparison.InvariantCultureIgnoreCase)
                                    && l.Serial == Player.Serial);
                                if (lastSpell == null)
                                {
                                    if (_summonBenefitSpells.Any(word =>
                                            lastSpell.Text.Equals(word,
                                                StringComparison.InvariantCultureIgnoreCase)))
                                    {
                                        Target.TargetExecute(reply.buttonid);
                                    }
                                }
                                _lastJournalEntry = lines.Last();
                                break;
                        }
                        reply.buttonid = -1;
                        change = true;
                    }


                    if (!change && Summons.Count != LastLoop.Count)
                        change = true;

                    //compare all Mobiles in Summons with LastLoop, Check Mobile.Warmode and Mobile.Properties for any changes
                    if (!change)
                    {
                        change = Summons.Exists(s =>
                                !LastLoop.TryGetValue(s.Serial, out var last) ||
                                last.WarMode != s.WarMode ||
                                s.Properties.Count != last.Properties.Count ||
                                s.Properties.Any(p => !last.Properties.Exists(lp => lp.Number == p.Number)));
                    }
                    if (change)
                    {
                        UpdateGump();
                        LastLoop = Summons.ToDictionary(s => s.Serial, v => new MockMob
                        {
                            Serial = v.Serial,
                            WarMode = v.WarMode,
                            Properties = v.Properties.ToList()
                        });
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

        private List<Mobile> GetEnergyVortextOrBladeSpirit()
        {
            return Mobiles.ApplyFilter(new Mobiles.Filter
            {
                RangeMax = 20,
                RangeMin = 0,
                Notorieties = new List<byte> { 1, 2 },
                Bodies = new List<int>() { energyVortexMobileId, bladeSpiritMobileId }
            }).ToList();
        }
        private List<Mobile> FilterMySummons(List<Mobile> summons)
        {
            var result = new List<Mobile>();
            foreach (var mob in summons)
            {
                if (mob.CanRename)
                {
                    result.Add(mob);
                }
            }

            return result;
        }

        private void Follow()
        {
            foreach (var mob in Summons)
            {
                Misc.UseContextMenu(mob.Serial, "Command: Follow", 500);
                Target.WaitForTarget(1000);
                Target.TargetExecute(Mobiles.FindBySerial(Player.Serial));
            }
        }
        private void GuardMode()
        {
            foreach (var mob in Summons)
            {
                Misc.UseContextMenu(mob.Serial, "Command: Guard", 500);
            }
        }
        private void ReleaseAll()
        {
            foreach (var mob in Summons)
            {
                ReleaseSummon(mob.Serial);
            }
        }
        private void Attack(Mobile target)
        {
            if (target == null) { return; }
            foreach (var mob in Summons)
            {
                Misc.UseContextMenu(mob.Serial, "Command: Kill", 500);
                Target.WaitForTarget(1000);
                Target.TargetExecute(target);
            }
        }

        private void ReleaseSummon(int serial)
        {
            Misc.UseContextMenu(serial, "Release", 500);
        }

        private void UpdateGump()
        {
            var width = (Summons.Count * 40) + 200;
            var height = 150;
            var sumGump = Gumps.CreateGump();
            sumGump.gumpId = Gumpid;
            sumGump.serial = (uint)Player.Serial;
            Gumps.AddBackground(ref sumGump, 0, 0, width, height, 1755);

            foreach (var sum in Summons)
            {
                var index = Summons.IndexOf(sum) + 1;


                var healthFraction = (double)sum.Hits / sum.HitsMax;
                var healthVal = (int)Math.Floor(healthFraction * 44);
                Gumps.AddImageTiled(ref sumGump, index * 60 - 48, 30, 8, 44, 9740);
                if (sum.Poisoned)
                {
                    Gumps.AddImageTiled(ref sumGump, index * 60 - 48, 74 - healthVal, 8, healthVal, 9742);
                }
                else if (sum.YellowHits)
                {
                    Gumps.AddImageTiled(ref sumGump, index * 60 - 48, 74 - healthVal, 8, healthVal, 9743);
                }
                else
                {
                    Gumps.AddImageTiled(ref sumGump, index * 60 - 48, 74 - healthVal, 8, healthVal, 9741);
                }

                if (Misc.WaitForContext(sum,500).Any())
                {
                    Gumps.AddButton(ref sumGump, index * 60 - 40, 30, GetGumpKey(sum), GetGumpKey(sum), sum.Serial, 1, 1);
                }
                else
                {
                    Gumps.AddImage(ref sumGump, index * 60 - 40, 30, GetGumpKey(sum));
                }
                
                Gumps.AddTooltip(ref sumGump, "Release " + sum.Name);
                if (sum.Properties.Any(p => p.Number == GuardingPropNum))
                {
                    Gumps.AddLabel(ref sumGump, index * 60 - 40 + 5, 75, 0x35, "Guard");
                }
                if (sum.WarMode)
                {
                    Gumps.AddLabel(ref sumGump, index * 60 - 40 + 2, 90, 0x25, "Combat");
                }
                var timeprop = sum.Properties.Find(p => p.Number == TimeRemainingPropNum);
                if (timeprop != null)
                {
                    var time = timeprop.Args.Split('\t').LastOrDefault();
                    Gumps.AddLabel(ref sumGump, index * 60 - 40 + 5, 10, 0x35, $"{time}".Replace("\t", ":"));
                }
            }

            Gumps.AddButton(ref sumGump, width - 150, 10, 9903, 9904, (int)SumReply.Attack, 1, 0);
            Gumps.AddButton(ref sumGump, width - 150, 40, 9903, 9904, (int)SumReply.Release, 1, 0);
            Gumps.AddButton(ref sumGump, width - 150, 70, 9903, 9904, (int)SumReply.Guard, 1, 0);
            Gumps.AddButton(ref sumGump, width - 150, 100, 9903, 9904, (int)SumReply.Follow, 1, 0);
            Gumps.AddLabel(ref sumGump, width - 125, 10, 0x30, "Attack");
            Gumps.AddLabel(ref sumGump, width - 125, 40, 0x6D, "Release");
            Gumps.AddLabel(ref sumGump, width - 125, 70, 0x35, "Guard");
            Gumps.AddLabel(ref sumGump, width - 125, 100, 0x55, "Follow");

            Gumps.AddLabel(ref sumGump, width - 140, 125, 0x7b, "SummonMaster");
            Gumps.AddLabel(ref sumGump, width - 40, 125, 0x7b, _version);

            Gumps.CloseGump(Gumpid);
            Gumps.SendGump(sumGump, 500, 500);
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
                default:
                    return 2279;
            }
        }

        private enum SumReply
        {
            Attack = 1,
            Release = 2,
            Guard = 3,
            Follow = 4
        }

        private class MockMob
        {
            public int Serial { get; set; }
            public List<Property> Properties { get; set; }
            public bool WarMode { get; set; }
        }
    }
}