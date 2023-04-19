using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using RazorEnhanced;

namespace RazorScripts
{
    public class GembagSort
    {
        private Item TargetBag;
        public void Run()
        {
            var gems = Enum.GetValues(typeof(Gem)).Cast<Gem>().Select(g => (int)g).ToList().Union(new List<int> { 41779 }).ToList();
            var tar = new Target();
            var targetBagSerial = tar.PromptTarget("Pick Target Gem Bag");
            if (targetBagSerial != 0 && targetBagSerial != -1)
            {
                TargetBag = Items.FindBySerial(targetBagSerial);
                var root = Items.FindBySerial(TargetBag.RootContainer);
                FindInAllContainers(root, gems);
            }
        }
        
        private void FindInAllContainers(Item container, List<int> itemIds)
        {
            Items.WaitForContents(container,1000);
            Misc.Pause(250);
            var foundGems = container.Contains.Where(i => itemIds.Contains(i.ItemID)).ToList();
            foreach (var gem in foundGems)
            {
                Items.Move(gem.Serial, TargetBag, gem.Amount);
                Misc.Pause(250);
            }

            var subcontainers = container.Contains.Where(c => !c.IsBagOfSending && c.IsContainer).ToList();
            foreach (var subcontainer in subcontainers)
            {
                FindInAllContainers(subcontainer, itemIds);
            }
        }
    }
    
    internal enum Gem
    {
        StarSapphire = 3855,
        Ruby = 3859,
        Emerald = 3856,
        Sapphire = 3857,
        Citrine = 3861,
        Amethyst = 3862,
        Tourmaline = 3864,
        Amber = 3877,
        Diamond = 3878
    }
}