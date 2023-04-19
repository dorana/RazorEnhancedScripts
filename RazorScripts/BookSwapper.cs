using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RazorEnhanced;

namespace RazorScripts
{
    public class BookSwapper
    {
        private readonly List<Spellbook> _books = new List<Spellbook>();
        private TextInfo _tinfo;

        public BookSwapper()
        {
            
        }

        private int _nonSlayerSerial = -1;

        private readonly List<BookSlayerType> _slayerList = new List<BookSlayerType>
        {
            BookSlayerType.None,
            BookSlayerType.Repond,
            BookSlayerType.Undead,
            BookSlayerType.Reptile,
            BookSlayerType.Dragon,
            BookSlayerType.Arachnid,
            BookSlayerType.Elemental,
            BookSlayerType.AirElemental,
            BookSlayerType.FireElemental,
            BookSlayerType.WaterElemental,
            BookSlayerType.EarthElemental,
            BookSlayerType.BloodElemental,
            BookSlayerType.Demon,
        };

        public void Run()
        {
            _tinfo = CultureInfo.CurrentCulture.TextInfo;
            GatherBooks();
            UpdateBar();
            while (true)
            {
                var bar = Gumps.GetGumpData(788435749);
                if (bar.buttonid != -1 && bar.buttonid != 0)
                {
                    UpdateBar(); 
                    
                    EquipBook(_books.FirstOrDefault(b => b.BookSlayer == (BookSlayerType)bar.buttonid));
                    
                    UpdateBar(); 
                }
                    
                

                Misc.Pause(100);
            }
        }

        private void EquipBook(Spellbook book)
        {
            var held = Player.GetItemOnLayer("RightHand");
            var resolved = _books.FirstOrDefault(b => b.Serial == held?.Serial);
            var bookBinder = Items.FindByID(42783, -1, Player.Backpack.Serial, 0);
            if (bookBinder == null)
            {
                Misc.SendMessage("Unable to find book binder");
            }
            
            if (held != null && resolved != null)
            {
                if (resolved.Serial == book.Serial)
                {
                    return;
                }
                
                var gameEquipped = Items.FindBySerial(resolved.Serial);
                var index = _slayerList.IndexOf(resolved.BookSlayer);
                var offset = index > 5 ? 6 : 0;
                Items.Move(gameEquipped, bookBinder, 1,(index-offset)*20+45, offset == 0 ? 95 : 125);
                Misc.Pause(600);
            }
            else
            {
                if (held != null)
                {
                    Items.Move(held, Player.Backpack.Serial,held.Amount);
                    Misc.Pause(600);
                }
            }
            
            var gameTarget = Items.FindBySerial(book.Serial);
            Player.EquipItem(gameTarget);
            Misc.Pause(200);
        }

        private void UpdateBar()
        {
            var activeBookIndex = GetActiveBookIndex();
            var bar = Gumps.CreateGump();
            bar.buttonid = -1;
            bar.gumpId = 788435749;
            bar.serial = (uint)Player.Serial;
            bar.x = 500;
            bar.y = 500;
            Gumps.AddBackground(ref bar, 0, 0, (_books.Count*60-5), 55, 1755);
            var booksSorted = _books.OrderBy(b => _slayerList.IndexOf(b.BookSlayer)).ToList();
            foreach (var book in booksSorted)
            {
                var index = booksSorted.IndexOf(book);
                var x = index * 60 + 5;
                var y = 5;
                Gumps.AddButton(ref bar, x,y,(int)book.BookSlayer,(int)book.BookSlayer,(int)book.BookSlayer,1,0);
                Gumps.AddTooltip(ref bar, book.Name);
            }

            if (activeBookIndex != -1)
            {
                Gumps.AddImage(ref bar, (60 * activeBookIndex-17),0,30071);
            }
            
            Gumps.CloseGump(788435749);
            Gumps.SendGump(bar, 500,500);
        }

        private int GetActiveBookIndex()
        {
            var held = Player.GetItemOnLayer("RightHand");
            var booksSorted = _books.OrderBy(b => _slayerList.IndexOf(b.BookSlayer)).ToList();
            var resolved = _books.FirstOrDefault(b => b.Serial == held?.Serial);
            if (resolved != null)
            {
                return booksSorted.IndexOf(resolved);
            }

            return -1;
        }

        private void GatherBooks()
        {
            var bookBinder = Items.FindByID(42783, -1, Player.Backpack.Serial, 0);
            if (bookBinder != null)
            {
                var held = Player.GetItemOnLayer("RightHand");
                if (held != null && (held.Name.Contains("book") || held.Name.Contains("Scrapper")))
                {
                    CheckBook(held);
                }

                foreach (var book in bookBinder.Contains)
                {
                    CheckBook(book);
                }
            }
        }

        private void CheckBook(Item book)
        {
            if (book.Name == "Scrapper's Compendium" || book.Serial == _nonSlayerSerial)
            {
                _books.Add(new Spellbook {Serial = book.Serial, Name = _tinfo.ToTitleCase(book.Name), BookSlayer = BookSlayerType.None});
            }
            else
            {
                Items.WaitForProps(book,1000);
                var prop = book.Properties.FirstOrDefault(p => p.ToString().Contains("slayer")) ?? book.Properties.FirstOrDefault(p => p.Number == 1071451);
                if (prop == null)
                {
                    return;
                }

                var slayerString = prop.ToString().ToLower();
                if (slayerString == "silver")
                {
                    slayerString = "undead";
                }
                        
                _books.Add(new Spellbook
                {
                    Name = _tinfo.ToTitleCase(prop.ToString()),
                    Serial = book.Serial,
                    BookSlayer = _slayerList.FirstOrDefault(s => slayerString.Contains(s.ToString().ToLower()))
                });
            }
        }
        
    }
    
    public class Spellbook
    {
        public int Serial { get; set; }
        public string Name { get; set; }
        public BookSlayerType BookSlayer { get; set; }
    }

    public enum BookSlayerType
    {
        None = 20744,
        Repond = 20490,
        Undead = 20486,
        Reptile = 21282,
        Arachnid = 20994,
        Elemental = 24014,
        AirElemental = 2299,
        FireElemental = 2302,
        WaterElemental = 2303,
        EarthElemental = 2301,
        BloodElemental = 20993,
        Demon = 2300,
        Dragon = 21010,
    }
}