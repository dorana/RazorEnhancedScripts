using System.Collections.Generic;
using System.Linq;
using RazorEnhanced;

namespace Razorscripts
{
    public class GuardMe
    {
        public void Run()
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
                var sumProp = mob.Properties.FirstOrDefault(p => p.Number == 1049646);
                if (sumProp == null || !sumProp.ToString().Contains("summoned")) continue;
                Misc.WaitForContext(mob, 500);
                Misc.ContextReply(mob, 2);
            }
        }
    }
}