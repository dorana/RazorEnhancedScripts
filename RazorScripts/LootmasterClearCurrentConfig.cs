using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using RazorEnhanced;

namespace Razorscripts
{
    public class LootmasterClearCurrentConfig
    {
        public void Run()
        {
            Misc.SetSharedValue("Lootmaster:ClearCurrentCharacter", true);
        }
    }
}