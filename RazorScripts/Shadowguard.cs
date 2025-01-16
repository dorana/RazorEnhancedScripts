//Thank you for using this script, I hope it helps you in your adventures.
// Best Regards, Dorana

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using RazorEnhanced;

namespace RazorScripts
{
    public class Shadowguard
    {
        private List<int> philactories = new List<int>
        {
            0x4686,
            0x42B4
        };

        private List<int> philHuesCorrupted = new List<int>
        {
            0x081B,
            0x0ac0
        };

        private List<int> philHuesPure = new List<int>
        {
            0x048e,
            0x0000
        };

        private List<string> _virtues = new List<string>
        {
            "compassion",
            "honesty",
            "honor",
            "humility",
            "justice",
            "sacrifice",
            "spirituality",
            "valor",
        };

        private List<string> _dungeons = new List<string>
        {
            "despise",
            "deceit",
            "shame",
            "pride",
            "wrong",
            "covetous",
            "hythloth",
            "destard",
        };

        private List<int> _canalPieces = new List<int>
        {
            0x9BEF,
            0x9BF4,
            0x9BEB,
            0x9BF8,
            0x9BE7,
            0x9BFC
        };

        //Dictionary of 4 paths indexed 1-4 each witha value like _puzzleLocations

        private Dictionary<int, Dictionary<int, List<Point>>> _puzzlePathLocations =
            new Dictionary<int, Dictionary<int, List<Point>>>();


        private Dictionary<int, List<Point>> GetTemplate()
        {
            return new Dictionary<int, List<Point>>
            {
                {
                    0x9BEF, new List<Point>()
                },
                {
                    0x9BF4, new List<Point>()
                },
                {
                    0x9BEB, new List<Point>()
                },
                {
                    0x9BF8, new List<Point>()
                },
                {
                    0x9BE7, new List<Point>()
                },
                {
                    0x9BFC, new List<Point>()
                },
            };
        }

        //Spirrog Starts
        //1 :(90, 2024, -20)
        //2 :(101, 2024, -20)
        //3 : (104, 2021, -20)
        //4 :(104, 2010, -20)
        //< 0x9BEF
        //^ 0x9BFC
        //> 0x9BF8
        //V 0x9BEB
        // \ 0x9BF4


        private Mobile _player;

        private Dictionary<ShadowGuardRoom, bool> rooms = new Dictionary<ShadowGuardRoom, bool>
        {
            { ShadowGuardRoom.Bar, false },
            { ShadowGuardRoom.Orchard, false },
            { ShadowGuardRoom.Armory, false },
            { ShadowGuardRoom.Belfry, false },
            { ShadowGuardRoom.Fountain, false },
            { ShadowGuardRoom.Lobby, false },
            { ShadowGuardRoom.Roof, false },
        };

        public void Run()
        {
            try
            {
                _player = Mobiles.FindBySerial(Player.Serial);
                //Figure out what room we're in

                while (true)
                {
                    HandleRoom(GetCurrentRoom());
                    Misc.Pause(500);
                }
            }
            catch (ThreadAbortException)
            {
                //Silent
            }
            catch (Exception e)
            {
                Misc.SendMessage(e.ToString());
                throw;
            }

        }

        private void UpdateShadowGuardGump(ShadowGuardRoom room)
        {
            var data = new RoomData(room,false);
            
            UpdateShadowGuardGump(data);
        }

        private void UpdateShadowGuardGump(RoomData roomData)
        {
            var width = 350;
            var marginTop = 100;
            var gid = (uint)456426886;
            
            var fg = Gumps.CreateGump();
            Gumps.AddBackground(ref fg, 0, 0, width, marginTop, 1755);
            Gumps.AddLabel(ref fg, 15,15,0x7b, "Shadowguard by Dorana");
            Gumps.AddLabel(ref fg, 15,40,0x7b, "Rurrent Room: " );
            Gumps.AddLabel(ref fg, 105, 40, 0x16a, roomData.Room.ToString());
            if (roomData.Room != ShadowGuardRoom.Lobby)
            {
                var secondsElipsed = (int) Math.Ceiling((DateTime.UtcNow - roomData.EntryTime).TotalSeconds);
                var timeLeft = 1800 - secondsElipsed;
                var fraction = (decimal)timeLeft / 1800;
                Gumps.AddLabel(ref fg, 15, 65, 0x7b, "Room Timer: ");
                Gumps.AddBackground(ref fg, 100,69,109,11,2053);
                Gumps.AddImageTiled(ref fg,100,69,(int)Math.Floor(fraction*109),11,2056);
            }
            
            if (roomData.Room == ShadowGuardRoom.Fountain)
            {
                AddFountainGumpData(fg, roomData, marginTop, width);
            }
            if(roomData.Room == ShadowGuardRoom.Orchard)
            {
                AddOrchardGumpData(fg, roomData, marginTop, width);
            }
            if(roomData.Room == ShadowGuardRoom.Armory)
            {
                AddArmoryGumpData(fg, roomData, marginTop, width);
            }
            if (roomData.Room == ShadowGuardRoom.Bar)
            {
                AddBarGumpData(fg, roomData, marginTop, width);
            }
            fg.gumpId = gid;
            fg.serial = (uint)Player.Serial;
            Gumps.CloseGump(gid);
            Gumps.SendGump(fg, 15, 30);
        }

