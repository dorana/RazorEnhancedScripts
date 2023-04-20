using System;
using System.Collections.Generic;
using System.Globalization;
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
            var tInfo = CultureInfo.CurrentCulture.TextInfo;
            Misc.SendMessage(tInfo.ToTitleCase("RepondSlayer"));


        }
        
    }
}