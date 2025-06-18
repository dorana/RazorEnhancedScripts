using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Assistant;
using RazorEnhanced;
using Gumps = RazorEnhanced.Gumps;
using Item = RazorEnhanced.Item;

namespace Razorscripts
{
    public class MiningMaster
    {
        private List<int> _runeIds = new List<int>
        {
            0x1F14, 
            0x1F15,
            0x1F16,
            0x1F17
        };
        
        private List<int> _scrollIds = new List<int>
        {
            0x0E34, 
            0x0E35,
            0x0E36,
            0x0E37,
            0x0E38,
            0x0E39,
            0x0E3A
        };
        
        private string _version = "1.0.2";
        
        private bool _useBagOfSending = false;
        private MiningTool _tool = MiningTool.Shovel;
        private bool _pathSelectorEnabled = false;
        private PathData _editPath = null;
        
        
        
        private ScriptState _state = ScriptState.Idle;
        private MiningConfig _config = new MiningConfig();
        
        private Journal _journal = new Journal();
        private Journal.JournalEntry _journalEntry = null;

        private Item TinkerTool => GetTinkerTool();
        private Item DiggingTool => GetDiggingTool();
        private string _selectedPath = string.Empty;

        private List<int> directSmelts = new List<int>
        {
            0x19B8,
            0x19BA,
            0x19B9
        };
        private List<int> needs2Smelts = new List<int>
        {
            0x19B7
        };