        private void AddFountainGumpData(Gumps.GumpData fg, RoomData roomData, int marginTop, int width)
        {
            var paths = roomData.GetParam<Dictionary<int, Dictionary<Point, bool>>>(0);
            var partsRemaining = roomData.GetParam<Dictionary<int, int>>(1);
            var longest = 0;
            foreach (var pathEntry in paths)
            {
                var count = pathEntry.Value.Count;
                if (count > longest)
                {
                    longest = count;
                }
            }
            var marginx = 15;
            var marginy = 15;
            var rowx = 20;
            var rowy = 25;
            var rowIndex = 0;
            var needed = longest * rowx + marginx * 2;


            Gumps.AddBackground(ref fg, 0, marginTop, width, 195, 1755);

            var partindex = 0;
            foreach (var part in partsRemaining)
            {
                Gumps.AddItem(ref fg, marginx + partindex * 50, marginTop + 120, part.Key,
                    0);
                Gumps.AddLabel(ref fg, marginx + partindex * 50 + 10, marginTop + 170, 0x16a,
                    part.Value.ToString());
                partindex++;
            }

            foreach (var path in paths)
            {
                var gemIndex = 0;
                foreach (var pointValue in path.Value)
                {
                    if (pointValue.Value)
                    {
                        Gumps.AddImage(ref fg, marginx + rowx * gemIndex, marginTop + marginy + rowy * rowIndex, 11400);
                    }
                    else
                    {
                        Gumps.AddImage(ref fg, marginx + rowx * gemIndex, marginTop + marginy + rowy * rowIndex, 11410);
                    }

                    gemIndex++;
                }

                rowIndex++;
            }
        }

        private void AddOrchardGumpData(Gumps.GumpData fg, RoomData roomData, int marginTop, int width)
        {
            var clearedIndexes = roomData.GetParam<List<int>>(0);
            var task = roomData.GetParam<string>(1);
            var target = roomData.GetParam<string>(2);
            var extraTask = roomData.GetParam<string>(3);
            Gumps.AddBackground(ref fg, 0, marginTop, width, 230, 1755);
            Gumps.AddLabel(ref fg, 15, 15+marginTop,0x16a, task);
            if(target != string.Empty && task.Equals("Approach Tree", StringComparison.InvariantCultureIgnoreCase))
            {
                Gumps.AddLabel(ref fg, 15, 15+marginTop+25,0x16a, target);
            }
            for(int i = 0; i < 8; i++)
            {
                var hue = clearedIndexes.Contains(i) ? 0x16a : 0x85;
                Gumps.AddLabel(ref fg, 180, 15+marginTop+25*i,hue, _dungeons[i]);
                Gumps.AddLabel(ref fg, 265, 15+marginTop+25*i,hue, _virtues[i]);
            }
        }

        private void AddArmoryGumpData(Gumps.GumpData fg, RoomData roomData, int marginTop, int width)
        {
            var remaining = roomData.GetParam<int>(0);
            var philinBags = roomData.GetParam<List<Item>>(1);
            var task = roomData.GetParam<string>(2);
            Gumps.AddBackground(ref fg, 0, marginTop, width, 200, 1755);
            Gumps.AddLabel(ref fg, 15, 15+marginTop,0x16a, task);
            Gumps.AddItem(ref fg, 15,45+marginTop,0x151A,0);
            Gumps.AddLabel(ref fg, 20, 115+marginTop,0x16a, remaining.ToString());
            var timerIndex = 0;
            foreach (var philactory in philinBags)
            {
               var lifeSpanProp = philactory.Properties.FirstOrDefault(p => p.Number == 1072517);
               if(lifeSpanProp != null)
               {
                   var life = lifeSpanProp.Args;
                   if (int.TryParse(life, out int remainingLife))
                   {
                       var fraction = (decimal)remainingLife / 60;
                       Gumps.AddBackground(ref fg, 100,marginTop+15 + timerIndex*25,109,11,2053);
                       Gumps.AddImageTiled(ref fg,100,marginTop+15 + timerIndex*25,(int)Math.Floor(fraction*109),11,2056);
                       timerIndex++;
                   }
               }
            }
        }

        private void AddBarGumpData(Gumps.GumpData fg, RoomData roomData, int marginTop, int width)
        { 
            var lines = roomData.GetParam<List<string>>(0);
            var warning = roomData.GetParam<bool>(1);
            Gumps.AddBackground(ref fg, 0, marginTop, width, 80, 1755);
            var lineIndex = 0;
            var hue = warning ? 0x85 : 0x16a;
            foreach (var line in lines)
            {
                
                Gumps.AddLabel(ref fg, 15, 15+marginTop+25*lineIndex,hue, line);
                lineIndex++;
            }
        }

        private void HandleRoom(ShadowGuardRoom room)
        {
            switch (room)
            {
                case ShadowGuardRoom.Bar:
                    HandleBar();
                    break;
                case ShadowGuardRoom.Orchard:
                    HandleOrchard();
                    break;
                case ShadowGuardRoom.Armory:
                    HandleArmory();
                    break;
                case ShadowGuardRoom.Belfry:
                    HandleBelfry();
                    break;
                case ShadowGuardRoom.Fountain:
                    HandleFountain();
                    break;
                case ShadowGuardRoom.Lobby:
                    HandleLobby();
                    break;
                case ShadowGuardRoom.Roof:
                    handleRoof();
                    break;
                default:

                    break;
            }
        }

        private bool IsInRightPos(Item check)
        {
            if (!_canalPieces.Contains(check.ItemID))
            {
                return true;
            }

            foreach (var pair in _puzzlePathLocations)
            {
                var points = pair.Value[check.ItemID];
                var yes = points.Any(p =>
                    p.X == check.Position.X && p.Y == check.Position.Y);
                if (yes)
                {
                    return true;
                }
            }

            return false;
        }

