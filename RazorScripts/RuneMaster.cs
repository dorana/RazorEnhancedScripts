using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Assistant;
using RazorEnhanced;
using Item = RazorEnhanced.Item;
using Mobile = RazorEnhanced.Mobile;

namespace Razorscripts
{
    public class RuneMaster
    {
        private Target Tar = new Target();
        private uint _gumpId = 741584632;
        private double Magery;
        private double Chivalry;
        private int _activeRunePage = 0;
        private bool _optionShow = false;
        private bool _superPanelShow = false;
        private bool UseMagery => Magery > Chivalry;
        private RuneConfig _config;
        private List<Cord> _trackingArrows = new List<Cord>();
        
        Func<RuneDisplay,bool> _searchFilter = (rl) => true;
        
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
                
                if (!_config.GetRuneBooks().Any())
                {
                    RefreshRunes(false);
                }
                else
                {
                    Items.WaitForContents(Player.Backpack, 1000);
                    var straps = Player.Backpack.Contains.Where(i => i.ItemID == 0xA721 && i.Name.Equals("Runebook Strap", StringComparison.InvariantCultureIgnoreCase)).ToList();
                    Misc.Pause(1000);
                    foreach (var strap in straps)
                    {
                        if(!strap.Contains.Any())
                        {
                            Items.UseItem(strap);
                        }
                    }
                }
                
                UpdateGump();
                
                while (Player.Connected)
                {
                    HandleGumpResponse();
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
                ClearAllTracking();
                Gumps.CloseGump(_gumpId);
            }
        }

