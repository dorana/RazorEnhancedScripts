using System.Linq;
using RazorEnhanced;

namespace RazorScripts
{
    public class CasterTrain
    {
        public void Run()
        {
            var player = Mobiles.FindBySerial(Player.Serial);
            while (true)
            {
                var skill = Player.GetRealSkillValue("mysticism");
                if (skill >= 100)
                {
                    break;
                }
                if (Player.Mana < 30)
                {
                    if (Player.Buffs.Any(b => b.Contains("edit")))
                    {
                        while (Player.Mana < Player.ManaMax)
                        {
                            Misc.Pause(1000);
                        }
                    }
                    if (!Player.Buffs.Any(b => b.Contains("edit")))
                    {
                        Player.UseSkill("Mediation");
                        Misc.Pause(3000);
                        if (Player.Buffs.Any(b => b.Contains("edit")))
                        {
                            while (Player.Mana < Player.ManaMax)
                            {
                                Misc.Pause(1000);
                            }
                        }
                    }

                }
                if (skill < 60)
                {
                    Spells.CastMysticism("Stone Form");
                    Misc.Pause(4000);
                    continue;
                }

                if (skill < 80)
                {
                    Spells.CastMysticism("Cleansing Winds", player);
                    Misc.Pause(4000);
                    continue;
                }

                if (skill < 95)
                {
                    Spells.CastMysticism("Hail Storm", player);
                    Misc.Pause(4000);
                    continue;
                }
                Spells.CastMysticism("Nether Cyclone", player);
                Misc.Pause(4000);
            }
        }
    }
}