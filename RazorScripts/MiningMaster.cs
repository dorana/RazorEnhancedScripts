using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms.ComponentModel.Com2Interop;
using System.Windows.Forms.VisualStyles;
using System.Xml;
using Assistant;
using RazorEnhanced;
using Ultima;
using Gumps = RazorEnhanced.Gumps;
using Item = RazorEnhanced.Item;

namespace Razorscripts
{
    public class MiningMasterPilot
    {
        private bool _useBagOfSending = false;
        private MiningTool _tool = MiningTool.Shovel;
        
        
        
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
                        var pgd = Gumps.GetGumpData(26432189);
                        if (gd.buttonid == (int)Button.RecordingStart)
                        {
                            _state = ScriptState.Recording;
                            UpdateGump();
                        }
                        else if (gd.buttonid == (int)Button.PathSelect)
                        {
                            LoadPathsGump();
                            gd.buttonid = -1;
                            UpdateGump();
                        }
                        else if (pgd != null && pgd.buttonid != -1 && pgd.buttonid != 0)
                        {
                            _selectedPath = _config.Paths[pgd.buttonid-1].Name;
                            _state = ScriptState.Playback;
                            pgd.buttonid = -1;
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
                                    var lines = Gumps.GetLineList(0x59);
                                    var pattern = @"\d+o \d+'[SN], \d+o \d+'[EW]";
                                    var matches = lines.Where(s => Regex.IsMatch(s, pattern)).ToList();
                                    //get the lines above each of the obeserved matches
                                    var runeNameLines = matches.Select(m => lines[lines.IndexOf(m) - 1]).ToList();

                                    runeIndex = runeNameLines.IndexOf(_selectedPath);
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
            var gump = Gumps.CreateGump();
            gump.gumpId = 786543548;
            gump.serial = (uint)Player.Serial;
            Gumps.AddBackground(ref gump, 0, 0, 300, 200, 1755);
            Gumps.AddLabel(ref gump, 15,15,0x7b, "Mining Master Pilot");
            Gumps.AddLabel(ref gump, 15,40,0x7b, "Status :");
            Gumps.AddLabel(ref gump, 75,40,0x16a, _state.ToString());
            
            if (_state == ScriptState.Idle)
            {
                Gumps.AddButton(ref gump, 160, 16, 11410, 11411, (int)Button.RecordingStart, 1, 0);
                Gumps.AddLabel(ref gump, 180, 15, 0x16a, "Start Recording");
                Gumps.AddLabel(ref gump, 180, 40, 0x16a, "Open path select");
                Gumps.AddButton(ref gump, 160, 41, 11400, 11401, (int)Button.PathSelect, 1, 0);
            }
            else if(_state == ScriptState.Recording)
            {
                Gumps.AddLabel(ref gump, 180, 15, 0x16a, "Discard Recording");
                Gumps.AddButton(ref gump, 160, 16, 11400, 11401, (int)Button.RecordingDiscard, 1, 0);
                Gumps.AddLabel(ref gump, 180, 40, 0x16a, "Save Recording");
                Gumps.AddButton(ref gump, 160, 41, 11400, 11401, (int)Button.RecordingSave, 1, 0);
                Gumps.AddLabel(ref gump, 35, 80, 0x16a, "Add Waypoint");
                Gumps.AddButton(ref gump, 15, 79, 11400, 11401, (int)Button.AddWaypoint, 1, 0);
                Gumps.AddLabel(ref gump, 15, 120, 0x16a, "Name");
                Gumps.AddImageTiled(ref gump, 15, 160, 270, 16,1803);
                Gumps.AddTextEntry(ref gump, 15,160,270,32,0x16a,1,"");
            }
            else if(_state == ScriptState.Playback || _state == ScriptState.Paused)
            {
                Gumps.AddButton(ref gump, 160, 16, 11410, 11411, (int)Button.PlaybackStop, 1, 0);
                Gumps.AddLabel(ref gump, 180, 15, 0x16a, "Stop Playback");
                if (_state == ScriptState.Paused)
                {
                    Gumps.AddButton(ref gump, 160, 46, 11410, 11411, (int)Button.PlaybackResume, 1, 0);
                    Gumps.AddLabel(ref gump, 180, 46, 0x16a, "Resume Playback");
                }
                else
                {
                    Gumps.AddButton(ref gump, 160, 46, 11410, 11411, (int)Button.PlaybackPause, 1, 0);
                    Gumps.AddLabel(ref gump, 180, 46, 0x16a, "Pause Playback");
                }
            }
            Gumps.CloseGump(786543548);
            Gumps.SendGump(gump, 500,500);
        }

        private void LoadPathsGump()
        {
            var gump = Gumps.CreateGump();
            gump.gumpId = 26432189;
            gump.serial = (uint)Player.Serial;
            gump.x = 500;
            gump.y = 500;
            var paths = _config.Paths.OrderBy(g => g.Name).ToList();
            Gumps.AddBackground(ref gump, 0,0,200, 30+paths.Count * 25,1755);
            foreach (var path in paths)
            {
                Gumps.AddLabel(ref gump, 30, 15 + (paths.IndexOf(path) * 25), 0x7b, path.Name);
                Gumps.AddButton(ref gump, 15, 16 + (paths.IndexOf(path) * 25), 11410, 11411, _config.Paths.IndexOf(path)+1, 1, 0);
            }
            Gumps.CloseGump(26432189);
            Gumps.SendGump(gump,500,500);
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
                    File.WriteAllText(Path.Combine(Engine.RootPath, "MiningMasterPilot.config"), data);
                    break;
                }
            }
        }

        private void LoadConfig()
        {
            var configFile = Path.Combine(Engine.RootPath, "MiningMasterPilot.config");
            if (!File.Exists(configFile))
            {
                _config = new MiningConfig();
                return;
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
            RecordingStart = 1,
            RecordingSave = 2,
            RecordingDiscard = 3,
            AddWaypoint = 4,
            PathSelect = 5,
            PlaybackStop = 6,
            PlaybackPause = 7,
            PlaybackResume = 8
        }
    }
}