        private void HandleGumpResponse()
        {
            var gumpResponse = Gumps.GetGumpData(_gumpId);
            if (gumpResponse.buttonid != -1 && gumpResponse.buttonid != 0)
            {
                if (gumpResponse.buttonid == (int)Buttons.Search)
                {
                    _searchFilter = (rl) => rl.Name.ToLower().Contains(gumpResponse.text.FirstOrDefault()?.ToLower());
                    _activeRunePage = 0;
                    gumpResponse.buttonid = -1;
                    UpdateGump(gumpResponse.text.FirstOrDefault());
                    return;
                }

                if (gumpResponse.buttonid == (int)Buttons.ClearSearch)
                {
                    _searchFilter = (rl) => true;
                    _activeRunePage = 0;
                    gumpResponse.buttonid = -1;
                    UpdateGump();
                    return;
                }

                if (gumpResponse.buttonid == (int)Buttons.PreviousPage)
                {
                    _activeRunePage--;
                    gumpResponse.buttonid = -1;
                    UpdateGump();
                    return;
                }

                if (gumpResponse.buttonid == (int)Buttons.NextPage)
                {
                    _activeRunePage++;
                    gumpResponse.buttonid = -1;
                    UpdateGump();
                    return;
                }

                if (gumpResponse.buttonid == (int)Buttons.ToggleOptions)
                {
                    _optionShow = !_optionShow;
                    gumpResponse.buttonid = -1;
                    UpdateGump();
                    return;
                }

                if (gumpResponse.buttonid == (int)Buttons.ToggleIdocPanel)
                {
                    _superPanelShow = !_superPanelShow;
                    gumpResponse.buttonid = -1;
                    UpdateGump();
                    return;
                }

                if (gumpResponse.buttonid == (int)Buttons.SetListTypeSingle)
                {
                    _config.SetListings(RuneListing.SimplePaged);
                    gumpResponse.buttonid = -1;
                    _activeRunePage = 0;

                    UpdateGump();
                    return;
                }

                if (gumpResponse.buttonid == (int)Buttons.SetListTypeByBook)
                {
                    _config.SetListings(RuneListing.ByBook);
                    gumpResponse.buttonid = -1;
                    _activeRunePage = 0;
                    UpdateGump();
                    return;
                }

                if (gumpResponse.buttonid == (int)Buttons.SetListTypeByMap)
                {
                    _config.SetListings(RuneListing.ByMap);
                    gumpResponse.buttonid = -1;
                    _activeRunePage = 0;
                    UpdateGump();
                    return;
                }

                if (gumpResponse.buttonid == (int)Buttons.SetSortTypeAlphabetical)
                {
                    _config.SetSortType(SortType.Alphabetical);
                    gumpResponse.buttonid = -1;
                    _activeRunePage = 0;
                    UpdateGump();
                    return;
                }

                if (gumpResponse.buttonid == (int)Buttons.SetSortTypeBookOrder)
                {
                    _config.SetSortType(SortType.BookOrder);
                    gumpResponse.buttonid = -1;
                    _activeRunePage = 0;
                    UpdateGump();
                    return;
                }

                if (gumpResponse.buttonid == (int)Buttons.ToggleColors)
                {
                    _config.SetShowColors(!_config.GetUseColors());
                    gumpResponse.buttonid = -1;
                    UpdateGump();
                    return;
                }

                if (gumpResponse.buttonid == (int)Buttons.RunTheRunes)
                {
                    var runes = _config.GetAllRunes();
                    foreach (var runeLocation in runes)
                    {
                        if(runeLocation.Book.IsWorldBook)
                        {
                            continue;
                        }
                        if (runeLocation.Name.ToLower().Contains("ship"))
                        {
                            gumpResponse.buttonid = -1;
                            UpdateGump();
                            continue;

                        }

                        if (runeLocation.Map != null)
                        {
                            gumpResponse.buttonid = -1;
                            UpdateGump();
                            continue;
                        }

                        ProcessRuneIndex(runes.IndexOf(runeLocation));
                        Misc.Pause(2000);
                    }

                    return;
                }

                if (gumpResponse.buttonid == (int)Buttons.UpdateRunes)
                {
                    RefreshRunes(true);
                    gumpResponse.buttonid = -1;
                    UpdateGump();
                    return;
                }

                if (gumpResponse.buttonid == (int)Buttons.ResetRunes)
                {
                    RefreshRunes(false);
                    gumpResponse.buttonid = -1;
                    UpdateGump();
                    return;
                }

                if (gumpResponse.buttonid == (int)Buttons.SetPageSize)
                {
                    if (int.TryParse(gumpResponse.text.LastOrDefault(), out var size))
                    {
                        _config.SetPageSize(size);
                    }

                    gumpResponse.buttonid = -1;
                    UpdateGump();
                    return;
                }

                if (gumpResponse.buttonid == (int)Buttons.AddWorldBook)
                {
                    var addSerial = Tar.PromptTarget("Please Select book to add");
                    var addBook = Items.FindBySerial(addSerial);
                    if (addBook != null)
                    {
                        BookData bookData = null;
                        if (addBook.ItemID == 0x22C5)
                        {
                            bookData = GetRuneBookData(addBook);
                        }
                        else if (addBook.ItemID == 0x9C16 || addBook.ItemID == 0x9C17)
                        {
                            bookData = GetAtlasData(addBook);
                        }

                        bookData.BookLocation = new Cord
                        {
                            X = addBook.Position.X,
                            Y = addBook.Position.Y,
                            Z = addBook.Position.Z,
                            Map = (Map)Player.Map
                        };
                        _config.GetCharacter().Books.Add(bookData);
                        _config.Save();
                    }

                    gumpResponse.buttonid = -1;
                    UpdateGump();
                    return;
                }
                if(gumpResponse.buttonid == (int)Buttons.ToggleShowWorldRunes)
                {
                    _config.SetShowWorldRunes(!_config.GetShowWorldRunes());
                    gumpResponse.buttonid = -1;
                    UpdateGump();
                    return;
                }
                
                

                ProcessRuneIndex(gumpResponse.buttonid-1);
                gumpResponse.buttonid = -1;
                _searchFilter = (rl) => true;
                _activeRunePage = 0;
                UpdateGump();
            }

            gumpResponse.buttonid = -1;
        }

        private void RefreshRunes(bool merge)
        {
            var books = GetAllRuneLocations();
            _config.UpdateLocations(books, merge);
        }

        private List<BookData> GetAllRuneLocations()
        {
            var books = new List<BookData>();
            books.AddRange(GetAllRuneBookLocations());
            Misc.Pause(50);
            books.AddRange(GetAllRuneAtlasLocations());

            return books;
        }

        private List<BookData> GetAllRuneBookLocations()
        {
            var list = new List<BookData>();
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
                list.Add(GetRuneBookData(book));
            }

