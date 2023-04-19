using System;
using System.Linq;
using RazorEnhanced;

namespace Razorscripts
{
    public class MapRepeater
    {
        private int _itemId = Convert.ToInt32("0x0FBF", 16);

        public void Run()
        {
            var tool = Player.Backpack.Contains.FirstOrDefault(i => i.ItemID == _itemId);
            if (tool == null)
            {
                return;
            }

            Items.UseItem(tool);
        }

    }
}