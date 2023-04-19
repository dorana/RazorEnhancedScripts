using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using RazorEnhanced;

namespace Razorscripts
{
    public class LootmasterDirectContainer
    {
        public void Run()
        {
            var tar = new Target();
            var source = tar.PromptTarget("Select container to run LootMaster on");
            if (source == -1)
            {
                Misc.SendMessage("No target selected", 201);
                return;
            }
            
            Misc.SetSharedValue("Lootmaster:DirectContainer", source);
        }
    }
}