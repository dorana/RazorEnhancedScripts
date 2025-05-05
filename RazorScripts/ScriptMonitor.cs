using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using RazorEnhanced;

namespace RazorScripts
{
    public class ScriptMonitor
    {
        private int _checksum;
        private uint _gumpId = 344763792;
        
        // please edit this section with your own scripts, it's important that you add the file extension as well sicne this is part of the key that Razor uses to identify the script.
        // The script you add must also already be added into the script grid in Razor Enhance for this to work.
        //Always add the line with false (this is used internally and should not be changed)
        private Dictionary<string,bool> _scriptStatus = new Dictionary<string, bool>
        {
            {"Lootmaster.cs", false},
            {"SlayerBar.cs", false}
        };
        
        public void Run()
        {
            try
            {
                UpdateGump();
                while (Player.Connected)
                {
                    if (CheckScripts())
                    {
                        UpdateGump();
                    }

                    HandleReply();

                    Misc.Pause(1000);
                }
            }
            catch (ThreadAbortException)
            {
                // Silent
            }
            catch (Exception e)
            {
                Misc.SendMessage(e);
                throw;
            }
            finally
            {
                Gumps.CloseGump(_gumpId);
            }
            
        }

        private void HandleReply()
        {
            var reply = Gumps.GetGumpData(_gumpId);
            var replyIndex = reply.buttonid - 1;
            if (replyIndex >= 0)
            {
                UpdateGump();
                reply.buttonid = -1;
                var script = _scriptStatus.Keys.ToList()[replyIndex];
                var status = _scriptStatus[script];
                if (status)
                {
                    Misc.ScriptStop(script);
                }
                else
                {
                    Misc.ScriptRun(script);
                }
                Misc.Pause(3000);
            
                UpdateGump();
            }
        }

        private void UpdateGump()
        {
            var gump = Gumps.CreateGump();
            gump.gumpId = _gumpId;
            gump.serial = (uint)Player.Serial;
            var height = _scriptStatus.Count * 20 + 30;
            Gumps.AddBackground(ref gump, 0, 0, 200, height, 1755);
            var index = 0;
            foreach (var script in _scriptStatus)
            {
                var marker = script.Value ? 11400 : 11410;
                Gumps.AddButton(ref gump, 15, 15 + index * 20, marker,marker,index+1,1,0);
                Gumps.AddLabel(ref gump, 40,15 + index * 20, 0x7b, script.Key.Split('.').FirstOrDefault() ?? "N/A");
                index++;
            }
            
            Gumps.CloseGump(_gumpId);
            Gumps.SendGump(gump,500,500);
        }
        

        private bool CheckScripts()
        {
            var checkSum = 0;
            string checksumString = string.Empty;
            var keys = _scriptStatus.Keys.ToList();
            foreach (var script in keys)
            {
                var result = Misc.ScriptStatus(script);
                checksumString += result ? "1" : "0";
                _scriptStatus[script] = result;
            }
            
            checkSum = Convert.ToInt32(checksumString, 2);
            
            if (checkSum != _checksum)
            {
                _checksum = checkSum;
                return true;
            }

            return false;
        }
    }
}