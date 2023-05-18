using System;
using System.Linq;
using RazorEnhanced;
namespace Razorscripts
{
    public class HealSelf
    {
        public void Run()
        {
            var journal = new Journal();
            var counter = 0;
            bool retry =false;
            do
            {
                var lastJournal = journal.GetJournalEntry(null).OrderBy(j => j.Timestamp).LastOrDefault();

               

                if (Player.Poisoned)
                {
                    if (Player.Hits < 15 && Player.GetRealSkillValue("SpiritSpeak") >= 100)
                    {
                        var lastNecro = Misc.ReadSharedValue("HealSelf:LastSS");
                        //Check if value is not null and is dateTime
                        if (lastNecro != null && lastNecro is DateTime)
                        {
                            var lastNecroTime = (DateTime) lastNecro;
                            if (lastNecroTime.AddSeconds(5) <= DateTime.Now)
                            {
                                Misc.SetSharedValue("HealSelf:LastSS", DateTime.Now);
                                Player.UseSkill("SpiritSpeak");
                            }
                        }
                        else
                        {
                            Misc.SetSharedValue("HealSelf:LastSS", DateTime.Now);
                            Player.UseSkill("SpiritSpeak");
                        }
                    }
                    
                    Self.Cure();
                }
                else
                {
                    if (Self.NeedHealing)
                    {
                        Self.Heal();
                    }
                    else
                    {
                        Helper.Log("You are already at full health");
                        break;
                    }
                }

                if (journal.GetJournalEntry(lastJournal).Any(j => j.Text.ToLower().Contains("you must wait")))
                {
                    retry = true;
                    Misc.Pause(100);
                }
            } while (retry && counter++ < 5);
            
            Misc.Pause(300);
            Misc.RemoveSharedValue("Lootmaster:Pause");
        }
    }


    internal static class Helper
    {
        public static void Log(object messageString)
        {
            Misc.SendMessage(messageString, 201);
        }
    }

    public class Self : Player
    {
        private static uint USerial => (uint)Serial;
        public static bool NeedHealing => HitsMax > Hits; 
        public static readonly bool ForceBands = GetRealSkillValue("Healing") >= 80;
        public static void Cure()
        {
            
            Misc.SetSharedValue("Lootmaster:Pause", true);
            
            if (!ForceBands && GetRealSkillValue("Magery") >= 40)
            {
                Spells.CastMagery("Cure", USerial);
            }
            else if (!ForceBands && GetRealSkillValue("Chivalry") >= 40)
            {
                Spells.CastChivalry("Cleanse By Fire", USerial);
            }
            else
            {
                Items.UseItemByID(3617);
                Target.WaitForTarget(2000);
                Target.Self();
            }
            
        }
        public static void Heal()
        {
            Misc.SetSharedValue("Lootmaster:Pause", true);
            //Todo, check LRC Value in order to validate if the player might have died and won't have regs, if so check for Spirit Speak Skill to heal
            if (!ForceBands && GetRealSkillValue("Magery") > 30)
            {
                if (GetRealSkillValue("Magery") > 60 && HitsMax - Hits > 20)
                {
                    Spells.CastMagery("Greater Heal", USerial);
                }
                
                else
                {
                    Spells.CastMagery("Heal", USerial);
                }
                
            }
            else if (!ForceBands && GetRealSkillValue("Chivalry") > 60 && HitsMax - Hits > 20)
            {
                Spells.CastChivalry("Close Wounds", USerial);
            }
            else
            {
                var bands = Items.FindByID(3617, 0, Backpack.Serial);
                Items.UseItemByID(3617);
                Target.WaitForTarget(2000);
                Target.Self();
            }
        }
    }
}