        public void Run()
        {
            try
            {
                LoadConfig();
                
                _journalEntry = _journal.GetJournalEntry(_journalEntry).OrderBy(je => je.Timestamp).LastOrDefault();

                UpdateGump();

                while (true)
                {
                    if (_state == ScriptState.Idle)
                    {
                        var gd = Gumps.GetGumpData(786543548);
                        if (gd.buttonid == (int)Button.RecordingStart)
                        {
                            _state = ScriptState.Recording;
                            UpdateGump();
                        }
                        else if (gd.buttonid == (int)Button.PathSelect)
                        {
                            _pathSelectorEnabled = !_pathSelectorEnabled;
                            gd.buttonid = -1;
                            UpdateGump();
                        }
                        else if (gd != null && gd.buttonid != -1 && gd.buttonid != 0)
                        {
                            if (gd.buttonid == -2)
                            {
                                _editPath = null;
                                gd.buttonid = -1;
                                UpdateGump();
                                continue;
                            }
                            var index = gd.buttonid - 1;
                            if (index >= 20000)
                            {
                                index -= 20000;
                                _config.Paths.RemoveAt(index);
                                _editPath = null;
                                SaveConfig();
                                gd.buttonid = -1;
                                UpdateGump();
                                continue;
                            }
                            
                            if (index >= 10000)
                            {
                                index -= 10000;
                                _editPath = _config.Paths[index];
                                gd.buttonid = -1;
                                UpdateGump();
                                continue;
                            }
                            
                            _selectedPath = _config.Paths[index].Name;
                            _state = ScriptState.Playback;
                            gd.buttonid = -1;
                            _pathSelectorEnabled = false;
                            UpdateGump();
                            continue;
                        }
                    }
                    else if (_state == ScriptState.Playback)
                    {
                        var activePath = _config.Paths.FirstOrDefault(p =>
                            p.Name.Equals(_selectedPath, StringComparison.OrdinalIgnoreCase));
                        if (activePath == null)
                        {
                            _state = ScriptState.Idle;
                            Player.HeadMessage(33, "Path not found");
                            UpdateGump();
                            continue;
                        }

                        var start = activePath.Path.First();
                        var dist = Misc.Distance(Player.Position.X, Player.Position.Y, start.X, start.Y);

                        if (dist > 10)
                        {
                            RuneBinder? binder = null;
                            var books = Player.Backpack.Contains.Where(i => i.ItemID == 0x22C5).ToList();
                            var runeIndex = -1;
                            foreach (var book in books)
                            {
                                Items.UseItem(book);
                                Misc.Pause(200);
                                if (Gumps.HasGump(0x59))
                                {
                                    var gd = Gumps.GetGumpData(0x59);
                                    List<string> runeNameLines = gd.stringList.Skip(2).Take(16).Where(s => !s.Equals("Empty", StringComparison.OrdinalIgnoreCase)).Select(x => x.ToLower()).ToList();

                                    runeIndex = runeNameLines.IndexOf(_selectedPath.ToLower());
                                    if (runeIndex != -1)
                                    {
                                        binder = RuneBinder.Book;
                                        break;
                                    }
                                }
                            }
                            
                            if (runeIndex == -1)
                            {
                                Gumps.CloseGump(0x59);
                                var atlases = Player.Backpack.Contains.Where(i => i.ItemID == 0x9C16).ToList();
                                foreach (var atlas in atlases)
                                {
                                    var page = 1;
                                    Items.UseItem(atlas);
                                    Misc.Pause(200);
                                    while (!Gumps.HasGump(0x1f2))
                                    {
                                        Misc.Pause(200);
                                        Items.UseItem(atlas);
                                    }

                                    while (page <= 3)
                                    {
                                        var gd = Gumps.GetGumpData(0x1f2);
                                        List<string> lines = gd.stringList;
                                        var runeIndexStart = 1;
                                        var endIndex = lines.IndexOf(lines.First(l => l.StartsWith("<center>")));
                                        var runeNamesLines = lines.GetRange(runeIndexStart, endIndex - runeIndexStart)
                                            .Where(l => !l.Equals("Empty")).ToList();
                                        runeIndex = runeNamesLines.IndexOf(_selectedPath);
                                        if (runeIndex != -1)
                                        {
                                            binder = RuneBinder.Atlas;
                                            break;
                                        }

                                        if (Gumps.HasGump(0x1f2))
                                        {
                                            Gumps.SendAction(0x1f2, 1150);
                                        }

                                        Misc.Pause(200);
                                        page++;
                                    }

                                    if (runeIndex != -1)
                                    {
                                        break;
                                    }

                                    Gumps.CloseGump(0x1f2);
                                }
                                
                                if(runeIndex == -1)
                                {
                                    Player.HeadMessage(33, "Rune not found");
                                    _state = ScriptState.Idle;
                                    _selectedPath = string.Empty;
                                    UpdateGump();
                                    Misc.Pause(500);
                                    continue;
                                }
                            }
                            

                            UpdateGump();
                            var magery = Player.GetRealSkillValue("Magery");
                            var chivalry = Player.GetRealSkillValue("Chivalry");
                            if(binder == RuneBinder.Book)
                            {
                                var baseAcction = magery > chivalry ? 50 : 75;
                                Gumps.SendAction(0x59, baseAcction + runeIndex);
                            }
                            else if(binder == RuneBinder.Atlas)
                            {
                                Misc.Pause(200);
                                Gumps.SendAction(0x1f2, 100 + runeIndex);
                                while (!Gumps.HasGump(0x1f2))
                                {
                                    Misc.Pause(50);
                                }
                                var portAction = magery > chivalry ? 4 : 7;
                                Gumps.SendAction(0x1f2, portAction);
                            }

                            Misc.Pause(5000);
                        }

                        if (Player.Mount != null)
                        {
                            Player.HeadMessage(51, "Please Dismount");
                            while (Player.Mount != null)
                            {
                                Misc.Pause(1000);
                            }
                        }
                        
                        if(!CheckSmelter())
                        {
                            _state = ScriptState.Idle;
                            UpdateGump();
                            continue;
                        }

                        foreach (var point in activePath.Path)
                        {
                            var gd = Gumps.GetGumpData(786543548);
                            if (gd.buttonid == (int)Button.PlaybackStop)
                            {
                                _state = ScriptState.Idle;
                                gd.buttonid = -1;
                                UpdateGump();
                                break;
                            }

                            PathFinding.PathFindTo(point.X, point.Y, point.Z);
                            Misc.Pause(500);
                            if (point.MineSpot)
                            {
                                HandleMiningSpot();
                            }
                        }
                        _selectedPath = string.Empty;
                        _state = ScriptState.Idle;
                        UpdateGump();
                    }
                    else if (_state == ScriptState.Recording)
                    {
                        _journalEntry =
                            _journal.GetJournalEntry(_journalEntry).OrderBy(je => je.Timestamp).LastOrDefault() ??
                            _journalEntry;
                        var path = new List<Point>();
                        while (true)
                        {
                            var gd = Gumps.GetGumpData(786543548);
                            if (gd.buttonid == (int)Button.AddWaypoint)
                            {
                                var currentWaypoint = new Point(Player.Position.X, Player.Position.Y, Player.Position.Z,
                                    true);
                                var last = path.LastOrDefault();
                                if (last == null || !last.Equals(currentWaypoint))
                                {
                                    path.Add(currentWaypoint);
                                    Misc.SendMessage(
                                        $"Added waypoint : {currentWaypoint.X}, {currentWaypoint.Y}, {currentWaypoint.Z}");
                                }

                                gd.buttonid = -1;
                                UpdateGump();
                                continue;
                            }

                            if (gd.buttonid == (int)Button.RecordingSave)
                            {
                                var pathName = gd.text.FirstOrDefault();
                                if (string.IsNullOrEmpty(pathName))
                                {
                                    Player.HeadMessage(33, "Path name cannot be empty");
                                    gd.buttonid = -1;
                                    UpdateGump();
                                    continue;
                                }
                                
                                var existing = _config.Paths.Select(p => p.Name.ToLower()).ToList();
                                if (existing.Contains(pathName.ToLower()))
                                {
                                    Player.HeadMessage(33, "Path with that name already exists");
                                    gd.buttonid = -1;
                                    UpdateGump();
                                    continue;
                                }
                                
                                _config.Paths.Add(new PathData
                                {
                                    Name = pathName,
                                    Path = path
                                });
                                
                                SaveConfig();
                                Player.HeadMessage(33, "Path saved");

                                if (gd.switches.Any(s => s == 1))
                                {
                                    var magerySkill = Player.GetSkillValue("Magery");
                                    var runes = Player.Backpack.Contains.Where(i => _runeIds.Contains(i.ItemID))
                                        .ToList();
                                    
                                    runes.ForEach(rune => Items.WaitForProps(rune, 1000));
                                    var blank = runes.FirstOrDefault(r => r.Properties.Count == 2);
                                    if (blank == null)
                                    {
                                        var markBook = Player.Backpack.Contains.FirstOrDefault(i => i.Name.Equals("Recall Rune Tome", StringComparison.InvariantCultureIgnoreCase));
                                        if (markBook != null)
                                        {
                                            Items.UseItem(markBook);
                                            blank = Player.Backpack.Contains.FirstOrDefault(r => _runeIds.Contains(r.ItemID) && r.Properties.Count == 2);
                                            while (blank == null)
                                            {
                                                blank = Player.Backpack.Contains.FirstOrDefault(r => _runeIds.Contains(r.ItemID) && r.Properties.Count == 2);
                                            }
                                        }
                                    }

                                    if (blank != null)
                                    {
                                        if (magerySkill >= 70)
                                        {
                                            var success = false;
                                            while (!success)
                                            {
                                                Spells.CastMagery("Mark");
                                                Target.WaitForTarget(3000);
                                                Misc.Pause(100);
                                                Target.TargetExecute(blank);
                                                Misc.Pause(1000);
                                                Items.WaitForProps(blank, 1000);
                                                if (blank.Properties.Count > 2)
                                                {
                                                    success = true;
                                                }
                                                else
                                                {
                                                    Misc.Pause(2000);
                                                }
                                            }
                                        }
                                        else
                                        {
                                            var success = false;
                                            while (!success)
                                            {
                                                var markScrolls = Player.Backpack.Contains.Where(i => _scrollIds.Contains(i.ItemID) && i.Name.EndsWith("Mark")).ToList();
                                                if (!markScrolls.Any())
                                                {
                                                    Misc.SendMessage("No mark Scrolls found");
                                                    break;
                                                }
                                                
                                                Items.UseItem(markScrolls.First());
                                                Target.WaitForTarget(3000);
                                                Misc.Pause(100);
                                                Target.TargetExecute(blank);
                                                Misc.Pause(1000);
                                                Items.WaitForProps(blank, 1000);
                                                if (blank.Properties.Count > 2)
                                                {
                                                    success = true;
                                                }
                                                else
                                                {
                                                    Misc.Pause(2000);
                                                }
                                            }
                                        }

                                        Items.UseItem(blank);
                                            Misc.Pause(500);
                                            Misc.ResponsePrompt(pathName);
                                            Misc.Pause(500);
                                            
                                            var books = Player.Backpack.Contains.Where(i => i.ItemID == 0x22C5).ToList();
                                            var atlases = Player.Backpack.Contains.Where(i => i.ItemID == 0x9C16).ToList();
                                            
                                            var totalFound = books.Count() + atlases.Count();

                                            Item targetBook = null;
                                            
                                            if (totalFound > 1)
                                            {
                                                var targetAccepted = false;
                                                books.ForEach(b => Items.WaitForProps(b, 1000));
                                                atlases.ForEach(a => Items.WaitForProps(a, 1000));
                                                
                                                var mbooks = books.Where(b => b.Properties.Any(p => p.Number == 1042971 && p.Args.StartsWith("Mining"))).ToList();
                                                if (mbooks.Any())
                                                {
                                                    if (mbooks.Count == 1)
                                                    {
                                                        targetBook = mbooks.First();
                                                        targetAccepted = true;
                                                    }
                                                    else
                                                    {
                                                        foreach (var mbook in mbooks)
                                                        {
                                                            Items.UseItem(mbook);
                                                            while (!Gumps.HasGump(0x59))
                                                            {
                                                                Misc.Pause(200);
                                                            }
                                                            
                                                            var mbookGumpData = Gumps.GetGumpData(0x59);
                                                            var hasRoom = mbookGumpData.stringList.Any(s =>
                                                                s.Equals("Empty",
                                                                    StringComparison.InvariantCultureIgnoreCase));
                                                            if (hasRoom)
                                                            {
                                                                targetBook = mbook;
                                                                targetAccepted = true;
                                                            }
                                                            
                                                            Gumps.SendAction(0x59,0);
                                                            Misc.Pause(300);
                                                        }
                                                    }
                                                }

                                                while (!targetAccepted)
                                                {
                                                    Misc.SendMessage("Multiple rune books found, please select the one to add the rune to");
                                                    var tar = new Target();
                                                    var bookSerial = tar.PromptTarget();
                                                    if (bookSerial == -1)
                                                    {
                                                        break;
                                                    }
                                                    var targetBookCandidate = Items.FindBySerial(bookSerial);
                                                    
                                                    if (targetBookCandidate.ItemID == 0x22C5 ||
                                                        targetBookCandidate.ItemID == 0x9C16)
                                                    {
                                                        targetBook = targetBookCandidate;
                                                        targetAccepted = true;
                                                    }
                                                    else
                                                    {
                                                        Misc.SendMessage("That is not a valid book for your rune, please try again");
                                                    }
                                                }

                                            }
                                            else
                                            {
                                                targetBook = books.FirstOrDefault() ?? atlases.FirstOrDefault();
                                            }

                                            if (targetBook != null)
                                            {
                                                if (Gumps.HasGump(0x59))
                                                {
                                                    Gumps.SendAction(0x59,0);
                                                    Misc.Pause(300);
                                                }
                                                
                                                Misc.Pause(200);
                                                
                                                Items.Move(blank, targetBook,1);
                                            }
                                    }
                                }
                                
                                gd.buttonid = -1;
                                _state = ScriptState.Idle;
                                UpdateGump();
                                break;
                            }
                            else if (gd.buttonid == (int)Button.RecordingDiscard)
                            {
                                _state = ScriptState.Idle;

                                gd.buttonid = -1;
                                UpdateGump();
                                break;
                            }

                            var newLines = _journal.GetJournalEntry(_journalEntry);
                            if (newLines.Any(j => j.Text.ToLower().Contains("ore and put it in your backpack")))
                            {
                                var currentDigPoint = new Point(Player.Position.X, Player.Position.Y, Player.Position.Z,
                                    true);
                                //unsure no point of the same XYZ exists in path
                                if (path.Any(p => p.Equals(currentDigPoint)))
                                {
                                    _journalEntry = newLines.OrderBy(je => je.Timestamp).LastOrDefault() ??
                                                    _journalEntry;
                                    continue;
                                }

                                path.Add(currentDigPoint);
                                Misc.SendMessage(
                                    $"Added DigSpot : {currentDigPoint.X}, {currentDigPoint.Y}, {currentDigPoint.Z}");
                                _journalEntry = newLines.OrderBy(je => je.Timestamp).LastOrDefault() ?? _journalEntry;
                            }

                            Misc.Pause(500);
                        }
                    }

                    Misc.Pause(800);
                }
            }
            catch (ThreadAbortException)
            {
                //Silent
            }
            catch (Exception e)
            {
                Misc.SendMessage(e);
            }
            finally
            {
                Gumps.CloseGump(786543548);
                Gumps.CloseGump(26432189);
            }
        }

