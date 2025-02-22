using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms.VisualStyles;
using RazorEnhanced;

namespace Razorscripts
{
    public class MineNode
    {
        private List<int> _tools = new List<int>
        {
            0x0E86,
            0x0F39
        };
        private Journal _journal = new Journal();
        private Journal.JournalEntry _journalEntry = null;
        
        private int _firePetSerial = 0x026888DD; // set to fire beetle or fire fox serial, if you do not have one set to 0
        private int _packPetSerial = 0; // set to fire beetle or fire fox serial, if you do not have one set to 0

        private List<int> directSmelts = new List<int>
        {
            0x19B8,
            0x19BA,
            0x19B9
        };
        private List<int> needs2Smelts = new List<int>
        {
            0x19B7
        };
        
        public void Run()
        {
            _journalEntry = _journal.GetJournalEntry(_journalEntry).OrderBy(je => je.Timestamp).LastOrDefault();
            while (true)
            {
                if(Player.Weight > Player.MaxWeight - 50)
                {
                    if(_firePetSerial == 0)
                    {
                        if (Player.Weight >= Player.MaxWeight)
                        {
                            Player.HeadMessage(33, "Backpack is full, Stopping");
                            return;
                        }
                    }
                    else
                    {
                        Smelt();
                    }
                    
                    if(_packPetSerial != 0)
                    {
                        var ores = Player.Backpack.Contains.Where(i => i.Name.ToLower().Contains("ore")).ToList();
                        var ingots = Player.Backpack.Contains.Where(i => i.ItemID == 0x1BF2).ToList();

                        var combined = ores.Concat(ingots).ToList();
                        
                        foreach (var item in combined)
                        {
                            Items.Move(item, _packPetSerial, item.Amount);
                            Misc.Pause(200);
                        }
                    }
                    
                }
                
                var tool = Player.Backpack.Contains.FirstOrDefault(i => _tools.Contains(i.ItemID));
                if (tool == null)
                {
                    Player.HeadMessage(33, "No tool found");
                    return;
                }
                Target.TargetResource(tool, "ore");
                var newLines = _journal.GetJournalEntry(_journalEntry);
                if (newLines.Any(j => j.Text.Contains("There is no metal here to mine")))
                {
                    return;
                }
                Misc.Pause(600);
            }
        }

        private void Smelt()
        {
            var potentialSmeltings = Player.Backpack.Contains.Where(i => i.Name.ToLower().Contains("ore")).ToList();

            foreach (var ps in potentialSmeltings)
            {
                if (directSmelts.Contains(ps.ItemID))
                {
                    Items.UseItem(ps);
                    Target.WaitForTarget(1000);
                    Target.TargetExecute(_firePetSerial);
                    Misc.Pause(500);
                    continue;
                }
                if(needs2Smelts.Contains(ps.ItemID))
                {
                    if (ps.Amount >= 2)
                    {
                        Items.UseItem(ps);
                        Target.WaitForTarget(1000);
                        Target.TargetExecute(_firePetSerial);
                        Misc.Pause(500);
                        continue;
                    }
                }
            }
        }
    }
}