using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Assistant;
using RazorEnhanced;

namespace Razorscripts
{
    public class RuneMaster
    {
        private uint _gumpId = 741584632;
        private double Magery;
        private double Chivalry;
        private int _activeRunePage = 0;
        private int _runePageSize = 16;
        private bool _optionShow = false;
        private bool UseMagery => Magery > Chivalry;
        private RuneConfig _config;
        
        Func<RuneHelper,bool> _searchFilter = (rl) => true;
        
        public void Run()
        {
            Magery = Player.GetRealSkillValue("Magery");
            Chivalry = Player.GetRealSkillValue("Chivalry");
            
            try
            {
                LoadConfig();
                if(_config == null)
                {
                    _config = new RuneConfig();
                }
                
                if (!_config.RuneLocations().Any())
                {
                    RefreshRunes(false);
                }
                
                UpdateGump();
                
                while (Player.Connected)
                {
                    var gumpResponse = Gumps.GetGumpData(_gumpId);
                    if (gumpResponse.buttonid != -1)
                    {
                        if(gumpResponse.buttonid == (int)Buttons.Search)
                        {
                            _searchFilter = (rl) => rl.Name.ToLower().Contains(gumpResponse.text.FirstOrDefault()?.ToLower());
                            _activeRunePage = 0;
                            gumpResponse.buttonid = -1;
                            UpdateGump(gumpResponse.text.FirstOrDefault());
                            continue;
                        }
                        if(gumpResponse.buttonid == (int)Buttons.ClearSearch)
                        {
                            _searchFilter = (rl) => true;
                            _activeRunePage = 0;
                            gumpResponse.buttonid = -1;
                            UpdateGump();
                            continue;
                        }
                        if(gumpResponse.buttonid == (int)Buttons.PreviousPage)
                        {
                            _activeRunePage--;
                            gumpResponse.buttonid = -1;
                            UpdateGump();
                            continue;
                        }
                        if(gumpResponse.buttonid == (int)Buttons.NextPage)
                        {
                            _activeRunePage++;
                            gumpResponse.buttonid = -1;
                            UpdateGump();
                            continue;
                        }
                        if(gumpResponse.buttonid == (int)Buttons.ToggleOptions)
                        {
                            _optionShow = !_optionShow;
                            gumpResponse.buttonid = -1;
                            UpdateGump();
                            continue;
                        }
                        if(gumpResponse.buttonid == (int)Buttons.SetListTypeSingle)
                        {
                            _config.SetListings(RuneListing.SimplePaged);
                            gumpResponse.buttonid = -1;
                            _activeRunePage = 0;
                            
                            UpdateGump();
                            continue;
                        }
                        if(gumpResponse.buttonid == (int)Buttons.SetListTypeByBook)
                        {
                            _config.SetListings(RuneListing.ByBook);
                            gumpResponse.buttonid = -1;
                            _activeRunePage = 0;
                            UpdateGump();
                            continue;
                        }
                        if(gumpResponse.buttonid == (int)Buttons.SetListTypeByMap)
                        {
                            _config.SetListings(RuneListing.ByMap);
                            gumpResponse.buttonid = -1;
                            _activeRunePage = 0;
                            UpdateGump();
                            continue;
                        }
                        if(gumpResponse.buttonid == (int)Buttons.SetSortTypeAlphabetical)
                        {
                            _config.SetSortType(SortType.Alphabetical);
                            gumpResponse.buttonid = -1;
                            _activeRunePage = 0;
                            UpdateGump();
                            continue;
                        }
                        if(gumpResponse.buttonid == (int)Buttons.SetSortTypeBookOrder)
                        {
                            _config.SetSortType(SortType.BookOrder);
                            gumpResponse.buttonid = -1;
                            _activeRunePage = 0;
                            UpdateGump();
                            continue;
                        }
                        if(gumpResponse.buttonid == (int)Buttons.ToggleColors)
                        {
                            _config.SetShowColors(!_config.UseColors());
                            gumpResponse.buttonid = -1;
                            UpdateGump();
                            continue;
                        }
                        if(gumpResponse.buttonid == (int)Buttons.RunTheRunes)
                        {
                            var runes = _config.RuneLocations();
                            foreach (var runeLocation in runes)
                            {
                                if (runeLocation.Name.ToLower().Contains("ship"))
                                {
                                    gumpResponse.buttonid = -1;
                                    UpdateGump();
                                    continue;
                                    
                                }
                                if(runeLocation.Cord != null)
                                {
                                    gumpResponse.buttonid = -1;
                                    UpdateGump();
                                    continue;
                                }
                                
                                ExecuteTransfer(runes.IndexOf(runeLocation));
                                Misc.Pause(1000);
                            }
                            continue;
                        }
                        if(gumpResponse.buttonid == (int)Buttons.UpdateRunes)
                        {
                            RefreshRunes(true);
                            gumpResponse.buttonid = -1;
                            UpdateGump();
                            continue;
                        }
                        if(gumpResponse.buttonid == (int)Buttons.ResetRunes)
                        {
                            RefreshRunes(false);
                            gumpResponse.buttonid = -1;
                            UpdateGump();
                            continue;
                        }
                        
                        ExecuteTransfer(gumpResponse.buttonid);
                        gumpResponse.buttonid = -1;
                        _searchFilter = (rl) => true;
                        _activeRunePage = 0;
                        UpdateGump();
                    }
                    Misc.Pause(1000);
                }

            }
            catch (ThreadAbortException)
            {
                //silent
            }
            catch (Exception e)
            {
                Misc.SendMessage(e);
            }
            finally
            {
                Gumps.CloseGump(_gumpId);
            }
        }