        private void HandleFountain()
        {
            _puzzlePathLocations.Clear();
            for (var i = 1; i <= 4; i++)
            {
                _puzzlePathLocations.Add(i, GetTemplate());
            }
            
            var running = true;
            //Find the 4 spiggots
            var spigots = new List<Item>();
            var drains = new List<Item>();
            while (true)
            {
                var found = Items.ApplyFilter(new Items.Filter
                {
                    RangeMin = 0,
                    RangeMax = 24,
                    OnGround = 1
                }).Where(i => i.Name.ToLower().Contains("spigot")).ToList();

                foreach (var spigot in found)
                {
                    if (!spigots.Select(s => s.Serial).Contains(spigot.Serial))
                    {
                        spigots.Add(spigot);
                    }
                }

                Misc.Pause(50);
                if (spigots.Count >= 4)
                {
                    break;
                }
            }

            while (true)
            {
                var found = Items.ApplyFilter(new Items.Filter
                {
                    RangeMin = 0,
                    RangeMax = 24,
                    OnGround = 1
                }).Where(i => i.Name.ToLower().Contains("drain")).ToList();

                foreach (var drain in found)
                {
                    if (!drains.Select(d => d.Serial).Contains(drain.Serial))
                    {
                        drains.Add(drain);
                    }
                }

                Misc.Pause(50);

                if (drains.Count >= 2)
                {
                    break;
                }
            }

            var xSpigots = spigots.GroupBy(s => s.Position.X).Where(g => g.Count() > 1).SelectMany(g => g)
                .OrderByDescending(s => s.Position.Y).ToList();
            var ySpigots = spigots.GroupBy(s => s.Position.Y).Where(g => g.Count() > 1).SelectMany(g => g)
                .OrderBy(s => s.Position.X).ToList();

            var pathId = 0;
            var combined = xSpigots.Concat(ySpigots).Concat(drains).ToList();
            var minX = combined.Min(s => s.Position.X) - 5;
            var maxX = combined.Max(s => s.Position.X) + 5;
            var minY = combined.Min(s => s.Position.Y) - 5;
            var maxY = combined.Max(s => s.Position.Y) + 5;
            var grid = new List<Point>();
            
            var junk = Items.ApplyFilter(new Items.Filter
            {
                RangeMin = 0,
                RangeMax = 30,
                OnGround = 1,
                Name = "Broken Armor"
            }).ToList();
            
            for (var x = minX; x <= maxX; x++)
            {
                for (var y = minY; y <= maxY; y++)
                {
                    var cost = junk.Any(j => j.Position.X == x && j.Position.Y == y) ? 50 : 2;
                    grid.Add(new Point(x, y, cost));
                }
            }
            
            foreach (var spigot in ySpigots)
            {
                pathId++;
                var drain = drains.OrderBy(d => d.Position.X).First();
                var start = new Point(spigot.Position.X, spigot.Position.Y, 2);
                var goal = new Point(drain.Position.X, drain.Position.Y, 2);
                grid.Where(t => t.X == start.X+1 && t.Y == start.Y).First().Cost = 50;
                grid.Where(t => t.X == start.X-1 && t.Y == start.Y).First().Cost = 50;
                grid.Where(t => t.X == start.X && t.Y == start.Y-1).First().Cost = 50;
                
                BuildPath(grid,pathId,  start, goal, FountainEntryPoint.North);
                
                foreach (var pathData in _puzzlePathLocations[pathId])
                {
                    foreach (var point in pathData.Value)
                    {
                        grid.Where(t => t.X == point.X && t.Y == point.Y).First().Cost = 50;
                    }
                }
            }

            foreach (var spigot in xSpigots)
            {
                pathId++;
                var drain = drains.OrderBy(d => d.Position.Y).First();
                var start = new Point(spigot.Position.X, spigot.Position.Y, 2);
                var goal = new Point(drain.Position.X, drain.Position.Y, 2);
                
                grid.Where(t => t.X == start.X && t.Y == start.Y+1).First().Cost = 50;
                grid.Where(t => t.X == start.X && t.Y == start.Y-1).First().Cost = 50;
                grid.Where(t => t.X == start.X-1 && t.Y == start.Y).First().Cost = 50;
                
                BuildPath(grid,pathId,  start, goal, FountainEntryPoint.West);
                
                foreach (var pathData in _puzzlePathLocations[pathId])
                {
                    foreach (var point in pathData.Value)
                    {
                        grid.Where(t => t.X == point.X && t.Y == point.Y).First().Cost = 50;
                    }
                }
            }
            
            //Dictionary of Locations and a bool if placed
            var positions = new Dictionary<int, Dictionary<Point, bool>>();
            foreach (var path in _puzzlePathLocations)
            {
                var pathDict = new Dictionary<Point, bool>();
                foreach (var point in path.Value.SelectMany(g => g.Value))
                {
                    pathDict.Add(point, false);
                }

                positions.Add(path.Key, pathDict);
            }
            
            var partsNeeded = new Dictionary<int, int>();
            foreach (var path in _puzzlePathLocations)
            {
                foreach (var canalPeice in path.Value)
                {
                    if(!partsNeeded.ContainsKey(canalPeice.Key))
                    {
                        partsNeeded.Add(canalPeice.Key, 0);
                    }
                    var locations = canalPeice.Value;
                    var posData = positions[path.Key];
                    var count = posData.Count(p => locations.Contains(p.Key) && !p.Value);
                    partsNeeded[canalPeice.Key] += count;
                }
            }
            
            UpdateShadowGuardGump(new RoomData(ShadowGuardRoom.Fountain, positions, partsNeeded));

            while (running)
            {
                if (!StillInRoom(ShadowGuardRoom.Fountain))
                {
                    break;
                }
                
                //picking up pieces
                var partsInReach = Items.ApplyFilter(new Items.Filter
                {
                    Enabled = true,
                    OnGround = 1,
                    Graphics = _canalPieces,
                    RangeMin = 0,
                    RangeMax = 2
                }).OrderBy(p => p.DistanceTo(_player)).ToList();
                var takePart = partsInReach.FirstOrDefault();
                if (takePart != null)
                {
                    if (!IsInRightPos(takePart) && partsNeeded[takePart.ItemID] > 0)
                    {
                        Items.Move(takePart, Player.Backpack, 1);
                        partsInReach.Remove(takePart);
                        Misc.Pause(500);
                        continue;
                    }
                }


                var pos = Player.Position;
                foreach (var path in _puzzlePathLocations)
                {
                    if (positions[path.Key].Select(p => p.Value).All(v => v))
                    {
                        continue;
                    }

                    foreach (var group in path.Value)
                    {
                        foreach (var point in group.Value)
                        {
                            if (positions[path.Key][point])
                            {
                                continue;
                            }

                            if (Math.Abs(pos.X - point.X) <= 2 && Math.Abs(pos.Y - point.Y) <= 2)
                            {
                                var blockPart = partsInReach.FirstOrDefault(p =>
                                    p.Position.X == point.X && p.Position.Y == point.Y);

                                if (blockPart?.ItemID == group.Key)
                                {
                                    break;
                                }

                                if (blockPart != null)
                                {
                                    Items.Move(blockPart, Player.Backpack, 1);
                                    Misc.Pause(500);
                                    break;
                                }

                                var dropPart = Player.Backpack.Contains.FirstOrDefault(i => i.ItemID == group.Key);
                                if (dropPart != null)
                                {
                                    Items.MoveOnGround(dropPart, 1, point.X, point.Y, 0);
                                    Misc.Pause(500);
                                    // Items.FindBySerial(dropPart.Serial);
                                    // if (IsInRightPos(dropPart))
                                    // {
                                    //     positions[path.Key][point] = true;
                                    // }
                                    break;
                                }
                            }
                        }
                    }
                }


                Misc.Pause(500);
                //Find all parts in vision range
                //foreach part, check if IsRightPosition, if so update those positions with true
                var boardCheckParts = Items.ApplyFilter(new Items.Filter
                {
                    OnGround = 1,
                    RangeMin = 0,
                    RangeMax = 15,
                }).Where(p => p.Name.Equals("Canal", StringComparison.OrdinalIgnoreCase)).ToList();

                foreach (var bcp in boardCheckParts)
                {
                    var breaker = false;
                    if (IsInRightPos(bcp))
                    {
                        foreach (var path in _puzzlePathLocations)
                        {
                            foreach (var part in path.Value)
                            {
                                foreach (var posPoint in part.Value)
                                {
                                    if (posPoint.X == bcp.Position.X && posPoint.Y == bcp.Position.Y &&
                                        posPoint.X == bcp.Position.X)
                                    {
                                        positions[path.Key][posPoint] = true;
                                        breaker = true;
                                        break;
                                    }

                                    if (breaker) break;
                                }

                                if (breaker) break;
                            }

                            if (breaker) break;
                        }
                    }
                }
                
                partsNeeded.Clear();
                foreach (var path in _puzzlePathLocations)
                {
                    foreach (var canalPeice in path.Value)
                    {
                        if(!partsNeeded.ContainsKey(canalPeice.Key))
                        {
                            partsNeeded.Add(canalPeice.Key, 0);
                        }
                        var locations = canalPeice.Value;
                        var posData = positions[path.Key];
                        var count = posData.Count(p => locations.Contains(p.Key) && !p.Value);
                        partsNeeded[canalPeice.Key] += count;
                    }
                }

                UpdateShadowGuardGump(new RoomData(ShadowGuardRoom.Fountain, positions, partsNeeded));
            }

            Misc.Pause(1000);
            Gumps.CloseGump(456426886);
        }

