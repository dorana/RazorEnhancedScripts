using System;
using System.Collections.Generic;
using System.Linq;
using RazorEnhanced;

namespace RazorScripts
{
    public class TeleTalk
    {
        private List<TriggerTalk> Triggers = new List<TriggerTalk>
        {
            new TriggerTalk
            {
                Trigger = "beam me up",
                Script = "recall.py"
            },
            new TriggerTalk
            {
                Trigger = "take me home",
                Script = "gatehome.py"
            },
        };

        public void Run()
        {
            Journal jo = new Journal();
            Journal.JournalEntry lastEntry = null;

            try
            {
                while (true)
                {
                    var entries = jo.GetJournalEntry(lastEntry).OrderBy(j => j.Timestamp).ToList();
                    //Find first match between triggers and journal entries and run the script
                    foreach (var trigger in Triggers)
                    {
                        var entry = entries.FirstOrDefault(e => e.Text.Equals(trigger.Trigger, StringComparison.OrdinalIgnoreCase) && e.Name == Player.Name);
                        if (entry != null)
                        {
                            Misc.ScriptRun(trigger.Script);
                            Misc.Pause(3000);
                        }
                    }

                    lastEntry = entries.Last();
                    Misc.Pause(500);
                }
            }
            catch (Exception e)
            {
                Misc.SendMessage("Something went wrong, and the script has stopped");
                Misc.SendMessage(e.ToString());
            }
        }
    }
    
    public class TriggerTalk
    {
        public string Trigger { get; set; }
        public string Script { get; set; }
    }
}