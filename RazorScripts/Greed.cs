using System.Linq;
using RazorEnhanced;

namespace RazorScripts
{
    public class Greed
    {
        public void Run()
        {
            var bos = Player.Backpack.Contains.FirstOrDefault(i => i.ItemID == 0x0E76 && i.IsBagOfSending);
            if (bos == null)
            {
                Player.HeadMessage(201,"No Bag of Sending found in backpack");
                return;
            }
            Player.HeadMessage(201,"Global Response Electronic Emergency Deposit (G.R.E.E.D) program Online");
            while (true)
            {
                var goldPile =
                    Player.Backpack.Contains.FirstOrDefault(i => i.ItemID == 0x0EED && i.Hue == 0 && i.Amount > 10000);
                if(goldPile != null)
                {
                    if (Player.Hits < 20)
                    {
                        Player.HeadMessage(201,"Executing G.R.E.E.D protocol");
                        Items.UseItem(bos);
                        Target.WaitForTarget(1000);
                        Target.TargetExecute(goldPile);
                        Misc.Pause(2000);
                    }
                }
                Misc.Pause(1000);
            }
        }
    }
}