        private void HandleLobby()
        {
            
            UpdateShadowGuardGump(ShadowGuardRoom.Lobby);
            while (true)
            {
                Misc.Pause(5000);
                if (!StillInRoom(ShadowGuardRoom.Lobby))
                {
                    break;
                }
            }
        }

        private void handleRoof()
        {
            UpdateShadowGuardGump(ShadowGuardRoom.Roof);
            while (true)
            {
                Misc.Pause(5000);
                if (!StillInRoom(ShadowGuardRoom.Roof))
                {
                    break;
                }
            }
        }

        private void HandleBelfry()
        {
            UpdateShadowGuardGump(ShadowGuardRoom.Belfry);
            var running = true;
            while (running)
            {
                if (!StillInRoom(ShadowGuardRoom.Belfry))
                {
                    break;
                }

                Misc.Pause(5000);
            }
        }

        private void HandleArmory()
        {
            var brokenArmor = new List<Item>();
            var roomData = new RoomData(ShadowGuardRoom.Armory, 28, new List<Item>(), "Enter all rooms");
            UpdateShadowGuardGump(roomData);
            
            var philactoryFilter = new Items.Filter
            {
                Enabled = true,
                OnGround = 1,
                RangeMin = 0,
                RangeMax = 2,
                Graphics = philactories
            };
            
            var flameFilter = new Items.Filter
            {
                Enabled = true,
                RangeMin = 0,
                RangeMax = 3,
                Name = "Purifying Flames"
            };
            
            var armourFilter = new Items.Filter
            {
                Enabled = true,
                RangeMin = 0,
                RangeMax = 3,
                Name = "Cursed Suit Of Armor",
            };
            
            var brokenFilter = new Items.Filter
            {
                Enabled = true,
                RangeMin = 0,
                Name = "Broken Armor",
            };
            
            var allArmors = GetAllArmors(true);
            roomData.Params[2] = "Kill guards";
            
            while (true)
            {
                if (!StillInRoom(ShadowGuardRoom.Armory))
                {
                    break;
                }
                
                var newFoundArmors = GetAllArmors(false);
                var potentialBroken = allArmors.Where(a => !newFoundArmors.Select(n => n.Serial).Contains(a.Serial)).ToList();

                foreach (var pb in potentialBroken)
                {
                    if (pb.DistanceTo(_player) < 20)
                    {
                        if(!brokenArmor.Select(b => b.Serial).Contains(pb.Serial))
                        {
                            brokenArmor.Add(pb);
                        }
                    }
                }
                
                var philinBags = Player.Backpack.Contains
                    .Where(i => philactories.Contains(i.ItemID)).ToList();
                philactories.ForEach(p => Items.WaitForProps(p, 1000));
                
                roomData.Params[0] = allArmors.Count - brokenArmor.Count;
                roomData.Params[1] = philinBags;
                
                UpdateShadowGuardGump(roomData);;
                
                var phil = Items.ApplyFilter(philactoryFilter).FirstOrDefault();
                if (phil != null)
                {
                    Items.Move(phil, Player.Backpack.Serial, 1);
                    Misc.Pause(300);
                }

                var flame = Items.ApplyFilter(flameFilter).FirstOrDefault();
                if (flame != null)
                {
                    var ptc = Player.Backpack.Contains
                        .Where(p => philactories.Contains(p.ItemID) && philHuesCorrupted.Contains(p.Hue)).ToList();
                    foreach (var philactory in ptc)
                    {
                        Items.UseItem(philactory);
                        Target.WaitForTarget(1000);
                        Target.TargetExecute(flame);
                    }
                }

                if (Target.HasTarget())
                {
                    Misc.Pause(100);
                    continue;
                }
                
                var pps = Player.Backpack.Contains
                    .Where(i => philactories.Contains(i.ItemID) && philHuesPure.Contains(i.Hue)).ToList();
                if (pps.Any())
                {
                    foreach (var pp in pps)
                    {
                        var armours = Items.ApplyFilter(armourFilter);
                        var tarArmour = armours.OrderBy(o => o.DistanceTo(_player))
                            .FirstOrDefault();
                        if (tarArmour != null)
                        {
                            Items.UseItem(pp);
                            Target.WaitForTarget(1000);
                            Target.TargetExecute(tarArmour);
                            Misc.Pause(100);
                        }
                    }
                }
                
                Misc.Pause(200);
            }
        }