        private void RefreshRunes(bool merge)
        {
            var runes = GetAllRuneLocations();
            _config.UppdateLocations(runes, merge);
        }

        private List<RuneHelper> GetAllRuneLocations()
        {
            var runes = new List<RuneHelper>();
            runes.AddRange(GetAllRuneBookLocations());
            Misc.Pause(50);
            runes.AddRange(GetAllRuneAtlasLocations());

            return runes;
        }

        private List<RuneHelper> GetAllRuneBookLocations()
        {
            var list = new List<RuneHelper>();
            var books = Player.Backpack.Contains.Where(i => i.ItemID == 0x22C5).ToList();
            var straps = Player.Backpack.Contains.Where(i => i.ItemID == 0xA721 && i.Name.Equals("Runebook Strap", StringComparison.InvariantCultureIgnoreCase)).ToList();
            straps.ForEach( s =>
            {
                Items.WaitForContents(200, s.Serial);
                Misc.Pause(200);
            });
            
            var strapBooks = straps.SelectMany(s => s.Contains).Where(i => i.ItemID == 0x22C5).ToList();
            
            books.AddRange(strapBooks);
            
            foreach (var book in books)
            {
                Items.WaitForProps(book, 1000);
                var bookName = book.Properties.FirstOrDefault(p => p.Number == 1042971)?.ToString() ?? "NONAME";
                Items.UseItem(book);
                Misc.Pause(200);
                if (Gumps.HasGump(0x59))
                {
                    var gd = Gumps.GetGumpData(0x59);
                    List<string> runeNameLines = gd.stringList.Skip(2).Take(16).Where(s => !s.Equals("Empty", StringComparison.OrdinalIgnoreCase)).ToList();
                    var index = 0;
                    foreach (var name in runeNameLines)
                    {
                        list.Add(new RuneHelper
                        {
                            Name = name,
                            BookSerial = book.Serial,
                            BookName = bookName,
                            BookType = BookType.Runebook,
                            RecallIndex = 50+index,
                            GateIndex = 100+index,
                            SacredJourneyIndex = 75+index,
                            Cord = null
                        });

                        index++;
                    }
                    Gumps.CloseGump(0x59);
                }
            }

            return list;
        }
        
