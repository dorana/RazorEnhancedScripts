using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using RazorEnhanced;

namespace Razorscripts
{
    public class ChestBreaker
    {
        Journal _journal = new Journal();
        private Target _tar = new Target();

        public void Run()
        {
            var lastEntry = _journal.GetJournalEntry(null).LastOrDefault();
            Misc.SendMessage(lastEntry.Text);
            var chestSerial = _tar.PromptTarget("Select Treasure Chest to break open");
            if (chestSerial == 0)
            {
                Misc.SendMessage("No target selected", 201);
                return;
            }

            var lockPicks = DigDeep(Player.Backpack,Convert.ToInt32("0x14FC", 16));
            if (lockPicks == null)
            {
                Misc.SendMessage("Unable to find Lock Picks", 201);
            }
            
            
            lastEntry = _journal.GetJournalEntry(lastEntry).LastOrDefault();

            while (!_journal.GetJournalEntry(lastEntry).Any(j => (j.Text.ToLower().Contains("the lock quickly yields to your skill") || j.Text.ToLower().Contains("this does not appear to be locked"))))
            {
                Items.UseItem(lockPicks);
                Target.WaitForTarget(1000);
                Target.TargetExecute(chestSerial);
                Misc.Pause(1500);
            }


            lastEntry = _journal.GetJournalEntry(lastEntry).OrderBy(j => j.Timestamp).LastOrDefault();
            
            while(!_journal.GetJournalEntry(lastEntry).Any(j => j.Text.ToLower().Contains("you successfully disarm the trap")))
            {
                if (_journal.GetJournalEntry(lastEntry).Any(j => j.Type == "System" &&  j.Text.Contains("You must wait")))
                {
                    Misc.Pause(1000);
                    lastEntry = _journal.GetJournalEntry(lastEntry).LastOrDefault();
                    continue;
                }
                
                Player.UseSkill("Remove Trap");
                Target.WaitForTarget(3000);
                Target.TargetExecute(chestSerial);
                Misc.Pause(11250);
            }
        }
        
        
        private Item DigDeep(Item container, int itemId)
        {
            var found = container.Contains.FirstOrDefault(i => i.ItemID == itemId);
            if (found != null)
            {
                return found;
            }
            
            var subContainers = container.Contains.Where(c => c.IsContainer && c.Contains.Any() && c.Contains.First().Name != " (0000)").ToList();
            foreach (var subcont in subContainers)
            {
                return DigDeep(subcont, itemId);
            }

            return null;
        }
    }
}