using System.Collections.Generic;
using System.Linq;
using RazorEnhanced;

namespace RazorScripts
{
    public class CottonPicker
    {
        private List<int> CottonPlants = new List<int>
        {
            0x0C51,
            0x0C52,
            0x0C53,
            0x0C54
        };
        
        public void Run()
        {
            var plantFilter = new Items.Filter
            {
                RangeMin = 0,
                RangeMax = 2
            };
            foreach (var item in Items.ApplyFilter(plantFilter).Where(item => CottonPlants.Contains(item.ItemID)))
            {
                Items.UseItem(item);
                Misc.Pause(100);
            }
            
            Misc.Pause(200);
            
            var cottonFilter = new Items.Filter
            {
                RangeMin = 0,
                RangeMax = 2
            };
            foreach (var cotton in Items.ApplyFilter(cottonFilter))
            {
                Items.Move(cotton, Player.Backpack, cotton.Amount);
                Misc.Pause(300);
            }
        }
    }
}