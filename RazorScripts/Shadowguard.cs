//Thank you for using this script, I hope it helps you in your adventures.
// Best Regards, Dorana

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Assistant;
using RazorEnhanced;
using Item = RazorEnhanced.Item;
using Mobile = RazorEnhanced.Mobile;
using Timer = System.Timers.Timer;

namespace RazorScripts
{
    public class Shadowguard
    {
        private string _version = "2.0.0";
        private uint _gumpId = (uint)456426886;
        private bool _runningMaster = true;
        private uint _gumpAboutId = (uint)24536236;
        private int _gumpHueActiveInfo = 0x16a;
        private int _gumpHueActiveWarning = 0x85;
        private int _gumpHueInfo = 0x7b;

        private enum Buttons
        {
            About = 10,
            Coffee = 11,
            ExitRoom = 12,
            ToggleRunning = 13,
            BelfryFly = 14,
        }

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
        //> 0x9BF8z 
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
        Timer timer = new Timer(1000);
        public void Run()
        {
            try
            {
                timer.Enabled = true;
                timer.Elapsed += (sender, e) => CheckMasterRunner();
                timer.AutoReset = true;
                timer.Start();
                _player = Mobiles.FindBySerial(Player.Serial);
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
            finally
            {
                timer.Stop();
                timer.Dispose();
            }

        }

        private void CheckMasterRunner()
        {
            var gumpData = Gumps.GetGumpData(_gumpId);
            if(gumpData.buttonid == (int)Buttons.ToggleRunning)
            {
                _runningMaster = !_runningMaster;
                gumpData.buttonid = -1;
            }
        }
        
        private void HandlePause(RoomData roomData)
        {
            bool hasUpdated = false;
            while (!_runningMaster)
            {
                if (!hasUpdated)
                {
                    UpdateShadowGuardGump(roomData);
                    hasUpdated = true;
                }
                Misc.Pause(500);
            }
        }
        private void UpdateShadowGuardGump(ShadowGuardRoom room)
        {
            var data = new RoomData(room, false);

            UpdateShadowGuardGump(data);
        }

        private void UpdateShadowGuardGump(RoomData roomData)
        {
            var gumpData = Gumps.GetGumpData(_gumpId);
            var aboutGumpData = Gumps.GetGumpData(_gumpAboutId);
            var buttonId = gumpData?.buttonid ?? -1;
            var aboutButtonId = aboutGumpData?.buttonid ?? -1;
            if (buttonId == (int)Buttons.About)
            {
                ShowAbout();
                buttonId = -1;
            }

            if (buttonId == (int)Buttons.ExitRoom)
            {
                Misc.WaitForContext(_player, 500);
                Misc.ContextReply(_player, 1);
                buttonId = -1;
            }

            // if (gumpData.buttonid == (int)Buttons.ToggleRunning)
            // {
            //     ToggleRunning();
            //     gumpData.buttonid = -1;
            // }

            if (aboutButtonId == (int)Buttons.Coffee)
            {
                System.Diagnostics.Process.Start("https://www.buymeacoffee.com/Dorana");
                buttonId = -1;
            }

            var width = 350;
            var marginTop = 100;

            var fg = Gumps.CreateGump();
            fg.buttonid = buttonId;
            Gumps.AddBackground(ref fg, 0, 0, width, marginTop, 1755);
            Gumps.AddLabel(ref fg, 15, 15, _gumpHueInfo, "Shadowguard by Dorana");
            Gumps.AddLabel(ref fg, 15, 40, _gumpHueInfo, "Rurrent Room: ");
            Gumps.AddLabel(ref fg, 105, 40, _gumpHueActiveInfo, roomData.Room.ToString());
            Gumps.AddLabel(ref fg, 260, 15, _gumpHueInfo, "Running :");
            Gumps.AddButton(ref fg, 320, 18, (_runningMaster ? 11400 : 11410), _runningMaster ? 11402 : 11412,
                (int)Buttons.ToggleRunning, 1, 0);
            Gumps.AddLabel(ref fg, 260, 75, _gumpHueInfo, $"Version: {_version}");
            Gumps.AddButton(ref fg, 315, 45, 40024, 40024, (int)Buttons.About, 1, 0);
            if (roomData.Room != ShadowGuardRoom.Lobby)
            {
                Gumps.AddButton(ref fg, 160, 38, 241, 242, (int)Buttons.ExitRoom, 1, 0);
                var secondsElipsed = (int)Math.Ceiling((DateTime.UtcNow - roomData.EntryTime).TotalSeconds);
                var timeLeft = 1800 - secondsElipsed;
                var fraction = (decimal)timeLeft / 1800;
                Gumps.AddLabel(ref fg, 15, 65, _gumpHueInfo, "Room Timer: ");
                Gumps.AddBackground(ref fg, 95, 69, 109, 11, 2053);
                Gumps.AddImageTiled(ref fg, 95, 69, (int)Math.Floor(fraction * 109), 11, 2056);
                //remaining seconds in MM:ss
                var timeString = $"{timeLeft / 60}:{timeLeft % 60}";
                Gumps.AddLabel(ref fg, 210, 65, _gumpHueActiveInfo, timeString);
            }

            if (roomData.Room == ShadowGuardRoom.Fountain)
            {
                AddFountainGumpData(fg, roomData, marginTop, width);
            }

            if (roomData.Room == ShadowGuardRoom.Orchard)
            {
                AddOrchardGumpData(fg, roomData, marginTop, width);
            }

            if (roomData.Room == ShadowGuardRoom.Armory)
            {
                AddArmoryGumpData(fg, roomData, marginTop, width);
            }

            if (roomData.Room == ShadowGuardRoom.Bar)
            {
                AddBarGumpData(fg, roomData, marginTop, width);
            }

            if (roomData.Room == ShadowGuardRoom.Belfry)
            {
                AddBelfryGumpData(fg, roomData, marginTop, width);
            }

            fg.gumpId = _gumpId;
            fg.serial = (uint)Player.Serial;
            Gumps.CloseGump(_gumpId);
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
                Gumps.AddLabel(ref fg, marginx + partindex * 50 + 10, marginTop + 170, _gumpHueActiveInfo,
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
            Gumps.AddLabel(ref fg, 15, 15 + marginTop, _gumpHueActiveInfo, task);
            if (target != string.Empty && task.Equals("Approach Tree", StringComparison.InvariantCultureIgnoreCase))
            {
                Gumps.AddLabel(ref fg, 15, 15 + marginTop + 25, _gumpHueActiveInfo, target);
            }

            for (int i = 0; i < 8; i++)
            {
                var hue = clearedIndexes.Contains(i) ? _gumpHueActiveInfo : _gumpHueActiveWarning;
                Gumps.AddLabel(ref fg, 180, 15 + marginTop + 25 * i, hue, _dungeons[i].AsName());
                Gumps.AddLabel(ref fg, 265, 15 + marginTop + 25 * i, hue, _virtues[i].AsName());
            }
        }

        private void AddArmoryGumpData(Gumps.GumpData fg, RoomData roomData, int marginTop, int width)
        {
            var remaining = roomData.GetParam<int>(0);
            var philinBags = roomData.GetParam<List<Item>>(1);
            var task = roomData.GetParam<string>(2);
            
            var minHeight = 150;
            var neededHeight = philinBags.Count * 20 + 35;
            var useHeight = neededHeight > minHeight ? neededHeight : minHeight;
            
            Gumps.AddBackground(ref fg, 0, marginTop, width, useHeight, 1755);
            Gumps.AddLabel(ref fg, 15, 15 + marginTop, _gumpHueActiveInfo, task);
            Gumps.AddItem(ref fg, 15, 45 + marginTop, 0x151A, 0);
            Gumps.AddLabel(ref fg, 20, 115 + marginTop, _gumpHueActiveInfo, remaining.ToString());
            Gumps.AddLabel(ref fg, 130, 15 + marginTop, _gumpHueActiveInfo, "Phylacteries");
            var timerIndex = 0;
            foreach (var philactory in philinBags)
            {
                var lifeSpanProp = philactory.Properties.FirstOrDefault(p => p.Number == 1072517);
                if (lifeSpanProp != null)
                {
                    var life = lifeSpanProp.Args;
                    if (int.TryParse(life, out int remainingLife))
                    {
                        var fraction = (decimal)remainingLife / 60;
                        Gumps.AddBackground(ref fg, 110, marginTop + 35 + timerIndex * 20, 109, 11, 2053);
                        Gumps.AddImageTiled(ref fg, 110, marginTop + 35 + timerIndex * 20,
                            (int)Math.Floor(fraction * 109), 11, 2056);
                        timerIndex++;
                    }
                }
            }
        }

        private void AddBarGumpData(Gumps.GumpData fg, RoomData roomData, int marginTop, int width)
        {
            var lines = roomData.GetParam<List<string>>(0);
            var bottleCount = roomData.GetParam<int>(1);
            var warning = roomData.GetParam<bool>(2);
            Gumps.AddBackground(ref fg, 0, marginTop, width, 80, 1755);
            var lineIndex = 0;
            var hue = warning ? _gumpHueActiveWarning : _gumpHueActiveInfo;
            foreach (var line in lines)
            {
                Gumps.AddLabel(ref fg, 15, 15 + marginTop + 25 * lineIndex, hue, line);
                lineIndex++;
            }

            Gumps.AddItem(ref fg, 250, 15 + marginTop, 0x099B, 0);
            Gumps.AddLabel(ref fg, 285, 15 + marginTop, _gumpHueActiveInfo, bottleCount.ToString());
        }

        private void AddBelfryGumpData(Gumps.GumpData fg, RoomData roomData, int marginTop, int width)
        {
            var hasWing = roomData.GetParam<bool>(0);

            Gumps.AddBackground(ref fg, 0, marginTop, width, 50, 1755);
            if (hasWing)
            {
                Gumps.AddLabel(ref fg, 15, 15 + marginTop, _gumpHueActiveInfo, "Fly you fool!");
                Gumps.AddButton(ref fg, 100, 15 + marginTop, 247, 248, (int)Buttons.BelfryFly, 1, 0);
            }
            else
            {
                Gumps.AddLabel(ref fg, 15, 15 + marginTop, _gumpHueActiveInfo, "Kill Dragons, The wing will Auto loot");
            }
        }

        private void ToggleRunning()
        {
            _runningMaster = !_runningMaster;
        }

        private void ShowAbout()
        {
            var about = Gumps.CreateGump();
            Gumps.AddBackground(ref about, 0, 0, 426, 229, -1);
            Gumps.AddImage(ref about, 0, 0, 11055);
            Gumps.AddHtml(ref about, 95, 25, 400, 20, "<h1>About</h1>", false, false);
            Gumps.AddLabel(ref about, 55, 50, 0, "Shadowguard is created");
            Gumps.AddLabel(ref about, 55, 62, 0, "and is maintained by");
            Gumps.AddLabel(ref about, 55, 74, 0, "Matt Dorana");
            Gumps.AddLabel(ref about, 55, 98, 0, "It is free to use and");
            Gumps.AddLabel(ref about, 55, 110, 0, "will receive updates");
            Gumps.AddLabel(ref about, 55, 122, 0, "on the feedback");
            Gumps.AddLabel(ref about, 55, 134, 0, "and requests that are");
            Gumps.AddLabel(ref about, 55, 146, 0, "sent in");
            Gumps.AddLabel(ref about, 220, 50, 0, "If you enjoy this");
            Gumps.AddLabel(ref about, 220, 62, 0, "script feel free to");
            Gumps.AddLabel(ref about, 220, 74, 0, "reach out to me on");
            Gumps.AddLabel(ref about, 220, 86, 0, "Discord");
            Gumps.AddLabel(ref about, 250, 164, 0, "Buy me a coffee");
            Gumps.AddButton(ref about, 215, 159, 5843, 5844, (int)Buttons.Coffee, 1, 0);


            about.serial = (uint)Player.Serial;
            about.gumpId = _gumpAboutId;

            Gumps.CloseGump(_gumpAboutId);
            Gumps.SendGump(about, 500, 350);
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

        private bool StillInRoom(ShadowGuardRoom room)
        {
            var found = GetCurrentRoom();
            if (found == ShadowGuardRoom.Unknown)
            {
                return true;
            }

            return found == room;
        }

        private void HandleBar()
        {
            var running = true;
            var lines = new List<string>
            {
                "Run close to bottles",
                "Bottles are thrown at pirate automatically"
            };
            var roomData = new RoomData(ShadowGuardRoom.Bar, lines, 0, true);
            while (running)
            {
                HandlePause(roomData);

                if (!StillInRoom(ShadowGuardRoom.Bar))
                {
                    break;
                }

                roomData.Params[0] = new List<string>
                {
                    "Run close to bottles",
                    "Bottles are thrown at pirate automatically"
                };

                roomData.Params[2] = false;

                var bottles = Player.Backpack.Contains.Where(i =>
                    i.ItemID == 0x099B && i.Name.Equals("a bottle of Liquor",
                        System.StringComparison.InvariantCultureIgnoreCase)).ToList();

                roomData.Params[1] = bottles.Count;

                if (Player.Hits < 35)
                {
                    Misc.SendMessage("Heal thy self!");
                    roomData.Params[0] = new List<string>
                    {
                        "Low Health Detected",
                        "Heal thy self!"
                    };
                    roomData.Params[2] = true;
                    UpdateShadowGuardGump(roomData);
                    Misc.Pause(5000);
                    continue;
                }

                UpdateShadowGuardGump(roomData);

                Item useBottle = null;
                //Check backpack
                var backpackBottle = bottles.FirstOrDefault();

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

                Misc.Pause(400);
            }
        }

        private void HandleOrchard()
        {
            var task = "Approach all corners";
            var running = true;
            List<int> clearedIndexes = new List<int>();
            var roomData = new RoomData(ShadowGuardRoom.Orchard, clearedIndexes, task, string.Empty);
            UpdateShadowGuardGump(roomData);
            List<Item> allTrees = GetTrees();
            var grouped = FindPairs(allTrees.Select(t => t.Serial).ToList());

            roomData.Params[1] = "Pick an Apple";
            Item heldApple = null;
            Item nearestTree = null;
            while (running)
            {
                HandlePause(roomData);
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
                        Misc.Pause(500);
                        continue;
                    }

                    Items.UseItem(nearestTree);
                    Misc.Pause(500);
                }

                heldApple = GetApple();
                Misc.Pause(50);

                if (heldApple == null)
                {
                    Misc.Pause(500);
                    continue;
                }

                roomData.Params[1] = "Approach Tree";
                var sourcename = heldApple.Name.Split(' ').Last().ToLower();
                var pairIndex = 0;
                if (_dungeons.Contains(sourcename))
                {
                    pairIndex = _dungeons.IndexOf(sourcename);
                    roomData.Params[2] = _virtues[pairIndex].AsName();
                }
                else if (_virtues.Contains(sourcename))
                {
                    pairIndex = _virtues.IndexOf(sourcename);
                    roomData.Params[2] = _dungeons[pairIndex].AsName();
                }
                else if (sourcename.Equals("Sacrifice"))
                {
                    pairIndex = _virtues.IndexOf("Sacrafice"); //handles misspelling on serverside
                    if (pairIndex > -1)
                    {
                        roomData.Params[2] = _dungeons[pairIndex].AsName();
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
                HandlePause(roomData);

                var haspaused = 0;
                if (!StillInRoom(ShadowGuardRoom.Armory))
                {
                    break;
                }

                var newFoundArmors = GetAllArmors(false);
                var potentialBroken = allArmors.Where(a => !newFoundArmors.Select(n => n.Serial).Contains(a.Serial))
                    .ToList();

                foreach (var pb in potentialBroken)
                {
                    if (pb.DistanceTo(_player) < 20)
                    {
                        if (!brokenArmor.Select(b => b.Serial).Contains(pb.Serial))
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

                UpdateShadowGuardGump(roomData);
                ;

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
                        haspaused += 100;
                    }
                }

                if (Target.HasTarget())
                {
                    Misc.Pause(100);
                    haspaused += 100;
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
                            haspaused += 100;
                        }
                    }
                }

                var pausefor = 500 + haspaused;
                Misc.Pause(pausefor);
            }
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
                grid.Where(t => t.X == start.X + 1 && t.Y == start.Y).First().Cost = 50;
                grid.Where(t => t.X == start.X - 1 && t.Y == start.Y).First().Cost = 50;
                grid.Where(t => t.X == start.X && t.Y == start.Y - 1).First().Cost = 50;

                BuildPath(grid, pathId, start, goal, FountainEntryPoint.North);

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

                grid.Where(t => t.X == start.X && t.Y == start.Y + 1).First().Cost = 50;
                grid.Where(t => t.X == start.X && t.Y == start.Y - 1).First().Cost = 50;
                grid.Where(t => t.X == start.X - 1 && t.Y == start.Y).First().Cost = 50;

                BuildPath(grid, pathId, start, goal, FountainEntryPoint.West);

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
                    if (!partsNeeded.ContainsKey(canalPeice.Key))
                    {
                        partsNeeded.Add(canalPeice.Key, 0);
                    }

                    var locations = canalPeice.Value;
                    var posData = positions[path.Key];
                    var count = posData.Count(p => locations.Contains(p.Key) && !p.Value);
                    partsNeeded[canalPeice.Key] += count;
                }
            }
            var roomData = new RoomData(ShadowGuardRoom.Fountain, positions, partsNeeded);

