using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using RazorEnhanced;

namespace Razorscripts
{
    public class Reloader
    {
        private Item Quiver = null;
        private uint _gumpId = 32315123;
        private int Checksum = 0;
        private int _lastMap = 0;
        private Item _lastHeldWeapon = null;

        public void Run()
        {
            try
            {
                var success = RefreshQuiver();
                if (!success)
                {
                    Player.HeadMessage(0x23, "You do not have a quiver equipped");
                    return;
                }
                
                
                _lastHeldWeapon = Player.GetItemOnLayer("LeftHand");
                _lastMap = Player.Map;
                var a = Quiver.Contains.Where(i => i.ItemID == 3903).Sum(i => i.Amount);
                var b = Quiver.Contains.Where(i => i.ItemID == 7163).Sum(i => i.Amount);
                Checksum = a+b*1000;

                // Reload();
                UpdateGump();

                while (Player.Connected)
                {
                    var arrows = Quiver.Contains.Where(i => i.ItemID == 3903).Sum(i => i.Amount);
                    var bolts = Quiver.Contains.Where(i => i.ItemID == 7163).Sum(i => i.Amount);
                    var ammoChecksum = arrows+bolts*1000;
                    if (ammoChecksum != Checksum)
                    {
                        Checksum = ammoChecksum;
                        UpdateGump();
                    }

                    if (Player.Map != _lastMap)
                    {
                        _lastMap = Player.Map;
                        RefreshQuiver();
                    }

                    var held = Player.GetItemOnLayer("LeftHand");
                    if (held != null && held != _lastHeldWeapon )
                    {
                        if (WeaponUsesBolts(held) != WeaponUsesBolts(_lastHeldWeapon))
                        {
                            Reload();
                        }
                        _lastHeldWeapon = held;
                    }

                    var sharedValue = Misc.ReadSharedValue("Reloader:Ammo") as int?;
                    if (sharedValue != null && sharedValue.Value != 0)
                    {
                        var ammoTypeInQuiver = Quiver.Contains.FirstOrDefault();
                        if (sharedValue != ammoTypeInQuiver?.ItemID)
                        {
                            Reload();
                        }
                        Misc.SetSharedValue("Reloader:Ammo", 0);
                    }
                    
                    HandleReply();
                    
                    Misc.Pause(1000);
                }
            }
            catch (ThreadAbortException)
            {
                //Silent
            }
            catch (Exception e)
            {
                Misc.SendMessage(e);
                throw;
            }
            finally
            {
                Gumps.CloseGump(_gumpId);
            }
        }

        private void HandleReply()
        {
            
                    
            var reply = Gumps.GetGumpData(_gumpId);
            if (reply.buttonid > 0)
            {
                if (reply.buttonid == 1)
                {
                    Reload();
                    UpdateGump();
                }

                reply.buttonid = -1;
            }
        }


        private bool WeaponUsesBolts(Item item)
        {
            if(item == null)
            {
                return false;
            }
            
            var crossbows = new List<int> { 9923, 3920, 5117 };
            return crossbows.Contains(item.ItemID);
        }

        private void UpdateGump()
        {
            var gump = Gumps.CreateGump();
            var ammoArrows = Quiver.Contains.Where(i => i.ItemID == 3903).Sum(i => i.Amount);
            var ammoBolts = Quiver.Contains.Where(i => i.ItemID == 7163).Sum(i => i.Amount);
            var ammoTextArrows = $"Arrows: {ammoArrows} / 500";
            var ammoTextBolts = $"Bolts: {ammoBolts} / 500";
            var defaultText = "No Ammo";
            var height = 50;
            gump.gumpId = _gumpId;
            gump.serial = (uint)Player.Serial;
            Gumps.AddBackground(ref gump,0,0,300,height,1755);

            if (ammoArrows > 0)
            {
                Gumps.AddLabel(ref gump, 15, 15, 0x7b, ammoTextArrows);
            }

            if (ammoBolts > 0)
            {
                Gumps.AddLabel(ref gump, 15, 15, 0x7b, ammoTextBolts);
            }

            if (ammoBolts == 0 && ammoArrows == 0)
            {
                Gumps.AddLabel(ref gump, 15, 15, 0x7b, defaultText);
            }
            
            Gumps.AddButton(ref gump, 200,15,40018,40018,1,1,1);
            Gumps.AddLabel(ref gump, 215,15,0x481, "Reload");

            Gumps.CloseGump(_gumpId);
            Gumps.SendGump(gump,500,500);
        }

        private void Reload()
        {
            RefreshQuiver();
            var wep = Player.GetItemOnLayer("LeftHand");
            if (wep != null)
            {
                Items.WaitForProps(wep,1000);
                var archerWep = wep.Properties.Any(p => p.ToString().Contains("archery"));
                if (archerWep)
                {
                    var availableSpace = 500 - Quiver.Contains.Sum(i => i.Amount);
                    var ammoId = WeaponUsesBolts(wep) ? 7163 : 3903;
                    var ammoInQuiver = Quiver.Contains.FirstOrDefault();
                    if (ammoInQuiver != null && ammoInQuiver.ItemID != ammoId)
                    {
                        Items.Move(ammoInQuiver, Player.Backpack, ammoInQuiver.Amount);
                        availableSpace = 500;
                        Misc.Pause(350);
                    }
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
                        Items.Move(ammo, Quiver, reloadAmount);
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

        private bool RefreshQuiver()
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
                    return false;
                }
                
                Items.WaitForProps(quiver,1000);
                Items.WaitForContents(Quiver, 500);
            }

            Quiver = quiver;
            
            return true;
        }
    }
}