using System;
using System.Collections.Generic;
using System.Linq;
using RazorEnhanced;

namespace Razorscripts
{
    public class GemGrabber
    {
        public void Run()
        {
            var tarClass = new Target();
            var source = tarClass.PromptTarget("Select bag to clear of gems");
            var targetBag = Player.Backpack.Contains.FirstOrDefault(o => o.Serial == Convert.ToInt32("0x405BF413", 16)) ?? Player.Backpack;
            Organizer.RunOnce("gemsorter", source, targetBag.Serial, 500);
        }
    }
}