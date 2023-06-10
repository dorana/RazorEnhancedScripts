using System;
using System.Collections.Generic;
using System.Linq;
using RazorEnhanced;

namespace RazorScripts
{
    public class RuneBookFinder
    {
        private int GumpId = 4598751;
        private Item CurrentBook = null;
        private List<Item> CurrentHits = new List<Item>();

        private readonly List<string> Messages = new List<string>
        {
            "Pssst...over here...",
            "MARCO!",
            "Come closer little one...",
        };
        public void Run()
        {
            Gumps.CloseGump((uint)GumpId);
            ShowGump();
            while (true)
            {
                var sd = Gumps.GetGumpData((uint)GumpId);
                if (sd.buttonid != -1)
                {
                    if (sd.buttonid == 100)
                    {
                        var searchPrompt = sd.text.FirstOrDefault();
                        FindBooks(searchPrompt);
                    }
                    else if (sd.buttonid == 200)
                    {
                        CurrentBook = null;
                        CurrentHits.Clear();
                        sd.text.Clear();
                    }
                    else
                    {

                        CurrentBook = CurrentHits.FirstOrDefault(b => b.Serial == sd.buttonid);
                        
                        if (CurrentBook != null)
                        {
                            Items.Message(CurrentBook.Serial, 0x1d,GetItemMessage());
                            var hue = CurrentBook.Hue;
                            // for(var i = 0; i<15; i++)
                            // {
                            //     var curHue = i % 2 == 0 ? 0x7b : hue;
                            //     Items.SetColor(CurrentBook.Serial, curHue);
                            //     Misc.Pause(100);
                            // }
                            // Items.SetColor(CurrentBook.Serial, hue);
                        }
                        else
                        {
                            Misc.SendMessage("Book Missing");   
                        }
                    }
                    sd.buttonid = -1;
                    ShowGump();
                }
                Misc.Pause(100);
            }
        }

        private void FindBooks(string description)
        {
            CurrentHits = new List<Item>();
            if (string.IsNullOrEmpty(description))
            {
                return;
            }
            
            var books = Items.ApplyFilter(new Items.Filter
            {
                RangeMin = 0,
                RangeMax = 10,
            }).Where(b => b.Name.Equals("Runebook", StringComparison.Ordinal) || b.Name.Equals("Runic Atlas", StringComparison.OrdinalIgnoreCase)).ToList();
            
            books.ForEach(b => Items.WaitForProps(b, 1000));
            
            CurrentHits.AddRange(books.Where(b => b.Properties.Any(p => p.ToString().ToLower().Contains(description.ToLower()))));
        }

        private void ShowGump()
        {
            var searchGump = Gumps.CreateGump();
            searchGump.gumpId = (uint)GumpId;
            searchGump.serial = (uint)Player.Serial;
            Gumps.AddImage(ref searchGump, 0,0,206);
            Gumps.AddImage(ref searchGump, 44,0,201);
            Gumps.AddImage(ref searchGump, 471,0,207);
            Gumps.AddImageTiled(ref searchGump, 0,44,44,GetHeight(), 202);
            Gumps.AddImageTiled(ref searchGump, 471,44,44,GetHeight(), 203);
            Gumps.AddImage(ref searchGump, 0,44+GetHeight(),204);
            Gumps.AddImage(ref searchGump, 44,44+GetHeight(),233);
            Gumps.AddImage(ref searchGump, 471,44+GetHeight(),205);
            Gumps.AddImageTiled(ref searchGump, 44,44,427,GetHeight(), 200);
            Gumps.AddHtml(ref searchGump, 44,24,427,20, "<h1><center>Runebook Finder</center></h1>", false, false);
            Gumps.AddImage(ref searchGump, 57, 60,  1802);
            Gumps.AddImageTiled(ref searchGump, 65, 60, 334, 16,1803);
            Gumps.AddImage(ref searchGump, 399, 60,  1804);
            Gumps.AddTextEntry(ref searchGump, 65,60,342,32,3171,1,"");
            Gumps.AddButton(ref searchGump, 409, 60, 12000,12001,100,1,0);
            Gumps.AddButton(ref searchGump, 409, 80, 12003,12004,200,1,0);
            
            var orderedHits = CurrentHits.OrderBy(GetText).ToList();            

            foreach (var hit in orderedHits)
            {
                var index = orderedHits.IndexOf(hit);
                var baseIndex = CurrentHits.IndexOf(hit);
                Gumps.AddButton(ref searchGump, 20, 100 + index * 30, 4005, 4007, hit.Serial, 1, 0);
                Gumps.AddLabel(ref searchGump, 60, 100 + index * 30, 0, GetText(hit));
            }
            
            
            Gumps.SendGump(searchGump,500,500);
        }

        private string GetText(Item book)
        {
            return book.Properties.FirstOrDefault(b => b.Number == 1042971)?.ToString();
        }

        private int GetHeight()
        {
            return CurrentHits.Count * 30 + 50;
        }

        private string GetItemMessage()
        {
            return Messages[new Random().Next(0, Messages.Count)];
        }
    }
}