using System.Collections.Generic;
using System.Linq;
using RazorEnhanced;

namespace Razorscripts
{
    public class Reloader
    {
        private List<int> _crossbows = new List<int> { 9923, 3920, 5117 };


        public void Run()
        {
            var quiver= Player.Quiver;
            if (quiver == null)
            {
                var backup = Player.GetItemOnLayer("Cloak");
                if (backup != null && backup.Name.ToLower().Contains("quiver"))
                {
                    quiver = backup;
                }
                else
                {
                    Player.HeadMessage(0x23,"You do not have a quiver equipped");
                    return;
                }
                
                Items.WaitForProps(quiver,1000);
            }
            
            var wep = Player.GetItemOnLayer("LeftHand");
            if (wep != null)
            {
                Items.WaitForProps(wep,1000);
                var archerWep = wep.Properties.Any(p => p.ToString().Contains("archery"));
                if (archerWep)
                {
                    var availableSpace = 500 - quiver.Contains.Sum(i => i.Amount);
                    var ammoId = _crossbows.Contains(wep.ItemID) ? 7163 : 3903;
                    var ammo = Player.Backpack.Contains.FirstOrDefault(i => i.ItemID == ammoId && i.Amount > 0);
                    if (ammo != null)
                    {
                        Items.WaitForProps(ammo,1000);
                        var reloadAmount = ammo.Amount;
                        if (reloadAmount > availableSpace)
                        {
                            reloadAmount = availableSpace;
                        }

                        if (reloadAmount == 0)
                        {
                            
                            Player.HeadMessage(0x58,$"You are already full on {(ammoId == 7163 ? "Bolts" : "Arrows")}");
                            return;
                        }
                        
                        Player.HeadMessage(0x58,$"Reloading {reloadAmount} {(ammoId == 7163 ? "Bolts" : "Arrows")}");
                        Items.Move(ammo, quiver, reloadAmount);
                    }
                    else
                    {
                        Player.HeadMessage(0x23,"No Ammo in Backpack");
                    }
                }
                else
                {
                    Player.HeadMessage(0x23,"No Archery Weapon Equipped");
                }
            }
            else
            {
                Player.HeadMessage(0x23,"No Weapon Equipped");
            }
        }
    }
}