        private void HandleAmbush()
        {
            
        }

        private bool CheckSmelter()
        {
            if (_config.SmeltingPetSerial == 0)
            {
                Player.HeadMessage(33, "No smelting pet selected please pick one");
                return PickSmelter();
            }
            
            var mobSmelter = Mobiles.FindBySerial(_config.SmeltingPetSerial);
            if(mobSmelter == null)
            {
                Player.HeadMessage(33, "Stored smelting pet not found, please pick a new one");
                return PickSmelter();
            }

            return true;
        }

        private bool PickSmelter()
        {
            var tar = new Target();
            var tarSerial = tar.PromptTarget("Please select the fire pet used for smelting");
            if (tarSerial == -1)
            {
                Player.HeadMessage(33, "No fire pet selected");
                return false;
            } 
            
            _config.SmeltingPetSerial = tarSerial;
            SaveConfig();
            return true;
        }

        private void HandleMiningSpot()
        {
            _journalEntry = _journal.GetJournalEntry(_journalEntry).OrderBy(je => je.Timestamp).LastOrDefault() ?? _journalEntry;
            while (true)
            {
                var gd = Gumps.GetGumpData(786543548);
                if(gd.buttonid == (int)Button.PlaybackStop)
                {
                    break;
                }
                if(gd.buttonid == (int)Button.PlaybackPause)
                {
                    Pause();
                }
                if(Player.Weight > Player.MaxWeight - 50)
                {
                    Smelt();
                    continue;
                }
                
                if (DiggingTool == null)
                {
                    var tinkerSkill = Player.GetRealSkillValue("Tinkering");
                    if(tinkerSkill < 50)
                    {
                        Player.HeadMessage(33, "Not a tinker, Go get more tools");
                        Pause();
                    }
                    else
                    {
                        MakeTools();
                        Misc.Pause(200);
                    }
                }
                
                Target.TargetResource(DiggingTool, "ore");
                var newLines = _journal.GetJournalEntry(_journalEntry);
                if (newLines.Any(j => j.Text.Contains("There is no metal here to mine")))
                {
                    Misc.Pause(600);
                    _journalEntry = newLines.OrderBy(je => je.Timestamp).LastOrDefault() ?? _journalEntry;;
                    return;
                }
                Misc.Pause(600);
                _journalEntry = newLines.OrderBy(je => je.Timestamp).LastOrDefault() ?? _journalEntry;;
            }
        }