        private List<RuneHelper> GetAllRuneAtlasLocations()
        {
            var list = new List<RuneHelper>();
            var atlases = Player.Backpack.Contains.Where(i => i.ItemID == 0x9C16).ToList();
            var straps = Player.Backpack.Contains.Where(i => i.ItemID == 0xA721 && i.Name.Equals("Runebook Strap", StringComparison.InvariantCultureIgnoreCase)).ToList();
            straps.ForEach( s =>
            {
                Items.WaitForContents(200, s.Serial);
                Misc.Pause(200);
            });
            
            var strapAtlases = straps.SelectMany(s => s.Contains).Where(i => i.ItemID == 0x9C16).ToList();
            
            atlases.AddRange(strapAtlases);
            foreach (var atlas in atlases)
            {
                Items.WaitForProps(atlas, 300);
                var bookName = atlas.Properties.FirstOrDefault(p => p.Number == 1042971)?.ToString();
                Items.UseItem(atlas);
                Misc.Pause(200);
                if (Gumps.HasGump( 0x1f2))
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
                        // List<string> lines = gd.stringList;
                        var lines = Gumps.GetLineList(0x1f2);
                        var regex = new Regex(@"^\d+\s*/\s*\d+$");
                        var runeIndexStart = lines.IndexOf(lines.FirstOrDefault(l => regex.Match(l).Success));
                        var endIndex = lines.IndexOf(lines.First(l => l.StartsWith("<center>")));
                        if(runeIndexStart == -1 || endIndex == -1)
                        {
                            break;
                        }
                        var runeNamesLines = lines.GetRange(runeIndexStart+1, endIndex - runeIndexStart-1)
                            .Where(l => !l.Equals("Empty")).ToList();

                        var runeIndex = (page-1)*16;
                        foreach (var namesLine in runeNamesLines)
                        {
                            // var runeIIndex = runeNamesLines.IndexOf(namesLine);
                            list.Add(new RuneHelper
                            {
                                Name = namesLine,
                                BookSerial = atlas.Serial,
                                BookName = bookName,
                                BookType = BookType.RuneAtlas,
                                RecallIndex = 100+runeIndex,
                                GateIndex = 100+runeIndex,
                                SacredJourneyIndex = 100+runeIndex,
                                Cord = null
                            });
                            runeIndex++;
                        }

                        if (page < 3 && Gumps.HasGump(0x1f2))
                        {
                            Gumps.SendAction(0x1f2, 1150);
                        }

                        Misc.Pause(200);
                        page++;
                    }
                    
                    Gumps.CloseGump(0x1f2);
                }
            }

