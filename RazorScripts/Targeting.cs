using System;
using System.Collections.Generic;
using System.Linq;
using RazorEnhanced;

namespace RazorScripts
{
    public class Targeting
    {
        private bool _treasureHuntMode = true;

        private Journal _journal = new Journal();
        private Journal.JournalEntry _lastEntry = null;
        public void Run()
        {
            Mobile _player = Mobiles.FindBySerial(Player.Serial);
            if (!Target.HasTarget())
            {
                return;
            }

            if (_treasureHuntMode)
            {

                var lastObjSerial = Target.LastUsedObject();
                var lastObject = Items.FindBySerial(lastObjSerial);
                if (lastObject != null &&
                    lastObject.Name.ToLower().Contains("lockpick"))
                {
                    var chest = Items.FindByName("Treasure Chest", -1, -1, 3);
                    if (chest != null)
                    {
                        Target.TargetExecute(chest);
                    }
                }

                var newEntries = _journal.GetJournalEntry(_lastEntry).OrderBy(j => j.Timestamp).ToList();
                if (newEntries.Any())
                {
                    var systemEntries = newEntries.Where(j =>
                        j.Type.Equals("System", StringComparison.InvariantCultureIgnoreCase)).ToList();
                    DateTime dateTimeBase = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
                    var recent = systemEntries.Where(se => (DateTime.Now - dateTimeBase.AddSeconds(se.Timestamp)).TotalSeconds < 5).ToList();
                        
                    if (recent.Any(j => j.Text.Equals("Which trap will you attempt to disarm?",StringComparison.InvariantCultureIgnoreCase)))
                    {
                        var chest = Items.FindByName("Treasure Chest", -1, -1, 3);
                        if (chest != null)
                        {
                            Target.TargetExecute(chest);
                        }
                    }
                }

            }
            
            if (Target.GetLast() != 0)
            {
                var filter = new Mobiles.Filter
                {
                    IsGhost = 0,
                    RangeMax = 10,
                    RangeMin = 0,
                    Friend = 0,
                    Notorieties = new List<byte>
                    {
                        3, 4, 5, 6
                    },
                    CheckLineOfSight = true
                };

                var tSerial = Target.GetLast();
                
                var mobs = Mobiles.ApplyFilter(filter);
                var tar = mobs.FirstOrDefault(t => t.Serial == tSerial);

                if (tar != null)
                {
                    Target.Last();
                    return;
                }

                if(mobs.Any())
                {
                    var sorted = mobs.OrderBy(m => m.DistanceTo(_player)).ToList();

                    Mobile selected = null;
                    
                    for (int i = 0; i < mobs.Count; i++)
                    {
                        selected = sorted[i];
                        Mobiles.WaitForProps(selected,1000);
                        if (selected.Properties.All(p => p.Number != 1049646))
                        {
                            break;
                        }
                    }

                    if (selected != null)
                    {
                        Target.TargetExecute(selected);
                        Target.SetLast(selected);

                        return;
                    }
                }
                

                if (tSerial != -1 && tSerial != 0 && tSerial != Player.Serial)
                {
                    Target.Last();
                }
                
                Target.Cancel();
            }
        }
    }
}