        private bool StillInRoom(ShadowGuardRoom room)
        {
            var found = GetCurrentRoom();
            if (found == ShadowGuardRoom.Unknown)
            {
                return true;
            }

            return found == room;
        }


        private void HandleOrchard()
        {
            var task = "Approach all corners";
            var running = true;
            List<int> clearedIndexes = new List<int>();
            var roomData = new RoomData(ShadowGuardRoom.Orchard,clearedIndexes, task, string.Empty);
            UpdateShadowGuardGump(roomData);
            List<Item> allTrees = GetTrees();
            var grouped = FindPairs(allTrees.Select(t => t.Serial).ToList());
            
            roomData.Params[1] = "Pick an Apple";
            Item heldApple = null;
            Item nearestTree = null;
            while (running)
            {
                if (!StillInRoom(ShadowGuardRoom.Orchard))
                {
                    break;
                }
                
                UpdateShadowGuardGump(roomData);
                if (heldApple == null)
                {
                    nearestTree = GetTree(2);
                    if (nearestTree == null)
                    {
                        Misc.Pause(50);
                        continue;
                    }

                    Items.UseItem(nearestTree);
                    Misc.Pause(500);
                }

                heldApple = GetApple();
                Misc.Pause(50);
                
                if(heldApple == null)
                {
                    Misc.Pause(50);
                    continue;
                }
                
                roomData.Params[1] = "Approach Tree";
                var sourcename =  heldApple.Name.Split(' ').Last().ToLower();
                var pairIndex = 0;
                if (_dungeons.Contains(sourcename))
                {
                    pairIndex = _dungeons.IndexOf(sourcename);
                    roomData.Params[2] = _virtues[pairIndex];
                }
                else if(_virtues.Contains(sourcename))
                {
                    pairIndex = _virtues.IndexOf(sourcename);
                    roomData.Params[2] = _dungeons[pairIndex];
                }
                else if(sourcename.Equals("Sacrifice"))
                {
                    pairIndex = _virtues.IndexOf("Sacrafice");  //handles misspelling on serverside
                    if(pairIndex > -1)
                    {
                        roomData.Params[2] = _dungeons[pairIndex];
                    }
                }
                else
                {
                    roomData.Params[2] = string.Empty;
                }
                
                UpdateShadowGuardGump(roomData);

                if (!grouped.ContainsKey(nearestTree.Serial))
                {
                    UpdateShadowGuardGump(roomData);
                    continue;
                }

                var opposite = grouped[nearestTree.Serial];
                var targetTree = allTrees.FirstOrDefault(t => t.Serial == opposite);
                
                Player.TrackingArrow(Convert.ToUInt16(targetTree.Position.X),
                    Convert.ToUInt16(targetTree.Position.Y), true);
                
                while (targetTree.DistanceTo(_player) > 8)
                {
                    Misc.Pause(200);
                }
                
                Items.UseItem(heldApple);
                Target.WaitForTarget(1000);
                Target.TargetExecute(targetTree);
                Player.TrackingArrow(Convert.ToUInt16(targetTree.Position.X),
                    Convert.ToUInt16(targetTree.Position.Y), false);
                
                clearedIndexes.Add(pairIndex);
                roomData.Params[1] = "Pick an Apple";
                
                Misc.Pause(1000);

                
            }
        }