        private void Pause()
        {
            Point pausePoint = new Point((ushort)Player.Position.X-(Player.Position.Z/10)-1, (ushort)Player.Position.Y-(Player.Position.Z/10)-1, (ushort)Player.Position.Z);
            Player.TrackingArrow((ushort)pausePoint.X, (ushort)pausePoint.Y, true);
            _state = ScriptState.Paused;
            UpdateGump();
            while (true)
            {
                var gd = Gumps.GetGumpData(786543548);
                if(gd.buttonid == (int)Button.PlaybackResume)
                {
                    _state = ScriptState.Playback;
                    UpdateGump();
                    gd.buttonid = -1;
                    break;
                }
                if(gd.buttonid == (int)Button.PlaybackStop)
                {
                    return;
                }
                Misc.Pause(500);
            }
            if(Player.Position.X != pausePoint.X || Player.Position.Y != pausePoint.Y)
            {
                PathFinding.PathFindTo(pausePoint.X, pausePoint.Y, pausePoint.Z);
            }
            Player.TrackingArrow((ushort)pausePoint.X, (ushort)pausePoint.Y, false);
        }

        private bool SendHeaviest()
        {
            var container = Player.Backpack.Contains.FirstOrDefault(i =>
                i.ItemID == 0xA272 && i.Name.ToLower().Contains("miner")) ?? Player.Backpack;
            Items.WaitForContents(container, 1000);
            var bos = container.Contains.Where(i => i.ItemID == 0x0E76 && i.IsBagOfSending).FirstOrDefault();
            if (bos == null)
            {
                Player.HeadMessage(33, "No bag of sending found");
                return false;
            }

            Items.WaitForProps(bos, 1000);
            var chargesText = bos.Properties.FirstOrDefault(p => p.Number == 1060741)?.Args;
            if (int.TryParse(chargesText, out int charges))
            {
                if(charges < 1)
                {
                    var powder = Player.Backpack.Contains.Where(i => i.ItemID == 0x26B8 && i.Name.ToLower().Contains("translocation")).FirstOrDefault();
                    if (powder == null)
                    {
                        Player.HeadMessage(33, "No translocation powder found");
                        return false;
                    }
                    Items.UseItem(powder, bos.Serial);
                }
            }
            
            var ingots = Player.Backpack.Contains.Where(i => i.ItemID == 0x1BF2).ToList();
            if (!ingots.Any())
            {
                Player.HeadMessage(33, "No ingots found");
                return false;
            }
            var heaviest = ingots.OrderByDescending(i => i.Weight).FirstOrDefault();
            Items.UseItem(bos);
            Target.WaitForTarget(500);
            Target.TargetExecute(heaviest.Serial);
            Misc.Pause(200);
            return true;
        }
        
        

