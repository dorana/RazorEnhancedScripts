using System;
using System.Collections.Generic;
using System.Linq;
using RazorEnhanced;

namespace Razorscripts
{
    public class Honoring
    {
        public void Run()
        {
            var last = Target.GetLast();
            
            var target = PickTarget();
            if (target == null)
            {
                Misc.SendMessage("No target found");
                Target.SetLast(last);
                return;
            }
            InvokeHonor(target);
            Attack(target);
        }

        private void InvokeHonor(Mobile target)
        {
            Player.InvokeVirtue("Honor");
            Target.WaitForTarget(3000, true);
            Target.TargetExecute(target);
        }

        private void Attack(Mobile target)
        {
            if (Player.HasSecondarySpecial)
            {
                Player.WeaponSecondarySA();
            }

            Misc.Pause(300);
            Player.Attack(target);
            Target.SetLast(target);
        }

        private Mobile PickTarget()
        {
            var filter = new Mobiles.Filter
            {
                IsGhost = 0,
                RangeMax = 10,
                RangeMin = 0,
                Friend = 0,
                Notorieties = new List<byte>
                {
                    3, 4, 5, 6
                },
                CheckLineOfSight = true
            };

            var mobs = Mobiles.ApplyFilter(filter);
            var orderedMobs = mobs.OrderBy(m => m.DistanceTo(Mobiles.FindBySerial(Player.Serial))).ToList();
            
            foreach (var mob in orderedMobs)
            {
                Mobiles.WaitForProps(mob,1000);
                if (mob.Properties.Any(p => p.Number == 1049646))
                {
                    continue;
                }

                return mob;
            }

            return null;
        }
    }
}