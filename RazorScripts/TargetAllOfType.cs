using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using RazorEnhanced;

namespace Razorscripts
{
    public class TargetAllOfType
    {
        public void Run()
        {
            if (!Target.HasTarget())
            {
                Misc.SendMessage("You are not currently waiting for targets",201);
                return;
            }
            var itemSerial = Target.GetLast();
            var targetItem = Player.Backpack.Contains.FirstOrDefault(i => i.Serial == itemSerial);
            if (targetItem != null)
            {
                var all = Player.Backpack.Contains.Where(i => i.ItemID == targetItem.ItemID).ToList();
                foreach (var i in all)
                {
                    Target.TargetExecute(i);
                    Target.WaitForTarget(3000);
                }
            }
        }
    }
}