        private void UseMinersSatchel()
        {
            var satchel = Player.Backpack.Contains.FirstOrDefault(i => i.ItemID == 0xA272 && i.Name.ToLower().Contains("miner"));
            if (satchel == null)
            {
                return;
            }
            
            var ingots = Player.Backpack.Contains.Where(i => i.ItemID == 0x1BF2).ToList();
            foreach (var ingot in ingots)
            {
                Items.Move(ingot, satchel, ingot.Amount);
                Misc.Pause(300);
            }
        }

        private void MakeTools()
        {
            Player.HeadMessage(33, "No tool found");
            Player.Backpack.Contains.Where(i => i.ItemID == 0x1EB8);
            if(!Player.Backpack.Contains.Where(i => i.ItemID == 0x1EB8).Any())
            {
                Player.HeadMessage(33, "No tinker tool found");
                return;
            }

            int totalCharges = GetTinkerCharges();
            while (totalCharges < 10)
            {
                Items.UseItem(TinkerTool);
                Misc.Pause(500);
                Gumps.SendAction( 0xc1f33707, 62);
                Misc.Pause(500);
                totalCharges = GetTinkerCharges();
            }

            Items.UseItem(TinkerTool);
            while(GetMiningToolsCount() < 3)
            {
                if(_tool == MiningTool.Pickaxe)
                {
                    Gumps.SendAction( 0xc1f33707, 322);
                }
                else
                {
                    Gumps.SendAction( 0xc1f33707, 202);
                }
                Misc.Pause(500);
            }
            Gumps.CloseGump(0xc1f33707);
            
        }

