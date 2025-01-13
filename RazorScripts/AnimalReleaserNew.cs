using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RazorEnhanced;

namespace RazorScripts
{
    public class AnimalReleaser
    {
        Journal _journal = new Journal();
        public void Run()
        {
            Journal.JournalEntry _lastEntry = null;

            try
            {
                while (true)
                {
                    var preTame = Mobiles.ApplyFilter(new Mobiles.Filter
                    {
                        RangeMax = 12,
                        RangeMin = 0,
                        Notorieties = new List<byte> { 1,2 },
                    });

                    
                    var rows = _journal.GetJournalEntry(_lastEntry).OrderBy(j => j.Timestamp).ToList();
                    if(rows.Any(j => j.Text.Equals("It seems to accept you as master.", StringComparison.InvariantCultureIgnoreCase) && j.Type.Equals("Regular", StringComparison.InvariantCultureIgnoreCase)))
                    {
                        Misc.SendMessage("Animal Tamed");
                        
                        var postTame = Mobiles.ApplyFilter(new Mobiles.Filter
                        {
                            RangeMax = 12,
                            RangeMin = 0,
                            Notorieties = new List<byte> { 1,2,3,4,5,6 },
                        });
                        
                        var newStuff = postTame.Where(p => !preTame.Any(p2 => p2.Serial == p.Serial)).ToList();
                        
                        postTame.ForEach(p => Mobiles.WaitForProps(p, 500));
                        var filterred = newStuff.Where(m => m.Properties.Any(p => p.Number == 502006)).ToList();
                        
                        //get all new animals
                        
                        Misc.SendMessage(filterred.Count());
                        foreach (var animal in filterred)
                        {
                            Misc.WaitForContext(animal, 500);
                            Misc.ContextReply(animal, 5);
                            Misc.Pause(200);
                            Gumps.SendAction(814628313,2);
                            Misc.Pause(200);
                        }
                        
                        
                        // var found = Gumps.WaitForGump( 814628313, 5000);
                        // if (found)
                        // {
                        //     Gumps.SendAction(0x308e3dd9,2);
                        // }
                    }
                    if (rows.Any())
                    {
                        _lastEntry = rows.Last();
                    }
                
                    Misc.Pause(1000);
                }
            }
            catch(Exception e)
            {
                Misc.SendMessage(e);
            }
            
        }
    }
}