            return list;
        }

        private void UpdateGump(string lastSearch = "")
        {
            var useMagery = Magery > Chivalry && Magery > 40;
            var enableGates = Magery > 80;
            var gump = Gumps.CreateGump();
            gump.gumpId = _gumpId;
            gump.serial = (uint)Player.Serial;
            Gumps.AddBackground(ref gump,0,0,240,390,1755);
            Gumps.AddLabel(ref gump,18, 15,0x7b, "T");
            if (enableGates)
            {
                Gumps.AddLabel(ref gump,38, 15,0x7b, "G");
            }
            
            Gumps.AddImageTiled(ref gump, 55, 15, 100, 16,1803);
            Gumps.AddTextEntry(ref gump, 55,15,100,32,0x16a,1,lastSearch);
            Gumps.AddTooltip(ref gump,"Enter a text to filter runes");
            Gumps.AddButton(ref gump,160, 15, 11400, 11401, (int)Buttons.Search, 1, 1);
            Gumps.AddTooltip(ref gump,"Search");
            Gumps.AddButton(ref gump,180, 15, 11410, 11411, (int)Buttons.ClearSearch, 1, 1);
            Gumps.AddTooltip(ref gump,"Clear Search");
            if (_optionShow)
            {
                Gumps.AddBackground(ref gump,240,0,130,390,1755);
                Gumps.AddButton(ref gump, 345, 15, 9781, 9781, (int)Buttons.ToggleOptions, 1, 1);
                Gumps.AddTooltip(ref gump, "Hide Options");
                
                Gumps.AddLabel(ref gump,255, 15,0x75, "List Type");
                Gumps.AddButton(ref gump,255, 40, 5601, 5601, (int)Buttons.SetListTypeSingle, 1, 1);
                Gumps.AddLabel(ref gump,275, 40,GetListingColor(RuneListing.SimplePaged), "Single List");
                Gumps.AddButton(ref gump,255, 60, 5601, 5601, (int)Buttons.SetListTypeByBook, 1, 1);
                Gumps.AddLabel(ref gump,275, 60,GetListingColor(RuneListing.ByBook), "By Book");
                Gumps.AddButton(ref gump,255, 80, 5601, 5601, (int)Buttons.SetListTypeByMap, 1, 1);
                Gumps.AddLabel(ref gump,275, 80,GetListingColor(RuneListing.ByMap), "By Map");
                
                Gumps.AddLabel(ref gump,255, 115,0x75, "Sort By");
                Gumps.AddButton(ref gump,255, 145, 5601, 5601, (int)Buttons.SetSortTypeAlphabetical, 1, 1);
                Gumps.AddLabel(ref gump,275, 145,_config.Sorting() == SortType.Alphabetical ? 72 : 0x7b, "Alphabetical");
                Gumps.AddButton(ref gump,255, 165, 5601, 5601, (int)Buttons.SetSortTypeBookOrder, 1, 1);
                Gumps.AddLabel(ref gump,275, 165,_config.Sorting() == SortType.Alphabetical ? 0x7b : 72, "Book Order");
                
                Gumps.AddButton(ref gump,255, 200, 5601, 5601, (int)Buttons.ToggleColors, 1, 1);
                Gumps.AddLabel(ref gump, 275, 200, _config.UseColors() ? 72 : 0x7b, "Toggle Colors");
                
                Gumps.AddButton(ref gump,255, 325, 5601, 5601, (int)Buttons.UpdateRunes, 1, 1);
                Gumps.AddLabel(ref gump,275, 325,0x7b, "Update Runes");
                
                Gumps.AddButton(ref gump,255, 345, 5601, 5601, (int)Buttons.ResetRunes, 1, 1);
                Gumps.AddLabel(ref gump,275, 345,0x7b, "Reset Runes");
                
                Gumps.AddButton(ref gump,255, 365, 5601, 5601, (int)Buttons.RunTheRunes, 1, 1);
                Gumps.AddLabel(ref gump,275, 365,0x7b, "Run Runes");
            }
            else
            {
                Gumps.AddButton(ref gump, 215, 15, 9780, 9780, (int)Buttons.ToggleOptions, 1, 1);
                Gumps.AddTooltip(ref gump, "Show Options");
            }

            ListRunes(gump,enableGates, useMagery);
            
            HandlePaging(gump, 390);
            
            Gumps.CloseGump(_gumpId);
            Gumps.SendGump(gump, 500,500);
        }

        private void ListRunes(Gumps.GumpData gump, bool enableGates, bool useMagery)
        {
            List<RuneHelper> filteredRunes = new List<RuneHelper>();
            if(_config.Listings() == RuneListing.SimplePaged)
            {
                filteredRunes = _config.RuneLocations().Where(_searchFilter).OrderBy(b => b.Name).ToList();
            }
            else if(_config.Listings() == RuneListing.ByBook)
            {
                var books = _config.RuneLocations().Where(_searchFilter).GroupBy(b => b.BookName).OrderBy(g => g.Key);
                filteredRunes = new List<RuneHelper>();
                foreach (var book in books)
                {
                    filteredRunes.Add(new RuneHelper
                    {
                        Name = book.Key,
                        BookSerial = 0,
                        BookName = string.Empty,
                        BookType = BookType.None,
                        RecallIndex = 0,
                        GateIndex = 0,
                        SacredJourneyIndex = 0,
                        Cord = null
                    });
                    if(_config.Sorting() == SortType.Alphabetical)
                    {
                        filteredRunes.AddRange(book.OrderBy(b => b.Name));
                    }
                    else
                    {
                        filteredRunes.AddRange(book);
                    }
                }
                
                //get all indexes that would be right on the pageSize indexes
                var pageSizeindexes = filteredRunes.Select((r, i) => new {r, i}).Where(ri => ri.i % _runePageSize == 0).Select(ri => ri.i).ToList();

                foreach (var ix in pageSizeindexes)
                {
                    var checkBook = filteredRunes[ix];
                    if (checkBook.BookType != BookType.None)
                    {
                        // find previous Book of tyepe none
                        var previousBook = filteredRunes.Take(ix).LastOrDefault(b => b.BookType == BookType.None);
                        if (previousBook != null)
                        {
                            var index = filteredRunes.IndexOf(previousBook);
                            filteredRunes.Insert(ix, previousBook);
                        }
                    }
                }
            }
            else if (_config.Listings() == RuneListing.ByMap)
            {
                var maps = _config.RuneLocations().Where(_searchFilter).GroupBy(b => b.Cord?.Map).OrderBy(g => g.Key);
                var unknowns = maps.Where(m => m.Key == null);
                var knowns = maps.Where(m => m.Key != null).OrderBy(m => (int)m.Key);
                var listMaps = knowns.Concat(unknowns).ToList();
                filteredRunes = new List<RuneHelper>();
                foreach (var map in listMaps)
                {
                    filteredRunes.Add(new RuneHelper
                    {
                        Name = map.Key?.ToString() ?? "UNKNOWN",
                        BookSerial = 0,
                        BookName = string.Empty,
                        BookType = BookType.None,
                        RecallIndex = 0,
                        GateIndex = 0,
                        SacredJourneyIndex = 0,
                        Cord = null
                    });
                    if(_config.Sorting() == SortType.Alphabetical)
                    {
                        filteredRunes.AddRange(map.OrderBy(b => b.Name));
                    }
                    else
                    {
                        filteredRunes.AddRange(map);
                    }
                }
                
                //get all indexes that would be right on the pageSize indexes
                var pageSizeindexes = filteredRunes.Select((r, i) => new {r, i}).Where(ri => ri.i % _runePageSize == 0).Select(ri => ri.i).ToList();

                foreach (var ix in pageSizeindexes)
                {
                    var checkBook = filteredRunes[ix];
                    if (checkBook.BookType != BookType.None)
                    {
                        // find previous Book of tyepe none
                        var previousBook = filteredRunes.Take(ix).LastOrDefault(b => b.BookType == BookType.None);
                        if (previousBook != null)
                        {
                            var index = filteredRunes.IndexOf(previousBook);
                            filteredRunes.Insert(ix, previousBook);
                        }
                    }
                }
            }
            
            var pageRunes = filteredRunes.Skip(_activeRunePage * _runePageSize).Take(_runePageSize).ToList();
            
            foreach (var rl in pageRunes)
            {
                var displayIndex = pageRunes.IndexOf(rl);
                var runeIndex = _config.RuneLocations().IndexOf(rl);
                if (rl.BookType == BookType.None)
                {
                    Gumps.AddImageTiled(ref gump,8,35+displayIndex*20,194,4,9351);
                    Gumps.AddImageTiled(ref gump,8,35+displayIndex*20+4,194,12,9354);
                    Gumps.AddImageTiled(ref gump,8,35+displayIndex*20+16,194,4,9357);
                    if (rl.Name.Length > 28)
                    {
                        Gumps.AddLabel(ref gump,35, 35+displayIndex*20+2, 0x2F, rl.Name.Substring(0, 28).ToUpper());
                        Gumps.AddTooltip(ref gump, rl.Name);
                    }
                    else
                    {
                        Gumps.AddLabel(ref gump,35, 35+displayIndex*20+2, 0x2F, rl.Name.ToUpper());
                    }
                    
                }
                else
                {
                    if (rl.Name.Length > 28)
                    {
                        Gumps.AddLabel(ref gump,enableGates ? 55 : 35, 35+displayIndex*20, GetRuneColor(rl), rl.Name.Substring(0, 28));                    
                    }
                    else
                    {
                        Gumps.AddLabel(ref gump,enableGates ? 55 : 35, 35+displayIndex*20, GetRuneColor(rl), rl.Name);
                    }
                    Gumps.AddTooltip(ref gump, $"{rl.Name} ({rl.Cord?.Map.ToString() ?? ("Unknown")})");
                    
                    Gumps.AddButton(ref gump, 15, 35 + displayIndex * 20, 11400, 11401, runeIndex, 1, 1);
                    Gumps.AddTooltip(ref gump,useMagery ? "Recall" : "Sacred Journey");
                    if (enableGates)
                    {
                        Gumps.AddButton(ref gump, 35, 35 + displayIndex * 20, 11410, 11411, runeIndex + 10000, 1, 1);
                        Gumps.AddTooltip(ref gump,"Gate Travel");
                    }
                }
            }
        }

        private void HandlePaging(Gumps.GumpData gump, int height)
        {
            var bookCount = 0;
            var mapCount = 0;
            if (_config.Listings() == RuneListing.ByBook)
            {
                bookCount = _config.RuneLocations().GroupBy(g => g.BookName).Count();
            }
            if (_config.Listings() == RuneListing.ByMap)
            {
                mapCount = _config.RuneLocations().GroupBy(g => g.Cord?.Map).Count();
            }

            var pagecount = (int)Math.Ceiling((double)(_config.RuneLocations().Where(_searchFilter).ToList().Count + bookCount + mapCount) / _runePageSize);
            Gumps.AddLabel(ref gump, 60,height-24,0x7b, $"Page: {_activeRunePage+1}/{pagecount}");

            if (_activeRunePage != 0)
            {
                Gumps.AddButton(ref gump, 15, height-30, 9909, 9909, (int)Buttons.PreviousPage, 1, 0);
                Gumps.AddTooltip(ref gump,"Previous Page");
            }
            
            if (_activeRunePage < pagecount-1)
            {
                Gumps.AddButton(ref gump, 205, height-30, 9903, 9903, (int)Buttons.NextPage, 1, 0);
                Gumps.AddTooltip(ref gump,"Next Page");
            }
        }

        private int GetListingColor(RuneListing listing)
        {
            return listing == _config.Listings() ? 72 : 0x7b;
        }

        private int GetRuneColor(RuneHelper rune)
        {
            if(!_config.UseColors() || rune.Cord == null)
            {
                return 0x7b;
            }

            switch (rune.Cord.Map)
            {
                case Map.Trammel:
                    return 1000;
                case Map.Felucca:
                    return 0x26;
                case Map.Ilshenar:
                    return 0x3EA;
                case Map.Tokuno:
                    return 2128;
                case Map.Malas:
                    return 2764;
                case Map.TerMur:
                    return 0x1EB;
                default:
                    return 0x7b;
            }
        }
        
        private void ExecuteTransfer(int index)
        {
            UpdateGump();
            if (index >= 10000)
            {
                var rune = _config.RuneLocations()[index - 10000];
                Items.UseItem(rune.BookSerial);
                Misc.Pause(200);
                if(rune.BookType == BookType.Runebook)
                {
                    if(Gumps.HasGump(0x59))
                    {
                        Gumps.SendAction(0x59, rune.GateIndex);
                    }
                }
                else if (rune.BookType == BookType.RuneAtlas)
                {
                    if(Gumps.HasGump(0x1f2))
                    {
                        Gumps.SendAction(0x1f2, rune.GateIndex);
                        while (!Gumps.HasGump(0x1f2))
                        {
                            Misc.Pause(50);
                        }
                        Gumps.SendAction(0x1f2, 6);
                    }
                }
                
            }
            else
            {
                var rune = _config.RuneLocations()[index];
                Items.UseItem(rune.BookSerial);
                Misc.Pause(200);
                if(rune.BookType == BookType.Runebook)
                {
                    if(Gumps.HasGump(0x59))
                    {
                        Gumps.SendAction(0x59, rune.RecallIndex);
                        RegisterRuneLocation(rune);
                    }
                }
                else
                {
                    if(Gumps.HasGump(0x1f2))
                    { 
                        //Make sure we get to the correct page
                        var simplifiedIndex = rune.GateIndex - 100;
                        while(simplifiedIndex>15)
                        {
                            Gumps.SendAction(0x1f2, 1150);
                            simplifiedIndex -= 16;
                            Misc.Pause(200);
                            while(!Gumps.HasGump(0x1f2))
                            {
                                Misc.Pause(50);
                            }
                        }
                        Gumps.SendAction(0x1f2, rune.GateIndex);
                        while (!Gumps.HasGump(0x1f2))
                        {
                            Misc.Pause(50);
                        }
                        Gumps.SendAction(0x1f2, UseMagery ? 4 : 7);
                        RegisterRuneLocation(rune);
                    }
                }
            }
        }

        private void RegisterRuneLocation(RuneHelper rune)
        {
            if (rune.Cord != null)
            {
                return;
            }
            
            var px = Player.Position.X;
            var py = Player.Position.Y;

            var timeoutTime = DateTime.UtcNow.AddSeconds(10);

            while (DateTime.UtcNow < timeoutTime)
            {
                // have we moved more than 10 tiles in any direction
                if (Math.Abs(px - Player.Position.X) > 10 || Math.Abs(py - Player.Position.Y) > 10)
                {
                    rune.Cord = new Cord
                    {
                        X = Player.Position.X,
                        Y = Player.Position.Y,
                        Z = Player.Position.Z,
                        Map = (Map)Player.Map
                    };
                    break;
                }
                Misc.Pause(100);
            }
            _config.Save();
            UpdateGump();
        }
        
        private void LoadConfig()
        {
            var configFile = Path.Combine(Engine.RootPath, "RuneMaster.config");
            if (!File.Exists(configFile))
            {
                _config = new RuneConfig();
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
                        .MakeGenericMethod(typeof(RuneConfig));
                    var readConfig =
                        func.Invoke(type, BindingFlags.InvokeMethod, null, new object[] { data }, null) as RuneConfig;
                    if (readConfig != null)
                    {
                        _config = readConfig;
                    }
                }
            }
        }
    }

    internal class RuneConfig
    {
        
        public virtual bool UseColors() => GetCharacter().UseColors;
        public RuneListing Listings() => GetCharacter().RuneListing;
        public SortType Sorting() => GetCharacter().SortType;
        public List<Character> Characters { get; } = new List<Character>();

        public virtual List<RuneHelper> RuneLocations() => GetCharacter().RuneLocations;
        
        public Character GetCharacter()
        {
            var character = Characters.FirstOrDefault(c => c.ShardName == Misc.ShardName() && c.Name == Player.Name);
            if(character == null)
            {
                character = new Character();
                Characters.Add(character);
                character = Characters.FirstOrDefault(c => c.ShardName == Misc.ShardName() && c.Name == Player.Name);
                Save();
            }

            return character;
        }
        
        public void UppdateLocations(List<RuneHelper> runeLocations, bool merge)
        {
            if (merge)
            {
                var existingRunes = GetCharacter().RuneLocations;

                foreach (var runeLocation in runeLocations)
                {
                    var existingRune = existingRunes
                        .Where(r => r.Name == runeLocation.Name && r.BookSerial == runeLocation.BookSerial).ToList();
                    if (existingRune.Count() == 1)
                    {
                        runeLocation.Cord = existingRune.First().Cord;
                    }
                }

                GetCharacter().RuneLocations = runeLocations;
            }
            else
            {
                GetCharacter().RuneLocations = runeLocations;
            }

            Save();
        }

        public void AddRuneLocation(RuneHelper rune)
        {
            GetCharacter().RuneLocations.Add(rune);
            Save();
        }
        
        public void SetListings(RuneListing listing)
        {
            var c = GetCharacter();
            c.RuneListing = listing;
            Save();
        }
        
        public void SetSortType(SortType sortType)
        {
            var c = GetCharacter();
            c.SortType = sortType;
            Save();
        }
        public void SetShowColors(bool useColors)
        {
            var c = GetCharacter();
            c.UseColors = useColors;
            Save();
        }
        
        public void Save()
        {
            var ns = Assembly.LoadFile(Path.Combine(Engine.RootPath, "Newtonsoft.Json.dll"));
            string data = "";
            foreach(Type type in ns.GetExportedTypes())
            {
                if (type.Name == "JsonConvert")
                {
                    data = type.InvokeMember("SerializeObject", BindingFlags.InvokeMethod, null, null, new object[] { this }) as string;
                    File.WriteAllText(Path.Combine(Engine.RootPath, "RuneMaster.config"), data);
                    break;
                }
            }
        }
    }

    internal class Character
    {
        public Character()
        {
            ShardName = Misc.ShardName();
            Name = Player.Name;
        }
        public string ShardName { get; set; }
        public string Name { get; set; }
        
        public bool UseColors { get; set; }
        public RuneListing RuneListing { get; set; }
        public SortType SortType { get; set; }
        public List<RuneHelper> RuneLocations { get; set; } = new List<RuneHelper>();
    }
    
    
    
    internal class RuneHelper
    {
        public string Name {get; set; }
        public int BookSerial { get; set; }
        public string BookName { get; set; }
        public BookType BookType { get; set; }
        public int RecallIndex { get; set; }
        public int GateIndex { get; set; }
        public int SacredJourneyIndex { get; set; }
        public Cord Cord { get; set; }
    }
    
    internal enum BookType
    {
        Runebook,
        RuneAtlas,
        None
    }

    internal enum SortType
    {
        Alphabetical,
        BookOrder
    }

    internal enum RuneListing
    {
        SimplePaged,
        ByBook,
        ByMap
    }
    
    internal enum Buttons
    {
        Search = 100000,
        ClearSearch = 100001,
        PreviousPage = 100002,
        NextPage = 100003,
        ToggleOptions = 100004,
        SetListTypeSingle = 100005,
        SetListTypeByBook = 100006,
        SetListTypeByMap = 100007,
        SetSortTypeAlphabetical = 100008,
        SetSortTypeBookOrder = 100009,
        ToggleColors = 100010,
        RunTheRunes = 100011,
        UpdateRunes = 100012,
        ResetRunes = 100013
    }
    
    internal enum Map
    {
        Felucca,
        Trammel,
        Ilshenar,
        Malas,
        Tokuno,
        TerMur
    }
    
    internal class Cord
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public Map Map { get; set; }
        private bool _pointerShown = false;

        public void ShowPointer()
        {
            _pointerShown = !_pointerShown;
            Player.TrackingArrow((ushort)(X-Z/10+1), (ushort)(Y-(Z/10)-1), _pointerShown);
        }
    }
}