        private int GetMiningToolsCount()
        {
            var miningTools = Player.Backpack.Contains.Where(i => i.ItemID == (int)_tool);
            return miningTools.Count();
        }

        private Item GetTinkerTool()
        {
            return Player.Backpack.Contains.FirstOrDefault(i => i.ItemID == 0x1EB8);
        }
        
        private Item GetDiggingTool()
        {
            return Player.Backpack.Contains.FirstOrDefault(i => i.ItemID == (int)_tool);
        }

        private int GetTinkerCharges()
        {
            var tinkerTools = Player.Backpack.Contains.Where(i => i.ItemID == 0x1EB8);
            int totalCharges = 0;
            foreach (var tinkerTool in tinkerTools)
            {
                Items.WaitForProps(tinkerTool, 1000);
                var chargesText = tinkerTool.Properties.FirstOrDefault(p => p.Number == 1060584)?.Args;
                if (int.TryParse(chargesText, out int charges))
                {
                    totalCharges += charges;
                }
            }

            return totalCharges;
        }

        private void Smelt()
        {
            var potentialSmeltings = Player.Backpack.Contains.Where(i => i.Name.ToLower().Contains("ore")).ToList();

            foreach (var ps in potentialSmeltings)
            {
                if (directSmelts.Contains(ps.ItemID))
                {
                    Items.UseItem(ps);
                    Target.WaitForTarget(1000);
                    Target.TargetExecute(_config.SmeltingPetSerial);
                    Misc.Pause(500);
                    continue;
                }
                if(needs2Smelts.Contains(ps.ItemID))
                {
                    if (ps.Amount >= 2)
                    {
                        Items.UseItem(ps);
                        Target.WaitForTarget(1000);
                        Target.TargetExecute(_config.SmeltingPetSerial);
                        Misc.Pause(500);
                        continue;
                    }
                }
            }
            Misc.Pause(500);
            UseMinersSatchel();
            Misc.Pause(1500);
            if(Player.Weight > Player.MaxWeight - 100)
            {
                if(_useBagOfSending)
                {
                    SendHeaviest();
                }
                else
                {
                    Player.HeadMessage(33, "Please empty your backpack");
                    Pause();
                }
            }
        }

