using System;
using System.Globalization;
using System.Linq;
using RazorEnhanced;

namespace Razorscripts
{
    public class HealMaster
    {
        public void Run()
        {
            try
            {
                var journal = new Journal();
                var counter = 0;
                bool retry = false;
                do
                {
                    var lastJournal = journal.GetJournalEntry(null).OrderBy(j => j.Timestamp).LastOrDefault();
                    if (Player.Poisoned)
                    {
                        if (Player.Hits < 15 && Player.GetRealSkillValue("SpiritSpeak") >= 100)
                        {
                            var lastNecro = Misc.ReadSharedValue("HealSelf:LastSS").ToString();
                            //Check if value is not null and is dateTime
                            if (lastNecro != null && lastNecro != "0")
                            {
                                var lastNecroTime = DateTime.Parse(lastNecro, CultureInfo.InvariantCulture);
                                if (lastNecroTime.AddSeconds(5) <= DateTime.Now)
                                {
                                    Misc.SetSharedValue("HealSelf:LastSS", DateTime.Now.ToString(CultureInfo.InvariantCulture));
                                    Player.UseSkill("SpiritSpeak");
                                }
                            }
                            else
                            {
                                Misc.SetSharedValue("HealSelf:LastSS", DateTime.Now.ToString(CultureInfo.InvariantCulture));
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
            }
            catch (Exception e)
            {
                Misc.SendMessage(e);
            }
        }
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
            Misc.SetSharedValue("Lootmaster:Pause", 2000);
            var lastpotstring = Misc.ReadSharedValue("Healmaster:LastPotion").ToString();
            DateTime? lastpot = lastpotstring != "0" ? DateTime.Parse(lastpotstring, CultureInfo.InvariantCulture) : null as DateTime?;
            var curepot = Items.FindByID(3847, 0, Backpack.Serial);
            var bands = Items.FindByID(3617, 0, Backpack.Serial);
            var blockQuickpot = lastpot != null && (DateTime.UtcNow - lastpot).Value.TotalSeconds < 5;
            
            if(!blockQuickpot && curepot != null && Hits < 20)
            {
                Items.UseItem(curepot);
                Misc.SetSharedValue("Healmaster:LastPotion", DateTime.Now.ToString(CultureInfo.InvariantCulture));
            }
            else if (!ForceBands && GetRealSkillValue("Magery") >= 40)
            {
                Spells.CastMagery("Cure", USerial);
            }
            else if (!ForceBands && GetRealSkillValue("Chivalry") >= 40)
            {
                Spells.CastChivalry("Cleanse By Fire", USerial);
            }
            else if(bands != null)
            {
                Items.UseItem(bands, (int)USerial);
            }
            else if (curepot != null)
            {
                Items.UseItem(curepot);
            }
            
        }
        public static void Heal()
        {
            Misc.SetSharedValue("Lootmaster:Pause", 2000);
            var healpot = Items.FindByID(3852, 0, Backpack.Serial);
            var barrabPot = Items.FindByID(3846, 1272, Backpack.Serial);
            var bands = Items.FindByID(3617, 0, Backpack.Serial);
            var lastpotString = Misc.ReadSharedValue("Healmaster:LastPotion").ToString();
            DateTime? lastPot = lastpotString != "0" ? DateTime.Parse(lastpotString) : null as DateTime?;
            var lastBarabPotString = Misc.ReadSharedValue("Healmaster:LastBarrabPotion").ToString();
            DateTime? lastBarabPot = lastpotString != "0" ? DateTime.Parse(lastpotString) : null as DateTime?;
            var blockQuickpot = lastPot != null && (DateTime.UtcNow - lastPot).Value.TotalSeconds < 5;
            var blockBarrabPot = true;//lastPot != null && (DateTime.UtcNow - lastPot).Value.TotalSeconds < 1200;
            
            //Todo, check LRC Value in order to validate if the player might have died and won't have regs, if so check for Spirit Speak Skill to heal
            if(!blockBarrabPot && barrabPot != null && Hits < 40)
            {
                Items.UseItem(healpot);
                Misc.SetSharedValue("Healmaster:LastPotion", DateTime.UtcNow);
            }
            if(!blockBarrabPot && healpot != null && Hits < 20)
            {
                Items.UseItem(healpot);
                Misc.SetSharedValue("Healmaster:LastPotion", DateTime.UtcNow);
            }
            else if (!ForceBands && GetRealSkillValue("Magery") > 30)
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
            else if(bands != null)
            {
                Items.UseItem(bands, (int)USerial);
            }
            else if (healpot != null)
            {
                Items.UseItem(healpot);
                Misc.SetSharedValue("Healmaster:LastPotion", DateTime.UtcNow);
            }
        }
    }
