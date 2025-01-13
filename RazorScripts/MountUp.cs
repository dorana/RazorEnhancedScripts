using System.Collections.Generic;
using System.Linq;
using RazorEnhanced;

namespace RazorScripts
{
    public class CutMaster
    {
        private bool _cutHides = true;
        private bool _cutCloth = false;
        private bool _cutBolts = true;
        
        private int _scissorsId = 0x0F9F;
        private int _hideId = 4217;
        private int _clothId = 5991;
        private List<int> _boltIds = new List<int> { 0x0F9B, 0x0F97, 0x0F96, 0x0F9C };
     
        public void Run()
        {
            var backpack = Player.Backpack;
            if (backpack == null)
            {
                Misc.SendMessage("No backpack found", 201);
                return;
            }
            Items.WaitForContents(backpack, 3000);
            Misc.Pause(500);
            
            var scissors = backpack.Contains.FirstOrDefault(i => i.ItemID == _scissorsId);
            if(scissors == null)
            {
                Misc.SendMessage("No scissors found in backpack", 201);
                return;
            }

            var bolts = _cutBolts ? backpack.Contains.Where(i => _boltIds.Contains(i.ItemID)).ToList() : new List<Item>();
            var hides = _cutHides ? backpack.Contains.Where(i => i.ItemID == _hideId).ToList() : new List<Item>();
            var cloth = _cutCloth ? backpack.Contains.Where(i => i.ItemID == _clothId).ToList() : new List<Item>();
            
            var cutTargets = bolts.Concat(hides).Concat(cloth).ToList();
            foreach (var cutTarget in cutTargets)
            {
                Items.UseItemByID(_scissorsId);
                Target.WaitForTarget(2000);
                Target.TargetExecute(cutTarget);
                Misc.Pause(400);
            }

            Player.HeadMessage(201,"All Done");
        }
    }
}