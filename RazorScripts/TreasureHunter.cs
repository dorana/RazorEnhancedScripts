using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RazorEnhanced;

namespace Razorscripts
{

    public class TreasureHunter
    {
        private readonly Dictionary<int, string> _facets = new Dictionary<int, string>
        {
            {0, "Felucca"},
            {1, "Trammel"},
            {2, "Ilshenar"},
            {3, "Malas"},
            {4, "Tokuno"},
            {5, "Ter Mur"}
        };
        
        public void Run()
        {
            var tar = new Target();
            var journal = new Journal();
            var lastEntry = journal.GetJournalEntry(null).OrderBy(j => j.Timestamp).LastOrDefault();
            var mapSerial = tar.PromptTarget("Select Treasure Map");
            var map = Items.FindBySerial(mapSerial);
            Items.WaitForProps(map, 2000);
            if (!map.Name.ToLower().Contains("treasure map"))
            {
                Misc.SendMessage("Not a treasure map", 201);
                return;
            }
            if (!map.Properties.Any(p => p.ToString().Contains("Location")))
            {
                var loop = true;
                while (loop)
                {
                    Items.UseItem(map);
                    Misc.Pause(2000);
                    loop = journal.GetJournalEntry(lastEntry).All(j => !j.Text.ToLower().Contains("you successfully decode a treasure map"));
                }
            }
            else
            {
                var locProp = map.Properties.FirstOrDefault(p => p.ToString().ToLower().Contains("location"))?.ToString();
                var facetProp = map.Properties.FirstOrDefault(p => p.ToString().ToLower().Contains("for somewhere in"))?.ToString();
                if (locProp == null || facetProp == null)
                {
                    Misc.SendMessage("Something went wrong", 201);
                    return;
                }
                var facet = facetProp.ToString().Substring(17).Trim();
                if (_facets.ContainsValue(facet))
                {
                    var start = locProp.IndexOf("(");
                    var lenght = locProp.IndexOf(")") - start;
                    var cords = locProp.Substring(start, lenght).Replace("(", "").Replace(")", "").Split(',');
                    var x = int.Parse(cords[0]);
                    var y = int.Parse(cords[1]);
                    CUO.GoToMarker(x, y);
                    
                    while (_facets[Player.Map] == facet)
                    {
                        Player.TrackingArrow((ushort)x, (ushort)y, true);
                    }
                    
                    Player.TrackingArrow((ushort)x, (ushort)y, false);
                }
                else
                {
                    Misc.SendMessage($"Unknown facet: {facet}", 201);
                }
                
            }
        }
    }
}