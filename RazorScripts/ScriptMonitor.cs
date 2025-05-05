using System.Linq;
using RazorEnhanced;

namespace RazorScripts
{
    public class BagTransfer
    {
        public void Run()
        {
            var tar = new Target();
            var sourceSerial = tar.PromptTarget("Select Source bag");
            var sourceBag = Items.FindBySerial(sourceSerial);
            var targetSerial = tar.PromptTarget("Select Target bag");
            var targetBag = Items.FindBySerial(targetSerial);
            Items.WaitForContents(sourceBag,3000);
            Misc.Pause(200);
            while (sourceBag.Contains.Any(i => i.IsLootable && i.Container == sourceBag.Serial))
            {
                foreach (var item in sourceBag.Contains.Where(i => i.IsLootable))
                {
                    Items.Move(item.Serial, targetBag, item.Amount);
                    Misc.Pause(250);
                }
                Misc.Pause(2000);
                Items.WaitForContents(sourceBag, 3000);
            }
        }
    }
}