        private void UpdateGump()
        {
            var allowMark = false;

            var magerySkill = Player.GetSkillValue("Magery");
            if (magerySkill >= 70)
            {
                allowMark = true;
            }

            if (!allowMark)
            {
                var scroll = Player.Backpack.Contains.FirstOrDefault(i => _scrollIds.Contains(i.ItemID) && i.Name.Contains("Mark"));
                if (scroll != null)
                {
                    allowMark = true;
                }
            }

            if (allowMark)
            {
                var runes = Player.Backpack.Contains.Where(i => _runeIds.Contains(i.ItemID)).ToList();
                runes.ForEach(rune => Items.WaitForProps(rune, 1000));
                var hasBlanks = runes.Any(r => r.Properties.Count == 2);

                if (hasBlanks == false)
                {
                    var markBook = Player.Backpack.Contains.FirstOrDefault(i => i.Name.Equals("Recall Rune Tome", StringComparison.InvariantCultureIgnoreCase));
                    if (markBook != null)
                    {
                        hasBlanks = true;
                    }
                }

                if (hasBlanks == false)
                {
                    allowMark = false;
                }
            }


            var gump = Gumps.CreateGump();
            gump.gumpId = 786543548;
            gump.serial = (uint)Player.Serial;
            var width = 230;
            
            Gumps.AddBackground(ref gump, 0, 0, width, 200, 1755);
            Gumps.AddLabel(ref gump, 15,15,0x7b, "Mining Master");
            Gumps.AddLabel(ref gump, 15,35,0x7b, "Status");
            Gumps.AddLabel(ref gump, 60,35,0x7b, ":");
            Gumps.AddLabel(ref gump, 70,35,0x16a, _state.ToString());
            Gumps.AddLabel(ref gump, 15,55,0x7b, "Version");
            Gumps.AddLabel(ref gump, 60,55,0x7b, ":");
            Gumps.AddLabel(ref gump, 70,55,0x16a, _version);
            
            if (_state == ScriptState.Idle)
            {
                Gumps.AddButton(ref gump, 130, 15, 40018, 40018, (int)Button.RecordingStart, 1, 0);
                Gumps.AddLabel(ref gump, 138, 15, 0x16a, "Record");
            }
            else if(_state == ScriptState.Recording)
            {
                Gumps.AddButton(ref gump, 130, 15, 40018, 40018, (int)Button.RecordingDiscard, 1, 0);
                Gumps.AddLabel(ref gump, 135, 17, 0xF1, "Discard");
                Gumps.AddButton(ref gump, 130, 40, 40018, 40018, (int)Button.RecordingSave, 1, 0);
                Gumps.AddLabel(ref gump, 135, 42, 0x16a, "Save");
                Gumps.AddButton(ref gump, 130, 65, 40018, 40018, (int)Button.AddWaypoint, 1, 0);
                Gumps.AddLabel(ref gump, 135, 67, 0x16a, "Waypoint");
                Gumps.AddLabel(ref gump, 15, 140, 0x16a, "Name");
                
                if (allowMark)
                {
                    Gumps.AddLabel(ref gump, 100, 140, 0x16a, "Auto Mark :");
                    Gumps.AddCheck(ref gump, 180, 140, 9028, 9027, false, 1);
                }

                Gumps.AddImageTiled(ref gump, 15, 165, width-30, 16,1803);
                Gumps.AddTextEntry(ref gump, 15,165,width-30,32,0x16a,1,"");
            }
            else if(_state == ScriptState.Playback || _state == ScriptState.Paused)
            {
                Gumps.AddButton(ref gump, 130, 15, 40018, 40018, (int)Button.PlaybackStop, 1, 0);
                Gumps.AddLabel(ref gump, 148, 17, 0x16a, "Stop");
                if (_state == ScriptState.Paused)
                {
                    Gumps.AddButton(ref gump, 130, 45, 40018, 40018, (int)Button.PlaybackResume, 1, 0);
                    Gumps.AddLabel(ref gump, 142, 47, 0x16a, "Resume");
                }
                else
                {
                    Gumps.AddButton(ref gump, 130, 45, 40018, 40018, (int)Button.PlaybackPause, 1, 0);
                    Gumps.AddLabel(ref gump, 145, 47, 0x16a, "Pause");
                }
            }
            if(_editPath != null && _state == ScriptState.Idle)
            {
                ShowEdit(gump, width);
            }
            else if (_pathSelectorEnabled && _state == ScriptState.Idle)
            {
                ShowPaths(gump, width);
            }
            
            var flagId = _pathSelectorEnabled ? 9781 : 9780;
            var flagX = !_pathSelectorEnabled && _editPath == null ? width-25 : width+300-25;

            if (_state == ScriptState.Idle)
            {
                Gumps.AddButton(ref gump, flagX, 10, flagId, flagId, (int)Button.PathSelect, 1, 1);
                Gumps.AddTooltip(ref gump, "Toggle Path Selector");

            }
            
            Gumps.CloseGump(786543548);
            Gumps.SendGump(gump, 500,500);
        }

        private void ShowPaths(Gumps.GumpData gump, int baseX)
        {
            var paths = _config.Paths.OrderBy(g => g.Name).ToList();
            Gumps.AddBackground(ref gump, baseX,0,300, 30+paths.Count * 25,1755);
            foreach (var path in paths)
            {
                Gumps.AddLabel(ref gump, baseX+15, 15 + (paths.IndexOf(path) * 25), 0x7b, path.Name);
                // Gumps.AddButton(ref gump, baseX+15, 16 + (paths.IndexOf(path) * 25), 11410, 11411, _config.Paths.IndexOf(path)+1, 1, 0);
                Gumps.AddButton(ref gump, baseX+120,  15 + (paths.IndexOf(path) * 25), 40018, 40018, _config.Paths.IndexOf(path)+1, 1, 0);
                Gumps.AddLabel(ref gump, baseX+135,  17 + (paths.IndexOf(path) * 25), 0x16a, "Select");
                Gumps.AddButton(ref gump, baseX+185,  15 + (paths.IndexOf(path) * 25), 40018, 40018, _config.Paths.IndexOf(path)+1+10000, 1, 0);
                Gumps.AddLabel(ref gump, baseX+205,  17 + (paths.IndexOf(path) * 25), 0x16a, "Edit");
            }
            
            Gumps.CloseGump(26432189);
            Gumps.SendGump(gump,500,500);
        }

