using System;
using System.Linq;
using System.Text.RegularExpressions;
using RazorEnhanced;

namespace RazorScripts
{
    public class MountUp
    {
        public void Run()
        {
            //&Check for bonded bet 1049608
            var found = Mobiles.ApplyFilter(new Mobiles.Filter
            {
                Notorieties = { 1, 2 },
                RangeMax = 2,
            }).ToList();

            foreach (var mob in found)
            {
                Mobiles.WaitForProps(mob, 1000);
            }
            
            var bonded = found.Where(m => m.Properties.Any(p => p.Number == 1049608));
            if (bonded.Any())
            {
                foreach (var mobile in bonded)
                {
                    var isMine = mobile.CanRename;
                    if (mobile.Backpack == null && isMine)
                    {
                        Mobiles.UseMobile(mobile);
                        return;
                    }
                }
            }
            
            
            var regex = new Regex(@"\bEthereal\b.*\bStatuette\b|\bStatuette\b.*\bEthereal\b", RegexOptions.IgnoreCase);

            var mount = Player.Backpack.Contains.FirstOrDefault(i => regex.Match(i.Name).Success);
            if (mount == null)
            {
                return;
            }

            Items.UseItem(mount);
        }
    }
}