using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using RazorEnhanced;

namespace Razorscripts
{
    public class Debugger
    {
        public void Run()
        {
            var item = Player.GetItemOnLayer("RightHand");
            if (item != null)
            {
                Misc.SendMessage(item.Name);
                return;
            }
            item = Player.GetItemOnLayer("LeftHand");
            if (item != null)
            {
                if (item.Properties.Any(p => p.ToString().ToLower().Contains("two-handed")))
                {
                    Misc.SendMessage(item.Name);
                }
            }
            
            
        }
        
    }
}