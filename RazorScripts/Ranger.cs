using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Forms.VisualStyles;
using RazorEnhanced;

namespace Razorscripts
{
    public class Ranger
    {
        private uint _gumpId = 788435742;

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
                UpdateGump();
                while (Player.Connected)
                {
                    var reply = Gumps.GetGumpData(_gumpId);
                    if (reply.buttonid > 0)
                    {
                        _selectedPower = LoadedPowers[reply.buttonid - 1];
                        UpdateGump();
                        
                        reply.buttonid = -1;
                    }

                    PrimePower();
                    
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
            var bar = Gumps.CreateGump();
            bar.buttonid = -1;
            // bar.gumpId = ;
            bar.serial = (uint)Player.Serial;
            bar.gumpId = _gumpId;
            bar.x = 500;
            bar.y = 500;
            var powerIndex = 0;
            Gumps.AddBackground(ref bar, 0, 0, (LoadedPowers.Count*60-5), 55, 1755);
            foreach (var power in LoadedPowers)
            {
                var x = powerIndex * 60 + 5;
                var y = 5;
                Gumps.AddButton(ref bar, x,y,(int)power.GumpId,(int)power.GumpId,LoadedPowers.IndexOf(power)+1,1,0);
                Gumps.AddTooltip(ref bar, power.Name);
                powerIndex++;
            }

            if (_selectedPower != null)
            {
                var index = LoadedPowers.IndexOf(_selectedPower);
                Gumps.AddImage(ref bar, (60 * index-17),0,30071);
            }
            
            Gumps.CloseGump(_gumpId);
            Gumps.SendGump(bar, 500,500);
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