        private void ShowEdit(Gumps.GumpData gump, int baseX)
        {
            Gumps.AddBackground(ref gump, baseX,0,300, 100,1755);
            Gumps.AddLabel(ref gump, baseX+15,  17 , 0x7b, _editPath.Name);
            Gumps.AddButton(ref gump, baseX+35,  45, 40018, 40018, _config.Paths.IndexOf(_editPath)+1+20000, 1, 0);
            Gumps.AddLabel(ref gump, baseX+50,  47,  0x16a, "Delete");
            Gumps.AddButton(ref gump, baseX+105,  45, 40018, 40018, -2, 1, 0);
            Gumps.AddLabel(ref gump, baseX+120,  47,  0x16a, "Cancel");
        }
        
        private void SaveConfig()
        {
            var ns = Assembly.LoadFile(Path.Combine(Engine.RootPath, "Newtonsoft.Json.dll"));
            string data = "";
            foreach(Type type in ns.GetExportedTypes())
            {
                if (type.Name == "JsonConvert")
                {
                    data = type.InvokeMember("SerializeObject", BindingFlags.InvokeMethod, null, null, new object[] { _config }) as string;
                    File.WriteAllText(Path.Combine(Engine.RootPath, "MiningMaster.config"), data);
                    break;
                }
            }
        }

        private void LoadConfig()
        {
            var configFile = Path.Combine(Engine.RootPath, "MiningMaster.config");
            if (!File.Exists(configFile))
            {
                configFile = Path.Combine(Engine.RootPath, "MiningMasterPilot.config");
                if (!File.Exists(configFile))
                {
                    _config = new MiningConfig();
                    return;
                }
            }
            
            var data = File.ReadAllText(configFile);
            var ns = Assembly.LoadFile(Path.Combine(Engine.RootPath, "Newtonsoft.Json.dll"));
            foreach (Type type in ns.GetExportedTypes())
            {
                if (type.Name == "JsonConvert")
                {
                    var funcs = type.GetMethods(BindingFlags.InvokeMethod | BindingFlags.Static | BindingFlags.Public)
                        .Where(f => f.Name == "DeserializeObject" && f.IsGenericMethodDefinition);
                    var func = funcs
                        .FirstOrDefault(f =>
                            f.Name == "DeserializeObject" && f.GetParameters().Length == 1 &&
                            f.GetParameters()[0].ParameterType == typeof(string))
                        .MakeGenericMethod(typeof(MiningConfig));
                    var readConfig =
                        func.Invoke(type, BindingFlags.InvokeMethod, null, new object[] { data }, null) as MiningConfig;
                    if (readConfig != null)
                    {
                        _config = readConfig;
                    }
                }
            }
        }

        internal class MiningConfig
        {
            public List<PathData> Paths { get; set; }
            public int SmeltingPetSerial { get; set; }

            public MiningConfig()
            {
                Paths = new List<PathData>();
                SmeltingPetSerial = 0;
            }
        }

        internal class Point
        {
            public int X { get; set; }
            public int Y { get; set; }
            public int Z { get; set; }
            public bool MineSpot { get; set; }
            public Point(int x, int y, int z, bool mineSpot = false)
            {
                X = x;
                Y = y;
                Z = z;
                MineSpot = mineSpot;
            }
            
            public bool Equals(Point point)
            {
                return X == point.X && Y == point.Y && Z == point.Z && MineSpot == point.MineSpot;
            }
        }
        
        internal class PathData
        {
            public string Name { get; set; }
            public List<Point> Path { get; set; }
        }
        
        internal enum MiningTool
        {
            Pickaxe = 0x0E86,
            Shovel = 0x0F39
        }
        
        internal enum RuneBinder
        {
            Book,
            Atlas
        }

        internal enum ScriptState
        {
            Idle,
            Recording,
            Playback,
            Paused
        }

        internal enum Button
        {
            RecordingStart = 100,
            RecordingSave = 200,
            RecordingDiscard = 300,
            AddWaypoint = 400,
            PathSelect = 500,
            PlaybackStop = 600,
            PlaybackPause = 700,
            PlaybackResume = 800
        }
    }
}