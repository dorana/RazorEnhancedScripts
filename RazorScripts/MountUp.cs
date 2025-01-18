using System.Linq;
using System.Text.RegularExpressions;
using RazorEnhanced;

namespace RazorScripts
{
    public class MountUp
    {
        public void Run()
        {
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