using System.Linq;
using RazorEnhanced;

namespace RazorScripts
{
    public class Weak
    {
        private int? weightLimit = null;
        private int? goldLimit = 55000;
        
        public void Run()
        {
            var bos = Player.Backpack.Contains.FirstOrDefault(i => i.ItemID == 0x0E76 && i.IsBagOfSending);
            if (bos == null)
            {
                Player.HeadMessage(201,"No Bag of Sending found in backpack");
                return;
            }
            Player.HeadMessage(201,"When Encumbered, Automatically Kachink (W.E.A.K) program Online");
            while (true)
            {
                var goldPile =
                    Player.Backpack.Contains.FirstOrDefault(i => i.ItemID == 0x0EED && i.Hue == 0 && i.Amount > 10000);
                if(goldPile != null)
                {
                    var maxWeight = Player.MaxWeight;
                    if (weightLimit != null && weightLimit < maxWeight)
                    {
                        maxWeight = weightLimit.Value;
                    }
                    
                    if (Player.Weight >= maxWeight || ( goldLimit != null && goldPile.Amount >= goldLimit))
                    {
                        Player.HeadMessage(201,"Executing W.E.A.K protocol");
                        Items.UseItem(bos);
                        Target.WaitForTarget(1000);
                        Target.TargetExecute(goldPile);
                        Misc.Pause(2000);
                    }
                }
                Misc.Pause(200);
            }
        }
    }
}