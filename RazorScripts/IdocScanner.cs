using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using RazorEnhanced;

namespace RazorScripts
{
    public class IdocScanner
    {
        private List<Item> idocSerialCache = new List<Item>();
        private uint _guildGumpId = 1345112424;
        private List<Tracker> _trackings = new List<Tracker>();
        
        public void Run()
        {
            try
            {
                UpdateGump();
                while (Player.Connected)
                {
                    List<Item> idocHouses = new List<Item>();
                    var signs = Items.ApplyFilter(new Items.Filter
                    {
                        RangeMax = 100,
                        RangeMin = 0,
                        Name = "A House Sign"
                    }).ToList();

                    foreach (var sign in signs)
                    {
                        Items.WaitForProps(sign, 2000);
                        var conditionProp = sign.Properties.FirstOrDefault(p => p.Number == 1062028);
                        if (conditionProp?.Args == "#1043015")
                        {
                            idocHouses.Add(sign);
                        }
                    }

                    var cachevalue = idocSerialCache.Sum(i => i.Serial);
                    var newvalue = idocHouses.Sum(i => i.Serial);
                    if (cachevalue != newvalue)
                    {
                        idocSerialCache = idocHouses;
                        UpdateGump();
                        foreach (var arrow in _trackings)
                        {
                            Player.TrackingArrow((ushort)arrow.X, (ushort)arrow.Y, false);
                        }
                        foreach (var idoc in idocSerialCache)
                        {
                            _trackings.Add(new Tracker
                            {
                                X = idoc.Position.X+2,
                                Y = idoc.Position.Y+2,
                                Serial = idoc.Serial
                            });
                            Player.TrackingArrow((ushort)(idoc.Position.X+2), (ushort)(idoc.Position.Y+2), true);
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
                Gumps.CloseGump(_guildGumpId);
            }
            
        }

        private void UpdateGump()
        {
            var gump = Gumps.CreateGump();
            gump.gumpId = _guildGumpId;
            gump.serial = (uint)Player.Serial;
            var height = idocSerialCache.Count*75;
            Gumps.AddImage(ref gump, 0, 0,  1764);
            Gumps.AddBackground(ref gump, 0, 49, 205, height, 1755);
            Gumps.AddLabel(ref gump, 60, 18, 400, "IDOC Scanner");
            foreach (var sign in idocSerialCache)
            {
                Gumps.AddItem(ref gump, 5, 60 + idocSerialCache.IndexOf(sign) * 50, sign.ItemID);
                Gumps.AddLabel(ref gump, 40, 60 + idocSerialCache.IndexOf(sign) * 50, 0x7b, sign.Properties.FirstOrDefault(s => s.Number == 1061639)?.Args ?? "Unknown");
            }
            Gumps.CloseGump(_guildGumpId);
            Gumps.SendGump(gump,500,500);
        }

        internal class Tracker
        {
            public int X { get; set; }
            public int Y { get; set; }
            public int Serial { get; set; }
        }
    }
}