        private void HandleBar()
        {
            var running = true;
            while (running)
            {
                var lines = new List<string>
                {
                    "Run close to bottles",
                    "Bottles are thrown at pirate automatically"
                };
                if (Player.Hits < 25)
                {
                    Misc.SendMessage("Heal thy self!");
                    lines = new List<string>
                    {
                        "Low Health Detected",
                        "Heal thy self!"
                    };
                    UpdateShadowGuardGump(new RoomData(ShadowGuardRoom.Bar, lines, true));
                    Misc.Pause(5000);
                    continue;
                }
                
                UpdateShadowGuardGump(new RoomData(ShadowGuardRoom.Bar, lines));

                Item useBottle = null;
                //Check backpack
                var backpackBottle = Player.Backpack.Contains.FirstOrDefault(i =>
                    i.ItemID == 0x099B && i.Name.Equals("a bottle of Liquor",
                        System.StringComparison.InvariantCultureIgnoreCase));

                //else check the tables
                var bottleFilter = new Items.Filter
                {
                    Enabled = true,
                    OnGround = 1,
                    RangeMin = 0,
                    RangeMax = 2,
                    Graphics = new List<int>
                    {
                        0x099B,
                    }
                };

                var tableBottle = Items.ApplyFilter(bottleFilter).FirstOrDefault();

                if (backpackBottle != null || tableBottle != null)
                {
                    var mobList = Mobiles.ApplyFilter(new Mobiles.Filter
                    {
                        Notorieties = new List<byte>
                        {
                            6
                        },
                        RangeMin = 0,
                        RangeMax = 6
                    });

                    if (mobList.Any())
                    {
                        Mobile target = null;
                        foreach (var mobile in mobList.OrderBy(m => m.Hits))
                        {
                            Mobiles.WaitForProps(mobile, 300);
                            if (mobile.Properties.All(p => p.Number != 1049646))
                            {
                                target = mobile;
                                break;
                            }
                        }

                        if (target != null)
                        {
                            useBottle = tableBottle ?? backpackBottle;
                            Items.UseItem(useBottle);
                            Target.WaitForTarget(150);
                            Target.TargetExecute(target.Serial);
                        }
                    }

                    if (tableBottle != null)
                    {
                        var dragBottle = Items.FindBySerial(tableBottle.Serial);
                        if (dragBottle != null && dragBottle.Container != Player.Backpack.Serial)
                        {
                            Items.Move(dragBottle, Player.Backpack.Serial, 1);
                            Misc.Pause(200);
                        }
                    }
                }

                Misc.Pause(200);

                if (!StillInRoom(ShadowGuardRoom.Bar))
                {
                    break;
                }
            }
        }

        private bool IsBar()
        {
            var mobsInRoom = Mobiles.ApplyFilter(new Mobiles.Filter
            {
                Notorieties = new List<byte>
                {
                    6
                }
            });

            var itemsInRoom = Items.ApplyFilter(new Items.Filter
            {
                Graphics = new List<int>
                {
                    0x099B
                }
            });

            var hasPirates = false;
            foreach (var mobile in mobsInRoom)
            {
                Mobiles.WaitForProps(mobile, 1000);
                hasPirates = mobile.Properties.Any(p => p.ToString().ToLower().Contains("pirate"));
                if(hasPirates)
                {
                    break;
                }
            }

            return hasPirates || itemsInRoom.Any(i => i.Name == "a bottle of Liquor");
        }

        private Item GetTree(int maxDistance)
        {
            var treeFilter = new Items.Filter
            {
                Enabled = true,
                Name = "Cypress Tree",
                RangeMax = maxDistance,
            };
            return Items.ApplyFilter(treeFilter).OrderBy(t => t.DistanceTo(_player)).FirstOrDefault();
        }

        private List<Item> GetTrees()
        {
            var timeStart = DateTime.UtcNow;
            List<Item> trees = new List<Item>();
            while (trees.Count < 16 && (DateTime.UtcNow - timeStart).TotalSeconds < 10)
            {
                var treeFilter = new Items.Filter
                {
                    Enabled = true,
                    Name = "Cypress Tree",
                };

                var found = Items.ApplyFilter(treeFilter).ToList();
                foreach (var tree in found)
                {
                    if (!trees.Select(t => t.Serial).Contains(tree.Serial))
                    {
                        trees.Add(tree);
                    }
                }
            }

            return trees;
        }
        
        
        
        private Dictionary<int, int> FindPairs(List<int> numbers)
        {
            var pairs = new Dictionary<int, int>();
            var orderred = numbers.OrderBy(n => n).ToList();
            var cutList = orderred;
            while (cutList.Any())
            {
                if(cutList.Count < 2)
                {
                    break;
                }
                
                var first = cutList[0];
                var second = cutList[1];
                
                if(Math.Abs(first - second) == 2)
                {
                    pairs[first] = second;
                    pairs[second] = first;
                }
                
                cutList = cutList.Skip(2).ToList();
            }

            return pairs;
        }

