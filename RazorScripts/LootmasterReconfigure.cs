using System.Collections.Generic;
using System.Linq;
using RazorEnhanced;

namespace Razorscripts
{
    public class LootmasterReconfigure
    {
        public void Run()
        {
            Misc.SetSharedValue("Lootmaster:ReconfigureBags", true);
        }
    }
}