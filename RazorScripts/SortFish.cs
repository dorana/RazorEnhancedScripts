using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using RazorEnhanced;

namespace Razorscripts
{
    public class SortFish
    {
        public void Run()
        {
            var tar = new Target();
            var targetSerial = tar.PromptTarget("Select Container to Sort");
            Misc.SetSharedValue("Lootmaster:DirectContainerRule", "Fish Sorting");
            Misc.SetSharedValue("Lootmaster:DirectContainer", targetSerial);
        }
        
        
    }
}