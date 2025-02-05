using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using Assistant;
using RazorEnhanced;
using Item = RazorEnhanced.Item;

namespace Razorscripts
{
    public class MultiQuestmark
    {
        public void Run()
        {
            var player = Mobiles.FindBySerial(Player.Serial);
            Target.ClearLast();
            var rows = Misc.WaitForContext(Player.Serial, 10000);
            foreach (var row in rows)
            {
                if (row.Entry.Equals("Toggle Quest Item", StringComparison.InvariantCultureIgnoreCase))
                {
                    Misc.ContextReply(Player.Serial, rows.IndexOf(row));
                    break;
                }
            }
            
            
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