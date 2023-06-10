using RazorEnhanced;

namespace RazorScripts
{
    public class WeedPuller
    {
        public void Run()
        {
            while (true)
            {
                var weeds = Items.ApplyFilter(new Items.Filter
                {
                    RangeMin = 0,
                    RangeMax = 1,
                    Name = "Creepy weeds"
                });
                foreach (var weed in weeds)
                {
                    Items.UseItem(weed);
                    Misc.Pause(100);
                }
                
                Misc.Pause(100);
            }
        }
    }
}