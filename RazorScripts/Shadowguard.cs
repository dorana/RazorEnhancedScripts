using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing.Imaging;
using System.Linq;
using System.Net.NetworkInformation;
using RazorEnhanced;

namespace RazorScripts
{
    public class Shadowguard
    {

        private bool ProcessFountain = false;
        
        private List<string> _virtues = new List<string>
        {
            "compassion",
            "honesty",
            "honor",
            "humility",
            "justice",
            "sacrafice",
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

        private Dictionary<int, Dictionary<int, List<TriPoint>>> _puzzlePathLocations = new Dictionary<int, Dictionary<int, List<TriPoint>>>();


        private Dictionary<int, List<TriPoint>> GetTemplate()
        {
            return new Dictionary<int, List<TriPoint>>
            {
                {
                    0x9BEF, new List<TriPoint>()
                },
                {
                    0x9BF4, new List<TriPoint>()
                },
                {
                    0x9BEB, new List<TriPoint>()
                },
                {
                    0x9BF8, new List<TriPoint>()
                },
                {
                    0x9BE7, new List<TriPoint>()
                },
                {
                    0x9BFC, new List<TriPoint>()
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
            {ShadowGuardRoom.Bar, false},
            {ShadowGuardRoom.Orchard, false},
            {ShadowGuardRoom.Armory, false},
            {ShadowGuardRoom.Belfry, false},
            {ShadowGuardRoom.Fountain, false},
            {ShadowGuardRoom.Lobby, false},
            {ShadowGuardRoom.Roof, false},
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
                    Misc.Pause(2000);
                }
            }
            catch (Exception e)
            {
                Misc.SendMessage(e.ToString());
                throw;
            }
            
        }

        private void BuildPath(int pathId, TriPoint start, TriPoint end, string originalEnterypoint)
        {
            //Building Path
            var currentPos = new TriPoint(start.X, start.Y, start.Z);
            var currentEntrypoint = originalEnterypoint;
            while (currentPos.X != end.X || currentPos.Y != end.Y)
            {
                //Build a diagonal ZigZag until we hit X or Y of end, then Build straight till you hit end
                //find out which direction we're going, then put the block with the corresponding exits in CurrentEntryPoint and the direction we are going
                if (currentEntrypoint == "North")
                {
                    if (currentPos.X < end.X)
                    {
                        //Go East
                        BuildBlock(pathId, 0x9BEF, new TriPoint(currentPos.X,currentPos.Y,currentPos.Z));
                        currentPos.X++;
                        currentEntrypoint = "West";
                    }
                    else if (currentPos.X == end.X)
                    {
                        //Go South
                        BuildBlock(pathId, 0x9BE7, new TriPoint(currentPos.X,currentPos.Y,currentPos.Z));
                        currentPos.Y++;
                        currentEntrypoint = "North";
                    }
                    else
                    {
                        //Go West
                        BuildBlock(pathId, 0x9BEB, new TriPoint(currentPos.X,currentPos.Y,currentPos.Z));
                        currentPos.X--;
                        currentEntrypoint = "East";
                    }
                }
                else if (currentEntrypoint == "East")
                {
                    if (currentPos.Y < end.Y)
                    {
                        //Go South
                        BuildBlock(pathId, 0x9BFC, new TriPoint(currentPos.X,currentPos.Y,currentPos.Z));
                        currentPos.Y++;
                        currentEntrypoint = "North";
                    }
                    else if (currentPos.Y == end.Y)
                    {
                        //Go West
                        BuildBlock(pathId, 0x9BF4, new TriPoint(currentPos.X,currentPos.Y,currentPos.Z));
                        currentPos.X--;
                        currentEntrypoint = "East";
                    }
                    else
                    {
                        //Go North
                        BuildBlock(pathId,0x9BEF, new TriPoint(currentPos.X,currentPos.Y,currentPos.Z));
                        currentPos.Y--;
                        currentEntrypoint = "South";
                    }
                }
                else if (currentEntrypoint == "South")
                {
                    if (currentPos.X < end.X)
                    {
                        //Go East
                        BuildBlock(pathId,0x9BFC, new TriPoint(currentPos.X,currentPos.Y,currentPos.Z));
                        currentPos.X++;
                        currentEntrypoint = "West";
                    }
                    else if (currentPos.X == end.X)
                    {
                        //Go North
                        BuildBlock(pathId, 0x9BE7, new TriPoint(currentPos.X,currentPos.Y,currentPos.Z));
                        currentPos.Y--;
                        currentEntrypoint = "South";
                    }
                    else
                    {
                        //Go West
                        BuildBlock(pathId, 0x9BF8, new TriPoint(currentPos.X,currentPos.Y,currentPos.Z));
                        currentPos.X--;
                        currentEntrypoint = "East";
                    }
                }
                else if (currentEntrypoint == "West")
                {
                    if (currentPos.Y < end.Y)
                    {
                        //Go South
                        BuildBlock(pathId, 0x9BF8, new TriPoint(currentPos.X,currentPos.Y,currentPos.Z));
                        currentPos.Y++;
                        currentEntrypoint = "North";
                    }
                    else if (currentPos.Y == end.Y)
                    {
                        //Go East
                        BuildBlock(pathId, 0x9BF4, new TriPoint(currentPos.X,currentPos.Y,currentPos.Z));
                        currentPos.X++;
                        currentEntrypoint = "West";
                    }
                    else
                    {
                        //Go North
                        BuildBlock(pathId, 0x9BEB, new TriPoint(currentPos.X,currentPos.Y,currentPos.Z));
                        currentPos.Y--;
                        currentEntrypoint = "South";
                    }
                }
            }
            
            //Check if we have duplicate blocks with the main (_puzzlePathLocations)

        }

        private void BuildBlock(int pathId, int itemId, TriPoint currentPos)
        {
            _puzzlePathLocations[pathId][itemId].Add(currentPos);
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
                var yes = points.Any(p => p.X == check.Position.X && p.Y == check.Position.Y && p.Z == check.Position.Z);
                if (yes)
                {
                    return true;
                }
            }

            return false;
        }