        private Item GetApple()
        {
            return Items.FindByID(0x09D0, 0x0000, Player.Backpack.Serial);
        }

        private List<Item> GetAllArmors(bool forceGetAll = false)
        {
            var filter = new Items.Filter
            {
                Enabled = true,
                RangeMin = 0,
                Name = "Cursed Suit Of Armor",
            };
            List<Item> armors = new List<Item>();
            if (forceGetAll)
            {
                var timeStart = DateTime.UtcNow;
                while (armors.Count < 28 && (DateTime.UtcNow - timeStart).TotalSeconds < 10)
                {
                    var found = Items.ApplyFilter(filter).ToList();
                    foreach (var armor in found)
                    {
                        if (!armors.Select(t => t.Serial).Contains(armor.Serial))
                        {
                            armors.Add(armor);
                        }
                    }
                }
            }
            else
            {
                var found = Items.ApplyFilter(filter).ToList();
                foreach (var armor in found)
                {
                    if (!armors.Select(t => t.Serial).Contains(armor.Serial))
                    {
                        armors.Add(armor);
                    }
                }
            }

            return armors;
        }

        private ShadowGuardRoom GetCurrentRoom()
        {
            
            if(IsBar())
            {
                return ShadowGuardRoom.Bar;
            }
            if (IsOrchard())
            {
                return ShadowGuardRoom.Orchard;
            }
            if (IsArmory())
            {
                return ShadowGuardRoom.Armory;
            }
            if (IsBelfry())
            {
                return ShadowGuardRoom.Belfry;
            }
            if (IsFountain())
            {
                return ShadowGuardRoom.Fountain;
            }
            if (IsLobby())
            {
                return ShadowGuardRoom.Lobby;
            }
            if (IsRoof())
            {
                return ShadowGuardRoom.Roof;
            }

            return ShadowGuardRoom.Unknown;
        }

        private bool IsOrchard()
        {
            var itemsInRoom = Items.ApplyFilter(new Items.Filter
            {
                RangeMax = 20,
                RangeMin = 0,
                Graphics = new List<int>
                {
                    0x0D01,
                }
            });

            return itemsInRoom.Any();
        }

        private bool IsArmory()
        {
            var itemsInRoom = Items.ApplyFilter(new Items.Filter());

            return itemsInRoom.Any(i =>
                i.Name.Equals("Purifying Flames", System.StringComparison.InvariantCultureIgnoreCase));
        }

        private bool IsBelfry()
        {
            var items = Items.ApplyFilter(new Items.Filter
            {
                RangeMin = 0,
                RangeMax = 10,
            });

            return items.Any(i => i.Name.Equals("Feeding Bell", System.StringComparison.InvariantCultureIgnoreCase));
        }

        private bool IsFountain()
        {
            return Items.ApplyFilter(new Items.Filter
            {
                RangeMin = 0,
                RangeMax = 12,
                OnGround = 1
            }).Where(i => i.Name.ToLower().Contains("spigot")).ToList().Any();
        }

        private bool IsRoof()
        {
            var minax = Mobiles.ApplyFilter(new Mobiles.Filter
            {
                RangeMin = 0,
                RangeMax = 10,
                Name = "Minax the Enchantress"
            }).ToList();

            return minax.Any();
        }

        private bool IsLobby()
        {
            var items = Items.ApplyFilter(new Items.Filter
            {
                RangeMin = 0,
                RangeMax = 12,
            });

            return items.Any(i =>
                       i.Name.Equals("An Enchanting Crystal Ball",
                           System.StringComparison.InvariantCultureIgnoreCase)) &&
                   items.Any(i => i.Name.Equals("ankh", System.StringComparison.InvariantCultureIgnoreCase));
        }

        private void BuildPath(List<Point> grid,int pathId, Point start, Point end, FountainEntryPoint originalEntryPoint)
        {
            var gridminX = grid.Min(g => g.X);
            var gridminY = grid.Min(g => g.Y);
            var gridmaxX = grid.Max(g => g.X);
            var gridmaxY = grid.Max(g => g.Y);
            var solvePath = FindPath(ConvertToGrid(grid), start, end);
            var skipFirstandlast = solvePath.Skip(1).Take(solvePath.Count - 2).ToList();
            var tileIds = GetTileIdsFromPath(solvePath);

            for (int i = 0; i < skipFirstandlast.Count; i++)
            {
                var currentTile = tileIds[i+1];
                _puzzlePathLocations[pathId][currentTile].Add(skipFirstandlast[i]);
            }
        }
        
        public static List<int> GetTileIdsFromPath(List<Point> path) {
            var tileIds = new List<int>();
            tileIds.Add(0);
            for (int i = 1; i < path.Count - 1; i++) {
                tileIds.Add(GetTileId(path[i], path[i + 1], path[i - 1]));
            }
            return tileIds;
        }
    
