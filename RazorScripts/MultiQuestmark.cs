using System.Collections.Generic;
using System.Linq;
using RazorEnhanced;

namespace Razorscripts
{
    public class MultiQuestmark
    {
        public void Run()
        {
            var old = Target.GetLast();
            Target.ClearLast();
            Misc.WaitForContext(0x000661CE, 10000);
            Misc.ContextReply(0x000661CE, 7);
            
            for (var i = 0; i <= 200; i++)
            {
                if (Target.GetLast() != 0)
                {
                    break;
                }
                Misc.Pause(50);
            }

            if (Target.GetLast() == 0)
            {
                Misc.SendMessage("No item selected, aborting script",201);
                Target.Cancel();
                return;
            }
            
            
            var itemSerial = Target.GetLast();
            var targetItem = Player.Backpack.Contains.FirstOrDefault(i => i.Serial == itemSerial);
            
            if (targetItem != null)
            {
                var alreadyMarked = targetItem.Properties.Any(p => p.ToString() == "Quest Item");

                var all = Player.Backpack.Contains.Where(i=> i.ItemID == targetItem.ItemID && i.Serial != itemSerial).ToList();

                var filtered = new List<Item>();
                if (alreadyMarked)
                {
                    filtered = all.Where(i => i.Properties.Any(p => p.ToString() == "Quest Item")).ToList();
                }
                else
                {
                    filtered = all.Where(i => i.Properties.All(p => p.ToString() != "Quest Item")).ToList();
                }
                
                Misc.Pause(100);
                
                foreach (var i in filtered)
                {
                    Target.WaitForTarget(3000);
                    Target.TargetExecute(i);
                }
            }
            
            Misc.SendMessage("Multi Toggle Complete, any further clicks will toggle only selected Item");
        }
    }
}