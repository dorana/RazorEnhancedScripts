using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using RazorEnhanced;

namespace RazorScripts
{
    public class Targeting
    {
        public void Run()
        {
            
            Mobile _player = Mobiles.FindBySerial(Player.Serial);
            if (!Target.HasTarget())
            {
                return;
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
                };
                
                
                var mobs = Mobiles.ApplyFilter(filter);
                var tar = mobs.FirstOrDefault(t => t.Serial == Target.GetLast());

                if (tar != null)
                {
                    Target.Last();
                    return;
                }

                if(mobs.Any())
                {
                    var sorted = mobs.OrderBy(m => m.DistanceTo(_player)).ToList();
                    Target.TargetExecute(sorted.First());
                    Target.SetLast(mobs.First());
                    return;
                }
                
                Target.Cancel();
                
                
            }
        }
    }
}