        public static int GetTileId(Point current, Point next, Point previous) {
            var dx = next.X - current.X;
            var dy = next.Y - current.Y;
            var pdx = current.X - previous.X;
            var pdy = current.Y - previous.Y;
            //< 0x9BEF
            //^ 0x9BFC
            //> 0x9BF8
            //V 0x9BEB
            // \ 0x9BF4
            if(pdy == 1) //arriving from North
            {
                if(dx == 1) //going East
                {
                    return 0x9BEF;
                }
                if(dx == -1) //going West
                {
                    return 0x9BEB;
                }
                if(dy == 1) //going South
                {
                    return 0x9BE7;
                }
            }
            if(pdy == -1) //arriving from South
            {
                if(dx == 1) //going East
                {
                    return 0x9BFC;
                }
                if(dx == -1) //going West
                {
                    return 0x9BF8;
                }
                if(dy == -1) //going North
                {
                    return 0x9BE7;
                }
            }

            if (pdx == 1) //arriving from West
            {
                if (dy == 1) //going South
                {
                    return 0x9BF8;
                }

                if (dy == -1) //going North
                {
                    return 0x9BEB;
                }

                if (dx == 1) //going East
                {
                    return 0x9BF4;
                }
            }

            if(pdx == -1) //arriving from East
            {
                if(dy == 1) //going South
                {
                    return 0x9BFC;
                }
                if(dy == -1) //going North
                {
                    return 0x9BEF;
                }
                if(dx == -1) //going West
                {
                    return 0x9BF4;
                }
            }
            
            
            throw new Exception("Impossible pathing");
        }

        public int[,] ConvertToGrid(List<Point> points) {
        if (points == null || points.Count == 0) {
            throw new ArgumentException("The points list cannot be null or empty.");
        }

        int maxX = points.Max(p => p.X) + 1;
        int maxY = points.Max(p => p.Y) + 1;

        int[,] grid = new int[maxX, maxY];

        foreach (var point in points) {
            grid[point.X, point.Y] = point.Cost;
        }

        return grid;
    }
    
    public List<Point> FindPath(int[,] grid, Point start, Point end) {
        int rows = grid.GetLength(0);
        int cols = grid.GetLength(1);

        var openSet = new SortedSet<(Point Point, int FCost, int GCost)>(Comparer<(Point Point, int FCost, int GCost)>.Create((a, b) => 
        {
            if (a.FCost == b.FCost) return a.Point.GetHashCode().CompareTo(b.Point.GetHashCode());
            return a.FCost.CompareTo(b.FCost);
        }));

        var cameFrom = new Dictionary<Point, Point>();
        var gCost = new Dictionary<Point, int>();
        var fCost = new Dictionary<Point, int>();

        gCost[start] = 0;
        fCost[start] = Heuristic(start, end);
        openSet.Add((start, fCost[start], gCost[start]));

        while (openSet.Count > 0) {
            var current = openSet.First().Point;
            openSet.Remove(openSet.First());

            if (current.Equals(end)) {
                return ReconstructPath(cameFrom, current);
            }

            foreach (var neighbor in GetNeighbors(current, rows, cols, grid)) {
                int tentativeGCost = gCost[current] + neighbor.Cost;

                if (!gCost.ContainsKey(neighbor) || tentativeGCost < gCost[neighbor]) {
                    cameFrom[neighbor] = current;
                    gCost[neighbor] = tentativeGCost;
                    fCost[neighbor] = tentativeGCost + Heuristic(neighbor, end);

                    if (!openSet.Any(item => item.Point.Equals(neighbor))) {
                        openSet.Add((neighbor, fCost[neighbor], gCost[neighbor]));
                    }
                }
            }
        }
        
        Misc.SendMessage("Unable To find path");
        
        return new List<Point>(); // Return an empty path if no path exists
    }

    private static int Heuristic(Point a, Point b) {
        // Chebyshev distance for diagonal preference
        return Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
    }

    private static List<Point> GetNeighbors(Point point, int rows, int cols, int[,] grid) {
        var neighbors = new List<Point>();
        var directions = new List<(int X, int Y)> {
            (0, -1), // Up
            (0, 1),  // Down
            (-1, 0), // Left
            (1, 0),  // Right
        };

        foreach (var (dx, dy) in directions) {
            int newX = point.X + dx;
            int newY = point.Y + dy;
            if (newX >= 0 && newX < rows && newY >= 0 && newY < cols) {
                int additionalCost = Math.Abs(dx) + Math.Abs(dy) == 2 ? 1 : 0; // Favor diagonal moves
                neighbors.Add(new Point(newX, newY, grid[newX, newY] + additionalCost));
            }
        }

        return neighbors;
    }
    
    private static List<Point> ReconstructPath(Dictionary<Point, Point> cameFrom, Point current) {
        var path = new List<Point> { current };
        while (cameFrom.ContainsKey(current)) {
            current = cameFrom[current];
            path.Add(current);
        }
        path.Reverse();
        return path;
    }
    
    }

    internal class RoomData
    {
        public RoomData(ShadowGuardRoom room, params object[] parameters)
        {
            Room = room;
            Params = parameters;
        }
        
        public T GetParam<T>(int index)
        {
            if(Params.Length <= index)
            {
                return default;
            }
            var data = Params[index];
            if (data is T t)
            {
                return t;
            }

            return default;
        }
        public ShadowGuardRoom Room { get; set; }
        public object[] Params { get; set; }
        public DateTime EntryTime { get; set; } = DateTime.UtcNow;
    }

    public enum ShadowGuardRoom
    {
        Bar = 0,
        Orchard = 1,
        Armory = 2,
        Belfry = 3,
        Fountain = 4,
        Lobby = 5,
        Roof = 6,
        Unknown = 7
    }
    
    public enum FountainEntryPoint
    {
        North = 0,
        East = 1,
        South = 2,
        West = 3
    }
    
    public class Point
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Cost { get; set; }

        public Point(int x, int y, int cost)
        {
            X = x;
            Y = y;
            Cost = cost;
        }

        public override bool Equals(object obj)
        {
            return obj is Point other && X == other.X && Y == other.Y;
        }

        public override int GetHashCode()
        {
            return X * 31 + Y;
        }
    }
}