using RazorEnhanced;
using System.Linq;

namespace Razorscripts
{
    public class MoveBoards
    {
        private string OrganizerListName = "boardmove"; //The name of the organizer hat moves boards to horse
     
        public void Run()
        {
            var axe = Player.GetItemOnLayer("RightHand") ?? Player.GetItemOnLayer("LeftHand");
            if (axe == null)
            {
                Misc.SendMessage("No Axe Equipped", 201);
                return;
            }

            var logstacks = Player.Backpack.Contains.Where(l => l.ItemID == 7133).ToList();
            if (logstacks.Any())
            {
                foreach (var logs in logstacks)
                {
                    Items.UseItem(axe);
                    Target.WaitForTarget(2000);
                    Target.TargetExecute(logs);
                    Misc.Pause(500);
                }
            }
            else
            {
                Misc.SendMessage("No boards found in backpack", 201);
            }
            
            Organizer.ChangeList(OrganizerListName);
            Organizer.FStop();
            Organizer.FStart();
        }
    }
}