            return list;
        }

        private BookData GetRuneBookData(Item book)
        {
            Items.WaitForProps(book, 1000);
            var bookName = book.Properties.FirstOrDefault(p => p.Number == 1042971)?.ToString() ?? "NONAME";
            Items.UseItem(book);
            Misc.Pause(200);
            var currentBookData = new BookData
            {
                Serial = book.Serial,
                Name = bookName,
                Type = BookType.Runebook
            };
            
            if (Gumps.HasGump(0x59))
            {
                
                var gd = Gumps.GetGumpData(0x59);
                List<string> runeNameLines = gd.stringList.Skip(2).Take(16).Where(s => !s.Equals("Empty", StringComparison.OrdinalIgnoreCase)).ToList();
                var index = 0;
                foreach (var name in runeNameLines)
                {
                    currentBookData.Runes.Add(new RuneData
                    {
                        Name = name ?? "NONAME",
                        RecallIndex = 50+index,
                        GateIndex = 100+index,
                        SacredJourneyIndex = 75+index,
                        Cord = null
                    });

                    index++;
                }
                
                Gumps.CloseGump(0x59);
            }

            return currentBookData;
        }

        private BookData GetAtlasData(Item atlas)
        {
            Items.WaitForProps(atlas, 1000);
                var bookName = atlas.Properties.FirstOrDefault(p => p.Number == 1042971)?.ToString() ?? "NONAME";
                Items.UseItem(atlas);
                Misc.Pause(200);
                var currentBookData = new BookData
                {
                    Serial = atlas.Serial,
                    Name = bookName,
                    Type = BookType.RuneAtlas
                };
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
                            currentBookData.Runes.Add(new RuneData
                            {
                                Name = namesLine,
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

                return currentBookData;
        }
        
        private List<BookData> GetAllRuneAtlasLocations()
        {
            var list = new List<BookData>();
            var atlases = Player.Backpack.Contains.Where(i => i.ItemID == 0x9C16 || i.ItemID == 0x9C17).ToList();
            var straps = Player.Backpack.Contains.Where(i => i.ItemID == 0xA721 && i.Name.Equals("Runebook Strap", StringComparison.InvariantCultureIgnoreCase)).ToList();
            straps.ForEach( s =>
            {
                Items.WaitForContents(200, s.Serial);
                Misc.Pause(200);
            });
            
            var strapAtlases = straps.SelectMany(s => s.Contains).Where(i => i.ItemID == 0x9C16 || i.ItemID == 0x9C17).ToList();
            
            atlases.AddRange(strapAtlases);
            foreach (var atlas in atlases)
            {
                list.Add(GetAtlasData(atlas));
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
            var height = _config.GetPageSize() * 20 + 70;
            var width = 250;
            Gumps.AddBackground(ref gump,0,0,width,height,1755);
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
            
            HandleSuperPanel(gump, height, width);
            HandleOptionPanel(gump, height, width);
            HandleFlags(gump, width);

           var rows =  ListRunes(gump,enableGates, useMagery);
            
            HandlePaging(gump, height, rows);
            
            Gumps.CloseGump(_gumpId);
            Gumps.SendGump(gump, 500,500);
        }

        private void HandleFlags(Gumps.GumpData gump, int width)
        {
            var xPosition = width;
            var flagPosition = xPosition - 25;

            if (_optionShow)
            {
                flagPosition += 130;
            }
            if (_superPanelShow)
            {
                flagPosition += 200;
            }
            var optionsFlagId = _optionShow ? 9781 : 9780;
            var idocFlagId = _superPanelShow ? 9781 : 9780;
            
            
            Gumps.AddButton(ref gump, flagPosition, 10, optionsFlagId, optionsFlagId, (int)Buttons.ToggleOptions, 1, 1);
            Gumps.AddTooltip(ref gump, $"{(_optionShow ? "Hide" : "Show")} Options");
            
            Gumps.AddButton(ref gump, flagPosition, 40, idocFlagId, idocFlagId, (int)Buttons.ToggleIdocPanel, 1, 1);
            Gumps.AddTooltip(ref gump, $"{(_superPanelShow ? "Hide" : "Show")} Super Mode Panel");
        }

        private void HandleOptionPanel(Gumps.GumpData gump, int height, int width)
        {
            var xPosition = width;
            
            if (_optionShow)
            {
                var optionsHeight = height;
                if (height < 350)
                {
                    optionsHeight = 350;
                }
                
                if (_superPanelShow)
                {
                    xPosition += 200;
                }

                var baseX = xPosition + 15;
                var indentX = xPosition + 35;
                
                Gumps.AddBackground(ref gump,xPosition,0,130,optionsHeight,1755);
                
                Gumps.AddLabel(ref gump,baseX, 15,0x75, "List Type");
                Gumps.AddButton(ref gump,baseX, 40, 5601, 5601, (int)Buttons.SetListTypeSingle, 1, 1);
                Gumps.AddLabel(ref gump,indentX, 40,GetListingColor(RuneListing.SimplePaged), "Single List");
                Gumps.AddButton(ref gump,baseX, 60, 5601, 5601, (int)Buttons.SetListTypeByBook, 1, 1);
                Gumps.AddLabel(ref gump,indentX, 60,GetListingColor(RuneListing.ByBook), "By Book");
                Gumps.AddButton(ref gump,baseX, 80, 5601, 5601, (int)Buttons.SetListTypeByMap, 1, 1);
                Gumps.AddLabel(ref gump,indentX, 80,GetListingColor(RuneListing.ByMap), "By Map");
                
                Gumps.AddLabel(ref gump,baseX, 115,0x75, "Sort By");
                Gumps.AddButton(ref gump,baseX, 145, 5601, 5601, (int)Buttons.SetSortTypeAlphabetical, 1, 1);
                Gumps.AddLabel(ref gump,indentX, 145,_config.GetSorting() == SortType.Alphabetical ? 72 : 0x7b, "Alphabetical");
                Gumps.AddButton(ref gump,baseX, 165, 5601, 5601, (int)Buttons.SetSortTypeBookOrder, 1, 1);
                Gumps.AddLabel(ref gump,indentX, 165,_config.GetSorting() == SortType.Alphabetical ? 0x7b : 72, "Book Order");
                
                Gumps.AddButton(ref gump,baseX, 200, 5601, 5601, (int)Buttons.ToggleColors, 1, 1);
                Gumps.AddLabel(ref gump, indentX, 200, _config.GetUseColors() ? 72 : 0x7b, "Toggle Colors");
                
                Gumps.AddLabel(ref gump, baseX, 235, 0x7b, "Page Size");
                Gumps.AddImageTiled(ref gump, xPosition+25, 258, 20, 16,1803);
                Gumps.AddTextEntry(ref gump, xPosition+25,258,20,32,0x16a,1,_config.GetPageSize().ToString());
                Gumps.AddTooltip(ref gump, "Size of each page");
                Gumps.AddButton(ref gump,xPosition+50, 255, 247, 248, (int)Buttons.SetPageSize, 1, 1);
                
                Gumps.AddButton(ref gump,baseX, optionsHeight-65, 5601, 5601, (int)Buttons.UpdateRunes, 1, 1);
                Gumps.AddTooltip(ref gump, "Checks for changes and updates the runes");
                Gumps.AddLabel(ref gump,indentX, optionsHeight-65,0x7b, "Update Runes");
                Gumps.AddTooltip(ref gump, "Checks for changes and updates the runes");
                
                Gumps.AddButton(ref gump,baseX, optionsHeight-45, 5601, 5601, (int)Buttons.ResetRunes, 1, 1);
                Gumps.AddTooltip(ref gump, "WARNING : Clears all runes and reloads");
                Gumps.AddLabel(ref gump,indentX, optionsHeight-45,0x7b, "Reset Runes");
                Gumps.AddTooltip(ref gump, "WARNING : Clears all runes and reloads");
                
                Gumps.AddButton(ref gump,baseX, optionsHeight-25, 5601, 5601, (int)Buttons.RunTheRunes, 1, 1);
                Gumps.AddTooltip(ref gump, "Teleport to all yet unknown rune locations");
                Gumps.AddLabel(ref gump,indentX, optionsHeight-25,0x7b, "Run Runes");
                Gumps.AddTooltip(ref gump, "Teleport to all yet unknown rune locations");
            }
            
        }

        private void HandleSuperPanel(Gumps.GumpData gump, int height, int width)
        {
            var idocHeight = height;
            if (height < 350)
            {
                idocHeight = 350;
            }
            var xPosition = width;
            
            if (_superPanelShow)
            {
                Gumps.AddBackground(ref gump,xPosition,0,200,idocHeight,1755);
            
                Gumps.AddButton(ref gump,xPosition+15, height-25, 5601, 5601, (int)Buttons.ToggleShowWorldRunes, 1, 1);
                Gumps.AddLabel(ref gump, xPosition+30, height-25, _config.GetShowWorldRunes() ? 72 : 0x7b, "Toggle World Runes");
                
                Gumps.AddButton(ref gump,xPosition+20, 0, 2460, 2460, (int)Buttons.AddWorldBook, 1, 1);
                Gumps.AddTooltip(ref gump,"Add a Book in the world");
            }
            
        }

        private int ListRunes(Gumps.GumpData gump, bool enableGates, bool useMagery)
        {
            List<RuneDisplay> filteredRunes = new List<RuneDisplay>();
            var realRunes = _config.GetAllRunes();
            if(_config.GetListings() == RuneListing.SimplePaged)
            {
                if (_config.GetSorting() == SortType.Alphabetical)
                {
                    filteredRunes = realRunes.Where(_searchFilter).OrderBy(b => b.Name).ToList();
                }
                else
                {
                    filteredRunes = realRunes.Where(_searchFilter).ToList();
                }

                if(!_config.GetShowWorldRunes())
                {
                    filteredRunes = filteredRunes.Where(r => !r.Book.IsWorldBook).ToList();
                }
            }
            else if(_config.GetListings() == RuneListing.ByBook)
            {
                var books = realRunes.Where(_searchFilter).GroupBy(b => b.Book.Name).OrderBy(g => g.Key);
                filteredRunes = new List<RuneDisplay>();
                foreach (var book in books)
                {
                    if(!_config.GetShowWorldRunes())
                    {
                        if(book.ToList().All(r => r.Book.IsWorldBook))
                        {
                            continue;
                        }
                    }
                    filteredRunes.Add(new RuneDisplay
                    {
                        Name = book.Key,
                        Book = new BookDisplay
                        {
                            Name = book.Key,
                            Type = BookType.None,
                        },
                        RecallIndex = 0,
                        GateIndex = 0,
                        SacredJourneyIndex = 0,
                        Map = null
                    });
                    if(_config.GetSorting() == SortType.Alphabetical)
                    {
                        filteredRunes.AddRange(book.OrderBy(b => b.Name));
                    }
                    else
                    {
                        filteredRunes.AddRange(book);
                    }
                }
                
                //get all indexes that would be right on the pageSize indexes
                var pageSizeindexes = filteredRunes.Select((r, i) => new {r, i}).Where(ri => ri.i % _config.GetPageSize() == 0).Select(ri => ri.i).ToList();

                foreach (var ix in pageSizeindexes)
                {
                    var checkBook = filteredRunes[ix];
                    if (checkBook.Book.Type != BookType.None)
                    {
                        // find previous Book of tyepe none
                        var previousBook = filteredRunes.Take(ix).LastOrDefault(b => b.Book.Type == BookType.None);
                        if (previousBook != null)
                        {
                            var index = filteredRunes.IndexOf(previousBook);
                            filteredRunes.Insert(ix, previousBook);
                        }
                    }
                }
            }
            else if (_config.GetListings() == RuneListing.ByMap)
            {
                var maps = realRunes.Where(_searchFilter).GroupBy(b => b.Map).OrderBy(g => g.Key);
                var unknowns = maps.Where(m => m.Key == null);
                var knowns = maps.Where(m => m.Key != null).OrderBy(m => (int)m.Key);
                var listMaps = knowns.Concat(unknowns).ToList();
                filteredRunes = new List<RuneDisplay>();
                foreach (var map in listMaps)
                {
                    if(!_config.GetShowWorldRunes())
                    {
                        if(map.ToList().All(r => r.Book.IsWorldBook))
                        {
                            continue;
                        }
                    }
                    filteredRunes.Add(new RuneDisplay
                    {
                        Name = map.Key?.ToString() ?? "UNKNOWN",
                        Book = new BookDisplay
                        {
                            Type = BookType.None
                        },
                        RecallIndex = 0,
                        GateIndex = 0,
                        SacredJourneyIndex = 0,
                        Map = null
                    });
                    if(_config.GetSorting() == SortType.Alphabetical)
                    {
                        filteredRunes.AddRange(map.OrderBy(b => b.Name));
                    }
                    else
                    {
                        filteredRunes.AddRange(map);
                    }
                }
                
                
                if(!_config.GetShowWorldRunes())
                {
                    filteredRunes = filteredRunes.Where(r => !r.Book.IsWorldBook).ToList();
                }
                
                //get all indexes that would be right on the pageSize indexes
                var pageSizeindexes = filteredRunes.Select((r, i) => new {r, i}).Where(ri => ri.i % _config.GetPageSize() == 0).Select(ri => ri.i).ToList();

                foreach (var ix in pageSizeindexes)
                {
                    var checkBook = filteredRunes[ix];
                    if (checkBook.Book.Type != BookType.None)
                    {
                        // find previous Book of tyepe none
                        var previousBook = filteredRunes.Take(ix).LastOrDefault(b => b.Book.Type == BookType.None);
                        if (previousBook != null)
                        {
                            var index = filteredRunes.IndexOf(previousBook);
                            filteredRunes.Insert(ix, previousBook);
                        }
                    }
                }
            }
            
            var pageRunes = filteredRunes.Skip(_activeRunePage * _config.GetPageSize()).Take(_config.GetPageSize()).ToList();
            
            foreach (var rl in pageRunes)
            {
                var displayIndex = pageRunes.IndexOf(rl);
                var runeIndex = realRunes.IndexOf(rl);
                if (rl.Book.Type == BookType.None)
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
                    var runeName = rl.Book.IsWorldBook ? $"*{rl.Name}" : rl.Name;
                    if (runeName.Length > 28)
                    {
                        Gumps.AddLabel(ref gump,enableGates ? 55 : 35, 35+displayIndex*20, GetRuneColor(rl), runeName.Substring(0, 28));                    
                    }
                    else
                    {
                        Gumps.AddLabel(ref gump,enableGates ? 55 : 35, 35+displayIndex*20, GetRuneColor(rl), runeName);
                    }

                    if (rl.Book.IsWorldBook)
                    {
                        Gumps.AddTooltip(ref gump, $"{rl.Name} (Book in open world)");
                    }
                    else
                    {
                        Gumps.AddTooltip(ref gump, $"{rl.Name} ({rl.Map?.ToString() ?? ("Unknown")})");
                    }
                    
                    
                    Gumps.AddButton(ref gump, 15, 35 + displayIndex * 20, 11400, 11401, runeIndex+1, 1, 1);
                    Gumps.AddTooltip(ref gump,useMagery ? "Recall" : "Sacred Journey");
                    if (enableGates)
                    {
                        Gumps.AddButton(ref gump, 35, 35 + displayIndex * 20, 11410, 11411, runeIndex+1 + 10000, 1, 1);
                        Gumps.AddTooltip(ref gump,"Gate Travel");
                    }
                }
            }

            return filteredRunes.Count();
        }

        private void HandlePaging(Gumps.GumpData gump, int height, int rows)
        {
            var pagecount = (int)Math.Ceiling((double)(rows) / _config.GetPageSize());
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
            return listing == _config.GetListings() ? 72 : 0x7b;
        }

        private int GetRuneColor(RuneDisplay rune)
        {
            if(!_config.GetUseColors() || rune.Map == null)
            {
                return 0x7b;
            }
            
            if(rune.Book.IsWorldBook)
            {
                return 0x550;
            }

            switch (rune.Map)
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
        
        private void HandleWorldBook(RuneDisplay rune)
        {
            if (!rune.Book.IsWorldBook)
            {
                return;
            }

            var items = Items.ApplyFilter(new Items.Filter
            {
                RangeMin = 0,
                RangeMax = 40,
                OnGround = 1
            });
            var book = items.FirstOrDefault(i => i.Serial == rune.Book.Serial);
            if (book == null)
            {
                var runes = _config.GetAllRunes();
                var closestRune = runes.Where(r => r.Cord != null && !r.Book.IsWorldBook).OrderBy(r => Math.Sqrt(Math.Pow(r.Cord.X - rune.Book.Location.X, 2) + Math.Pow(r.Cord.Y - rune.Book.Location.Y, 2))).FirstOrDefault();
                
                // Get the Distance to the closest rune using the Pythagorean theorem
                var dist = Math.Sqrt(Math.Pow(closestRune.Cord.X - rune.Book.Location.X, 2) + Math.Pow(closestRune.Cord.Y - rune.Book.Location.Y, 2));

                if (dist > 200)
                {
                    throw new BookNotFoundException("No Available rune to close by location");
                }
                
                ExecuteTransfer(closestRune);
                var px = Player.Position.X;
                var py = Player.Position.Y;
                var timeoutTime = DateTime.UtcNow.AddSeconds(50);
                while (DateTime.UtcNow < timeoutTime)
                {
                    // have we moved more than 10 tiles in any direction
                    if (Math.Abs(px - Player.Position.X) > 10 || Math.Abs(py - Player.Position.Y) > 10)
                    {
                        break;
                    }
                    Misc.Pause(100);
                }
                Misc.Pause(1000);
                items = Items.ApplyFilter(new Items.Filter
                {
                    RangeMin = 0,
                    RangeMax = 40,
                    OnGround = 1
                });
                book = items.FirstOrDefault(i => i.Serial == rune.Book.Serial);
                if(book == null)
                {
                    throw new BookNotFoundException(
                        "The book should be here, Make sure you have \"Show House Content\" Active");
                }
                HandleCloseByBook(book,rune);
            }
            else
            {
                HandleCloseByBook(book,rune);
            }
        }

        private void HandleCloseByBook(Item book, RuneDisplay rune)
        {
            var player = Mobiles.FindBySerial(Player.Serial);
            
            var dist = book.DistanceTo(player);
            if (dist > 2)
            {
                ClearAllTracking();
                var x = rune.Book.Location.X - (rune.Book.Location.Z / 10) - 1;
                var y = rune.Book.Location.Y - (rune.Book.Location.Z / 10) - 1;
                Player.TrackingArrow((ushort)x, (ushort)y, true);
                _trackingArrows.Add(new Cord
                {
                    X = x,
                    Y = y
                });
            }
            while(dist >= 5)
            {
                Misc.Pause(200);
                player = Mobiles.FindBySerial(Player.Serial);
                dist = book.DistanceTo(player);
                HandleGumpResponse();
            }
            
            while(!Gumps.HasGump(0x1f2) && !Gumps.HasGump(0x59))
            {
                Items.UseItem(rune.Book.Serial);
                Misc.Pause(500);
            }
            
            ClearAllTracking();
        }

        private void ExecuteTransfer(RuneDisplay rune, bool gate = false)
        {
            if (gate)
            {
                Items.UseItem(rune.Book.Serial);
                Misc.Pause(200);
                if(rune.Book.Type == BookType.Runebook)
                {
                    if(Gumps.HasGump(0x59))
                    {
                        Gumps.SendAction(0x59, rune.GateIndex);
                    }
                }
                else if (rune.Book.Type == BookType.RuneAtlas)
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
                Items.UseItem(rune.Book.Serial);
                Misc.Pause(200);
                if(rune.Book.Type == BookType.Runebook)
                {
                    if(Gumps.HasGump(0x59))
                    {
                        if (UseMagery)
                        {
                            Gumps.SendAction(0x59, rune.RecallIndex);
                        }
                        else
                        {
                            Gumps.SendAction(0x59, rune.SacredJourneyIndex);
                        }

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
        
        private void ProcessRuneIndex(int index)
        {
            UpdateGump();
            RuneDisplay rune = null;
            var gate = false;
            if (index >= 10000)
            {
                rune = _config.GetAllRunes()[index - 10000];
                gate = true;
            }
            else
            {
                rune = _config.GetAllRunes()[index];
            }

            if (rune == null)
            {
                return;
            }

            try
            {
                HandleWorldBook(rune);
            }
            catch(BookNotFoundException e)  
            {
                Misc.SendMessage($"{e.Message}");
            }
            
            
            ExecuteTransfer(rune,gate);
        }

        private void RegisterRuneLocation(RuneDisplay rune)
        {
            var book = _config.GetRuneBooks().FirstOrDefault(b => b.Serial == rune.Book.Serial);
            if (book == null)
            {
                return;
            }
            
            var runeData = book.Runes.FirstOrDefault(r => r.Name == rune.Name && r.RecallIndex == rune.RecallIndex);
            
            if (runeData.Cord != null)
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
                    runeData.Cord = new Cord
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

        private void ClearAllTracking()
        {
            foreach (var arrow in _trackingArrows)
            {
                Player.TrackingArrow((ushort)arrow.X, (ushort)arrow.Y, false);
            }
            
            _trackingArrows.Clear();
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
        
        public bool GetUseColors() => GetCharacter().UseColors;
        public bool GetShowWorldRunes() => GetCharacter().ShowWorldRunes;
        public RuneListing GetListings() => GetCharacter().RuneListing;
        public SortType GetSorting() => GetCharacter().SortType;

        public List<BookData> GetRuneBooks() => GetCharacter().Books;
        
        public int GetPageSize() => GetCharacter().RunePageSize;
        public void SetPageSize(int size)
        {
            GetCharacter().RunePageSize = size;
            Save();
        }
        public List<Character> Characters { get; } = new List<Character>();

        public List<RuneDisplay> GetAllRunes()
        {
            var result = new List<RuneDisplay>();
            foreach (var book in GetCharacter().Books)
            {
                foreach (var rune in book.Runes)
                {
                    var dbook = new BookDisplay(book);
                    result.Add(new RuneDisplay
                    {
                        Name = rune.Name,
                        RecallIndex = rune.RecallIndex,
                        GateIndex = rune.GateIndex,
                        SacredJourneyIndex = rune.SacredJourneyIndex,
                        Map = rune.Cord?.Map,
                        Book = dbook,
                        Cord = rune.Cord
                    });
                }
            }

            return result;
        }
        
        
        
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
        
        public void UpdateLocations(List<BookData> books, bool merge)
        {
            if (merge)
            {
                var deleteBookSerials = new List<int>();
                var deleteRunes = new List<RuneData>();
                foreach (var bookData in GetCharacter().Books)
                {
                    var existingRunes = bookData.Runes;
                    var book = books.FirstOrDefault(b => b.Serial == bookData.Serial);
                    if (book == null)
                    {
                        //Book did not exist in the new list
                        deleteBookSerials.Add(bookData.Serial);
                        continue;
                    }
                    
                    var bookItem = Items.FindBySerial(book.Serial);
                    
                    var bookName = bookItem.Properties.FirstOrDefault(p => p.Number == 1042971)?.ToString() ?? "NONAME";
                    
                    bookData.Name = bookName;
                    
                    foreach (var rune in book.Runes)
                    {
                        //Handle Runes Found in both
                        var foundRunes = existingRunes
                            .Where(r => r.Name == rune.Name).ToList();
                        if (foundRunes.Count() == 1)
                        {
                            rune.Cord = foundRunes.First().Cord;
                        }
                        if (!foundRunes.Any())
                        {
                            bookData.Runes.Add(rune);
                        }
                    }
                }
                
                foreach (var serial in deleteBookSerials)
                {
                    var book = GetCharacter().Books.FirstOrDefault(b => b.Serial == serial);
                    GetCharacter().Books.Remove(book);
                }

                foreach (var book in books)
                {
                    var existingBook = GetCharacter().Books.FirstOrDefault(b => b.Serial == book.Serial);
                    if (existingBook == null)
                    {
                        GetCharacter().Books.Add(book);
                        continue;
                    }
                    foreach (var rune in existingBook.Runes)
                    {
                        var anyRune = book.Runes
                            .Any(r => r.Name == rune.Name);
                        if (!anyRune)
                        {
                            deleteRunes.Add(rune);
                        }
                    }

                    foreach (var deleteRune in deleteRunes)
                    {
                        existingBook.Runes.Remove(deleteRune);
                    }
                }
            }
            else
            {
                GetCharacter().Books = books;
            }

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

        public void SetShowWorldRunes(bool showWorldRunes)
        {
            var c = GetCharacter();
            c.ShowWorldRunes = showWorldRunes;
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
        public bool ShowWorldRunes { get; set; }
        public RuneListing RuneListing { get; set; }
        public SortType SortType { get; set; }
        public int RunePageSize { get; set; } = 16;
        public List<BookData> Books { get; set; } = new List<BookData>();
    }
    
    internal class BookData
    {
        
        public int Serial { get; set; }
        public string Name { get; set; }
        public BookType Type { get; set; }
        
        public List<RuneData> Runes { get; set; } = new List<RuneData>();
        public Cord BookLocation { get; set; }
    }
    
    internal class BookDisplay
    {
        public BookDisplay(BookData book)
        {
            Serial = book.Serial;
            Name = book.Name;
            Type = book.Type;
            IsWorldBook = book.BookLocation != null;
            Location = book.BookLocation;
        }

        public BookDisplay()
        {
            
        }
        
        public int Serial { get; set; }
        public string Name { get; set; }
        public BookType Type { get; set; }
        public bool IsWorldBook { get; set; } = false;
        public Cord Location { get; set; }
    }


    internal class RuneData
    {
        public string Name { get; set; }
        public int RecallIndex { get; set; }
        public int GateIndex { get; set; }
        public int SacredJourneyIndex { get; set; }
        public Cord Cord { get; set; }

    }

    internal class RuneDisplay
    {
        public string Name {get; set; }
        public int RecallIndex { get; set; }
        public int GateIndex { get; set; }
        public int SacredJourneyIndex { get; set; }
        public Map? Map { get; set; }
        public Cord Cord { get; set; }
        public BookDisplay Book { get; set; }
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
        ResetRunes = 100013,
        SetPageSize = 100014,
        AddWorldBook = 100015,
        ToggleIdocPanel = 100016,
        ToggleShowWorldRunes = 100017
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
    }

    internal class BookNotFoundException : Exception
    {
        public BookNotFoundException(string message) : base(message)
        {
            
        }
    }
}