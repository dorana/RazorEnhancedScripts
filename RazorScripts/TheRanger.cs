using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Forms.VisualStyles;
using RazorEnhanced;

namespace Razorscripts
{
    public class TheRanger
    {
        private uint _gumpId = 788435742;
        private Item Quiver = null;
        private int Checksum = 0;
        private int _lastMap = 0;
        private Item _lastHeldWeapon = null;
        private bool _runAutoReload = false;

        private List<Power> LoadedPowers = new List<Power>
        {
            new Power { Name = "None", Type = PowerType.None, GumpId = 21016 },
            new Power { Name = "Armor Ingore", Type = PowerType.WeaponPrimary, GumpId = 20992 },
            new Power { Name = "Lightening Strike", Type = PowerType.Bushido, GumpId = 21540 },
            new Power { Name = "Momentum Strike", Type = PowerType.Bushido, GumpId = 21541 },
        };
        
        private Power _selectedPower;
        
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
                
                _selectedPower = LoadedPowers[0];
                
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
                    
                    PrimePower();

                    if (_runAutoReload)
                    {
                        if(Quiver.Contains.Sum(i => i.Amount) < 50)
                        {
                            Reload();
                        }
                    }
                    
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
            if (reply.buttonid > 0 && reply.buttonid < 100)
            {
                _selectedPower = LoadedPowers[reply.buttonid - 1];
                UpdateGump();
            }
            else if (reply.buttonid == 100)
            {
                Reload();
                UpdateGump();
            }
            else if(reply.buttonid == 200)
            {
                _runAutoReload = !_runAutoReload;
                UpdateGump();
            }
                    
            reply.buttonid = -1;
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
        
        private bool WeaponUsesBolts(Item item)
        {
            if(item == null)
            {
                return false;
            }
            
            var crossbows = new List<int> { 9923, 3920, 5117 };
            return crossbows.Contains(item.ItemID);
        }

        private void PrimePower()
        {
            if (_selectedPower == null)
            {
                return;
            }
            switch (_selectedPower.Type)
            {
                case PowerType.Bushido:
                    if (!Player.BuffsExist(_selectedPower.Name))
                    {
                        Spells.CastBushido(_selectedPower.Name);
                    }
                    break;
                case PowerType.WeaponPrimary:
                    if (!Player.HasPrimarySpecial)
                    {
                        Player.WeaponPrimarySA();
                    }
                    break;
                case PowerType.WeaponSecondary:
                    if (!Player.HasSecondarySpecial)
                    {
                        Player.WeaponSecondarySA();
                    }
                    break;
                case PowerType.None:
                 default:   
                    break;
            }
        }

        private void UpdateGump()
        {
            var gump = Gumps.CreateGump();
            gump.buttonid = -1;
            // bar.gumpId = ;
            gump.serial = (uint)Player.Serial;
            gump.gumpId = _gumpId;
            gump.x = 500;
            gump.y = 500;
            var width = (LoadedPowers.Count * 60 - 5);
            if(width < 250)
            {
                width = 250;
            }
            UpdateRangerGumpDetails(gump, width);
            UpdateReloaderGumpDetails(gump, width);
            
            Gumps.CloseGump(_gumpId);
            Gumps.SendGump(gump, 500,500);
        }

        private void UpdateRangerGumpDetails(Gumps.GumpData gump, int width)
        {
            
            Gumps.AddBackground(ref gump, 0, 0, width, 55, 1755);
            var powerIndex = 0;
            foreach (var power in LoadedPowers)
            {
                var x = powerIndex * 60 + 5;
                var y = 5;
                Gumps.AddButton(ref gump, x,y,(int)power.GumpId,(int)power.GumpId,LoadedPowers.IndexOf(power)+1,1,0);
                Gumps.AddTooltip(ref gump, power.Name);
                powerIndex++;
            }

            if (_selectedPower != null)
            {
                var index = LoadedPowers.IndexOf(_selectedPower);
                Gumps.AddImage(ref gump, (60 * index-17),0,30071);
            }
        }

        private void UpdateReloaderGumpDetails(Gumps.GumpData gump, int width)
        {
            var baseY = 55;
            var ammoArrows = Quiver.Contains.Where(i => i.ItemID == 3903).Sum(i => i.Amount);
            var ammoBolts = Quiver.Contains.Where(i => i.ItemID == 7163).Sum(i => i.Amount);
            var ammoTextArrows = $"Arrows: {ammoArrows} / 500";
            var ammoTextBolts = $"Bolts: {ammoBolts} / 500";
            var defaultText = "No Ammo";
            var height = 50;
            gump.gumpId = _gumpId;
            gump.serial = (uint)Player.Serial;
            Gumps.AddBackground(ref gump,0,baseY,width,height,1755);

            if (ammoArrows > 0)
            {
                Gumps.AddLabel(ref gump, 15, baseY+15, 0x7b, ammoTextArrows);
            }

            if (ammoBolts > 0)
            {
                Gumps.AddLabel(ref gump, 15, baseY+15, 0x7b, ammoTextBolts);
            }

            if (ammoBolts == 0 && ammoArrows == 0)
            {
                Gumps.AddLabel(ref gump, 15, baseY+15, 0x7b, defaultText);
            }

            var buttonId = _runAutoReload ? 9027 : 9026;
            
            Gumps.AddButton(ref gump, width-105, baseY+15 , buttonId,buttonId,200,1,1);
            
            Gumps.AddButton(ref gump, width-80,baseY+15,40018,40018,1,1,1);
            Gumps.AddLabel(ref gump, width-65,baseY+15,0x481, "Reload");
        }
        
        
        private class Power
        {
            public string Name { get; set; }
            public PowerType Type { get; set; }
            public int GumpId { get; set; }
        }
    
        private enum PowerType
        {
            WeaponPrimary,
            WeaponSecondary,
            Bushido,
            None
        }
    }
}