            while (running)
            {
                HandlePause(roomData);
                UpdateShadowGuardGump(roomData);
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
                        if (!partsNeeded.ContainsKey(canalPeice.Key))
                        {
                            partsNeeded.Add(canalPeice.Key, 0);
                        }

                        var locations = canalPeice.Value;
                        var posData = positions[path.Key];
                        var count = posData.Count(p => locations.Contains(p.Key) && !p.Value);
                        partsNeeded[canalPeice.Key] += count;
                    }
                }

                
            }

            Misc.Pause(1000);
            Gumps.CloseGump(456426886);
        }

        private void HandleLobby()
        {
            var roomData = new RoomData(ShadowGuardRoom.Lobby);
            while (true)
            {
                HandlePause(roomData);
                UpdateShadowGuardGump(roomData);
                if (!StillInRoom(ShadowGuardRoom.Lobby))
                {
                    break;
                }
                Misc.Pause(500);
            }
        }

        private void handleRoof()
        {
            var roomData = new RoomData(ShadowGuardRoom.Roof);
            while (true)
            {
                HandlePause(roomData);
                
                UpdateShadowGuardGump(roomData);
                Misc.Pause(500);
                if (!StillInRoom(ShadowGuardRoom.Roof))
                {
                    break;
                }
            }
        }

        private void HandleBelfry()
        {
            var roomData = new RoomData(ShadowGuardRoom.Belfry, false);
            var running = true;
            var hasWing = false;
            var ignoreCopses = new List<int>();
            while (running)
            {
                HandlePause(roomData);

                var haspaused = 0;
                if (!StillInRoom(ShadowGuardRoom.Belfry))
                {
                    break;
                }

                var bpWing = Player.Backpack.Contains.FirstOrDefault(i => i.ItemID == 0x1E85);
                if (bpWing != null)
                {
                    var gumpData = Gumps.GetGumpData(_gumpId);
                    if (gumpData.buttonid == (int)Buttons.BelfryFly)
                    {
                        Items.UseItem(bpWing);
                        gumpData.buttonid = -1;
                    }

                    hasWing = true;
                }

                if (hasWing)
                {
                    roomData.Params[0] = true;
                    UpdateShadowGuardGump(roomData);
                    Misc.Pause(500);
                    continue;
                }

                hasWing = false;
                roomData.Params[0] = false;

                Misc.Pause(200);
                haspaused += 200;

                UpdateShadowGuardGump(roomData);

                var corpses = Items.ApplyFilter(new Items.Filter
                {
                    Enabled = true,
                    OnGround = 1,
                    RangeMin = 0,
                    RangeMax = 2,
                    IsCorpse = 1,

                }).ToList();

                if (Target.HasTarget())
                {
                    Misc.Pause(500);
                    continue;
                }

                foreach (var corpse in corpses.Where(c => !ignoreCopses.Contains(c.Serial)))
                {
                    Items.WaitForContents(corpse, 1000);
                    Misc.Pause(300);
                    var wing = corpse.Contains.FirstOrDefault(f => f.ItemID == 0x1E85);
                    if (wing != null)
                    {
                        Items.Move(wing.Serial, Player.Backpack.Serial, 1);
                        Misc.Pause(500);
                        haspaused += 500;
                        ignoreCopses.Add(corpse.Serial);
                        break;
                    }

                    Misc.Pause(500);
                    haspaused += 500;
                }

                ;

                Misc.Pause(500 - haspaused);
            }
        }

        

        private ShadowGuardRoom GetCurrentRoom()
        {

            if (IsBar())
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
                if (hasPirates)
                {
                    break;
                }
            }

            return hasPirates || itemsInRoom.Any(i => i.Name == "a bottle of Liquor");
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
                RangeMax = 20,
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
                RangeMax = 30,
            });

            return items.Any(i =>
                       i.Name.Equals("An Enchanting Crystal Ball",
                           System.StringComparison.InvariantCultureIgnoreCase)) &&
                   items.Any(i => i.Name.Equals("ankh", System.StringComparison.InvariantCultureIgnoreCase));
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

        private void BuildPath(List<Point> grid, int pathId, Point start, Point end,
            FountainEntryPoint originalEntryPoint)
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
                var currentTile = tileIds[i + 1];
                _puzzlePathLocations[pathId][currentTile].Add(skipFirstandlast[i]);
            }
        }

        public static List<int> GetTileIdsFromPath(List<Point> path)
        {
            var tileIds = new List<int>();
            tileIds.Add(0);
            for (int i = 1; i < path.Count - 1; i++)
            {
                tileIds.Add(GetTileId(path[i], path[i + 1], path[i - 1]));
            }

            return tileIds;
        }

        public static int GetTileId(Point current, Point next, Point previous)
        {
            var dx = next.X - current.X;
            var dy = next.Y - current.Y;
            var pdx = current.X - previous.X;
            var pdy = current.Y - previous.Y;
            //< 0x9BEF
            //^ 0x9BFC
            //> 0x9BF8
            //V 0x9BEB
            // \ 0x9BF4
            if (pdy == 1) //arriving from North
            {
                if (dx == 1) //going East
                {
                    return 0x9BEF;
                }

                if (dx == -1) //going West
                {
                    return 0x9BEB;
                }

                if (dy == 1) //going South
                {
                    return 0x9BE7;
                }
            }

            if (pdy == -1) //arriving from South
            {
                if (dx == 1) //going East
                {
                    return 0x9BFC;
                }

                if (dx == -1) //going West
                {
                    return 0x9BF8;
                }

                if (dy == -1) //going North
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

            if (pdx == -1) //arriving from East
            {
                if (dy == 1) //going South
                {
                    return 0x9BFC;
                }

                if (dy == -1) //going North
                {
                    return 0x9BEF;
                }

                if (dx == -1) //going West
                {
                    return 0x9BF4;
                }
            }


            throw new Exception("Impossible pathing");
        }

        public int[,] ConvertToGrid(List<Point> points)
        {
            if (points == null || points.Count == 0)
            {
                throw new ArgumentException("The points list cannot be null or empty.");
            }

            int maxX = points.Max(p => p.X) + 1;
            int maxY = points.Max(p => p.Y) + 1;

            int[,] grid = new int[maxX, maxY];

            foreach (var point in points)
            {
                grid[point.X, point.Y] = point.Cost;
            }

            return grid;
        }

        public List<Point> FindPath(int[,] grid, Point start, Point end)
        {
            int rows = grid.GetLength(0);
            int cols = grid.GetLength(1);

            var openSet = new SortedSet<(Point Point, int FCost, int GCost)>(
                Comparer<(Point Point, int FCost, int GCost)>.Create((a, b) =>
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

            while (openSet.Count > 0)
            {
                var current = openSet.First().Point;
                openSet.Remove(openSet.First());

                if (current.Equals(end))
                {
                    return ReconstructPath(cameFrom, current);
                }

                foreach (var neighbor in GetNeighbors(current, rows, cols, grid))
                {
                    int tentativeGCost = gCost[current] + neighbor.Cost;

                    if (!gCost.ContainsKey(neighbor) || tentativeGCost < gCost[neighbor])
                    {
                        cameFrom[neighbor] = current;
                        gCost[neighbor] = tentativeGCost;
                        fCost[neighbor] = tentativeGCost + Heuristic(neighbor, end);

                        if (!openSet.Any(item => item.Point.Equals(neighbor)))
                        {
                            openSet.Add((neighbor, fCost[neighbor], gCost[neighbor]));
                        }
                    }
                }
            }

            Misc.SendMessage("Unable To find path");

            return new List<Point>(); // Return an empty path if no path exists
        }

        private static int Heuristic(Point a, Point b)
        {
            // Chebyshev distance for diagonal preference
            return Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
        }

        private static List<Point> GetNeighbors(Point point, int rows, int cols, int[,] grid)
        {
            var neighbors = new List<Point>();
            var directions = new List<(int X, int Y)>
            {
                (0, -1), // Up
                (0, 1), // Down
                (-1, 0), // Left
                (1, 0), // Right
            };

            foreach (var (dx, dy) in directions)
            {
                int newX = point.X + dx;
                int newY = point.Y + dy;
                if (newX >= 0 && newX < rows && newY >= 0 && newY < cols)
                {
                    int additionalCost = Math.Abs(dx) + Math.Abs(dy) == 2 ? 1 : 0; // Favor diagonal moves
                    neighbors.Add(new Point(newX, newY, grid[newX, newY] + additionalCost));
                }
            }

            return neighbors;
        }

        private static List<Point> ReconstructPath(Dictionary<Point, Point> cameFrom, Point current)
        {
            var path = new List<Point> { current };
            while (cameFrom.ContainsKey(current))
            {
                current = cameFrom[current];
                path.Add(current);
            }

            path.Reverse();
            return path;
        }

    }

    public static class StringExtension
    {
        public static string AsName(this string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            return value.First().ToString().ToUpper() + value.Substring(1);
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