        private void HandleFountain()
        {
            ProcessFountain = false;
            _puzzlePathLocations.Clear();
            for (var i = 1; i<=4; i++)
            {
                _puzzlePathLocations.Add(i, GetTemplate());
            }
            
            Player.HeadMessage(180, "Entering Fountain");
            var running = true;
            //Find the 4 spiggots
            var spigots = new List<Item>();
            var drains = new List<Item>();
            while (true)
            {
                var found = Items.ApplyFilter(new Items.Filter
                {
                    RangeMin = 0,
                    RangeMax = 12,
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
            
            Player.HeadMessage(150, "All Spigots Found");

            while (true)
            {
                var found = Items.ApplyFilter(new Items.Filter
                {
                    RangeMin = 0,
                    RangeMax = 12,
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
            Player.HeadMessage(150, "All Drains Found");

            var xSpigots = spigots.GroupBy(s => s.Position.X).Where(g => g.Count() > 1).SelectMany(g => g).OrderByDescending(s => s.Position.Y).ToList();
            var ySpigots = spigots.GroupBy(s => s.Position.Y).Where(g => g.Count() > 1).SelectMany(g => g).OrderBy(s => s.Position.X).ToList();

            var pathId = 0;
            foreach (var spigot in ySpigots)
            {
                pathId++;
                var drain = drains.OrderBy(d => d.Position.X).First();
                var start = new TriPoint(spigot.Position.X, spigot.Position.Y+1, spigot.Position.Z);
                var goal = new TriPoint(drain.Position.X, drain.Position.Y, drain.Position.Z);
                BuildPath(pathId, start,goal, "North");
            }
            
            foreach (var spigot in xSpigots)
            {
                pathId++;
                var drain = drains.OrderBy(d => d.Position.Y).First();
                var start = new TriPoint(spigot.Position.X+1, spigot.Position.Y, spigot.Position.Z);
                var goal = new TriPoint(drain.Position.X, drain.Position.Y, drain.Position.Z);
                BuildPath(pathId,start,goal, "West");
            }
            
            
            //Check is same location is stored on 2 different Keys in _puzzleLocations
            var puzzleLocations = _puzzlePathLocations.SelectMany(p => p.Value.SelectMany(sp => sp.Value)).ToList();
            var duplicates = puzzleLocations.GroupBy(p => p).Where(g => g.Count() > 1).Select(g => g.Key).ToList();

            if (duplicates.Any())
            {
                Player.HeadMessage(150,"Crossing Paths located, Please adjust manually");
            }
            
            //Dictionary of Loacations and a bool if placed
            var positions = new Dictionary<int, Dictionary<TriPoint, bool>>();
            foreach (var path in _puzzlePathLocations)
            {
                var pathDict = new Dictionary<TriPoint, bool>();
                foreach (var point in path.Value.SelectMany(g => g.Value))
                {
                    pathDict.Add(point, false);
                }
                positions.Add(path.Key, pathDict);
            }
            Player.HeadMessage(150, "Path Plotted");
            UpdateFountainGump(positions);

            while (running)
            {
                if (!ProcessFountain)
                {
                    var fg = Gumps.GetGumpData(456426886);
                    if (fg?.buttonid == 1)
                    {
                        ProcessFountain = true;
                        Player.HeadMessage(150, "Start gathering parts");
                    }
                }

                if (!ProcessFountain)
                {
                    Misc.Pause(500);
                    continue;
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
                    if (!IsInRightPos(takePart))
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
                                var blockPart = partsInReach.FirstOrDefault(p => p.Position.X == point.X && p.Position.Y == point.Y);

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
                                    Items.MoveOnGround(dropPart, 1, point.X, point.Y, point.Z);
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
                                    if (posPoint.X == bcp.Position.X && posPoint.Y == bcp.Position.Y && posPoint.X == bcp.Position.X)
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

                UpdateFountainGump(positions, true);
                if (!StillInRoom(ShadowGuardRoom.Fountain))
                {
                    break;
                }
            }
            Misc.Pause(1000);
            Gumps.CloseGump(456426886);
        }

        private void UpdateFountainGump(Dictionary<int, Dictionary<TriPoint, bool>> paths, bool ready = false)
        {
            var gid = (uint)456426886;
            var fg = Gumps.CreateGump();
            Gumps.AddBackground(ref fg, 0,0,500,150,1755);
            var marginx = 15;
            var marginy = 15;
            var rowx = 30;
            var rowy = 30;
            var rowIndex = 0;
            foreach (var path in paths)
            {
                var gemIndex = 0;
                foreach (var pointValue in path.Value)
                {
                    if (pointValue.Value)
                    {
                        Gumps.AddImage(ref fg,marginx+rowx*gemIndex,marginy+rowy*rowIndex,5825);
                    }
                    else
                    {
                        Gumps.AddImage(ref fg,marginx+rowx*gemIndex,marginy+rowy*rowIndex,5831);
                    }

                    gemIndex++;
                }

                rowIndex++;
            }

            if (!ready)
            {
                Gumps.AddButton(ref fg, 400,100,2450,2451,1,1,0);
            }

            fg.gumpId = gid;
            fg.serial = (uint)Player.Serial;
            Gumps.CloseGump(gid);
            Gumps.SendGump(fg, 15,30);
        }

        private void HandleLobby()
        {
            Player.HeadMessage(180, "Entering Lobby");
            while (true)
            {
                Misc.Pause(5000);
                if (!StillInRoom(ShadowGuardRoom.Lobby))
                {
                    break;
                }
            }
        }

        private void HandleBelfry()
        {
            Player.HeadMessage(180, "Entering Belfry");
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
            Player.HeadMessage(180, "Entering Armory");
            var running = true;
            while (running)
            {
   
                var philactoryFilter = new Items.Filter
                {
                    Enabled = true,
                    OnGround = 1,
                    RangeMin = 0,
                    RangeMax = 2,
                    Graphics = new List<int>
                    {
                        0x4686,
                    }
                };

                var phil = Items.ApplyFilter(philactoryFilter).FirstOrDefault();
                if (phil != null)
                {
                    Items.Move(phil, Player.Backpack.Serial, 1);
                    Misc.Pause(300);
                }

                var flameFilter = new Items.Filter
                {
                    Enabled = true,
                    RangeMin = 0,
                    RangeMax = 3,
                    Name = "Purifying Flames"
                };

                var flame = Items.ApplyFilter(flameFilter).FirstOrDefault();
                if (flame != null)
                {
                    var ptc = Player.Backpack.Contains.Where(p => p.ItemID == 0x4686 && p.Hue == 0x081B).ToList();
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

                var tilesPlates = Items.ApplyFilter(new Items.Filter
                {
                    RangeMax = 1,
                    RangeMin = 0,
                    Graphics = new List<int>{0x9B39}
                });
                
                var px = Player.Position.X;
                var py = Player.Position.Y;
                if (tilesPlates.Count < 9)
                {
                    Misc.Pause(500);
                    continue;
                }

                var pps = Player.Backpack.Contains.Where(i => i.ItemID == 0x4686 && i.Hue == 0x0000).ToList();
                if (pps.Any())
                {
                    var armourFilter = new Items.Filter
                    {
                        Enabled = true,
                        RangeMin = 0,
                        RangeMax = 3,
                        Name = "Cursed Suit Of Armor",
                    };
                    var destroyed = new List<int>();
                    foreach (var pp in pps)
                    {
                        var armours = Items.ApplyFilter(armourFilter);
                        var tarArmour = armours.OrderBy(o => o.DistanceTo(_player)).FirstOrDefault(a => !destroyed.Contains(a.Serial));
                        if (tarArmour != null)
                        {
                            Items.UseItem(pp);
                            Target.WaitForTarget(1000);

                            Target.TargetExecute(tarArmour);
                            Misc.Pause(100);
                        }

                    }
                }

                if (!StillInRoom(ShadowGuardRoom.Armory))
                {
                    break;
                }

                Items.WaitForProps(Player.Backpack.Serial, 1000);
                Misc.Pause(50);
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
        
        private TriPoint GetCords(string value)
        {
            return new TriPoint(int.Parse(value.Split('|')[0]), int.Parse(value.Split('|')[1]), int.Parse(value.Split('|')[2]));
        }

        private void HandleOrchard()
        {
            var trees = new Dictionary<string, Item>();  
            var checkedTrees = new List<int>();
            Player.HeadMessage(180, "Entering Orchard");
            var running = true;
            while (running)
            {
                var tree = GetTree(checkedTrees,1);
                if (tree != null)
                {
                    Player.HeadMessage(1100, "Picking apple...");
                    Items.UseItem(tree);
                    Misc.Pause(500);
                    
                    var apple = GetApple();
                    if (apple != null)
                    {
                        checkedTrees.Add(tree.Serial);
                        var virt = apple.Name.Split(' ').Last().ToLower();
                        var opposite = GetOpposite(virt);
                        trees[virt] = tree;
                        Items.SetColor(tree.Serial, 0x0023);
                        
                        if (trees.ContainsKey(opposite))
                        {
                            Player.HeadMessage(1100, $"{virt} => {opposite}");
                            var oppositeTree = trees[opposite];
                            //Items.SetColor(oppositeTree.Serial, 0x0022);
                            Player.TrackingArrow(Convert.ToUInt16(oppositeTree.Position.X),Convert.ToUInt16(oppositeTree.Position.Y), true);
                            while (oppositeTree.DistanceTo(_player) > 8)
                            {
                                Misc.Pause(200);
                            }
                            Items.UseItem(apple);
                            Target.WaitForTarget(1000);
                            Target.TargetExecute(oppositeTree);
                            Player.TrackingArrow(Convert.ToUInt16(oppositeTree.Position.X),Convert.ToUInt16(oppositeTree.Position.Y), false);
                        }
                        else
                        {
                            Player.HeadMessage(1100, $"GUESS {trees.Count}");
                            var guessTree = GetTree(checkedTrees, 9);
                            while (guessTree == null)
                            {
                                guessTree = GetTree(checkedTrees, 9);
                                Misc.Pause(200);
                            }
                            
                            Items.UseItem(apple);
                            Target.WaitForTarget(1000);
                            Target.TargetExecute(guessTree);
                        }
                        
                    }
                    else
                    {
                        Player.HeadMessage(1100, "Failed to find apple!");
                    }
                }
                
                if (!StillInRoom(ShadowGuardRoom.Orchard))
                {
                    break;
                }
                Misc.Pause(50);
            }
        }



        private void HandleBar()
        {
            Player.HeadMessage(180, "Entering Bar");
            var running = true;
            while (running)
            {
                if (Player.Hits < 25)
                {
                    Misc.SendMessage("Heal thy self!");
                    Misc.Pause(5000);
                    continue;
                }
                
                Item useBottle = null;
                //Check backpack
                var backpackBottle = Player.Backpack.Contains.FirstOrDefault(i => i.ItemID == 0x099B && i.Name.Equals("a bottle of Liquor", System.StringComparison.InvariantCultureIgnoreCase));
                
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
            }

            return hasPirates || itemsInRoom.Any(i => i.Name == "a bottle of Liquor");
        }

        private Item GetTree(List<int> ignore, int maxDistance)
        {
            var treeFilter = new Items.Filter
            {
                Enabled = true,
                Name = "Cypress Tree",
                RangeMax = maxDistance,
            };
            return Items.ApplyFilter(treeFilter).Where(t => !ignore.Contains(t.Serial)).OrderBy(t => t.DistanceTo(_player)).FirstOrDefault();
        }

        private Item GetApple()
        {
            return Items.FindByID(0x09D0, 0x0000, Player.Backpack.Serial);
        }

        private string GetOpposite(string word)
        {
            if (_virtues.Contains(word))
            {
                return _dungeons[_virtues.IndexOf(word)];
            }
            else if (_dungeons.Contains(word))
            {
                return _virtues[_dungeons.IndexOf(word)];
            }

            Player.HeadMessage(1100, $"Lookup failed: {word}!");
            return null;
        }

        private ShadowGuardRoom GetCurrentRoom()
        {
            foreach (var v in rooms.Where(r => !r.Value))
            {
                switch (v.Key)
                {
                    case ShadowGuardRoom.Bar:
                        if (IsBar())
                        {
                            return ShadowGuardRoom.Bar;
                        }
                        break;
                    case ShadowGuardRoom.Orchard:
                        if (IsOrchard())
                        {
                            return ShadowGuardRoom.Orchard;
                        }
                        break;
                    case ShadowGuardRoom.Armory:
                        if (IsArmory())
                        {
                            return ShadowGuardRoom.Armory;
                        }
                        break;
                    case ShadowGuardRoom.Belfry:
                        if (IsBelfry())
                        {
                            return ShadowGuardRoom.Belfry;
                        }
                        break;
                    case ShadowGuardRoom.Lobby:
                        if (IsLobby())
                        {
                            return ShadowGuardRoom.Lobby;
                        }
                        break;
                    case ShadowGuardRoom.Fountain:
                        if (IsFountain())
                        {
                            return ShadowGuardRoom.Fountain;
                        }
                        break;
                }
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
            
            return itemsInRoom.Any(i => i.Name.Equals("Purifying Flames", System.StringComparison.InvariantCultureIgnoreCase));
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

        private bool IsLobby()
        {
            var items = Items.ApplyFilter(new Items.Filter
            {
                RangeMin = 0,
                RangeMax = 12,
            });
            
            return items.Any(i => i.Name.Equals("An Enchanting Crystal Ball", System.StringComparison.InvariantCultureIgnoreCase)) && items.Any(i => i.Name.Equals("ankh", System.StringComparison.InvariantCultureIgnoreCase));
        }
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

    internal class TriPoint
    {
        public TriPoint(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
    }
}