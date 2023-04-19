using System.Collections.Generic;
using System.Linq;
using RazorEnhanced;

namespace Razorscripts
{
    public class Repeater
    {
        public void Run()
        {
            while (Player.GetSkillValue("Necromancy") < 95)
            {
                Spells.Cast("Wither");
                
                Misc.Pause(3000);

                if (Player.Mana < 25)
                {
                    Player.UseSkill("Meditation");
                    Misc.Pause(15000);
                }
            }
            
        }
    }
}