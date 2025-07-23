using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using RazorEnhanced;
using Engine = Assistant.Engine;

namespace RazorScripts
{
    public class Lootmaster
    {
        public static readonly bool Debug = false;
        private readonly string _version = "v1.8.8";
        public static readonly bool IsOSI = false;
        
        private Target _tar = new Target();
        private readonly List<int> ignoreList = new List<int>();
        private LootMasterConfig _config = new LootMasterConfig();
        private Journal.JournalEntry _lastEntry = null;

        private Mobile _player;
        private int _lootDelay =  IsOSI ? 800 : 200;
        private DateTime? DeathClock = null;
        readonly Journal _journal = new Journal();
        private Hue _status = Hue.Idle;
        
        public void Run()
        {
            try
            {
                if (Player.IsGhost)
                {
                    Handler.SendMessage(MessageType.Error,
                        "You are a ghost, please ressurrect before running Lootmaster");
                    return;
                }

                var firstRun = false;
                //Remove existing debug log file
                var logFile = Path.Combine(Engine.RootPath, "Lootmaster.log");
                if (File.Exists(logFile))
                {
                    File.Delete(logFile);
                }

                var configFile = Path.Combine(Engine.RootPath, "Lootmaster.config");
                firstRun = !File.Exists(configFile);

                _config.Init(_version);

                Misc.RemoveSharedValue("Lootmaster:DirectContainer");
                Misc.RemoveSharedValue("Lootmaster:ReconfigureBags");
                Misc.RemoveSharedValue("Lootmaster:ClearCurrentCharacter");
                Misc.RemoveSharedValue("Lootmaster:DirectContainerRule");
                Misc.RemoveSharedValue("Lootmaster:Pause");
                Misc.RemoveSharedValue("Lootmaster:OpenConfig");
                Misc.RemoveSharedValue("Lootmaster:ForceClose");
                //SetSpecialRules();
                Setup();
                UpdateLootMasterGump();
                if (firstRun)
                {
                    ShowWelcomeGump();
                }

                _config.ItemColorLookup.AddUnique(new ItemColorIdentifier(3821, 0, "Gold Coin"));
                _config.ItemColorLookup.AddUnique(new ItemColorIdentifier(3821, null, "Gold Coin"));
                _config.ItemColorLookup.AddUnique(new ItemColorIdentifier(41777, 0, "Coin Purse"));
                _config.ItemColorLookup.AddUnique(new ItemColorIdentifier(41777, null, "Coin Purse"));
                _config.ItemColorLookup.AddUnique(new ItemColorIdentifier(41779, 0, "Gem Purse"));

                var gems = Enum.GetValues(typeof(Gem)).Cast<Gem>().ToList();
                foreach (var g in gems)
                    _config.ItemColorLookup.AddUnique(new ItemColorIdentifier((int)g, 0,
                        Handler.SplitCamelCase(g.ToString())));

                var materials = Enum.GetValues(typeof(Materials)).Cast<Materials>().ToList();
                foreach (var m in materials)
                    _config.ItemColorLookup.AddUnique(new ItemColorIdentifier((int)m, 0,
                        Handler.SplitCamelCase(m.ToString())));

                var reagensMagery = Enum.GetValues(typeof(ReagentsMagery)).Cast<ReagentsMagery>().ToList();
                foreach (var rm in reagensMagery)
                    _config.ItemColorLookup.AddUnique(new ItemColorIdentifier((int)rm, 0,
                        Handler.SplitCamelCase(rm.ToString())));

                var reagentsNecro = Enum.GetValues(typeof(ReagentsNecro)).Cast<ReagentsNecro>().ToList();
                foreach (var rn in reagentsNecro)
                    _config.ItemColorLookup.AddUnique(new ItemColorIdentifier((int)rn, 0,
                        Handler.SplitCamelCase(rn.ToString())));

                var reagentsMysticism = Enum.GetValues(typeof(ReagentsMysticism)).Cast<ReagentsMysticism>().ToList();
                foreach (var rmy in reagentsMysticism)
                    _config.ItemColorLookup.AddUnique(new ItemColorIdentifier((int)rmy, 0,
                        Handler.SplitCamelCase(rmy.ToString())));




                while (true)
                {
                    var lm = Gumps.GetGumpData(13659823);
                    var co = Gumps.GetGumpData(96523485);
                    var we = Gumps.GetGumpData(546165464);
                    var ab = Gumps.GetGumpData(492828);
                    if (lm.buttonid == 500)
                    {
                        ShowOptions();
                        lm.buttonid = -1;
                        UpdateLootMasterGump();
                    }
                    else if (lm.buttonid == (int)OptionsItem.ManualRun)
                    {
                        Gumps.SendGump(lm, 150, 150);
                        lm.buttonid = -1;
                        var target = Prompt("Target Container to loot");
                        LootDirectContainer(target);
                    }
                    else if (lm.buttonid == (int)OptionsItem.OpenConfig)
                    {
                        lm.buttonid = -1;
                        _status = Hue.Paused;
                        UpdateLootMasterGump();
                        ShowConfigurator();
                        _status = Hue.Idle;
                        UpdateLootMasterGump();
                    }

                    if (we?.buttonid == 1)
                    {
                        Handler.SendMessage(MessageType.Log, "Loading Starter Rules");
                        LoadStarterRules(true);
                        we.buttonid = -1;
                    }

                    if ((co?.buttonid ?? -1) != -1)
                    {
                        var selected = (OptionsItem)co.buttonid;
                        switch (selected)
                        {
                            case OptionsItem.Reload:
                                ReconfigureBags();
                                break;
                            case OptionsItem.Reset:
                                ClearCurrentCharacterConfig();
                                break;
                            case OptionsItem.ManualRun:
                                var target = Prompt("Target Container to loot");
                                LootDirectContainer(target);
                                break;
                            case OptionsItem.OpenConfig:
                                co.buttonid = -1;
                                ShowConfigurator();
                                break;
                            case OptionsItem.LoadStarter:
                                var confirmResult = MessageBox.Show(
                                    "Are you sure you want to reset config to Starter Rules? \r\n\r\nIt's recomended to have at least 4 target bags ready.",
                                    "Confirm Reset",
                                    MessageBoxButtons.YesNo);
                                if (confirmResult == DialogResult.Yes)
                                {
                                    LoadStarterRules();
                                }

                                break;
                            case OptionsItem.About:
                                ShowAbout();
                                break;
                        }

                        co.buttonid = -1;
                    }

                    if ((ab?.buttonid ?? -1) != -1)
                    {
                        switch (ab.buttonid)
                        {
                            case (int)OptionsItem.Wiki:
                                System.Diagnostics.Process.Start("https://gamebible.net/wiki/doku.php?id=lootmaster");
                                break;
                            case (int)OptionsItem.Coffee:
                                System.Diagnostics.Process.Start("https://www.buymeacoffee.com/Dorana");
                                break;
                        }

                        ab.buttonid = -1;
                    }

                    if (Player.IsGhost)
                    {
                        Misc.Pause(500);
                        DeathClock = DeathClock ?? DateTime.Now;
                        continue;
                    }

                    HandlePause();

                    if (JustRessed())
                    {
                        Handler.SendMessage(MessageType.Info, "Just Ressed");
                        Misc.Pause(2000);
                        continue;
                    }

                    if (Misc.ReadSharedValue("Lootmaster:ReconfigureBags") is bool reconfigure && reconfigure)
                    {
                        ReconfigureBags();
                    }

                    if (Misc.ReadSharedValue("Lootmaster:ClearCurrentCharacter") is bool clearCurrentCharacterConfig &&
                        clearCurrentCharacterConfig)
                    {
                        ClearCurrentCharacterConfig();
                    }

                    if (Misc.ReadSharedValue("Lootmaster:OpenConfig") is bool openConfig &&
                        openConfig)
                    {
                        Misc.RemoveSharedValue("Lootmaster:OpenConfigs");
                        ShowConfigurator();
                    }

                    if (Misc.ReadSharedValue("Lootmaster:ForceClose") is bool forceClose &&
                        forceClose)
                    {
                        Misc.RemoveSharedValue("Lootmaster:ForceClose");
                        return;
                    }

                    if (Misc.ReadSharedValue("Lootmaster:DirectContainer") is int directContainerSerial &&
                        directContainerSerial != 0)
                    {
                        if (directContainerSerial == -1)
                        {
                            continue;
                        }

                        LootRule rule = null;
                        if (Misc.ReadSharedValue("Lootmaster:DirectContainerRule") is string directContainerRule &&
                            !string.IsNullOrEmpty(directContainerRule))
                        {
                            rule = _config.GetCharacter().Rules.FirstOrDefault(r => r.RuleName == directContainerRule);
                        }

                        LootDirectContainer(directContainerSerial, rule);

                        Misc.RemoveSharedValue("Lootmaster:DirectContainer");
                        Misc.RemoveSharedValue("Lootmaster:DirectContainerRule");

                        Misc.Pause(200);
                        continue;
                    }

                    var corpses = Items.ApplyFilter(new Items.Filter
                    {
                        IsCorpse = 1,
                        RangeMax = 2,
                        RangeMin = 0,
                    });
                    if (corpses.Any(c => !ignoreList.Contains(c.Serial)))
                    {
                        _status = Hue.Looting;
                        UpdateLootMasterGump();
                        foreach (var corpse in corpses.Where(c => !ignoreList.Contains(c.Serial)))
                        {
                            HandlePause();
                            if (corpse.DistanceTo(_player) > 2)
                            {
                                break;
                            }

                            if (Target.HasTarget())
                            {
                                continue;
                            }

                            _lastEntry = _journal.GetJournalEntry(null).OrderBy(j => j.Timestamp).LastOrDefault();
                            LootContainer(corpse);

                            var rows = _journal.GetJournalEntry(_lastEntry);
                            if (rows == null) continue;
                        }

                        _status = Hue.Idle;
                        UpdateLootMasterGump();
                    }

                    Misc.Pause(100);
                }
            }
            catch (ThreadAbortException)
            {
               //Silent
            }
            catch (LootMasterException)
            {
                //Silent
            }
            catch (Exception e)
            {

                if (!Debug)
                {
                    Handler.SendMessage(MessageType.Error,
                        "Lootmaster encountered an error, and was forced to shut down");
                    var logFile = Path.Combine(Engine.RootPath, "Lootmaster.log");
                    File.AppendAllText(logFile, e.ToString());
                }
                else
                {
                    Handler.SendMessage(MessageType.Debug, e.ToString());
                }

                throw;
            }
            finally
            {
                Gumps.CloseGump(13659823);
            }
        }

        private void HandlePause()
        {
            var pauseCheckvalue = Misc.ReadSharedValue("Lootmaster:Pause");
            if (pauseCheckvalue is int pauseTimer && pauseTimer > 0)
            {
                var prevHue = _status;
                _status = Hue.Paused;
                UpdateLootMasterGump();
                Misc.Pause(pauseTimer);
                Misc.SetSharedValue("Lootmaster:Pause", 0);
                _status = prevHue;
                UpdateLootMasterGump();
            }
            else if (pauseCheckvalue is bool value)
            {
                var prevHue = _status;
                _status = Hue.Paused;
                UpdateLootMasterGump();
                Misc.Pause(2000);
                Misc.SetSharedValue("Lootmaster:Pause", 0);
                _status = prevHue;
                UpdateLootMasterGump();
            }
        }

        private bool JustRessed()
        {
            if (DeathClock == null)
            {
                return false;
            }
            var bagRules = _config.GetCharacter().Rules.Where(r => !r.Disabled && r.TargetBag != null && r.TargetBag != Player.Backpack.Serial).ToList();
            if (!bagRules.Any())
            {
                return false;
            }

            return bagRules.All(br => Items.FindBySerial(br.TargetBag ?? int.MinValue) == null);
        }

        private void SetStarterRules()
        {
            _config.CreateRule(LootRule.Gold);
            _config.CreateRule(LootRule.Gems);
            _config.CreateRule(new LootRule
            {
                RuleName = "Valuable Weapons",
                EquipmentSlots = new List<EquipmentSlot>
                {
                    EquipmentSlot.RightHand
                },
                Properties = new List<PropertyMatch>
                {
                    new PropertyMatch
                    {
                        Property = ItemProperty.HitChanceIncrease,
                        Value = 15
                    },
                    new PropertyMatch
                    {
                        Property = ItemProperty.DamageIncrease,
                        Value = 30,
                    },
                    new PropertyMatch
                    {
                        Property = ItemProperty.SwingSpeedIncrease,
                        Value = 10
                    }
                }
            });
            
            _config.CreateRule(new LootRule
            {
                RuleName = "Valuable Caster Jewelry",
                EquipmentSlots = new List<EquipmentSlot>
                {
                    EquipmentSlot.Jewellery
                },
                Properties = new List<PropertyMatch>
                {
                    new PropertyMatch
                    {
                        Property = ItemProperty.SpellDamageIncrease,
                        Value = 18
                    },
                    new PropertyMatch
                    {
                        Property = ItemProperty.LowerReagentCost,
                        Value = 20,
                    },
                    new PropertyMatch
                    {
                        Property = ItemProperty.FasterCasting,
                        Value = 1
                    },
                    new PropertyMatch
                    {
                        Property = ItemProperty.FasterCastRecovery,
                        Value = 2
                    }
                }
            });
            
            _config.CreateRule(new LootRule
            {
                RuleName = "Valuable Gear #1",
                EquipmentSlots = new List<EquipmentSlot>
                {
                    EquipmentSlot.Armour
                },
                Properties = new List<PropertyMatch>
                {
                    new PropertyMatch
                    {
                        Property = ItemProperty.DamageEater,
                        Value = 10
                    },
                    new PropertyMatch
                    {
                        Property = ItemProperty.Luck,
                        Value = 100
                    },
                }
            });
            
            _config.CreateRule(new LootRule
            {
                RuleName = "Valuable Gear #2",
                EquipmentSlots = new List<EquipmentSlot>
                {
                    EquipmentSlot.Armour
                },
                Properties = new List<PropertyMatch>
                {
                    new PropertyMatch
                    {
                        Property = ItemProperty.Cartography
                    }
                }
            });

            _config.CreateRule(new LootRule
            {
                RuleName = "CUB Items",
                MinimumRarity = ItemRarity.MajorMagicItem
            });

            _config.CreateRule(new LootRule("Maps", "Treasure Map"));
            
        }
        

        

        private void ReconfigureBags()
        {
            foreach (var rule in _config.GetCharacter().Rules)
            {
                var tempBag = rule.GetTargetBag();
                rule.TargetBag = (tempBag?.Serial == Player.Backpack.Serial || tempBag?.RootContainer == Player.Backpack.Serial) ? tempBag?.Serial : null;
            }

            Setup();
        }
        
        private void ClearCurrentCharacterConfig()
        {
            var confirmResult =  MessageBox.Show("Are you sure you want to clear config for current character?",
                "Confirm Delete!",
                MessageBoxButtons.YesNo);
            if (confirmResult == DialogResult.Yes)
            {
                var current = _config.Characters.FirstOrDefault(c => c.PlayerName == Player.Name);
                _config.Characters.Remove(current);
                _config.Save();
                Misc.RemoveSharedValue("Lootmaster:ClearCurrentCharacter");
                //SetSpecialRules();
                Setup();
            }
        }
        
        private void LoadStarterRules(bool openConfig = false)
        {
            var current = _config.Characters.FirstOrDefault(c => c.PlayerName == Player.Name);
            _config.Characters.Remove(current);
            _config.Save();
            Misc.RemoveSharedValue("Lootmaster:ClearCurrentCharacter");
            SetStarterRules();
            if (openConfig)
            {
                ShowConfigurator();
            }
            else
            {
                ReconfigureBags();
            }
        }
        
        private void LootDirectContainer(int directContainerSerial) => LootDirectContainer(directContainerSerial, null);

        private void LootDirectContainer(int directContainerSerial, LootRule rule)
        {
            var directContainer = Items.FindBySerial(directContainerSerial);
            
            if (directContainer != null)
            {
                Handler.SendMessage(MessageType.Log, $"Looting direct container {directContainer.Name}");
                _status = Hue.Looting;
                UpdateLootMasterGump();
                LootContainer(directContainer, rule);
                Misc.Pause(500);
                _status = Hue.Idle;
                UpdateLootMasterGump();
            }
        }

        private void ShowWelcomeGump()
        {
            var welcome = Gumps.CreateGump();
                    Gumps.AddBackground(ref welcome, 0, 0, 500, 800, -1);
                    Gumps.AddPage(ref welcome, 0);
                    Gumps.AddImage(ref welcome, 0, 0, 1596);
                    Gumps.AddImage(ref welcome, 0, 142, 1597);
                    Gumps.AddImage(ref welcome, 0, 283, 1598);
                    Gumps.AddImage(ref welcome, 0, 425, 1599);
                    Gumps.AddHtml(ref welcome, 0, 60, 400, 20, "<center><h1>Welcome to Lootmaster</h1></center>", false, false);
                    Gumps.AddLabel(ref welcome, 30, 90, 0, "It seems this is your first time starting lootmaster.");
                    Gumps.AddLabel(ref welcome, 30, 105, 0, "First off, let me say THANK YOU");
                    Gumps.AddLabel(ref welcome, 30, 120, 0, "for deciding to use my script.");
                    Gumps.AddLabel(ref welcome, 30, 150, 0, "Lootmaster is an Autolooter designed to simplify");
                    Gumps.AddLabel(ref welcome, 30, 165, 0, "the looting process in Ultima Online.");
                    Gumps.AddLabel(ref welcome, 30, 195, 0, "It does so by using a set of User Defined Rules");
                    Gumps.AddLabel(ref welcome, 30, 210, 0, "That you can access from the Configurator");
                    Gumps.AddLabel(ref welcome, 30, 240, 0, "In order to access the Options of Lootmaster");
                    Gumps.AddLabel(ref welcome, 30, 255, 0, "press the blue gem in the Lootmaster status window");
                    Gumps.AddLabel(ref welcome, 30, 270, 0, "This will open the Options Menu");
                    Gumps.AddLabel(ref welcome, 30, 300, 0, "Since this is your first time using Lootmaster");
                    Gumps.AddLabel(ref welcome, 30, 315, 0, "I recommend that you start by setting up some of the");
                    Gumps.AddLabel(ref welcome, 30, 330, 0, "Preset rules found in Configurator");
                    Gumps.AddLabel(ref welcome, 30, 360, 0, "This will give you a good starting point to learn");
                    Gumps.AddLabel(ref welcome, 30, 375, 0, "How Lootmaster works and how to use it");
                    Gumps.AddLabel(ref welcome, 30, 405, 0, "If you are new to Ultima Online or do not know");
                    Gumps.AddLabel(ref welcome, 30, 420, 0, "What items to look for, I suggest you start with");
                    Gumps.AddLabel(ref welcome, 30, 435, 0, "our Preset Starter Rules for New Players");
                    Gumps.AddButton(ref welcome, 25, 470, 2152, 2151, 1, 1, 0);
                    Gumps.AddLabel(ref welcome, 65, 475, 0, "Load Starter Rules for New Players");


                    welcome.gumpId = 546165464;
                    welcome.serial = (uint)Player.Serial;
                    Gumps.CloseGump(546165464);
                    Gumps.SendGump(welcome, 700, 500);
        }

        private void ShowOptions()
        {
            var options = Gumps.CreateGump();
            Gumps.AddBackground(ref options, 0, 0, 400, 290, 1228);
            Gumps.AddLabel(ref options, 50, 3, 0, "Lootmaster Options");
            Gumps.AddButton(ref options, 25, 25, 2152, 2151, (int)OptionsItem.OpenConfig, 1, 0);
            Gumps.AddLabel(ref options, 65, 30, 0, "Open Loot rules Configurator");
            Gumps.AddButton(ref options, 25, 65, 2152, 2151, (int)OptionsItem.ManualRun, 1, 0);
            Gumps.AddLabel(ref options, 65, 70, 0, "Run Lootmaster on Target Container");
            Gumps.AddButton(ref options, 25, 105, 2152, 2151, (int)OptionsItem.Reload, 1, 0);
            Gumps.AddLabel(ref options, 65, 110, 0, "Reload Character Config");
            Gumps.AddButton(ref options, 25, 145, 5832, 5833, (int)OptionsItem.Reset, 1, 0);
            Gumps.AddLabel(ref options, 65, 150, 0, "Reset Character Config");
            Gumps.AddButton(ref options, 25, 185, 5832, 5833, (int)OptionsItem.LoadStarter, 1, 0);
            Gumps.AddLabel(ref options, 65, 190, 0, "Load Starter Rules (Clears current config)");
            Gumps.AddButton(ref options, 25, 225, 5826, 5827, (int)OptionsItem.About, 1, 0);
            Gumps.AddLabel(ref options, 65, 230, 0, "About Lootmaster");
            Gumps.AddLabel(ref options,300,260,0,_version);
            Gumps.CloseGump(96523485);
            options.serial = (uint)Player.Serial;
            options.gumpId = 96523485;
            Gumps.SendGump(options, 150, 150);
        }

        private void ShowConfigurator()
        {
            Handler.SendMessage(MessageType.Log, "Pausing Lootmaster while configure is open");
            var conf = new Configurator();
            conf.Open(_config);
            ReconfigureBags();
        }

        private void ShowAbout()
        {
            var about = Gumps.CreateGump();
            Gumps.AddBackground(ref about, 0, 0, 426, 229, -1);
            Gumps.AddImage(ref about, 0,0,11055);
            Gumps.AddHtml(ref about, 95, 25, 400, 20, "<h1>About</h1>", false, false);
            Gumps.AddLabel(ref about, 55,50,0,"Lootmaster is created");
            Gumps.AddLabel(ref about, 55,62,0,"and is maintained by");
            Gumps.AddLabel(ref about, 55,74,0,"Matt Dorana");
            Gumps.AddLabel(ref about, 55,98,0,"It is free to use and");
            Gumps.AddLabel(ref about, 55,110,0,"will receive updates");
            Gumps.AddLabel(ref about, 55,122,0,"on the  feedback");
            Gumps.AddLabel(ref about, 55,134,0,"and requests");
            Gumps.AddLabel(ref about, 55,146,0,"is sent in");
            Gumps.AddLabel(ref about, 220,50,0,"If you enjoy this");
            Gumps.AddLabel(ref about, 220,62,0,"script feel free to");
            Gumps.AddLabel(ref about, 220,74,0,"reach out to me on");
            Gumps.AddLabel(ref about, 220,86,0,"Discord");
            Gumps.AddLabel(ref about, 250,130,0,"Wiki Page");
            Gumps.AddButton(ref about, 215, 125, 5843, 5844, (int)OptionsItem.Wiki, 1, 0);
            Gumps.AddLabel(ref about, 250,164,0,"Buy me a coffee");
            Gumps.AddButton(ref about, 215, 159, 5843, 5844, (int)OptionsItem.Coffee, 1, 0);
            
            
            about.serial = (uint)Player.Serial;
            about.gumpId = 492828;
            
            Gumps.CloseGump(492828);
            Gumps.SendGump(about, 500, 350);
        }


        private void UpdateLootMasterGump()
        {
            var controller = Gumps.CreateGump();
            controller.x = 300;
            controller.y = 300;
            Gumps.AddPage(ref controller, 0);
            Gumps.AddBackground(ref controller, 0, 0, 187, 47, 1755);
            Gumps.AddBackground(ref controller, 0, 47, 140, 47, 1755);
            Gumps.AddBackground(ref controller, 140, 47, 47, 47, 1755);
            if(_status == Hue.Looting)
            {
                Gumps.AddButton(ref controller, 150, 55, 5826, 5827, 500, 1, 0);
            }
            else if(_status == Hue.Paused)
            {
                Gumps.AddButton(ref controller, 150, 55, 9721, 9722, 500, 1, 0);    
            }
            else
            {
                Gumps.AddButton(ref controller, 150, 55, 2152, 2151, 500, 1, 0);    
            }
            Gumps.AddLabel(ref controller, 8, 12, (int)_status, $"Lootmaster : {_status.ToString()}");
            Gumps.AddButton(ref controller,8,58,2116,2115, (int)OptionsItem.ManualRun,1,0);
            Gumps.AddButton(ref controller,68,58,2006,2007, (int)OptionsItem.OpenConfig,1,0);
            Gumps.CloseGump(13659823);
            controller.serial = (uint)Player.Serial;
            controller.gumpId = 13659823;
            Gumps.SendGump(controller, 150, 150);
        }

        private void LootContainer(Item container) => LootContainer(container, null);
        private void LootContainer(Item container, LootRule rule)
        {
            Handler.SendMessage(MessageType.Debug, $"Waiting for contents of {container.Name}");
            Items.UseItem(container);
            Misc.Pause(_lootDelay);
            
            var entries = _journal.GetJournalEntry(_lastEntry);
            if (entries != null && entries.Any(e => e.Type.Equals("System", StringComparison.InvariantCultureIgnoreCase) && (e.Text.Equals("you may not loot this corpse.", StringComparison.CurrentCultureIgnoreCase) || e.Text.Equals("You did not earn the right to loot this creature!", StringComparison.InvariantCultureIgnoreCase))))
            {
                IgnoreCorpse(container);
                _lastEntry = _journal.GetJournalEntry(null).OrderBy(j => j.Timestamp).LastOrDefault();
                return;
            }
            Items.WaitForContents(container, 5000);
            
            var stamp = DateTime.Now;
            Handler.SendMessage(MessageType.Debug, $"Looting {container.Name}");
            var timeValidator = DateTimeOffset.Now;

            if (container.IsCorpse && container.DistanceTo(_player) > 2)
            {
                return;
            }

            if (IsOSI)
            {
                Misc.Pause(1000);
            }
            Misc.Pause(50);


            var sum = 1;
            Handler.SendMessage(MessageType.Debug, $"Starting loot cycle");
            while (sum != 0)
            {
                sum = Loot(container, rule);
                

                if (sum == int.MinValue)
                {
                    return;
                }
                if (sum == int.MaxValue)
                {
                    Misc.Pause(2000);
                    return;
                }
                
                Misc.Pause(100);
                
                if (DateTimeOffset.Now - timeValidator > TimeSpan.FromSeconds(IsOSI ? 20 : 10))
                {
                    Handler.SendMessage(MessageType.Error, "Something seems to have locked up, aborting loot cycle");
                    return;
                }
            }

            if (Player.IsGhost)
            {
                return;
            }

            Misc.Pause(IsOSI ? 200 : 100);
            IgnoreCorpse(container);
        }

        private void IgnoreCorpse(Item container)
        {
            ignoreList.Add(container.Serial);
            if (container.IsCorpse && _config.ColorCorpses)
            {
                Items.SetColor(container.Serial,_config.ColorCorpsesColor ?? 0x3F6);
            }
        }

        private int Loot(Item container, LootRule singleRule)
        {
            List <GrabTarget> lootItems = new List<GrabTarget>();
            
            var sum = 0;

            foreach (var item in container.Contains)
            {
                Handler.SendMessage(MessageType.Debug, $"Item {item.Name} is lootable:{item.IsLootable}");
            }

            foreach (var item in container.Contains.Where(c => c.IsLootable && !(c.Name.Trim().StartsWith("(") && c.Name.Trim().EndsWith(")"))))
            {
                Handler.SendMessage(MessageType.Debug,$"Checking Item {item.Name}");
                if (container.IsCorpse && container.DistanceTo(_player) > 2)
                {
                    return int.MinValue;
                }

                if (Player.IsGhost)
                {
                    return int.MinValue;
                }

                Items.WaitForProps(item, 3000);

                if (singleRule != null)
                {
                    if (singleRule.Match(item))
                    {
                        lootItems.Add(new GrabTarget
                        {
                            Item = item,
                            Rule = singleRule
                        });
                    }
                }

                foreach (var rule in _config.GetCharacter().Rules.Where(r => !r.Disabled))
                {
                    Handler.SendMessage(MessageType.Debug, $"Checking Rule {rule.RuleName}");
                    if (container.IsCorpse && container.DistanceTo(_player) > 2)
                    {
                        Handler.SendMessage(MessageType.Debug, $"Container is too far away");
                        return int.MinValue;
                    }
                    
                    if (rule.TargetBag == null)
                    {
                        Handler.SendMessage(MessageType.Debug, $"Target bag is null");
                        continue;
                    }
                    
                    if (rule.TargetBag == container.Serial)
                    {
                        Handler.SendMessage(MessageType.Debug, $"Target bag is same as container");
                        continue;
                    }

                    if (!item.IsChildOf(container))
                    {
                        var itemContainer = Items.FindBySerial(item.Container);
                        
                        Handler.SendMessage(MessageType.Debug, $"Item is not a child of container {container.Name} but {itemContainer?.Name ?? "of nonexiting container"}");
                        if (itemContainer != null)
                        {
                            if (itemContainer.IsChildOf(container))
                            {
                                Handler.SendMessage(MessageType.Debug, $"Item is in a sub container");
                            }
                        }
                        continue;
                    }
                    
                    if (rule.TargetBag == item.Serial)
                    {
                        Handler.SendMessage(MessageType.Debug, $"Target bag is same as item");
                        continue;
                    }
                    
                    if (rule.Match(item))
                    {
                        Handler.SendMessage(MessageType.Debug, $"Adding {item.Name} to loot list");
                        lootItems.Add(new GrabTarget
                        {
                            Item = item,
                            Rule = rule
                        });
                        break;
                    }
                }
                
                Handler.SendMessage(MessageType.Debug,"All rules checked");
            }
            
            Handler.SendMessage(MessageType.Debug,"Items collected, starting loot");
            Misc.Pause(_lootDelay);
            var overLimit = false;
            
            foreach (var li in lootItems)
            {
                HandlePause();
                var checkItem = Items.FindBySerial(li.Item.Serial);
                if (checkItem?.Container == container.Serial)
                {
                    if (li.Item.Weight + Player.Weight > Player.MaxWeight)
                    {
                        overLimit = true;
                    }
                    else
                    {
                        sum += GrabItem(li.Item, li.Rule);
                    }
                    
                }
            }

            if (overLimit)
            {
                Handler.SendMessage(MessageType.Info, "Maximum weight reached, one or more items were left ont he corpse");
                return int.MaxValue;
            }

            return sum;
        }

        private bool Setup()
        {
            var character = _config.GetCharacter();

            if (character.Rules.Any(r => string.IsNullOrEmpty(r.RuleName)))
            {
                Handler.SendMessage(MessageType.Error, "One or more rules are missing a name");
                return false;
            }
            foreach (var rule in character.Rules)
            {
                while (rule.GetTargetBag() == null)
                {
                    var target = Prompt($"Pick loot bag for {rule.RuleName}");
                    SetBag(target, rule);
                }
            }

            Misc.RemoveSharedValue("LootmasterDirectContainer");
            
            _player = Mobiles.FindBySerial(Player.Serial);
            if (_config.BaseDelay > 0)
            {
                _lootDelay = _config.BaseDelay;
            }

            Misc.RemoveSharedValue("Lootmaster:ReconfigureBags");

            Handler.SendMessage(MessageType.Info, "Lootmaster is ready to loot");
            
            _config.Save();

            return true;
        }

        private int GrabItem(Item item,LootRule rule)
        {
            MoveToBag(item, rule.GetTargetBag());
            // Misc.Pause(200);
            
            if (rule.Alert)
            {
                Handler.SendMessage(MessageType.Info, $"Looted item for rule {rule.RuleName}");
            }

            
            
            return item.Amount;
        }

        private void SetBag(int itemSerial, LootRule rule)
        {
            Item current;
            if (itemSerial == Player.Backpack.Serial)
                current = Player.Backpack;
            else
                current = FindBag(itemSerial);

            if (current == null || (current.Serial != Player.Backpack.Serial && current.RootContainer != Player.Backpack.Serial))
            {
                Handler.SendMessage(MessageType.Error, $"Target is not a suitable container for {rule.RuleName}");
                return;
            }

            rule.TargetBag = current.Serial;
        }

        private Item FindBag(int target)
        {
            var targetItem = Items.FindBySerial(target);
            return targetItem?.IsContainer != true ? null : targetItem;
        }

        private int Prompt(string text)
        {
            Handler.SendMessage(MessageType.Prompt, text);
            return _tar.PromptTarget(text);
        }

        private void MoveToBag(Item item, Item destinationBag)
        {
            Items.Move(item, destinationBag, item.Amount);
            Misc.Pause(_lootDelay);
        }
    }

    public class GrabTarget
    {
        public Item Item { get; set; }
        public LootRule Rule { get; set; }
    }

    public class LootRule
    {

        public Guid Id { get; set; }
        public string RuleName { get; set; }
        public List<string> ItemNames { get; set; } = new List<string>();
        public List<EquipmentSlot> EquipmentSlots { get; set; } = new List<EquipmentSlot>();
        public List<PropertyMatch> Properties { get; set; } = new List<PropertyMatch>();
        public ItemRarity? MinimumRarity { get; set; }
        public ItemRarity? MaximumRarity { get; set; }
        public int? MaxWeight { get; set; }
        public bool IgnoreWeightCurse { get; set; }
        public List<ItemColorIdentifier> ItemColorIds { get; set; } = new List<ItemColorIdentifier>();
        public List<int> ItemIds { get; set; }
        public List<PropertyMatch> BlackListedProperties { get; set; } = new List<PropertyMatch>();

        public bool Alert { get; set; }


        public bool Disabled { get; set; }

        public int? TargetBag { get; set; }
        
        public int? PropertyMatchRequirement { get; set; }

        public string RegExString { get; set; }
        
        public Item GetTargetBag() => Items.FindBySerial(TargetBag ?? -1);

        public static LootRule Gold =>
            new LootRule
            {
                RuleName = "Gold",
                ItemColorIds = new List<ItemColorIdentifier> {
                    new ItemColorIdentifier(3821,0, "Gold Coin"),
                    new ItemColorIdentifier(41777,0, "Coin Purse")
                }, //GoldStacks and GoldBags
                
                MaxWeight = 100
            };

        public static LootRule Gems =>
            new LootRule
            {
                RuleName = "Gems",
                ItemColorIds = Enum.GetValues(typeof(Gem)).Cast<Gem>().Select(g =>
                    new ItemColorIdentifier((int)g,0, Handler.SplitCamelCase(g.ToString()))).ToList().Union(new List<ItemColorIdentifier> { new ItemColorIdentifier(41779,0, "Gem Bag") }).ToList(),
                MaxWeight = 100
            };

        public static LootRule ImbueMaterials
        {
            get
            {
                var essancesMatch = new List<ItemColorIdentifier>
                {
                    new ItemColorIdentifier((int)Materials.Essence, 0x048e, "Essence of Diligence"),
                    new ItemColorIdentifier((int)Materials.Essence, 0x04f4, "Essence of Balance"),
                    new ItemColorIdentifier((int)Materials.Essence, 0x01c7, "Essence of Feeling"),
                    new ItemColorIdentifier((int)Materials.Essence, 0x0025, "Essence of Persistence"),
                    new ItemColorIdentifier((int)Materials.Essence, 0x0455, "Essence of Singularity"),
                    new ItemColorIdentifier((int)Materials.Essence, 0x0486, "Essence of Precision"),
                    new ItemColorIdentifier((int)Materials.Essence, 0x0486, "Essence of Direction"),
                    new ItemColorIdentifier((int)Materials.Essence, 0x048d, "Essence of Control"),
                    new ItemColorIdentifier((int)Materials.Essence, 0x06bc, "Essence of Achievement"),
                    new ItemColorIdentifier((int)Materials.Essence, 0x0489, "Essence of Passion"),
                    new ItemColorIdentifier((int)Materials.Essence, 0x0481, "Essence of Order")
                };
                return new LootRule
                {
                    RuleName = "Imbue Materials",
                    ItemColorIds = Enum.GetValues(typeof(Materials)).Cast<Materials>().Where(e => e != Materials.Essence).Select(g => new ItemColorIdentifier((int)g,0,Handler.SplitCamelCase(g.ToString()))).Union(essancesMatch).ToList(),
                    MaxWeight = 100
                };
            }
        }
            
        
        public static LootRule ReagentsMagery =>
            new LootRule
            {
                RuleName = "Reagents Magery",
                ItemColorIds = Enum.GetValues(typeof(ReagentsMagery)).Cast<ReagentsMagery>().Select(g => new ItemColorIdentifier((int)g,0,Handler.SplitCamelCase(g.ToString()))).ToList(),
                MaxWeight = 100
            };
        
        public static LootRule ReagentsNecromancy =>
            new LootRule
            {
                RuleName = "Reagents Necromancy",
                ItemColorIds = Enum.GetValues(typeof(ReagentsNecro)).Cast<ReagentsNecro>().Select(g => new ItemColorIdentifier((int)g, 0, Handler.SplitCamelCase(g.ToString()))).ToList(),
                MaxWeight = 100
            };
        
        public static LootRule ReagentsMysticism =>
            new LootRule
            {
                RuleName = "Reagents Mysticism",
                ItemColorIds = Enum.GetValues(typeof(ReagentsMysticism)).Cast<ReagentsMysticism>().Select(g => new ItemColorIdentifier((int)g, 0, Handler.SplitCamelCase(g.ToString()))).ToList(),
                MaxWeight = 100
            };
        
        public static LootRule ReagentsAll =>
            new LootRule
            {
                RuleName = "Reagents",
                ItemColorIds = Enum.GetValues(typeof(ReagentsMagery)).Cast<ReagentsMagery>().Select(g => new ItemColorIdentifier((int)g, 0, Handler.SplitCamelCase(g.ToString())))
                    .Union(Enum.GetValues(typeof(ReagentsNecro)).Cast<ReagentsNecro>().Select(g => new ItemColorIdentifier((int)g, 0, Handler.SplitCamelCase(g.ToString())))
                    .Union(Enum.GetValues(typeof(ReagentsMysticism)).Cast<ReagentsMysticism>().Select(g => new ItemColorIdentifier((int)g, 0, Handler.SplitCamelCase(g.ToString())))))
                    .ToList(),
                MaxWeight = 100
            };


        public static LootRule Ammo =>
            new LootRule
            {
                RuleName = "Bolts and Arrows",
                ItemColorIds = new List<ItemColorIdentifier>
                {
                    new ItemColorIdentifier(3903,0, "Bolt"),
                    new ItemColorIdentifier(7163,0, "Arrow")
                } //Arrows and Bolts
            };

        public static LootRule PureColdWeapon =>
            new LootRule
            {
                RuleName = "Pure Cold Weapon",
                Properties = new List<PropertyMatch>
                {
                    new PropertyMatch
                    {
                        Property = ItemProperty.ColdDamage,
                        Value = 100
                    }
                }
            };

        public static LootRule PureFireWeapon =>
            new LootRule
            {
                RuleName = "Pure Fire Weapon",
                Properties = new List<PropertyMatch>
                {
                    new PropertyMatch
                    {
                        Property = ItemProperty.FireDamage,
                        Value = 100
                    }
                }
            };

        public static LootRule PurePoisonWeapon =>
            new LootRule
            {
                RuleName = "Pure Poison Weapon",
                Properties = new List<PropertyMatch>
                {
                    new PropertyMatch
                    {
                        Property = ItemProperty.PoisonDamage,
                        Value = 100
                    }
                }
            };
        
        public static LootRule Slayers =>
        new LootRule
        {
            RuleName = "Slayers",
            Properties = new List<PropertyMatch>
            {
                new PropertyMatch
                {
                    Property = ItemProperty.AnySlayer
                }
            }
        };
        
        public static LootRule PureElementalWeapons =>
            new LootRule
            {
                RuleName = "Pure Elemental Weapons",
                Properties = new List<PropertyMatch>
                {
                    new PropertyMatch
                    {
                        Property = ItemProperty.AnyElement,
                        Value = 100
                    }
                }
            };
        

        public static LootRule PureEnergyWeapon =>
            new LootRule
            {
                RuleName = "Pure Energy Weapon",
                Properties = new List<PropertyMatch>
                {
                    new PropertyMatch
                    {
                        Property = ItemProperty.EnergyDamage,
                        Value = 100
                    }
                }
            };
        
        public LootRule(string ruleName, string itemName, bool alert = false)
        {
            RuleName = ruleName;
            ItemNames = new List<string> { itemName };
            Alert = alert;
            TargetBag = null;
            Id = Guid.NewGuid();
        }

        public LootRule()
        {
            TargetBag = null;
            Id = Guid.NewGuid();
        }

        public bool Match(Item item)
        {
            var match = CheckItemIdOrName(item);
            Handler.SendMessage(MessageType.Debug,$"Check ItemId / ItemName : {match}");
            match = match && CheckBlackListProperties(item);
            
            match = match && CheckWeightCursed(item);
            Handler.SendMessage(MessageType.Debug,$"Check WeightCursed : {match}");

            match = match && CheckRarityProps(item);
            Handler.SendMessage(MessageType.Debug,$"Check RarityProps : {match}");

            match = match && CheckEquipmentSlot(item);
            Handler.SendMessage(MessageType.Debug,$"Check EquipmentSlot : {match}");

            match = match && CheckSpecialProps(item);
            Handler.SendMessage(MessageType.Debug,$"Check SpecialProps : {match}");

            match = match && CheckRegEx(item);
            Handler.SendMessage(MessageType.Debug,$"CheckRegex : {match}");
                
            return match;

        }

        private bool CheckName(Item item)
        {
            var nameList = ItemNames ?? new List<string>();
            

            if (!nameList.Any())
            {
                return true;
            }

            foreach (var itemName in nameList)
            {
                if (item.Name.ToLower().Contains(itemName.ToLower()))
                {
                    return true;
                }
            }

            return false;
        }

        private bool CheckEquipmentSlot(Item item)
        {
            if (EquipmentSlots == null || !EquipmentSlots.Any())
            {
                return true;
            }

            var matchFound = false;
            
            if (EquipmentSlots.Contains(EquipmentSlot.Armour))
            {
                var tempList = Enum.GetValues(typeof(EquipmentSlot)).Cast<EquipmentSlot>()
                    .Where(x =>
                        x != EquipmentSlot.Armour &&
                        x != EquipmentSlot.Jewellery &&
                        x != EquipmentSlot.RightHand &&
                        x != EquipmentSlot.LeftHand &&
                        x != EquipmentSlot.Ring &&
                        x != EquipmentSlot.Bracelet 
                    ).ToList();
                matchFound = tempList.Select(x => x.ToString()).Any(s => s.ToString().Equals(item.Layer, StringComparison.OrdinalIgnoreCase));
            }
            if (EquipmentSlots.Contains(EquipmentSlot.Jewellery))
            {
                matchFound = matchFound ||
                             item.Layer.Equals(EquipmentSlot.Ring.ToString(), StringComparison.OrdinalIgnoreCase) ||
                             item.Layer.Equals(EquipmentSlot.Bracelet.ToString(), StringComparison.OrdinalIgnoreCase) ||
                             item.Layer.Equals(EquipmentSlot.Earrings.ToString(), StringComparison.OrdinalIgnoreCase);
            }

            return matchFound || EquipmentSlots.Any(s => s.ToString().Equals(item.Layer, StringComparison.OrdinalIgnoreCase));
        }

        private bool CheckItemIdOrName(Item item)
        {
            if ((ItemColorIds == null || !ItemColorIds.Any()) && (ItemNames == null || !ItemNames.Any()))
            {
                return true;
            }
            
            if (ItemColorIds == null || !ItemColorIds.Any())
            {
                return CheckName(item);
            }
            
            if (ItemNames == null || !ItemNames.Any())
            {
                return CheckItemId(item);
            }
            
            return CheckItemId(item) || CheckName(item);
        }
        
        
        private bool CheckItemId(Item item)
        {
            if (ItemColorIds == null || !ItemColorIds.Any())
            {
                return true;
            }
            
            return ItemColorIds.Any(i => i.ItemId == item.ItemID && (i.Color == null || i.Color == item.Hue));
        }

        private bool CheckBlackListProperties(Item item)
        {
            if (BlackListedProperties == null)
            {
                return true;
            }

            var re = new Regex(@"\d+");

            foreach (var ruleProp in BlackListedProperties)
            {
                foreach (var prop in item.Properties)
                {
                    var stringVal = prop.ToString().Replace("%", "");
                    var reMatch = re.Match(stringVal);
                    var numIndex = reMatch.Success ? reMatch.Index : stringVal.Length;
                    var propString = Handler.ResolvePropertyName(ruleProp.Property);
                    if (propString.Equals(stringVal.Substring(0, numIndex).Trim(), StringComparison.InvariantCultureIgnoreCase))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private bool CheckWeightCursed(Item item)
        {
            if (MaxWeight != null)
            {
                return item.Weight <= MaxWeight;
            }

            return true;
        }

        private bool CheckRarityProps(Item item)
        {
            if (MinimumRarity == null && MaximumRarity == null)
            {
                return true;
            }
            Property rarityProp = null;
            if (Lootmaster.IsOSI)
            {
                rarityProp = item.Properties.FirstOrDefault(p => p.ToString().ToLower().Contains("magic item") || p.ToString().ToLower().Contains("artifact"));
            }
            else
            {
                rarityProp = item.Properties.FirstOrDefault(p => p.Number == 1042971);
            }
            

            if (rarityProp == null)
            {
                return false;
            }


            var checkRarities = Enum.GetValues(typeof(ItemRarity)).Cast<ItemRarity>().ToList();
            
            if(MinimumRarity != null)
            {
                checkRarities = checkRarities.Where(r => (int)r >= (int)MinimumRarity).ToList();
            }

            if (MaximumRarity != null)
            {
                checkRarities = checkRarities.Where(r => (int)r <= (int)MaximumRarity).ToList();
            }

            var matched = false;
            
            var rarityString = "";
            string cleaned = String.Empty;
            if (Lootmaster.IsOSI)
            {
                cleaned = rarityProp.ToString().Substring(rarityProp.Args.IndexOf(">", StringComparison.Ordinal) + 1).Replace(" ", "");
            }
            else
            {
                cleaned = rarityProp.Args.Substring(rarityProp.Args.IndexOf(">", StringComparison.Ordinal) + 1).Replace(" ", "");
            }
            
            var length = cleaned.IndexOf("<", StringComparison.Ordinal);
            if (length == -1)
            {
                rarityString = cleaned.Substring(0);
            }
            else
            {
                rarityString = cleaned.Substring(0, cleaned.IndexOf("<", StringComparison.Ordinal));
            }
            
            foreach (var rarity in checkRarities)
            {
                matched = rarityString.Equals(rarity.ToString(), StringComparison.OrdinalIgnoreCase);
                if (matched)
                {
                    break;
                }
            }

            return matched;
        }

        private bool CheckSpecialProps(Item item)
        {
            if (Properties == null || !Properties.Any())
            {
                return true;
            }

            var re = new Regex(@"\d+");

            var ruleDict = Properties.ToDictionary(p => p.Property, p => false);

            foreach (var ruleProp in Properties)
            {
                if ((int)ruleProp.Property >= 1000)
                {
                    Handler.SendMessage(MessageType.Debug, "Special Any Property");
                    var checkProps = new List<string>();
                    //Special Case for the Any Property
                    switch (ruleProp.Property)
                    {
                        case ItemProperty.AnyElement:
                            checkProps = new List<string>
                            {
                                Handler.ResolvePropertyName(ItemProperty.PoisonDamage),
                                Handler.ResolvePropertyName(ItemProperty.ColdDamage),
                                Handler.ResolvePropertyName(ItemProperty.EnergyDamage),
                                Handler.ResolvePropertyName(ItemProperty.FireDamage)
                            };
                            
                            break;
                        case ItemProperty.AnySkill:
                            var props = Enum.GetValues(typeof(ItemProperty)).Cast<ItemProperty>();
                            checkProps.AddRange(props.Where(x => (int)x >= 100 && (int)x <= 199).Select(x => Handler.ResolvePropertyName(x)));
                            break;
                        case ItemProperty.AnySlayer:
                            if (item.Properties.Any(prop => prop.ToString().ToLower().Contains("slayer")) || item.Properties.Any(prop => prop.ToString().ToLower() == "silver"))
                            {
                                ruleDict[ruleProp.Property] = true;
                            }
                            break;
                        case ItemProperty.AnyStat:
                            checkProps = new List<string>
                            {
                                Handler.ResolvePropertyName(ItemProperty.BonusStr),
                                Handler.ResolvePropertyName(ItemProperty.BonusDex),
                                Handler.ResolvePropertyName(ItemProperty.BonusInt),
                            };
                            break;
                        case ItemProperty.AnyEater:
                            checkProps = new List<string>
                            {
                                Handler.ResolvePropertyName(ItemProperty.FireEater),
                                Handler.ResolvePropertyName(ItemProperty.ColdEater),
                                Handler.ResolvePropertyName(ItemProperty.EnergyEater),
                                Handler.ResolvePropertyName(ItemProperty.PoisonEater),
                                Handler.ResolvePropertyName(ItemProperty.KineticEater),
                                Handler.ResolvePropertyName(ItemProperty.DamageEater),
                            };
                            break;
                    }

                    if (ruleDict[ruleProp.Property])
                    {
                        continue;
                    }
                    
                    foreach (var prop in item.Properties)
                    {
                        if (prop.ToString().Equals(item.Name, StringComparison.InvariantCultureIgnoreCase))
                        {
                            continue;
                        }
                        var stringVal = prop.ToString().Replace("%", "").Replace("+","");
                        var reMatch = re.Match(stringVal);
                        var numIndex = reMatch.Success ? reMatch.Index : stringVal.Length;
                        
                        if (checkProps.Any(propString => propString.Equals(stringVal.Substring(0, numIndex).Trim(), StringComparison.InvariantCultureIgnoreCase)))
                        {
                            var numstring = prop.Args;
                            int.TryParse(numstring, out var parseValue);
                            if (ruleProp.Value == null || parseValue >= ruleProp.Value || !reMatch.Success)
                            {
                                ruleDict[ruleProp.Property] = true;
                                break;
                            }
                        }
                    }
                }
                else
                {
                    foreach (var prop in item.Properties)
                    {
                        if (prop.ToString().Equals(item.Name, StringComparison.InvariantCultureIgnoreCase))
                        {
                            continue;
                        }
                        
                        var stringVal = prop.ToString().Replace("%", "").Replace("+","");
                        var reMatch = re.Match(stringVal);
                        var numIndex = reMatch.Success ? reMatch.Index : stringVal.Length;
                        var propString = Handler.ResolvePropertyName(ruleProp.Property);
                        if (propString.Equals(stringVal.Substring(0, numIndex).Trim(), StringComparison.InvariantCultureIgnoreCase))
                        {
                            var numstring = prop.Args.Split('\t').Last();
                            int.TryParse(numstring, out var parseValue);
                            if (ruleProp.Value == null || parseValue >= ruleProp.Value || !reMatch.Success)
                            {
                                ruleDict[ruleProp.Property] = true;
                                break;
                            }
                        }
                    }
                }
            }
            foreach (var keyValuePair in ruleDict)
            {
                Handler.SendMessage(MessageType.Debug, $"{keyValuePair.Key} = {keyValuePair.Value}");
            }

            //atleast PropertyMatchRequirement properties must match if PropertyMatchRequirement is null assume All must match
            return PropertyMatchRequirement == null ? ruleDict.All(p => p.Value) : ruleDict.Count(p => p.Value) >= PropertyMatchRequirement;
        }

        private bool CheckRegEx(Item item)
        {
            if(string.IsNullOrEmpty(RegExString))
            {
                return true;
            }

            var regEx = new Regex(RegExString, RegexOptions.IgnoreCase);
            
            List<string> stringList = new List<string>();
            
            stringList.Add(item.Name);
            foreach (var prop in item.Properties)
            {
                stringList.Add(prop.ToString());
            }
            
            return stringList.Any(s => regEx.Match(s).Success);
            
        }
    }


    public class LootMasterConfig
    {
        public string Version { get; set; }
        public int BaseDelay { get; set; } = 200;
        public List<LootMasterCharacter> Characters { get; set; }
        public bool ColorCorpses { get; set; }
        public int? ColorCorpsesColor { get; set; }
        
        public List<ItemColorIdentifier> ItemColorLookup { get; set; }
        public Dictionary<int, string> ItemLookup { get; set; }


        public LootMasterConfig()
        {
            Characters = new List<LootMasterCharacter>();
            ColorCorpses = true;
            ItemColorLookup = new List<ItemColorIdentifier>();
            ItemLookup = new Dictionary<int, string>();
        }
        
        public void Init(string version)
        {
            Version = version;
            ReadConfig(version);
        }

        public void Save()
        {
            WriteConfig();
        }
        public LootMasterCharacter GetCharacter(string characterName = null)
        {
            var checkname = characterName ?? Player.Name;
            var character = Characters.FirstOrDefault(c => c.PlayerName.Equals(checkname, StringComparison.OrdinalIgnoreCase));
            if (character == null)
            {
                Handler.SendMessage(MessageType.Log, $"Creating new character config for {checkname}");
                Characters.Add(new LootMasterCharacter()
                {
                    PlayerName = Player.Name
                });
                
                character = Characters.FirstOrDefault(c => c.PlayerName.Equals(checkname, StringComparison.OrdinalIgnoreCase));
            }

            return character;
        }

        public void CreateRule(LootRule rule)
        {
            var character = GetCharacter();
            if (character.Rules.Any(r => r.RuleName.Equals(rule.RuleName, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }
            character.Rules.Add(rule);
        }
        
        private void ReadConfig(string version)
        {
            try
            {

                var file = Path.Combine(Engine.RootPath, "Lootmaster.config");
                if (!File.Exists(file))
                {
                    return;
                }

                var data = File.ReadAllText(file);
                var ns = Assembly.LoadFile(Path.Combine(Engine.RootPath, "Newtonsoft.Json.dll"));
                foreach (Type type in ns.GetExportedTypes())
                {
                    if (type.Name == "JsonConvert")
                    {
                        var funcs = type
                            .GetMethods(BindingFlags.InvokeMethod | BindingFlags.Static | BindingFlags.Public)
                            .Where(f => f.Name == "DeserializeObject" && f.IsGenericMethodDefinition);
                        var func = funcs
                            .FirstOrDefault(f =>
                                f.Name == "DeserializeObject" && f.GetParameters().Length == 1 &&
                                f.GetParameters()[0].ParameterType == typeof(string))
                            .MakeGenericMethod(typeof(LootMasterConfig));
                        var readConfig =
                            func.Invoke(type, BindingFlags.InvokeMethod, null, new object[] { data }, null) as
                                LootMasterConfig;

                        if (!Handler.ValidateVersion(readConfig?.Version, version))
                        {
                            Handler.SendMessage(MessageType.Critical,
                                "Lootmaster config is newer than script, please update");
                            return;
                        }


                        Characters = new List<LootMasterCharacter>();
                        foreach (var rcc in readConfig?.Characters ?? new List<LootMasterCharacter>())
                        {
                            Characters.Add(new LootMasterCharacter
                            {
                                PlayerName = rcc.PlayerName,
                                Rules = rcc.Rules.Select(r => new LootRule
                                {
                                    Id = (r.Id == default ? Guid.NewGuid() : r.Id),
                                    RuleName = r.RuleName,
                                    ItemColorIds = r.ItemColorIds ??
                                                   r.ItemIds?.Select(i => new ItemColorIdentifier(i, 0, string.Empty))
                                                       .ToList() ?? new List<ItemColorIdentifier>(),
                                    ItemNames = r.ItemNames ?? new List<string>(),
                                    Properties = r.Properties ?? new List<PropertyMatch>(),
                                    EquipmentSlots = r.EquipmentSlots ?? new List<EquipmentSlot>(),
                                    Alert = r.Alert,
                                    MinimumRarity = r.MinimumRarity,
                                    MaximumRarity = r.MaximumRarity,
                                    TargetBag = r.TargetBag,
                                    BlackListedProperties = r.BlackListedProperties ?? new List<PropertyMatch>(),
                                    MaxWeight = r.MaxWeight ?? (r.IgnoreWeightCurse ? (int?)null : 49),
                                    Disabled = r.Disabled,
                                    PropertyMatchRequirement = r.PropertyMatchRequirement,
                                    RegExString = string.IsNullOrEmpty(r.RegExString) ? null : r.RegExString
                                }).ToList()
                            });
                        }

                        ColorCorpses = readConfig?.ColorCorpses ?? true;
                        ColorCorpsesColor = readConfig?.ColorCorpsesColor ?? 0x3F6;
                        
                        BaseDelay = readConfig?.BaseDelay ?? 200;

                        ItemLookup = readConfig?.ItemLookup ?? new Dictionary<int, string>();
                        ItemColorLookup = readConfig?.ItemColorLookup ?? new List<ItemColorIdentifier>();

                        if (ItemColorLookup == null)
                        {
                            ItemColorLookup = new List<ItemColorIdentifier>();
                            foreach (var il in ItemLookup)
                            {
                                ItemColorLookup.AddUnique(new ItemColorIdentifier(il.Key, null, il.Value));
                            }
                        }

                        break;
                    }
                }
            }
            catch (LootMasterException)
            {
                throw;
            }
            catch(Exception ex)
            {
                Misc.SendMessage(ex);
                throw;
            }

        }
        
        
        
        private void WriteConfig()
        {
            try
            {
                var file = Path.Combine(Engine.RootPath, "Lootmaster.config");
                var ns = Assembly.LoadFile(Path.Combine(Engine.RootPath, "Newtonsoft.Json.dll"));
                string data = "";
                foreach(Type type in ns.GetExportedTypes())
                {
                    if (type.Name == "JsonConvert")
                    {
                        data = type.InvokeMember("SerializeObject", BindingFlags.InvokeMethod, null, null, new object[] { this }) as string;
                    }
                    
                }
                
                var ms = File.Create(file);
                var writer = new StreamWriter(ms);
                writer.Write(data);
                writer.Flush();
                ms.Close();
                writer.Close();
            }
            catch
            {
                // ignored
            }
        }
    }

    public class LootMasterCharacter
    {
        public string PlayerName { get; set; }
        
        public List<LootRule> Rules { get; set; }

        public LootMasterCharacter()
        {
            Rules = new List<LootRule>();
        }
    }

    public class LootMasterBagConfig
    {
        public string RuleName { get; set; }
        public int BagSerial { get; set; }
    }

    internal enum MessageType
    {
        Prompt = 0,
        Log = 1,
        Error = 2,
        Info = 3,
        Debug = 4,
        Critical = 5,
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

    internal enum ReagentsMagery
    {
        BlackPearl = 3962,      // 0x0F7A
        BloodMoss = 3963,       // 0x0F7B
        Garlic = 3972,          // 0x0F84
        Ginseng = 3973,         // 0x0F85
        MandrakeRoot = 3974,    // 0x0F86
        Nightshade = 3976,      // 0x0F88
        SpiderSilk = 3560,      // 0x0DF8
        SulfurousAsh = 3980     // 0x0F8C
    }

    internal enum ReagentsNecro
    {
        BatWing = 3960,        // 0x0F78
        GraveDust = 3981,      // 0x0F8D
        DaemonBlood = 3965,    // 0x0F7D
        NoxCrystal = 3982,     // 0x0F8E
        PigIron = 3983         // 0x0F8F
    }
    
    internal enum ReagentsMysticism
    {
        DragonBlood = 16503,    // 0x4077
        Bone = 3966,           // 0x0F7E
        DaemonBone = 3968,     // 0x0F80
        FertileDirt = 3969     // 0x0F81
    }

    internal enum Materials
    {
        ArcanicRuneStone = 22332, // 0x573C
        BlueDiamond = 12696,      // 0x3198
        BottleOfIchor = 22344,    // 0x5748
        BouraPelt = 22338,        // 03x5742
        ChagaMushroom = 22339,    // 0x5743
        CrushedGlass = 22331,     // 0x573B
        CrystalShards = 22328,    // 0x5738
        CrystallineBlackrock = 22322, // 0x5732
        DaemonClaw = 22049,       // 0x5721
        DelicateScales = 22330,   // 0x573A
        ElvenFletching = 22327,   // 0x5737
        Essence = 22300,          // 0x571C
        FaeryDust = 22341,        // 0x5745
        FeyWings = 22310,         // 0x5726
        FireRuby = 12695,         // 0x3197
        GoblinBlood = 22316,      // 0x572C
        LavaSerpentCrust = 22317, // 0x572D
        LuminescentFungi = 12689, // 0x3191
        ParasiticPlant = 12688,   // 0x3190
        RaptorTeeth = 22343,      // 0x5747
        ReflectiveWolfEye = 22345, // 0x5749
        SeedOfRenewal = 22326,    // 0x5736
        SilverSnakeSkin = 22340,  // 0x5744
        SlithTongue = 22342,      // 0x5746
        SpiderCarapace = 22304,   // 0x5720
        Turquoise = 12691,        // 0x3193
        UndyingFlesh = 22321,     // 0x5731
        VialOfVitriol = 22306,    // 0x5722
        VoidOrb = 22334,          // 0x573E
        WhitePearl = 12694        // 0x3196
    }
    public class PropertyMatch
    {
        public ItemProperty Property { get; set; }
        public int? Value { get; set; }

        public string DisplayName => Handler.ResolvePropertyName(Property);
    }
    
    public class ItemColorIdentifier
    {
        public override string ToString()
        {
            return $"{ItemId}|{Color}";
        }

        public ItemColorIdentifier(int itemId, int? color, string name)
        {
            ItemId = itemId;
            Color = color;
            Name = name;
        }
           
        public int ItemId { get; set; }
        public int? Color { get; set; }
        public string Name { get; set; }
    }

    public enum ItemProperty
    {
        SpellDamageIncrease = 0,
        EnhancePotions = 1,
        FasterCastRecovery = 2,
        FasterCasting = 3,
        LowerManaCost = 4,
        LowerReagentCost = 5,
        NightSight = 6,
        ReactiveParalyze = 7,
        ReactiveFireball = 8,
        ReactiveCurse = 9,
        ReactiveLightning = 10,
        ReactiveManaDrain = 11,
        HitLowerAttack = 12,
        HitLowerDefense = 13,
        ReflectPhysicalDamage = 14,
        EnhanceDamage = 15,
        EnhanceDefense = 16,
        BonusStr = 17,
        BonusDex = 18,
        BonusInt = 19,
        HitsIncrease = 20,
        StamIncrease = 21,
        ManaIncrease = 22,
        SpellChanneling = 23,
        DamageIncrease = 24,
        Luck = 25,
        SwingSpeedIncrease = 26,
        HitChanceIncrease = 27,
        DefenseChanceIncrease = 28,
        ManaRegeneration = 29,
        StaminaRegeneration = 30,
        HitPointRegeneration = 31,
        PhysicalDamage = 32,
        ColdDamage = 33,
        FireDamage = 34,
        PoisonDamage = 35,
        EnergyDamage = 36,
        CastingFocus = 37,
        HitMagicArrow = 38,
        HitHarm = 39,
        HitFireball = 40,
        HitLightning = 41,
        HitDispel = 42,
        HitCurse = 43,
        HitLifeLeech = 44,
        HitManaLeech = 45,
        HitStaminaLeech = 46,
        HitColdArea = 47,
        HitFireArea = 48,
        HitPoisonArea = 49,
        HitEnergyArea = 50,
        HitPhysicalArea = 51,
        PhysicalResist = 52,
        ColdResist = 53,
        FireResist = 54,
        PoisonResist = 55,
        EnergyResist = 56,
        ElvesOnly = 57,
        GargoylesOnly = 58,
        
        //Slayers
        DemonSlayer = 200,
        UndeadSlayer = 201,
        ReptileSlayer = 202,
        RepondSlayer = 203,
        ElementalSlayer = 204,
        FeySlayer = 205,
        ArachnidSlayer = 206,
        Silver = 207,
        
        //Eaters
        FireEater = 90,
        ColdEater = 91,
        PoisonEater = 92,
        EnergyEater = 93,
        KineticEater = 94,
        DamageEater = 95,

        
        //Skills
        Alchemy = 100,
        Anatomy = 101,
        AnimalLore = 102,
        ItemID = 103,
        ArmsLore = 104,
        Parry = 105,
        Begging = 106,
        Blacksmith = 107,
        Fletching = 108,
        Peacemaking = 109,
        Camping = 110,
        Carpentry = 111,
        Cartography = 112,
        Cooking = 113,
        DetectHidden = 114,
        Discordance = 115,
        EvalInt = 116,
        Healing = 117,
        Fishing = 118,
        ForensicEvaluation = 119,
        Herding = 120,
        Hiding = 121,
        Provocation = 122,
        Inscription = 123,
        Lockpicking = 124,
        Magery = 125,
        MagicResist = 126,
        Tactics = 127,
        Snooping = 128,
        Musicianship = 129,
        Poisoning = 130,
        Archery = 131,
        SpiritSpeak = 132,
        Stealing = 133,
        Tailoring = 134,
        AnimalTaming = 135,
        TasteID = 136,
        Tinkering = 137,
        Tracking = 138,
        Veterinary = 139,
        Swords = 140,
        Macing = 141,
        Fencing = 142,
        Wrestling = 143,
        Lumberjacking = 144,
        Mining = 145,
        Meditation = 146,
        Stealth = 147,
        RemoveTrap = 148,
        Necromancy = 149,
        Focus = 150,
        Chivalry = 151,
        Bushido = 152,
        Ninjitsu = 153,
        SpellWeaving = 154,
        Mysticism = 155,
        Imbuing = 156,
        Throwing = 157,
        
        AnySkill = 1000,
        AnySlayer = 1001,
        AnyElement = 1002,
        AnyStat = 1003,
        AnyEater = 1004,
        
        //Negatives
        Cursed = 2000,
        Antique = 2001,
        Prized = 2002,
        Brittle = 2003,

    }

    public enum EquipmentSlot : byte
    {
        RightHand = 0x01,
        LeftHand = 0x02,
        Shoes = 0x03,
        Pants = 0x04,
        Shirt = 0x05,
        Head = 0x06,
        Gloves = 0x07,
        Ring = 0x08,
        Talisman = 0x09,
        Neck = 0x0A,
        Waist = 0x0C,
        InnerTorso = 0x0D,
        Bracelet = 0x0E,
        MiddleTorso = 0x11,
        Earrings = 0x12,
        Arms = 0x13,
        Cloak = 0x14,
        OuterTorso = 0x16,
        OuterLegs = 0x17,
        InnerLegs = 0x18,
        Armour = 0x19,
        Jewellery = 0x1A,
    }

    public enum ItemRarity
    {
        MinorMagicItem = 0,
        LesserMagicItem = 1,
        GreaterMagicItem = 2,
        MajorMagicItem = 3,
        MinorArtifact = 4,
        LesserArtifact = 5,
        GreaterArtifact = 6,
        MajorArtifact = 7,
        LegendaryArtifact = 8
    }

    public static class Handler
    {
        
        public static string ResolvePropertyName(ItemProperty prop)
        {
            switch (prop)
            {
                case ItemProperty.SpellDamageIncrease:
                    return "Spell Damage Increase";
                case ItemProperty.EnhancePotions:
                    return "Enhance Potions";
                case ItemProperty.FasterCastRecovery:
                    return "Faster Cast Recovery";
                case ItemProperty.FasterCasting:
                    return "Faster Casting";
                case ItemProperty.LowerManaCost:
                    return "Lower Mana Cost";
                case ItemProperty.LowerReagentCost:
                    return "Lower Reagent Cost";
                case ItemProperty.NightSight:
                    return "Night Sight";
                case ItemProperty.ReactiveParalyze:
                    return "Reactive Paralyze";
                case ItemProperty.ReactiveFireball:
                    return "Reactive Fireball";
                case ItemProperty.ReactiveCurse:
                    return "Reactive Curse";
                case ItemProperty.ReactiveLightning:
                    return "Reactive Lightning";
                case ItemProperty.ReactiveManaDrain:
                    return "Reactive Mana Drain";
                case ItemProperty.HitLowerAttack:
                    return "Hit Lower Attack";
                case ItemProperty.HitLowerDefense:
                    return "Hit Lower Defense";
                case ItemProperty.ReflectPhysicalDamage:
                    return "Reflect Physical Damage";
                case ItemProperty.EnhanceDamage:
                    return "Enhance Damage";
                case ItemProperty.EnhanceDefense:
                    return "Enhance Defense";
                case ItemProperty.BonusStr:
                    return "Strength Bonus";
                case ItemProperty.BonusDex:
                    return "Dexterity Bonus";
                case ItemProperty.BonusInt:
                    return "Intelligence Bonus";
                case ItemProperty.HitsIncrease:
                    return "Hit Point Increase";
                case ItemProperty.StamIncrease:
                    return "Stamina Increase";
                case ItemProperty.ManaIncrease:
                    return "Mana Increase";
                case ItemProperty.SpellChanneling:
                    return "Spell Channeling";
                case ItemProperty.DamageIncrease:
                    return "Damage Increase";
                case ItemProperty.Luck:
                    return "Luck";
                case ItemProperty.SwingSpeedIncrease:
                    return "Swing Speed Increase";
                case ItemProperty.HitChanceIncrease:
                    return "Hit Chance Increase";
                case ItemProperty.DefenseChanceIncrease:
                    return "Defense Chance Increase";
                case ItemProperty.HitPointRegeneration:
                    return "Hit Point Regeneration";
                case ItemProperty.StaminaRegeneration:
                    return "Stamina Regeneration";
                case ItemProperty.ManaRegeneration:
                    return "Mana Regeneration";
                case ItemProperty.PhysicalDamage:
                    return "Physical Damage";
                case ItemProperty.ColdDamage:
                    return "Cold Damage";
                case ItemProperty.FireDamage:
                    return "Fire Damage";
                case ItemProperty.PoisonDamage:
                    return "Poison Damage";
                case ItemProperty.EnergyDamage:
                    return "Energy Damage";
                case ItemProperty.CastingFocus:
                    return "Casting Focus";
                case ItemProperty.HitColdArea:
                    return "Hit Cold Area";
                case ItemProperty.HitFireArea:
                    return "Hit Fire Area";
                case ItemProperty.HitEnergyArea:
                    return "Hit Energy Area";
                case ItemProperty.HitPhysicalArea:
                    return "Hit Physical Area";
                case ItemProperty.HitPoisonArea:
                    return "Hit Poison Area";
                case ItemProperty.HitMagicArrow:
                    return "Hit Magic Arrow";
                case ItemProperty.HitDispel:
                    return "Hit Dispel";
                case ItemProperty.HitFireball:
                    return "Hit Fireball";
                case ItemProperty.HitHarm:
                    return "Hit Harm";
                case ItemProperty.HitCurse:
                    return "Hit Curse";
                case ItemProperty.HitLightning:
                    return "Hit Lightning";
                case ItemProperty.HitLifeLeech:
                    return "Hit Life Leech";
                case ItemProperty.HitManaLeech:
                    return "Hit Mana Leech";
                case ItemProperty.HitStaminaLeech:
                    return "Hit Stamina Leech";
                case ItemProperty.PhysicalResist:
                    return "Physical Resist";
                case ItemProperty.ColdResist:
                    return "Cold Resist";
                case ItemProperty.FireResist:
                    return "Fire Resist";
                case ItemProperty.PoisonResist:
                    return "Poison Resist";
                case ItemProperty.EnergyResist:
                    return "Energy Resist";
                case ItemProperty.ElvesOnly:
                    return "Elves Only";
                case ItemProperty.GargoylesOnly:
                    return "Gargoyles Only";

                //Eaters
                case ItemProperty.FireEater:
                    return "Fire Eater";
                case ItemProperty.ColdEater:
                    return "Cold Eater";
                case ItemProperty.PoisonEater:
                    return "Poison Eater";
                case ItemProperty.EnergyEater:
                    return "Energy Eater";
                case ItemProperty.KineticEater:
                    return "Kinetic Eater";
                case ItemProperty.DamageEater:
                    return "Damage Eater";
                
                
                //Slayers
                case ItemProperty.DemonSlayer:
                    return "Demon Slayer";
                case ItemProperty.UndeadSlayer:
                    return "Undead Slayer";
                case ItemProperty.RepondSlayer:
                    return "Repond Slayer";
                case ItemProperty.ElementalSlayer:
                    return "Elemental Slayer";
                case ItemProperty.FeySlayer:
                    return "Fey Slayer";
                case ItemProperty.ReptileSlayer:
                    return "Reptile Slayer";
                case ItemProperty.ArachnidSlayer:
                    return "Arachnid Slayer";
                case ItemProperty.Silver:
                    return "Silver";
                
                //Anys
                case ItemProperty.AnySlayer:
                    return "Any Slayer";
                case ItemProperty.AnyElement:
                    return "Any Element";
                case ItemProperty.AnySkill:
                    return "Any Skill";
                case ItemProperty.AnyStat:
                    return "Any Stat";
                case ItemProperty.AnyEater:
                    return "Any Eater";
                
                //Map all enum values between 100 and 999 (skills)
                case ItemProperty.Alchemy:
                    return "Alchemy";
                case ItemProperty.Anatomy:
                    return "Anatomy";
                case ItemProperty.AnimalLore:
                    return "Animal Lore";
                case ItemProperty.ItemID:
                    return "Item Identification";
                case ItemProperty.ArmsLore:
                    return "Arms Lore";
                case ItemProperty.Parry:
                    return "Parry";
                case ItemProperty.Begging:
                    return "Begging";
                case ItemProperty.Blacksmith:
                    return "Blacksmithing";
                case ItemProperty.Fletching:
                    return "Bowcraft/Fletching";
                case ItemProperty.Peacemaking:
                    return "Peacemaking";
                case ItemProperty.Camping:
                    return "Camping";
                case ItemProperty.Carpentry:
                    return "Carpentry";
                case ItemProperty.Cartography:
                    return "Cartography";
                case ItemProperty.Cooking:
                    return "Cooking";
                case ItemProperty.DetectHidden:
                    return "Detect Hidden";
                case ItemProperty.Discordance:
                    return "Discordance";
                case ItemProperty.EvalInt:
                    return "Evaluating Intelligence";
                case ItemProperty.Healing:
                    return "Healing";
                case ItemProperty.Fishing:
                    return "Fishing";
                case ItemProperty.ForensicEvaluation:
                    return "Forensic Evaluation";
                case ItemProperty.Herding:
                    return "Herding";
                case ItemProperty.Hiding:
                    return "Hiding";
                case ItemProperty.Provocation:
                    return "Provocation";
                case ItemProperty.Inscription:
                    return "Inscription";
                case ItemProperty.Lockpicking:
                    return "Lockpicking";
                case ItemProperty.Magery:
                    return "Magery";
                case ItemProperty.MagicResist:
                    return "Magic Resist";
                case ItemProperty.Tactics:
                    return "Tactics";
                case ItemProperty.Snooping:
                    return "Snooping";
                case ItemProperty.Musicianship:
                    return "Musicianship";
                case ItemProperty.Poisoning:
                    return "Poisoning";
                case ItemProperty.Archery:
                    return "Archery";
                case ItemProperty.SpiritSpeak:
                    return "Spirit Speak";
                case ItemProperty.Stealing:
                    return "Stealing";
                case ItemProperty.Tailoring:
                    return "Tailoring";
                case ItemProperty.AnimalTaming:
                    return "Animal Taming";
                case ItemProperty.TasteID:
                    return "Taste Identification";
                case ItemProperty.Tinkering:
                    return "Tinkering";
                case ItemProperty.Tracking:
                    return "Tracking";
                case ItemProperty.Veterinary:
                    return "Veterinary";
                case ItemProperty.Swords:
                    return "Swordsmanship";
                case ItemProperty.Macing:
                    return "Mace Fighting";
                case ItemProperty.Fencing:
                    return "Fencing";
                case ItemProperty.Wrestling:
                    return "Wrestling";
                case ItemProperty.Lumberjacking:
                    return "Lumberjacking";
                case ItemProperty.Mining:
                    return "Mining";
                case ItemProperty.Meditation:
                    return "Meditation";
                case ItemProperty.Stealth:
                    return "Stealth";
                case ItemProperty.RemoveTrap:
                    return "Remove Trap";
                case ItemProperty.Necromancy:
                    return "Necromancy";
                case ItemProperty.Focus:
                    return "Focus";
                case ItemProperty.Chivalry:
                    return "Chivalry";
                case ItemProperty.Bushido:
                    return "Bushido";
                case ItemProperty.Ninjitsu:
                    return "Ninjitsu";
                case ItemProperty.SpellWeaving:
                    return "Spellweaving";
                case ItemProperty.Imbuing:
                    return "Imbuing";
                case ItemProperty.Mysticism:
                    return "Mysticism";
                case ItemProperty.Throwing:
                    return "Throwing";
                
                //Negatives
                case ItemProperty.Cursed:
                    return "Cursed";
                case ItemProperty.Antique:
                    return "Antique";
                case ItemProperty.Prized:
                    return "Prized";
                case ItemProperty.Brittle:
                    return "Brittle";
            }

            return null;
        }
        internal static void SendMessage(MessageType type, string message)
        {
            switch (type)
            {
                case MessageType.Prompt:
                    Player.HeadMessage(0x90, message);
                    break;
                case MessageType.Log:
                    Misc.SendMessage(message);
                    break;
                case MessageType.Error:
                    Misc.SendMessage(message, 33);
                    Player.HeadMessage(0x23, message);
                    break;
                case MessageType.Critical:
                    Misc.SendMessage(message, 33);
                    Player.HeadMessage(0x23, message);
                    throw new LootMasterException(message);
                    
                case MessageType.Info:
                    Misc.SendMessage(message, 0x99);
                    Player.HeadMessage(0x99, message);
                    break;
                case MessageType.Debug:
                    if (Lootmaster.Debug)
                    {
                        Misc.SendMessage(message);
                        var logFile = Path.Combine(Engine.RootPath, "Lootmaster.log");
                        File.AppendAllText(logFile, message + Environment.NewLine);
                    }

                    break;
            }
        }
        
        public static string SplitCamelCase(string str)
        {
            return Regex.Replace(
                Regex.Replace(
                    str,
                    @"(\P{Ll})(\P{Ll}\p{Ll})",
                    "$1 $2"
                ),
                @"(\p{Ll})(\P{Ll})",
                "$1 $2"
            );
        }

        public static bool ValidateVersion(string readConfigVersion, string scriptVersion)
        {
            if(string.IsNullOrEmpty(readConfigVersion))
            {
                //Read Config has no known version, the means it's either older or nonexisting
                return true;
            }
            
            var svStrip = scriptVersion.Replace("v", "");
            var rvStrip = readConfigVersion.Replace("v", "");
            //split version into parts
            var svParts = svStrip.Split('.');
            var rvParts = rvStrip.Split('.');
            //compare major version
            var isOk = true;
            if(int.TryParse(svParts[0], out var svMajor) && int.TryParse(rvParts[0], out var rvMajor))
            {
                if (svMajor < rvMajor)
                {
                    isOk = false;
                }
            }
            if(isOk && int.TryParse(svParts[1], out var svMinor) && int.TryParse(rvParts[1], out var rvMinor))
            {
                if (svMinor < rvMinor)
                {
                    isOk = false;
                }
            }
            if(isOk && int.TryParse(svParts[2], out var svRevision) && int.TryParse(rvParts[2], out var rvRevision))
            {
                if (svRevision < rvRevision)
                {
                    isOk = false;
                }
            }

            return isOk;
        }
    }

    internal class LootMasterException : Exception
    {
        public LootMasterException(string message) : base(message)
        {
        }
    }

    public class Configurator : Form
    {
        private Target _tar = new Target();
        private List<DropDownItem> Rarities = new List<DropDownItem>();
        private List<DropDownItem> EquipmentSlots = new List<DropDownItem>();
        private List<DropDownItem> Properties = new List<DropDownItem>();
        private LootMasterConfig Config;
        private LootRule ActiveRule { get; set; }
        
        public Configurator()
        {
            InitializeComponent();
        }
        
        private void addButton_Click(object sender, EventArgs e)
        {
            ClearConfig();
        }

        private void ClearConfig()
        {
            ActiveRule = null;
            foreach (Control rc in rulesList.Controls)
            {
                if (rc is RuleController ruleController)
                {
                    ruleController.SetActive(false);
                }
            }
            ruleNameTextBox.Text = string.Empty;
            rarityMinDropDown.SelectedIndex = 0;
            slotDropDown.SelectedIndex = 0;
            propertyDropDown.SelectedIndex = 0;
            presetDropDown.SelectedIndex = 0;
            itemNamesList.Controls.Clear();
            propertiesList.Controls.Clear();
            propertiesIgnoreList.Controls.Clear();
            equipmentSlotList.Controls.Clear();
            weightCurseTextBox.Text = string.Empty;
            enabledCheckbox.Checked = true;
            ruleDownButton.Enabled = false;
            ruleUpButton.Enabled = false;
            deleteButton.Enabled = false;
            clearTargetBagButton.Enabled = false;
            alertCheckbox.Checked = false;
            
        }
        
        private void colorCorpseCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Config.ColorCorpses = colorCorpseCheckbox.Checked;
            Config.Save();
        }

        private bool DetectChanges()
        {
            if (ActiveRule != null)
            {
                var nameList = new List<string>();
                var idList = new List<ItemColorIdentifier>();
                var slotList = new List<EquipmentSlot>();
                foreach (var val in itemNamesList.Controls)
                {
                    if (val is IdNameControl idcVal)
                    {
                        if (idcVal.IsIdColor)
                        {
                            idList.Add(idcVal.Get());
                        }
                        else
                        {
                            nameList.Add(idcVal.GetName());
                        }
                    }
                }
                
                foreach (var val in equipmentSlotList.Controls)
                {
                    if (val is EquipmentSlotControl eqc)
                    {
                        slotList.Add(eqc.Get());
                    }
                }

                var currentValues = new LootRule
                {
                    RuleName = ruleNameTextBox.Text,
                    EquipmentSlots = slotList,
                    MinimumRarity = rarityMinDropDown.SelectedIndex == 0 ? null : (ItemRarity?)(rarityMinDropDown.SelectedItem as DropDownItem).Value,
                    MaximumRarity = rarityMaxDropDown.SelectedIndex == 0 ? null : (ItemRarity?)(rarityMaxDropDown.SelectedItem as DropDownItem).Value,
                    ItemNames = nameList,
                    ItemColorIds = idList,
                    Properties = propertiesList.Controls.Cast<PropertyControl>().Select(ctr => ctr.Get()).ToList(),
                    BlackListedProperties = propertiesIgnoreList.Controls.Cast<PropertyControl>().Select(ctr => ctr.Get()).ToList(),
                    MaxWeight = weightCurseTextBox.Text == string.Empty ? (int?)null : int.Parse(weightCurseTextBox.Text),
                    Alert = alertCheckbox.Checked,
                    Disabled = !enabledCheckbox.Checked,
                    RegExString = string.IsNullOrEmpty(regExTextBox.Text.Trim()) ? null : regExTextBox.Text
                };
                
                
                // check if currentRule and originalRule differ on any property
                return currentValues.EquipmentSlots.Count != ActiveRule.EquipmentSlots.Count ||
                       currentValues.EquipmentSlots.Except(ActiveRule.EquipmentSlots).Any() ||
                       currentValues.MinimumRarity != ActiveRule.MinimumRarity ||
                       currentValues.MaximumRarity != ActiveRule.MaximumRarity ||
                       currentValues.ItemNames.Count != ActiveRule.ItemNames.Count ||
                       currentValues.ItemNames.Except(ActiveRule.ItemNames).Any() ||
                       currentValues.ItemColorIds.Count != ActiveRule.ItemColorIds.Count ||
                       currentValues.ItemColorIds.Select(l1 => l1.ToString()).Except(ActiveRule.ItemColorIds.Select(l2 => l2.ToString())).Any() ||
                       currentValues.Properties.Count != ActiveRule.Properties.Count ||
                       currentValues.Properties.Except(ActiveRule.Properties).Any() ||
                       currentValues.MaxWeight != ActiveRule.MaxWeight ||
                       currentValues.Alert != ActiveRule.Alert ||
                       currentValues.Disabled != ActiveRule.Disabled ||
                       currentValues.RegExString != ActiveRule.RegExString;
            }

            return false;
        }

        private bool DetectChangeAndPromptSave()
        {
            if (DetectChanges())
            {
                var result = MessageBox.Show("Save changes to rule?", "Save Changes", MessageBoxButtons.YesNoCancel);
                switch (result)
                {
                    case DialogResult.Yes:
                        SaveRule();
                        return true;
                    case DialogResult.Cancel:
                        return false;
                }
            }

            return true;
        }


        private void LoadRule(LootRule rule)
        {
            ActiveRule = rule;
            slotDropDown.SelectedIndex = 0;
            ruleNameTextBox.Text = rule.RuleName;
            rarityMinDropDown.SelectedIndex = rule.MinimumRarity == null ? 0 : Rarities.IndexOf(Rarities.First(r => r.Name == rule.MinimumRarity.ToString()));
            rarityMaxDropDown.SelectedIndex = rule.MaximumRarity == null ? 0 : Rarities.IndexOf(Rarities.First(r => r.Name == rule.MaximumRarity.ToString()));
            
            equipmentSlotList.Controls.Clear();

            if (rule.EquipmentSlots != null)
            {
                equipmentSlotList.SuspendLayout();
                foreach (var slot in rule.EquipmentSlots)
                {
                    equipmentSlotList.Controls.Add(new EquipmentSlotControl(slot,DeleteRuleData));
                }
                equipmentSlotList.ResumeLayout();
            }
            
            itemNamesList.Controls.Clear();
            regExTextBox.Text = rule.RegExString;
            
            if (rule.ItemColorIds != null)
            {
                itemNamesList.SuspendLayout();
                foreach (var itemId in rule.ItemColorIds)
                {
                    Item item = null;
                    if (string.IsNullOrEmpty(itemId.Name))
                    {
                        if (string.IsNullOrEmpty(Config.ItemColorLookup.GetNameFromItem(itemId)))
                        {
                            item = Items.FindByID(itemId.ItemId, itemId.Color ?? -1, -1, false, false);

                            if (item != null)
                            {
                                Config.ItemColorLookup.AddUnique(new ItemColorIdentifier(item.ItemID, item.Hue,
                                    item.Name.Replace(item.Amount.ToString(), string.Empty).Trim()));
                            }
                        }
                        var lookupName =  Config.ItemColorLookup.GetNameFromSet(itemId.ItemId, item == null ? itemId.Color ?? -1 : item.Hue);
                        itemNamesList.Controls.Add(new IdNameControl(new ItemColorIdentifier(itemId.ItemId, itemId.Color, lookupName), DeleteRuleData, EditRuleData));
                    }
                    else
                    {
                        itemNamesList.Controls.Add(new IdNameControl(itemId, DeleteRuleData,EditRuleData));
                    }
                }
                itemNamesList.ResumeLayout();
            }
            
            var nameList = rule.ItemNames ?? new List<string>();
            if (rule.ItemNames != null)
            {
                foreach (var name in rule.ItemNames)
                {
                    itemNamesList.Controls.Add(new IdNameControl(name, DeleteRuleData,EditRuleData));
                }
            }

            propertiesList.Controls.Clear();
            propertiesIgnoreList.Controls.Clear();

            if (rule.Properties != null)
            {
                foreach (var pm in rule.Properties)
                {
                    propertiesList.Controls.Add(new PropertyControl(pm, DeleteRuleData, EditRuleData));
                }
            }
            
            if (rule.BlackListedProperties != null)
            {
                foreach (var pm in rule.BlackListedProperties)
                {
                    propertiesIgnoreList.Controls.Add(new PropertyControl(pm, DeleteRuleData, EditRuleData, true));
                }
            }
            
            weightCurseTextBox.Text = rule.MaxWeight?.ToString() ?? string.Empty;
            alertCheckbox.Checked = rule.Alert;
            enabledCheckbox.Checked = !rule.Disabled;
            minimumMatchPropsTextBox.Text = rule.PropertyMatchRequirement?.ToString() ?? string.Empty;
            
            ruleDownButton.Enabled = true;
            ruleUpButton.Enabled = true;
            deleteButton.Enabled = true;
            clearTargetBagButton.Enabled = rule.TargetBag != null;
        }

        private void DeleteRuleData(Guid id, Type type)
        {
            var typeName = type.Name;
            switch (typeName)
            {
                case "ItemColorIdentifier" :
                case "String":
                    var iciCtr = itemNamesList.Controls.Cast<IdNameControl>().FirstOrDefault(c => c.UniqueId == id);
                    if (iciCtr != null)
                    {
                        itemNamesList.Controls.RemoveAt(itemNamesList.Controls.Cast<IdNameControl>().ToList().IndexOf(iciCtr));
                    }

                    break;
                case "EquipmentSlot":
                    var eqsCtr = equipmentSlotList.Controls.Cast<EquipmentSlotControl>().FirstOrDefault(c => c.UniqueId == id);
                    if (eqsCtr != null)
                    {
                        equipmentSlotList.Controls.RemoveAt(equipmentSlotList.Controls.Cast<EquipmentSlotControl>().ToList().IndexOf(eqsCtr));
                    }
                    break;
                case "PropertyMatch":
                    var pmCtr = propertiesList.Controls.Cast<PropertyControl>().FirstOrDefault(c => c.UniqueId == id)
                        ?? propertiesIgnoreList.Controls.Cast<PropertyControl>().FirstOrDefault(c => c.UniqueId == id);
                    if (pmCtr != null)
                    {
                        if (pmCtr.IsIgnoreProperty)
                        {
                            propertiesIgnoreList.Controls.RemoveAt(propertiesIgnoreList.Controls.Cast<PropertyControl>().ToList().IndexOf(pmCtr));
                        }
                        else
                        {
                            propertiesList.Controls.RemoveAt(propertiesList.Controls.Cast<PropertyControl>().ToList().IndexOf(pmCtr));
                        }
                    }
                    break;
            }
        }
        
        private void EditRuleData(Guid id, object data)
        {
            EditRuleItem editForm = null;
            DialogResult result;
            if (data is string dataString)
            {
                editForm = new EditRuleItem(dataString);
                result = editForm.ShowDialog();
                if (result == DialogResult.OK)
                {
                    var ctr = itemNamesList.Controls.Cast<IdNameControl>().FirstOrDefault(c => c.UniqueId == id);
                    if (ctr != null)
                    {
                        var index = itemNamesList.Controls.IndexOf(ctr);
                        var newData = editForm.GetResponse<string>();
                        itemNamesList.SuspendLayout();
                        itemNamesList.Controls.RemoveAt(index);
                        var newControl = new IdNameControl(newData, DeleteRuleData, EditRuleData);
                        itemNamesList.Controls.Add(newControl);
                        itemNamesList.Controls.SetChildIndex(newControl, index);
                        itemNamesList.ResumeLayout();
                    }
                }
                
            }
            else if (data is ItemColorIdentifier dataId)
            {
                editForm = new EditRuleItem(dataId);
                result = editForm.ShowDialog();
                if(result == DialogResult.OK)
                {
                    var ctr = itemNamesList.Controls.Cast<IdNameControl>().FirstOrDefault(c => c.UniqueId == id);
                    if (ctr != null)
                    {
                        var index = itemNamesList.Controls.IndexOf(ctr);
                        var newData = editForm.GetResponse<ItemColorIdentifier>();
                        itemNamesList.SuspendLayout();
                        itemNamesList.Controls.RemoveAt(index);
                        var newControl = new IdNameControl(newData, DeleteRuleData, EditRuleData);
                        itemNamesList.Controls.Add(newControl);
                        itemNamesList.Controls.SetChildIndex(newControl, index);
                        itemNamesList.ResumeLayout();
                    }
                    
                }
                
            }
            else if (data is PropertyMatch dataProp)
            {
                editForm = new EditRuleItem(dataProp);
                result = editForm.ShowDialog();
                if(result == DialogResult.OK)
                {
                    var ctr = propertiesList.Controls.Cast<PropertyControl>().FirstOrDefault(c => c.UniqueId == id);
                    if (ctr != null)
                    {
                        var index = propertiesList.Controls.IndexOf(ctr);
                        var newData = editForm.GetResponse<PropertyMatch>();
                        propertiesList.SuspendLayout();
                        propertiesList.Controls.RemoveAt(index);
                        var newControl = new PropertyControl(newData, DeleteRuleData, EditRuleData);
                        propertiesList.Controls.Add(newControl);
                        propertiesList.Controls.SetChildIndex(newControl, index);
                        propertiesList.ResumeLayout();
                    }
                    
                }
            }
            
            editForm?.Dispose();
        }

        private void addSlotButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (!equipmentSlotList.Controls.Cast<EquipmentSlotControl>().Any(s => s.GetName() == (slotDropDown.SelectedItem as DropDownItem).Name))
                {
                    equipmentSlotList.Controls.Add(new EquipmentSlotControl((slotDropDown.SelectedItem as DropDownItem).Name,  DeleteRuleData));
                }
            }
            catch
            {
                // ignored
            }
        }

        private void idAddButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(itemIdAddTextBox.Text))
                {
                    if (int.TryParse(itemIdAddTextBox.Text, out var intVal))
                    {
                        if (string.IsNullOrEmpty(Config.ItemColorLookup.GetNameFromSet(intVal,0)))
                        {
                            var item = Items.FindByID(intVal, 0, -1, false, false);
                            if (item != null)
                            {
                                Config.ItemColorLookup.AddUnique(new ItemColorIdentifier(intVal,0, item.Name.Replace(item.Amount.ToString(), string.Empty).Trim()));
                            }
                        }

                        var lookupName = Config.ItemColorLookup.GetNameFromSet(intVal, 0);
                        
                        itemNamesList.Controls.Add(new IdNameControl(new ItemColorIdentifier(intVal, null, lookupName), DeleteRuleData,EditRuleData));
                    }
                    else
                    {
                        try
                        {
                            intVal = Convert.ToInt32(itemIdAddTextBox.Text , 16);
                            if (string.IsNullOrEmpty(Config.ItemColorLookup.GetNameFromSet(intVal,0)))
                            {
                                var item = Items.FindByID(intVal, 0, -1, false, false);
                                if (item != null)
                                {
                                    Config.ItemColorLookup.AddUnique(new ItemColorIdentifier(intVal,0, item.Name.Replace(item.Amount.ToString(), string.Empty).Trim()));
                                }
                            }

                            var lookupName = Config.ItemColorLookup.GetNameFromSet(intVal,0);
                        
                            itemNamesList.Controls.Add(new IdNameControl(new ItemColorIdentifier(intVal,null, lookupName), DeleteRuleData,EditRuleData));
                        }
                        catch
                        {
                            itemNamesList.Controls.Add(new IdNameControl(itemIdAddTextBox.Text, DeleteRuleData,EditRuleData));
                        }
                        
                    }
                    
                    itemIdAddTextBox.Text = string.Empty;
                }
                else
                {
                    var tarSerial = _tar.PromptTarget("Pick Item for ID");
                    var item = Items.FindBySerial(tarSerial);
                    if (item != null)
                    {
                        if (string.IsNullOrEmpty(Config.ItemColorLookup.GetNameFromSet(item.ItemID, item.Hue)))
                        {
                            Config.ItemColorLookup.AddUnique(new ItemColorIdentifier(item.ItemID, item.Hue, item.Name.Replace(item.Amount.ToString(), string.Empty).Trim()));
                        }

                        var lookupName = Config.ItemColorLookup.GetNameFromSet(item.ItemID, item.Hue);
                    
                        itemNamesList.Controls.Add(new IdNameControl(new ItemColorIdentifier(item.ItemID, item.Hue, item.Name.Replace(item.Amount.ToString(), string.Empty).Trim()), DeleteRuleData,EditRuleData));
                    }
                }
            }
            catch
            {
                // ignored
            }
        }

        private Guid SaveRule()
        {
            var nameList = new List<string>();
            var idList = new List<ItemColorIdentifier>();
            foreach (var val in itemNamesList.Controls)
            {
                if(val is IdNameControl idcVal)
                {
                    if (idcVal.IsIdColor)
                    {
                        idList.Add(idcVal.Get());
                    }
                    else
                    {
                        nameList.Add(idcVal.GetName());
                    }
                }
            }

            int? matchPropCount = null;

            if (int.TryParse(minimumMatchPropsTextBox.Text, out var minMatch))
            {
                matchPropCount = minMatch;
            }

            if (propertiesList.Controls.Count == 0)
            {
                matchPropCount = null;
            }

            var rule = new LootRule
            {
                RuleName = ruleNameTextBox.Text,
                EquipmentSlots = equipmentSlotList.Controls.Cast<EquipmentSlotControl>().Select(x => x.Get())
                    .ToList(),
                MinimumRarity = rarityMinDropDown.SelectedIndex == 0
                    ? null
                    : (ItemRarity?)(rarityMinDropDown.SelectedItem as DropDownItem).Value,
                MaximumRarity = rarityMaxDropDown.SelectedIndex == 0
                    ? null
                    : (ItemRarity?)(rarityMaxDropDown.SelectedItem as DropDownItem).Value,
                ItemNames = nameList,
                ItemColorIds = idList,
                Properties = propertiesList.Controls.Cast<PropertyControl>().Select(p => p.Get()).ToList(),
                MaxWeight = weightCurseTextBox.Text == string.Empty ? (int?)null : int.Parse(weightCurseTextBox.Text),
                Alert = alertCheckbox.Checked,
                Disabled = !enabledCheckbox.Checked,
                BlackListedProperties = propertiesIgnoreList.Controls.Cast<PropertyControl>().Select(p => p.Get()).ToList(),
                PropertyMatchRequirement = matchPropCount,
                RegExString = string.IsNullOrEmpty(regExTextBox.Text) ? null : regExTextBox.Text
            };

            if (string.IsNullOrEmpty(rule.RuleName))
            {
                MessageBox.Show("Please enter a rule name.");
                return Guid.Empty;
            }



            var blockSave = rule.EquipmentSlots.Count == 0
                            && rule.MinimumRarity == null
                            && rule.MaximumRarity == null
                            && rule.ItemNames.Count == 0
                            && rule.ItemColorIds.Count == 0
                            && rule.Properties.Count == 0
                            && string.IsNullOrEmpty(rule.RegExString);
            if (blockSave)
            {
                MessageBox.Show("This rule will match all items. Please adjust the rule to be more specific.");
                return Guid.Empty;
            }

            var ruleIndex = Config.GetCharacter().Rules.IndexOf(Config.GetCharacter().Rules.FirstOrDefault(x => x.Id == ActiveRule?.Id));
            
            if (ActiveRule != null && ruleIndex != -1)
            {
                ActiveRule.RuleName = rule.RuleName;
                ActiveRule.EquipmentSlots = equipmentSlotList.Controls.Cast<EquipmentSlotControl>()
                    .Select(x => x.Get()).ToList();
                ActiveRule.MinimumRarity = rarityMinDropDown.SelectedIndex == 0
                    ? null
                    : (ItemRarity?)(rarityMinDropDown.SelectedItem as DropDownItem).Value;
                ActiveRule.MaximumRarity = rarityMaxDropDown.SelectedIndex == 0
                    ? null
                    : (ItemRarity?)(rarityMaxDropDown.SelectedItem as DropDownItem).Value;
                ActiveRule.ItemNames = nameList;
                ActiveRule.ItemColorIds = idList;
                ActiveRule.Properties = propertiesList.Controls.Cast<PropertyControl>().Select(pm => pm.Get()).ToList();
                ActiveRule.MaxWeight = weightCurseTextBox.Text == string.Empty
                    ? (int?)null
                    : int.Parse(weightCurseTextBox.Text);
                ActiveRule.Alert = alertCheckbox.Checked;
                ActiveRule.Disabled = !enabledCheckbox.Checked;
                ActiveRule.BlackListedProperties = propertiesIgnoreList.Controls.Cast<PropertyControl>().Select(pm => pm.Get()).ToList();
                ActiveRule.PropertyMatchRequirement = matchPropCount;
                ActiveRule.RegExString = string.IsNullOrEmpty(regExTextBox.Text) ? null : regExTextBox.Text;
                
                //Replace rule with sameID with updated activeRule
                var existing = Config.GetCharacter().Rules;
                
                Config.GetCharacter().Rules.Remove(Config.GetCharacter().Rules.First(x => x.Id == ActiveRule.Id));
                Config.GetCharacter().Rules.Insert(ruleIndex,ActiveRule);
                
                foreach (var ctr in rulesList.Controls)
                {
                    if (ctr is RuleController ruleController)
                    {
                        ruleController.UpdateData();
                    }
                }
                
                return ActiveRule.Id;
            }

            rulesList.Controls.Add(new RuleController(rule, DeleteRule, MoveRule, SetActiveRule));
            Config.GetCharacter().Rules.Clear();
            foreach (var control in rulesList.Controls)
            {
                if (control is RuleController ruleItem)
                {
                    Config.GetCharacter().Rules.Add(ruleItem.GetRule());
                }
            }

            return rule.Id;
        }

        private void saveButton_Click(object sender, EventArgs e)
        {
            try
            {

                var newId = SaveRule();
                if (newId == Guid.Empty)
                {
                    return;
                }

                SetActiveRule(newId);
                
                Config.Save();
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void SetActiveRule(Guid newId)
        {
            if (DetectChangeAndPromptSave())
            {
                foreach (var control in rulesList.Controls)
                {
                    if (control is RuleController ruleController)
                    {
                        if (ruleController.RuleId == newId)
                        {
                            ActiveRule = ruleController.GetRule();
                            ruleController.SetActive(true);
                            LoadRule(ruleController.GetRule());
                        }
                        else
                        {
                            ruleController.SetActive(false);
                        }
                    }
                }
            }
        }

        internal class Hue
        {
            public string Name { get; set; }
            public int Value { get; set; }
        }
        
        

        private void presetDropDown_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (presetDropDown.SelectedIndex == 0)
            {
                return;
            }
            
            LootRule rule = presetDropDown.SelectedItem as LootRule;
            if (ActiveRule != null && ActiveRule.Id != Guid.NewGuid())
            {
                rule.Id = ActiveRule?.Id ?? Guid.NewGuid();
                rule.TargetBag = ActiveRule?.TargetBag;
                rulesList.Controls.Cast<RuleController>().ToList().FirstOrDefault(l => l.RuleId == rule.Id)?.SetRule(rule);
            }

            LoadRule(rule);
        }
        
        private void characterDropdown_SelectedIndexChanged(object sender, EventArgs e)
        {
            var name = characterDropdown.SelectedItem as string;
            if (!string.IsNullOrEmpty(name))
            {
                LoadRules(Config.GetCharacter(name).Rules);
            }
        }
        
        private void huePicker_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (huePicker.SelectedIndex != 0)
            {
                var hue = huePicker.SelectedItem as Hue;
                hueTextBox.Text = hue.Value.ToString();
                Config.ColorCorpsesColor = hue.Value;
                Config.Save();
            }
        }
        
        private void hueTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                SetHue();
            }
        }
        
        private void lootDelayTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                SetDelay();
            }
        }

        private void SetHue()
        {
            if (int.TryParse(hueTextBox.Text, out var hueVal))
            {
                Config.ColorCorpsesColor = hueVal;
                Config.Save();
            }
        }

        private void SetDelay()
        {
            if (int.TryParse(lootDelayTextBox.Text, out var delayVal))
            {
                Config.BaseDelay = delayVal;
                Config.Save();
            }
        }
        
        

        private void lootDelayTextBox_Leave(object sender, EventArgs e)
        {
            SetDelay();
        }

        private void hueTextBox_Leave(object sender, EventArgs e)
        {
            SetHue();
        }
 
        private void deleteButton_Click(object sender, EventArgs e)
        {
            if (rulesList.Controls.Count == 0)
            {
                return;
            }
            
            var controlscopy = rulesList.Controls.Cast<RuleController>().ToList();
            
            foreach (RuleController rulesListControl in controlscopy)
            {
                if(rulesListControl.IsSelected)
                {
                    DeleteRule(rulesListControl.RuleId, true);
                }
            }

            if (rulesList.Controls.Count > 0)
            {
                var lc = rulesList.Controls[0] as RuleController;
                lc.SetActive(true);
                LoadRule(lc.GetRule());
            }
            else
            {
                ClearConfig();
            }
            
        }

        private void moveDownSelectedRuleMenuItem_Click(object sender, EventArgs e)
        {
            MoveRule(ActiveRule.Id, 1);
        }

        private void moveUpSelectedRuleMenuItem_Click(object sender, EventArgs e)
        {
            MoveRule(ActiveRule.Id, -1);
        }

        private void addPropIgnoreButton_Click(object sender, EventArgs e)
        {
            try
            {
                var selectedProp = propertyIgnoreDropDown.SelectedItem as DropDownItem;
                
                if (!propertiesIgnoreList.Controls.Cast<PropertyControl>().Any(s => s.GetName() == (propertyIgnoreDropDown.SelectedItem as DropDownItem).Name))
                {
                    var prop = new PropertyMatch
                    {
                        Property = (ItemProperty)selectedProp.Value,
                        Value = null
                    };
                        
                    propertiesIgnoreList.Controls.Add(new PropertyControl(prop, DeleteRuleData, EditRuleData, true));
                }
                
                propertyIgnoreDropDown.SelectedIndex = 0;

            }
            catch
            {
                // ignored
            }
        }
        
        
        private void addPropButton_Click(object sender, EventArgs e)
        {
            try
            {
                var selectedProp = propertyDropDown.SelectedItem as DropDownItem;
                int? value = null;
                if (int.TryParse(propertyValueTextBox.Text, out var tmpValue))
                {
                    value = tmpValue;
                }
                
                if (!propertiesList.Controls.Cast<PropertyControl>().Any(s => s.GetName() == (propertyDropDown.SelectedItem as DropDownItem).Name))
                {
                    var prop = new PropertyMatch
                    {
                        Property = (ItemProperty)selectedProp.Value,
                        Value = value
                    };
                        
                    propertiesList.Controls.Add(new PropertyControl(prop, DeleteRuleData, EditRuleData));
                }
                
                propertyDropDown.SelectedIndex = 0;
                propertyValueTextBox.Text = string.Empty;

            }
            catch
            {
                // ignored
            }
        }
        
        private void clearTargetBagButton_Click(object sender, EventArgs e)
        {
            Config.GetCharacter().Rules.Clear();
            foreach (RuleController rulesListControl in rulesList.Controls)
            {
                if(rulesListControl.IsSelected)
                {
                    rulesListControl.ClearTargetBag();
                }
                Config.GetCharacter().Rules.Add(rulesListControl.GetRule());
            }
            Config.Save();
        }

        private void exportCharacterButton_Click(object sender, EventArgs e)
        {
            var name = characterDropdown.SelectedItem as string;
            if (!string.IsNullOrEmpty(name))
            {
                var character = Config.GetCharacter(name);
                var ns = Assembly.LoadFile(Path.Combine(Engine.RootPath, "Newtonsoft.Json.dll"));
                string data = "";
                foreach(Type type in ns.GetExportedTypes())
                {
                    if (type.Name == "JsonConvert")
                    {
                        data = type.InvokeMember("SerializeObject", BindingFlags.InvokeMethod, null, null, new object[] { character }) as string;
                    }
                    
                }
                var plainTextBytes = Encoding.UTF8.GetBytes(data);
                var enc = Convert.ToBase64String(plainTextBytes);
                var impexp = new ImpExp();
                impexp.textField.ReadOnly = true;
                impexp.importButton.Enabled = false;
                impexp.textField.Text = enc;
                impexp.ShowDialog();
            }
        }
        
        private void importCharacterButton_Click(object sender, EventArgs e)
        {
            var name = characterDropdown.SelectedItem as string;
            if (!string.IsNullOrEmpty(name))
            {
                
                var impexp = new ImpExp();
                impexp.textField.ReadOnly = false;
                impexp.importButton.Enabled = true;
                impexp.ShowDialog();

                if (impexp.DecodedCharacter != null)
                {
                    Misc.SendMessage("Replacing Character");
                    impexp.DecodedCharacter.PlayerName = name;
                    //Delete the selected character
                    Config.Characters.Remove(Config.Characters.First(c => c.PlayerName == name));
                    Config.Characters.Add(impexp.DecodedCharacter);
                    Config.Save();
                    LoadRules(Config.GetCharacter(name).Rules);
                }
            }
        }
        
        private void LoadRules(List<LootRule> rules)
        {
            rulesList.Controls.Clear();
            rulesList.Controls.AddRange(rules.Select(r => new RuleController(r,DeleteRule,MoveRule, SetActiveRule)).ToArray());
        }

        private void MoveRule(Guid ruleId, int step)
        {
            var name = characterDropdown.SelectedItem as string;
            var character = Config.GetCharacter(name);
            var existing = character.Rules.FirstOrDefault(r => r.Id == ruleId);
            if (existing != null)
            {
                var index = character.Rules.IndexOf(existing);
                if ((index + step) > 0 && (index + step) < character.Rules.Count)
                {
                    character.Rules.RemoveAt(index);
                    character.Rules.Insert(index + step, existing);
                    
                    rulesList.Controls.SetChildIndex(rulesList.Controls[index], index + step);
                }
            }
        }

        private void DeleteRule(Guid ruleId)
        {
            DeleteRule(ruleId,false);
        }
        private void DeleteRule(Guid ruleId, bool isGroupedDelete)
        {
            var character = Config.GetCharacter();
            var existing = character.Rules.FirstOrDefault(r => r.Id == ruleId);
            if (existing != null)
            {
                character.Rules.Remove(existing);
            }

            var deleteSelected = false;
            
            foreach (Control control in rulesList.Controls)
            {
                if (control is RuleController ruleController)
                {
                    if (ruleController.RuleId == ruleId)
                    {
                        rulesList.Controls.Remove(control);
                        deleteSelected = ruleId == ActiveRule?.Id;
                        break;
                    }
                }
            }

            if (!isGroupedDelete)
            {
                if (deleteSelected)
                {
                    if (rulesList.Controls.Count > 0)
                    {
                        var lc = rulesList.Controls[0] as RuleController;
                        lc.SetActive(true);
                        LoadRule(lc.GetRule());
                    }
                    else
                    {
                        ClearConfig();
                    }
                }
            }

            Config.Save();
        }

        
        public void Open(LootMasterConfig config)
        {
            
            Config = config;
            
            characterDropdown.Items.Clear();
            characterDropdown.Items.AddRange(Config.Characters.Select(c => c.PlayerName).OrderBy(n => n).ToArray());
            characterDropdown.SelectedIndex = characterDropdown.Items.IndexOf(Player.Name);
            
            LoadRules(Config.GetCharacter((string)characterDropdown.SelectedValue).Rules);
            colorCorpseCheckbox.Checked = Config.ColorCorpses;
            
            Rarities.Add(new DropDownItem
            {
                Name = "None",
                Value = 999
            });
            
            Rarities.AddRange(Enum.GetValues(typeof(ItemRarity)).Cast<ItemRarity>().Select(x => new DropDownItem
            {
                Name = x.ToString(),
                Value = (int)x
            }).ToArray());
            
            EquipmentSlots.AddRange(Enum.GetValues(typeof(EquipmentSlot)).Cast<EquipmentSlot>().Where(x => x == EquipmentSlot.Armour || x == EquipmentSlot.Jewellery).OrderBy(x => x.ToString()).Select(x => new DropDownItem
            {
                Name = x.ToString(),
                Value = (int)x
            }).ToArray());
            
            EquipmentSlots.AddRange(Enum.GetValues(typeof(EquipmentSlot)).Cast<EquipmentSlot>().Where(x => x != EquipmentSlot.Armour && x != EquipmentSlot.Jewellery).OrderBy(x => x.ToString()).Select(x => new DropDownItem
            {
                Name = x.ToString(),
                Value = (int)x
            }).ToArray());
            var props = Enum.GetValues(typeof(ItemProperty)).Cast<ItemProperty>();
            
            //Add All Option
            Properties.AddRange(props.Where(x => (int)x >= 1000 && (int)x < 1100).OrderBy(x => x.ToString()).Select(x => new DropDownItem
            {
                Name = Handler.ResolvePropertyName(x),
                Value = (int)x
            }).ToArray());
            
            //Add Slayers
            Properties.AddRange(props.Where(x => (int)x >= 200 && (int)x <= 299).OrderBy(x => x.ToString()).Select(x => new DropDownItem
            {
                Name = Handler.ResolvePropertyName(x),
                Value = (int)x
            }).ToArray());
            
            //Add Damage Types
            Properties.AddRange(props.Where(x => (int)x >= 32 && (int)x <= 36).OrderBy(x => x.ToString()).Select(x => new DropDownItem
            {
                Name = Handler.ResolvePropertyName(x),
                Value = (int)x
            }).ToArray());
            
            //Add Raw Damage
            Properties.AddRange(props.Where(x => x == ItemProperty.DamageIncrease || x == ItemProperty.SwingSpeedIncrease || x == ItemProperty.HitChanceIncrease).OrderBy(x => x.ToString()).Select(x => new DropDownItem
            {
                Name = Handler.ResolvePropertyName(x),
                Value = (int)x
            }).ToArray());
            
            //Add Casting
            Properties.AddRange(props.Where(x => x == ItemProperty.SpellDamageIncrease
                                                 || x == ItemProperty.LowerManaCost
                                                 || x == ItemProperty.LowerReagentCost
                                                 || x == ItemProperty.FasterCasting
                                                 || x == ItemProperty.FasterCastRecovery
                                                 || x == ItemProperty.CastingFocus
                                                 ).OrderBy(x => x.ToString()).Select(x => new DropDownItem
            {
                Name = Handler.ResolvePropertyName(x),
                Value = (int)x
            }).ToArray());
            
            //Add Stats
            Properties.AddRange(props.Where(x => (int)x >= 17 && (int)x <= 22).OrderBy(x => x.ToString()).Select(x => new DropDownItem
            {
                Name = Handler.ResolvePropertyName(x),
                Value = (int)x
            }).ToArray());
            
            //Add Eaters
            
            Properties.AddRange(props.Where(x => (int)x >= 90 && (int)x <= 95).OrderBy(x => x.ToString()).Select(x => new DropDownItem
            {
                Name = Handler.ResolvePropertyName(x),
                Value = (int)x
            }).ToArray());
            
            
            //Add Properties
            Properties.AddRange(props.Where(x => (int)x < 100 && !Properties.Select(p => p.Name).Contains(Handler.ResolvePropertyName(x))).OrderBy(x => x.ToString()).Select(x => new DropDownItem
            {
                Name = Handler.ResolvePropertyName(x),
                Value = (int)x
            }).ToArray());
            
            //Add Skills
            Properties.AddRange(props.Where(x => (int)x >= 100 && (int)x < 1000).OrderBy(x => x.ToString()).Select(x => new DropDownItem
            {
                Name = Handler.ResolvePropertyName(x),
                Value = (int)x
            }).ToArray());
            
            //Add Negatives
            Properties.AddRange(props.Where(x => (int)x >= 2000).OrderBy(x => x.ToString()).Select(x => new DropDownItem
            {
                Name = Handler.ResolvePropertyName(x),
                Value = (int)x
            }).ToArray());
            
            
            var presets = new List<LootRule>
            {
                new LootRule
                {
                    RuleName = "Select a preset...",
                },
                LootRule.Gold,
                LootRule.Gems,
                LootRule.ImbueMaterials,
                LootRule.Ammo,
                LootRule.PureElementalWeapons,
                LootRule.PureColdWeapon,
                LootRule.PureFireWeapon,
                LootRule.PureEnergyWeapon,
                LootRule.PurePoisonWeapon,
                LootRule.Slayers,
                LootRule.ReagentsMagery,
                LootRule.ReagentsNecromancy,
                LootRule.ReagentsMysticism,
                LootRule.ReagentsAll,
                
            }.ToArray();

            presetDropDown.Items.Clear();
            presetDropDown.Items.AddRange(presets);
            
            
            rarityMinDropDown.Items.AddRange(Rarities.ToArray());
            rarityMaxDropDown.Items.AddRange(Rarities.ToArray());
            slotDropDown.Items.AddRange(EquipmentSlots.ToArray());
            propertyDropDown.Items.AddRange(Properties.ToArray());
            propertyIgnoreDropDown.Items.AddRange(Properties.ToArray());
            huePicker.Items.Clear();
            huePicker.Items.Add(new Hue
            {
                Name = "Custom",
            });
            huePicker.Items.Add(new Hue
            {
                Name = "Default Brown",
                Value = 0x3F6
            });
            huePicker.Items.Add(new Hue
            {
                Name = "Frostwood",
                Value = 0x047F
            });
            huePicker.Items.Add(new Hue
            {
                Name = "Glossy Blue",
                Value = 0x077C
            });
            huePicker.Items.Add(new Hue
            {
                Name = "Ocean Blue",
                Value = 0x04AB
            });
            huePicker.Items.Add(new Hue
            {
                Name = "Vivid Blue",
                Value = 0x0502
            });
            huePicker.Items.Add(new Hue
            {
                Name = "Dryad Green",
                Value = 0x048F
            });
            huePicker.Items.Add(new Hue
            {
                Name = "Mossy Green",
                Value = 0x0A7C
            });
            huePicker.Items.Add(new Hue
            {
                Name = "Heartwood",
                Value = 0x04A9
            });
            huePicker.Items.Add(new Hue
            {
                Name = "Ice Yellow",
                Value = 0x0038
            });
            huePicker.Items.Add(new Hue
            {
                Name = "Paragon Gold",
                Value = 0x0501
            });
            huePicker.Items.Add(new Hue
            {
                Name = "Rare Fire Red",
                Value = 0x054E
            });
            huePicker.Items.Add(new Hue
            {
                Name = "Crimson",
                Value = 0x00E8
            });
            huePicker.Items.Add(new Hue
            {
                Name = "Phoenix Red",
                Value = 0x07AC
            });
            huePicker.Items.Add(new Hue
            {
                Name = "Darkness",
                Value = 0x0497
            });
            
            
            hueTextBox.Text = Config.ColorCorpsesColor?.ToString() ?? String.Empty;
            lootDelayTextBox.Text = Config.BaseDelay.ToString();
            var foundColorMatch = huePicker.Items.Cast<Hue>().FirstOrDefault(h => h.Value == Config.ColorCorpsesColor);
            if(foundColorMatch != null)
            {
                huePicker.SelectedItem = foundColorMatch;
            }
            else
            {
                huePicker.SelectedIndex = 0;
            }
            
            rarityMinDropDown.SelectedIndex = 0;
            rarityMaxDropDown.SelectedIndex = 0;
            slotDropDown.SelectedIndex = 0;
            propertyDropDown.SelectedIndex = 0;
            presetDropDown.SelectedIndex = 0;
            
            enabledCheckbox.Checked = true;
            
            ShowDialog();
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            colorCorpseCheckbox = new CheckBox();
            characterDropdown = new ComboBox();
            exportCharacterButton = new Button();
            importCharacterButton = new Button();
            this.listContainer = new GroupBox();
            this.rulesList = new FlowLayoutPanel();
            this.ruleUpButton = new Button();
            this.ruleDownButton = new Button();
            this.addButton = new Button();
            this.deleteButton = new Button();
            this.clearTargetBagButton = new Button();
            this.saveButton = new Button();
            this.ruleContainer = new GroupBox();
            this.presetDropDown = new ComboBox();
            this.label1 = new Label();
            this.label2 = new Label();
            this.ruleNameTextBox = new TextBox();
            this.rarityMinDropDown = new ComboBox();
            this.rarityMaxDropDown = new ComboBox();
            this.label3 = new Label();
            this.slotDropDown = new ComboBox();
            this.alertCheckbox = new CheckBox();
            this.enabledCheckbox = new CheckBox();
            this.settingContainer = new GroupBox();
            this.itemNameContainer = new GroupBox();
            this.slotContainer = new GroupBox();
            this.propertiesContainer = new GroupBox();
            this.idAddButton = new Button();
            this.itemIdAddTextBox = new TextBox();
            this.itemNamesList = new FlowLayoutPanel();
            this.equipmentSlotList = new FlowLayoutPanel();
            this.propertiesList = new FlowLayoutPanel();
            this.propertyDropDown = new ComboBox();
            this.addPropButton = new Button();
            this.propertyValueTextBox = new TextBox();
            this.label5 = new Label();
            this.slotDropDownMenu = new ContextMenuStrip(this.components);
            this.ruleDropDownMenu = new ContextMenuStrip(this.components);
            slotDropDown = new ComboBox();
            slotAddButton = new Button();
            weightCurseTextBox = new TextBox();
            label6 = new Label();
            label7 = new Label();
            label9 = new Label();
            label10 = new Label();
            regExTextBox = new TextBox();
            minimumMatchPropsTextBox = new TextBox();
            propertiesIgnoreContainer = new GroupBox();
            addPropIgnoreButton = new Button();
            propertyIgnoreDropDown = new ComboBox();
            propertiesIgnoreList = new FlowLayoutPanel();
            label4 = new Label();
            hueTextBox = new TextBox();
            huePicker = new ComboBox();
            label8 = new Label();
            lootDelayTextBox = new TextBox();
            
            this.listContainer.SuspendLayout();
            this.ruleContainer.SuspendLayout();
            this.settingContainer.SuspendLayout();
            this.itemNameContainer.SuspendLayout();
            this.slotContainer.SuspendLayout();
            this.propertiesContainer.SuspendLayout();
            this.SuspendLayout();
            // 
            // colorCorpseCheckbox
            // 
            this.colorCorpseCheckbox.AutoSize = true;
            this.colorCorpseCheckbox.Location = new System.Drawing.Point(471, 12);
            this.colorCorpseCheckbox.Name = "colorCorpseCheckbox";
            this.colorCorpseCheckbox.Size = new System.Drawing.Size(149, 17);
            this.colorCorpseCheckbox.TabIndex = 0;
            this.colorCorpseCheckbox.Text = "Color Corpses after looting";// 
            this.colorCorpseCheckbox.CheckedChanged += this.colorCorpseCheckbox_CheckedChanged;
            //
            // label4
            // 
            this.label4.Location = new System.Drawing.Point(626, 12);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(35, 23);
            this.label4.TabIndex = 4;
            this.label4.Text = "Hue";
            // 
            // hueTextBox
            // 
            this.hueTextBox.Location = new System.Drawing.Point(659, 8);
            this.hueTextBox.Name = "hueTextBox";
            this.hueTextBox.Size = new System.Drawing.Size(56, 20);
            this.hueTextBox.TabIndex = 5;
            this.hueTextBox.KeyPress += this.hueTextBox_KeyPress;
            this.hueTextBox.Leave += this.hueTextBox_Leave;
            //
            // label10
            // 
            this.label10.Location = new System.Drawing.Point(360, 12);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(35, 23);
            this.label10.TabIndex = 4;
            this.label10.Text = "Wait";
            // 
            // lootDelayTextBox
            // 
            this.lootDelayTextBox.Location = new System.Drawing.Point(395,8);
            this.lootDelayTextBox.Name = "lootDelayTextBox";
            this.lootDelayTextBox.Size = new System.Drawing.Size(56, 20);
            this.lootDelayTextBox.TabIndex = 5;
            this.lootDelayTextBox.KeyPress += this.lootDelayTextBox_KeyPress;
            this.lootDelayTextBox.Leave += this.lootDelayTextBox_Leave;
            // 
            // huePicker
            // 
            this.huePicker.FormattingEnabled = true;
            this.huePicker.Location = new System.Drawing.Point(726, 8);
            this.huePicker.Name = "huePicker";
            this.huePicker.Size = new System.Drawing.Size(100, 21);
            this.huePicker.TabIndex = 6;
            this.huePicker.SelectedIndexChanged += this.huePicker_SelectedIndexChanged;
            this.huePicker.ValueMember = "Value";
            this.huePicker.DisplayMember = "Name";
            // 
            // characterDropdown
            // 
            this.characterDropdown.FormattingEnabled = true;
            this.characterDropdown.Location = new System.Drawing.Point(17, 8);
            this.characterDropdown.Name = "characterDropdown";
            this.characterDropdown.Size = new System.Drawing.Size(138, 21);
            this.characterDropdown.TabIndex = 0;
            this.characterDropdown.SelectedIndexChanged += new System.EventHandler(this.characterDropdown_SelectedIndexChanged);
            // 
            // exportCharacterButton
            // 
            this.exportCharacterButton.Location = new System.Drawing.Point(171, 8);
            this.exportCharacterButton.Name = "exportCharacterButton";
            this.exportCharacterButton.Size = new System.Drawing.Size(73, 22);
            this.exportCharacterButton.TabIndex = 0;
            this.exportCharacterButton.Text = "Export";
            this.exportCharacterButton.UseVisualStyleBackColor = true;
            this.exportCharacterButton.Click += new System.EventHandler(this.exportCharacterButton_Click);
            // 
            // importCharacterButton
            // 
            this.importCharacterButton.Location = new System.Drawing.Point(270, 8);
            this.importCharacterButton.Name = "importCharacterButton";
            this.importCharacterButton.Size = new System.Drawing.Size(73, 22);
            this.importCharacterButton.TabIndex = 0;
            this.importCharacterButton.Text = "Import";
            this.importCharacterButton.UseVisualStyleBackColor = true;
            this.importCharacterButton.Click += new System.EventHandler(this.importCharacterButton_Click);
            // 
            // listContainer
            // 
            this.listContainer.Controls.Add(this.rulesList);
            this.listContainer.Controls.Add(this.ruleUpButton);
            this.listContainer.Controls.Add(this.ruleDownButton);
            this.listContainer.Controls.Add(this.addButton);
            this.listContainer.Controls.Add(this.deleteButton);
            this.listContainer.Controls.Add(this.clearTargetBagButton);
            this.listContainer.Location = new System.Drawing.Point(10, 31);
            this.listContainer.Name = "listContainer";
            this.listContainer.Size = new System.Drawing.Size(170, 483);
            this.listContainer.TabIndex = 2;
            this.listContainer.TabStop = false;
            this.listContainer.Text = "Current Rules";
            // 
            // rulesList
            // 
            this.rulesList.ContextMenuStrip = this.ruleDropDownMenu;
            this.rulesList.Dock = System.Windows.Forms.DockStyle.Top;
            this.rulesList.Location = new System.Drawing.Point(3, 16);
            this.rulesList.Name = "rulesList";
            this.rulesList.Size = new System.Drawing.Size(166, 371);
            this.rulesList.TabIndex = 0;
            this.rulesList.VerticalScroll.Enabled = true;
            this.rulesList.VerticalScroll.Visible = true;
            this.rulesList.AutoScroll = true;
            // 
            // ruleUpButton
            // 
            this.ruleUpButton.Location = new System.Drawing.Point(5, 389);
            this.ruleUpButton.Name = "ruleUpButton";
            this.ruleUpButton.Size = new System.Drawing.Size(73, 22);
            this.ruleUpButton.TabIndex = 0;
            this.ruleUpButton.Text = "Move Up";
            this.ruleUpButton.UseVisualStyleBackColor = true;
            this.ruleUpButton.Click += this.moveUpSelectedRuleMenuItem_Click;
            // 
            // ruleDownButton
            // 
            this.ruleDownButton.Location = new System.Drawing.Point(5, 414);
            this.ruleDownButton.Name = "ruleDownButton";
            this.ruleDownButton.Size = new System.Drawing.Size(73, 22);
            this.ruleDownButton.TabIndex = 0;
            this.ruleDownButton.Text = "Move Down";
            this.ruleDownButton.UseVisualStyleBackColor = true;
            this.ruleDownButton.Click += this.moveDownSelectedRuleMenuItem_Click;
            // 
            // addButton
            // 
            this.addButton.Location = new System.Drawing.Point(80, 389);
            this.addButton.Name = "addButton";
            this.addButton.Size = new System.Drawing.Size(47, 22);
            this.addButton.TabIndex = 1;
            this.addButton.Text = "New";
            this.addButton.UseVisualStyleBackColor = true;
            this.addButton.Click += new System.EventHandler(this.addButton_Click);
            // 
            // deleteButton
            // 
            this.deleteButton.Location = new System.Drawing.Point(80, 415);
            this.deleteButton.Name = "deleteButton";
            this.deleteButton.Size = new System.Drawing.Size(47, 22);
            this.deleteButton.TabIndex = 1;
            this.deleteButton.Text = "Delete";
            this.deleteButton.UseVisualStyleBackColor = true;
            this.deleteButton.Click += new System.EventHandler(this.deleteButton_Click);
            // 
            // clearTargetBagButton
            // 
            this.clearTargetBagButton.Location = new System.Drawing.Point(5, 441);
            this.clearTargetBagButton.Name = "clearTargetBagButton";
            this.clearTargetBagButton.Size = new System.Drawing.Size(122, 22);
            this.clearTargetBagButton.TabIndex = 1;
            this.clearTargetBagButton.Text = "Clear Target Bags";
            this.clearTargetBagButton.UseVisualStyleBackColor = true;
            this.clearTargetBagButton.Click += this.clearTargetBagButton_Click;
            // 
            // saveButton
            // 
            this.saveButton.Location = new System.Drawing.Point(410, 15);
            this.saveButton.Name = "saveButton";
            this.saveButton.Size = new System.Drawing.Size(85, 23);
            this.saveButton.TabIndex = 1;
            this.saveButton.Text = "Save Rule";
            this.saveButton.UseVisualStyleBackColor = true;
            this.saveButton.Click += new System.EventHandler(this.saveButton_Click);
            // 
            // ruleContainer
            // 
            this.ruleContainer.Controls.Add(this.settingContainer);
            this.ruleContainer.Controls.Add(this.label1);
            this.ruleContainer.Controls.Add(this.presetDropDown);
            this.ruleContainer.Controls.Add(this.saveButton);
            this.ruleContainer.Location = new System.Drawing.Point(186, 31);
            this.ruleContainer.Name = "ruleContainer";
            this.ruleContainer.Size = new System.Drawing.Size(833, 483);
            this.ruleContainer.TabIndex = 3;
            this.ruleContainer.TabStop = false;
            this.ruleContainer.Text = "Rule Settings";
            // 
            // settingContainer
            // 
            this.settingContainer.Controls.Add(this.label6);
            this.settingContainer.Controls.Add(this.weightCurseTextBox);
            this.settingContainer.Controls.Add(this.propertiesIgnoreContainer);
            this.settingContainer.Controls.Add(this.enabledCheckbox);
            this.settingContainer.Controls.Add(this.alertCheckbox);
            this.settingContainer.Controls.Add(this.propertiesContainer);
            this.settingContainer.Controls.Add(this.slotContainer);
            this.settingContainer.Controls.Add(this.itemNameContainer);
            this.settingContainer.Controls.Add(this.ruleNameTextBox);
            this.settingContainer.Controls.Add(this.label2);
            this.settingContainer.Controls.Add(this.rarityMinDropDown);
            this.settingContainer.Controls.Add(this.label3);
            this.settingContainer.Controls.Add(this.label8);
            this.settingContainer.Controls.Add(this.rarityMaxDropDown);
            this.settingContainer.Controls.Add(this.label9);
            this.settingContainer.Controls.Add(this.regExTextBox);
            this.settingContainer.Location = new System.Drawing.Point(5, 42);
            this.settingContainer.Name = "settingContainer";
            this.settingContainer.Size = new System.Drawing.Size(823, 435);
            this.settingContainer.TabIndex = 10;
            this.settingContainer.TabStop = false;
            this.settingContainer.Text = "Settings";
            // 
            // label6
            // 
            this.label6.Location = new System.Drawing.Point(584, 23);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(69, 18);
            this.label6.TabIndex = 16;
            this.label6.Text = "Max Weight";
            // 
            // label9
            // 
            this.label9.Location = new System.Drawing.Point(379, 47);
            this.label9.Name = "label7";
            this.label9.Size = new System.Drawing.Size(40, 23);
            this.label9.TabIndex = 15;
            this.label9.Text = "Regex";
            // 
            // regExTextBox
            // 
            this.regExTextBox.Location = new System.Drawing.Point(420, 45);
            this.regExTextBox.Name = "regExTextBox";
            this.regExTextBox.Size = new System.Drawing.Size(344, 20);
            this.regExTextBox.TabIndex = 15;
            // 
            // weightCurseTextBox
            // 
            this.weightCurseTextBox.Location = new System.Drawing.Point(654, 18);
            this.weightCurseTextBox.Name = "weightCurseTextBox";
            this.weightCurseTextBox.Size = new System.Drawing.Size(31, 20);
            this.weightCurseTextBox.TabIndex = 15;
            // 
            // propertiesIgnoreContainer
            // 
            this.propertiesIgnoreContainer.Controls.Add(this.addPropIgnoreButton);
            this.propertiesIgnoreContainer.Controls.Add(this.propertyIgnoreDropDown);
            this.propertiesIgnoreContainer.Controls.Add(this.propertiesIgnoreList);
            this.propertiesIgnoreContainer.Location = new System.Drawing.Point(601, 69);
            this.propertiesIgnoreContainer.Name = "propertiesIgnoreContainer";
            this.propertiesIgnoreContainer.Size = new System.Drawing.Size(215, 357);
            this.propertiesIgnoreContainer.TabIndex = 14;
            this.propertiesIgnoreContainer.TabStop = false;
            this.propertiesIgnoreContainer.Text = "Ignore Properties";
            // 
            // addPropIgnoreButton
            // 
            this.addPropIgnoreButton.Location = new System.Drawing.Point(155, 18);
            this.addPropIgnoreButton.Name = "addPropIgnoreButton";
            this.addPropIgnoreButton.Size = new System.Drawing.Size(48, 22);
            this.addPropIgnoreButton.TabIndex = 3;
            this.addPropIgnoreButton.Text = "Add";
            this.addPropIgnoreButton.UseVisualStyleBackColor = true;
            this.addPropIgnoreButton.Click += this.addPropIgnoreButton_Click;
            // 
            // propertyIgnoreDropDown
            // 
            this.propertyIgnoreDropDown.DisplayMember = "Name";
            this.propertyIgnoreDropDown.FormattingEnabled = true;
            this.propertyIgnoreDropDown.Location = new System.Drawing.Point(5, 19);
            this.propertyIgnoreDropDown.Name = "propertyIgnoreDropDown";
            this.propertyIgnoreDropDown.Size = new System.Drawing.Size(145, 21);
            this.propertyIgnoreDropDown.TabIndex = 1;
            this.propertyIgnoreDropDown.ValueMember = "Value";
            // 
            // propertiesIgnoreList
            // 
            this.propertiesIgnoreList.Location = new System.Drawing.Point(6, 69);
            this.propertiesIgnoreList.Name = "propertiesIgnoreList";
            this.propertiesIgnoreList.Size = new System.Drawing.Size(205, 277);
            this.propertiesIgnoreList.TabIndex = 0;
            this.propertiesIgnoreList.VerticalScroll.Enabled = true;
            this.propertiesIgnoreList.VerticalScroll.Visible = true;
            this.propertiesIgnoreList.AutoScroll = true;
            // 
            // enabledCheckbox
            // 
            this.enabledCheckbox.AutoSize = true;
            this.enabledCheckbox.Location = new System.Drawing.Point(382, 21);
            this.enabledCheckbox.Name = "enabledCheckbox";
            this.enabledCheckbox.Size = new System.Drawing.Size(65, 17);
            this.enabledCheckbox.TabIndex = 13;
            this.enabledCheckbox.Text = "Enabled";
            this.enabledCheckbox.UseVisualStyleBackColor = true;
            // 
            // alertCheckbox
            // 
            this.alertCheckbox.AutoSize = true;
            this.alertCheckbox.Location = new System.Drawing.Point(452, 21);
            this.alertCheckbox.Name = "alertCheckbox";
            this.alertCheckbox.Size = new System.Drawing.Size(123, 17);
            this.alertCheckbox.TabIndex = 13;
            this.alertCheckbox.Text = "Notify When Looting";
            this.alertCheckbox.UseVisualStyleBackColor = true;
            // 
            // propertiesContainer
            // 
            this.propertiesContainer.Controls.Add(this.label7);
            this.propertiesContainer.Controls.Add(this.minimumMatchPropsTextBox);
            this.propertiesContainer.Controls.Add(this.label5);
            this.propertiesContainer.Controls.Add(this.propertyValueTextBox);
            this.propertiesContainer.Controls.Add(this.addPropButton);
            this.propertiesContainer.Controls.Add(this.propertyDropDown);
            this.propertiesContainer.Controls.Add(this.propertiesList);
            this.propertiesContainer.Location = new System.Drawing.Point(377, 69);
            this.propertiesContainer.Name = "propertiesContainer";
            this.propertiesContainer.Size = new System.Drawing.Size(218, 357);
            this.propertiesContainer.TabIndex = 12;
            this.propertiesContainer.TabStop = false;
            this.propertiesContainer.Text = "Properties";
            // 
            // label7
            // 
            this.label7.Location = new System.Drawing.Point(6, 328);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(97, 23);
            this.label7.TabIndex = 15;
            this.label7.Text = "Minumum Matches";
            // 
            // minimumMatchPropsTextBox
            // 
            this.minimumMatchPropsTextBox.Location = new System.Drawing.Point(109, 325);
            this.minimumMatchPropsTextBox.Name = "minimumMatchPropsTextBox";
            this.minimumMatchPropsTextBox.Size = new System.Drawing.Size(81, 20);
            this.minimumMatchPropsTextBox.TabIndex = 14;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(5, 48);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(54, 13);
            this.label5.TabIndex = 13;
            this.label5.Text = "Min Value";
            // 
            // propertyValueTextBox
            // 
            this.propertyValueTextBox.Location = new System.Drawing.Point(94, 44);
            this.propertyValueTextBox.Name = "propertyValueTextBox";
            this.propertyValueTextBox.Size = new System.Drawing.Size(43, 20);
            this.propertyValueTextBox.TabIndex = 5;
            // 
            // addPropButton
            // 
            this.addPropButton.Location = new System.Drawing.Point(160, 18);
            this.addPropButton.Name = "addPropButton";
            this.addPropButton.Size = new System.Drawing.Size(48, 22);
            this.addPropButton.TabIndex = 3;
            this.addPropButton.Text = "Add";
            this.addPropButton.UseVisualStyleBackColor = true;
            this.addPropButton.Click += new System.EventHandler(this.addPropButton_Click);
            // 
            // propertyDropDown
            // 
            this.propertyDropDown.DisplayMember = "Name";
            this.propertyDropDown.FormattingEnabled = true;
            this.propertyDropDown.Location = new System.Drawing.Point(5, 19);
            this.propertyDropDown.Name = "propertyDropDown";
            this.propertyDropDown.Size = new System.Drawing.Size(145, 21);
            this.propertyDropDown.TabIndex = 1;
            this.propertyDropDown.ValueMember = "Value";
            // 
            // propertiesList
            // 
            this.propertiesList.Location = new System.Drawing.Point(6, 69);
            this.propertiesList.Name = "propertiesList";
            this.propertiesList.Size = new System.Drawing.Size(205, 251);
            this.propertiesList.TabIndex = 0;
            this.propertiesList.VerticalScroll.Enabled = true;
            this.propertiesList.VerticalScroll.Visible = true;
            this.propertiesList.AutoScroll = true;
            // 
            // slotContainer
            // 
            this.slotContainer.Controls.Add(this.slotDropDown);
            this.slotContainer.Controls.Add(this.slotAddButton);
            this.slotContainer.Controls.Add(this.equipmentSlotList);
            this.slotContainer.Location = new System.Drawing.Point(195, 69);
            this.slotContainer.Name = "slotContainer";
            this.slotContainer.Size = new System.Drawing.Size(180, 357);
            this.slotContainer.TabIndex = 11;
            this.slotContainer.TabStop = false;
            this.slotContainer.Text = "Equipment Slots";
            // 
            // slotDropDown
            // 
            this.slotDropDown.DisplayMember = "Name";
            this.slotDropDown.FormattingEnabled = true;
            this.slotDropDown.Location = new System.Drawing.Point(5, 20);
            this.slotDropDown.Name = "slotDropDown";
            this.slotDropDown.Size = new System.Drawing.Size(115, 21);
            this.slotDropDown.TabIndex = 6;
            this.slotDropDown.ValueMember = "Value";
            // 
            // slotAddButton
            // 
            this.slotAddButton.Location = new System.Drawing.Point(122, 19);
            this.slotAddButton.Name = "slotAddButton";
            this.slotAddButton.Size = new System.Drawing.Size(48, 22);
            this.slotAddButton.TabIndex = 2;
            this.slotAddButton.Text = "Add";
            this.slotAddButton.UseVisualStyleBackColor = true;
            this.slotAddButton.Click += new System.EventHandler(this.addSlotButton_Click);
            // 
            // eqipmentSlotList
            // 
            this.equipmentSlotList.ContextMenuStrip = this.slotDropDownMenu;
            this.equipmentSlotList.Location = new System.Drawing.Point(5, 48);
            this.equipmentSlotList.Name = "equipmentSlotList";
            this.equipmentSlotList.Size = new System.Drawing.Size(163, 303);
            this.equipmentSlotList.TabIndex = 4;
            this.equipmentSlotList.VerticalScroll.Enabled = true;
            this.equipmentSlotList.VerticalScroll.Visible = true;
            this.equipmentSlotList.AutoScroll = true;
            // 
            // slotDropDownMenu
            // 
            this.slotDropDownMenu.Name = "slotDropDownMenu";
            this.slotDropDownMenu.Size = new System.Drawing.Size(155, 26);
            // 
            // itemNameContainer
            // 
            this.itemNameContainer.Controls.Add(this.itemNamesList);
            this.itemNameContainer.Controls.Add(this.itemIdAddTextBox);
            this.itemNameContainer.Controls.Add(this.idAddButton);
            this.itemNameContainer.Location = new System.Drawing.Point(8, 69);
            this.itemNameContainer.Name = "itemNameContainer";
            this.itemNameContainer.Size = new System.Drawing.Size(185, 357);
            this.itemNameContainer.TabIndex = 10;
            this.itemNameContainer.TabStop = false;
            this.itemNameContainer.Text = "Names / Id\'s";
            // 
            // itemNamesList
            // 
            this.itemNamesList.Location = new System.Drawing.Point(5, 48);
            this.itemNamesList.Name = "itemNamesList";
            this.itemNamesList.Size = new System.Drawing.Size(175, 303);
            this.itemNamesList.TabIndex = 4;
            this.itemNamesList.VerticalScroll.Enabled = true;
            this.itemNamesList.VerticalScroll.Visible = true;
            this.itemNamesList.AutoScroll = true;
            // 
            // itemIdAddTextBox
            // 
            this.itemIdAddTextBox.Location = new System.Drawing.Point(5, 19);
            this.itemIdAddTextBox.Name = "itemIdAddTextBox";
            this.itemIdAddTextBox.Size = new System.Drawing.Size(102, 20);
            this.itemIdAddTextBox.TabIndex = 1;
            // 
            // idAddButton
            // 
            this.idAddButton.Location = new System.Drawing.Point(111, 19);
            this.idAddButton.Name = "idAddButton";
            this.idAddButton.Size = new System.Drawing.Size(48, 22);
            this.idAddButton.TabIndex = 0;
            this.idAddButton.Text = "Add";
            this.idAddButton.UseVisualStyleBackColor = true;
            this.idAddButton.Click += new System.EventHandler(this.idAddButton_Click);
            // 
            // ruleNameTextBox
            // 
            this.ruleNameTextBox.Location = new System.Drawing.Point(69, 19);
            this.ruleNameTextBox.Name = "ruleNameTextBox";
            this.ruleNameTextBox.Size = new System.Drawing.Size(289, 20);
            this.ruleNameTextBox.TabIndex = 3;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(8, 22);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(60, 13);
            this.label2.TabIndex = 2;
            this.label2.Text = "Rule Name";
            // 
            // rarityMinDropDown
            // 
            this.rarityMinDropDown.DisplayMember = "Name";
            this.rarityMinDropDown.FormattingEnabled = true;
            this.rarityMinDropDown.Location = new System.Drawing.Point(69, 44);
            this.rarityMinDropDown.Name = "rarityMinDropDown";
            this.rarityMinDropDown.Size = new System.Drawing.Size(104, 21);
            this.rarityMinDropDown.TabIndex = 4;
            this.rarityMinDropDown.ValueMember = "Value";
            // 
            // rarityMinDropDown
            // 
            this.rarityMaxDropDown.DisplayMember = "Name";
            this.rarityMaxDropDown.FormattingEnabled = true;
            this.rarityMaxDropDown.Location = new System.Drawing.Point(252, 44);
            this.rarityMaxDropDown.Name = "rarityMinDropDown";
            this.rarityMaxDropDown.Size = new System.Drawing.Size(104, 21);
            this.rarityMaxDropDown.TabIndex = 4;
            this.rarityMaxDropDown.ValueMember = "Value";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(8, 47);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(54, 13);
            this.label3.TabIndex = 5;
            this.label3.Text = "Min Rarity";
            // 
            // label3
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(190, 47);
            this.label8.Name = "label3";
            this.label8.Size = new System.Drawing.Size(54, 13);
            this.label8.TabIndex = 5;
            this.label8.Text = "Max Rarity";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(5, 19);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(37, 13);
            this.label1.TabIndex = 1;
            this.label1.Text = "Preset";
            // 
            // presetDropDown
            // 
            this.presetDropDown.DisplayMember = "RuleName";
            this.presetDropDown.FormattingEnabled = true;
            this.presetDropDown.Location = new System.Drawing.Point(44, 16);
            this.presetDropDown.Name = "presetDropDown";
            this.presetDropDown.Size = new System.Drawing.Size(319, 21);
            this.presetDropDown.TabIndex = 0;
            this.presetDropDown.SelectedIndexChanged += new System.EventHandler(this.presetDropDown_SelectedIndexChanged);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1020, 524);
            this.Controls.Add(this.huePicker);
            this.Controls.Add(this.hueTextBox);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.ruleContainer);
            this.Controls.Add(this.listContainer);
            this.Controls.Add(this.characterDropdown);
            this.Controls.Add(this.exportCharacterButton);
            this.Controls.Add(this.importCharacterButton);
            this.Controls.Add(this.colorCorpseCheckbox);
            this.Controls.Add(this.label10);
            this.Controls.Add(this.lootDelayTextBox);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Name = "Form1";
            this.ShowIcon = false;
            this.Text = "Lootmaster Configurator";
            this.listContainer.ResumeLayout(false);
            this.ruleDropDownMenu.ResumeLayout(false);
            this.ruleContainer.ResumeLayout(false);
            this.ruleContainer.PerformLayout();
            this.settingContainer.ResumeLayout(false);
            this.settingContainer.PerformLayout();
            this.propertiesIgnoreContainer.ResumeLayout(false);
            this.propertiesContainer.ResumeLayout(false);
            this.propertiesContainer.PerformLayout();
            this.slotContainer.ResumeLayout(false);
            this.slotDropDownMenu.ResumeLayout(false);
            this.itemNameContainer.ResumeLayout(false);
            this.itemNameContainer.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private CheckBox colorCorpseCheckbox;
        private ComboBox characterDropdown;
        private Button exportCharacterButton;
        private Button importCharacterButton;
        private GroupBox listContainer;
        private FlowLayoutPanel rulesList;
        private Button ruleUpButton;
        private Button ruleDownButton;
        private Button addButton;
        private Button deleteButton;
        private Button clearTargetBagButton;
        private Button saveButton;
        private GroupBox ruleContainer;
        private GroupBox settingContainer;
        private GroupBox propertiesContainer;
        private GroupBox slotContainer;
        private TextBox itemIdAddTextBox;
        private Button idAddButton;
        private Button slotAddButton;
        private GroupBox itemNameContainer;
        private FlowLayoutPanel itemNamesList;
        private FlowLayoutPanel equipmentSlotList;
        private TextBox ruleNameTextBox;
        private Label label2;
        private ComboBox rarityMinDropDown;
        private ComboBox rarityMaxDropDown;
        private Label label3;
        private Label label8;
        private ComboBox slotDropDown;
        private Label label1;
        private ComboBox presetDropDown;
        private Label label5;
        private TextBox propertyValueTextBox;
        private Button addPropButton;
        private ComboBox propertyDropDown;
        private FlowLayoutPanel propertiesList;
        private ContextMenuStrip slotDropDownMenu;
        private ContextMenuStrip ruleDropDownMenu;
        private CheckBox alertCheckbox;
        private CheckBox enabledCheckbox;
        
        private TextBox weightCurseTextBox;
        private Label label6;
        private Label label7;
        private Label label9;
        private Label label10;
        private TextBox regExTextBox;

        private TextBox minimumMatchPropsTextBox;

        private GroupBox propertiesIgnoreContainer;
        private Button addPropIgnoreButton;
        private ComboBox propertyIgnoreDropDown;
        private FlowLayoutPanel propertiesIgnoreList;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox hueTextBox;
        private System.Windows.Forms.TextBox lootDelayTextBox;
        private System.Windows.Forms.ComboBox huePicker;

        private System.ComponentModel.IContainer components = null;
        
            
        
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    public class EditRuleItem : Form
    {
        
        private PropertyMatch _propMatch { get; set; }
        private ItemColorIdentifier _idColor { get; set; }
        private string _name { get; set; }
        
        private TypeObject _typeObject { get; set; }
        
        private enum TypeObject
        {
            PropertyMatch,
            ItemColorIdentifier,
            String
        }
        public EditRuleItem(string name)
        {
            _name = name;
            _typeObject = TypeObject.String;
            InitializeComponent();
            value1Tb.Text = name;
            nameLbl.Text = name;
            value1Lbl.Text = "Name";
            value2Lbl.Text = "N/A";
            value2Tb.Enabled = false;
            value1Tb.Focus();
        }

        public EditRuleItem(ItemColorIdentifier idColor)
        {
            _name = idColor.Name;
            _idColor = new ItemColorIdentifier(idColor.ItemId, idColor.Color, idColor.Name);
            
            _typeObject = TypeObject.ItemColorIdentifier;
            InitializeComponent();
            value1Tb.Text = $"0x{idColor.ItemId.ToString("X")}";
            value2Tb.Text = idColor.Color == null ? string.Empty : $"0x{idColor.Color?.ToString("X")}";
            nameLbl.Text = idColor.Name;
            value1Lbl.Text = "Item Id";
            value2Lbl.Text = "Hue";
            value1Tb.Focus();
        }

        public EditRuleItem(PropertyMatch propMatch)
        {
            _propMatch = new PropertyMatch
            {
                Property = propMatch.Property,
                Value = propMatch.Value,
            };
            
            InitializeComponent();
            
            nameLbl.Text = propMatch.DisplayName;
            value1Lbl.Text = "Value";
            value1Tb.Text = _propMatch.Value.ToString();
            value2Lbl.Text = "N/A";
            value2Tb.Enabled = false;
            value1Tb.Focus();
            
            _typeObject = TypeObject.PropertyMatch;
        }
        
        
        
        public T GetResponse<T>()
        {
            if(typeof(T) == typeof(PropertyMatch))
                return (T)Convert.ChangeType(_propMatch, typeof(T));
            if(typeof(T) == typeof(ItemColorIdentifier))
                return (T)Convert.ChangeType(_idColor, typeof(T));
            if(typeof(T) == typeof(string))
                return (T)Convert.ChangeType(_name, typeof(T));
            
            return default(T);
        }
        
        private void saveButton_Click(object sender, EventArgs e)
        {
            switch (_typeObject)
            {
                case TypeObject.ItemColorIdentifier:
                    if (int.TryParse(value1Tb.Text, out int inIdVal))
                    {
                        _idColor.ItemId = inIdVal;
                    }
                    else
                    {
                        var inIdVal2 = Convert.ToInt32(value1Tb.Text , 16);
                        _idColor.ItemId = inIdVal2;
                    }
                    int? saveVal = null;
                    if (value2Tb.Text.Trim() == string.Empty || value2Tb.Text.Equals("any", StringComparison.OrdinalIgnoreCase))
                    {
                        saveVal = null;
                    }
                    else if (int.TryParse(value2Tb.Text, out int inHueVal))
                    {
                        saveVal = inHueVal;
                    }
                    else
                    {
                        var inHueVal2 = Convert.ToInt32(value2Tb.Text , 16);
                        saveVal = inHueVal2;
                    }
                    _idColor.Color = saveVal;
                    break;
                case TypeObject.String:
                    _name = value1Tb.Text;
                    break;
                case TypeObject.PropertyMatch:
                    if (int.TryParse(value1Tb.Text, out int inVal))
                    {
                        _propMatch.Value = inVal;
                    }
                    break;
                default:
                    break;
            }
            
            DialogResult = DialogResult.OK;
            Close();
        }
        
        
        public Button saveButton;
        public TextBox value1Tb;
        public TextBox value2Tb;
        public Label nameLbl;
        public Label value1Lbl;
        public Label value2Lbl;

        void InitializeComponent()
        {
            value1Tb = new TextBox();
            value2Tb = new TextBox();
            nameLbl = new Label();
            value1Lbl = new Label();
            value2Lbl = new Label();
            saveButton = new Button();

            this.SuspendLayout();

            //
            // nameLbl
            // 
            this.nameLbl.Location = new System.Drawing.Point(3, 4);
            this.nameLbl.Name = "nameLbl";
            this.nameLbl.Size = new System.Drawing.Size(120, 14);
            this.nameLbl.TabIndex = 1;
            //
            // value1Lbl
            // 
            this.value1Lbl.Location = new System.Drawing.Point(3, 28);
            this.value1Lbl.Name = "value1Lbl";
            this.value1Lbl.Size = new System.Drawing.Size(50, 14);
            this.value1Lbl.TabIndex = 1;
            this.value1Lbl.Text = "Label1";
            //
            // value1Tb
            // 
            this.value1Tb.Location = new System.Drawing.Point(56, 28);
            this.value1Tb.Name = "value1Tb";
            this.value1Tb.Size = new System.Drawing.Size(120, 14);
            this.value1Tb.TabIndex = 2;
            this.value1Tb.Enabled = true;
            //
            // value2Lbl
            // 
            this.value2Lbl.Location = new System.Drawing.Point(3, 52);
            this.value1Lbl.Name = "value2Lbl";
            this.value2Lbl.Size = new System.Drawing.Size(120, 14);
            this.value2Lbl.TabIndex = 1;
            this.value2Lbl.Text = "Label2";
            //
            // value2Tb
            // 
            this.value2Tb.Location = new System.Drawing.Point(56, 52);
            this.value2Tb.Name = "value2Tb";
            this.value2Tb.Size = new System.Drawing.Size(50, 14);
            this.value2Tb.TabIndex = 4;
            //
            // saveButton
            // 
            this.saveButton.Location = new System.Drawing.Point(123, 4);
            this.saveButton.Name = "saveButton";
            this.saveButton.Size = new System.Drawing.Size(50, 24);
            this.saveButton.TabIndex = 4;
            this.saveButton.Text = "Save";
            this.saveButton.Click += new System.EventHandler(this.saveButton_Click);
            //
            // Form1
            //
            this.Controls.Add(this.nameLbl);
            this.Controls.Add(this.value1Tb);
            this.Controls.Add(this.value2Tb);
            this.Controls.Add(this.value1Lbl);
            this.Controls.Add(this.value2Lbl);
            this.Controls.Add(this.saveButton);
            this.Width = 200;
            this.Height = 120;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Text = "Edit value";
            
            this.ResumeLayout();
        }
    }

    public class ImpExp : Form
    {
        public TextBox textField;
        public Button importButton;
        public LootMasterCharacter DecodedCharacter;

        public ImpExp()
        {
            InitializeComponent();
        }
        
        void InitializeComponent()
        {
            this.SuspendLayout();
            
            textField = new TextBox();
            importButton = new Button();


            this.Size = new System.Drawing.Size(400, 400);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            
            textField.Multiline = true;
            textField.Location = new System.Drawing.Point(0, 0);
            textField.Size = new System.Drawing.Size(380, 300);
            
            importButton.Location = new System.Drawing.Point(0, 310);
            importButton.Size = new System.Drawing.Size(380, 45);
            importButton.Text = "Import Character Config";
            importButton.Click += new System.EventHandler(Decode);
            
            this.Controls.Add(this.textField);
            this.Controls.Add(this.importButton);
            this.Text = "Import Export";
            
            this.ResumeLayout(false);
            this.PerformLayout();
            
        }

        private void Decode(object sender, EventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(textField.Text))
                {
                    var plainTextBytes = Convert.FromBase64String(textField.Text);
                    var plainText = Encoding.UTF8.GetString(plainTextBytes);
                    
                    LootMasterCharacter readConfig = null;
                    var ns = Assembly.LoadFile(Path.Combine(Engine.RootPath, "Newtonsoft.Json.dll"));
                    foreach (Type type in ns.GetExportedTypes())
                    {
                        if (type.Name == "JsonConvert")
                        {
                            var funcs = type.GetMethods(BindingFlags.InvokeMethod | BindingFlags.Static | BindingFlags.Public).Where(f => f.Name == "DeserializeObject" && f.IsGenericMethodDefinition);
                            var func = funcs.FirstOrDefault(f => f.Name == "DeserializeObject" && f.GetParameters().Length == 1 && f.GetParameters()[0].ParameterType == typeof(string))
                                .MakeGenericMethod(typeof(LootMasterCharacter));
                            readConfig = func.Invoke(type, BindingFlags.InvokeMethod, null, new object[] { plainText }, null) as LootMasterCharacter;
                        }
                    }
                    
                    DecodedCharacter = readConfig;
                }
                
                this.Close();
            }
            catch
            {
                MessageBox.Show("Invalid Config String");
            }
            
        }
    }
    
    public partial class RuleController : UserControl
    {
        public bool IsSelected => checkBox1.Checked;

        public Guid RuleId => _rule.Id;
        public LootRule GetRule() => _rule;
        public void SetRule(LootRule rule) => _rule = rule;
        
        private bool _isEnabled = false;
        private LootRule _rule;
        Action<Guid> _deleteAction;
        Action<Guid, int> _moveAction;
        Action<Guid> _selectRule;

        public void ClearTargetBag()
        {
            _rule.TargetBag = null;
        }

        public void UpdateData()
        {
            ruleNameLbl.Text = _rule.RuleName;
            SetEnabled(!_rule.Disabled);
        }
        
        public RuleController(LootRule rule, Action<Guid> deleteAction, Action<Guid,int> moveAction, Action<Guid> selectRule)
        {
            _rule = rule;
            _isEnabled = !rule.Disabled;
            
            _deleteAction = deleteAction;
            _moveAction = moveAction;
            _selectRule = selectRule;
            InitializeComponent();
            
            
            ruleNameLbl.Text = rule.RuleName;
            SetEnabled(_isEnabled);
        }
        
        public void SetEnabled(bool enabled)
        {
            if (enabled)
            {
                enabledLbl.ForeColor = Color.Green;
                enabledLbl.Text = "\u2713";
            }
            else
            {
                enabledLbl.ForeColor = Color.Red;
                enabledLbl.Text = "X";
            }
        }
        
        public void SetActive(bool active)
        {
            if (active)
            {
                panelMain.BackColor = Color.LightBlue;
                panelMain.BorderStyle = BorderStyle.None;
                checkBox1.Checked = true;
                checkBox1.Enabled = false;
            }
            else
            {
                panelMain.BackColor = SystemColors.Control;
                panelMain.BorderStyle = BorderStyle.None;
                checkBox1.Checked = false;
                checkBox1.Enabled = true;
            }
        }
        
        private void deleteSelectedRuleMenuItem_Click(object sender, EventArgs e)
        {
            _deleteAction(_rule.Id);
        }

        private void SetActiveClick(object sender, EventArgs e)
        {
            panelMain.BorderStyle = BorderStyle.FixedSingle;
            _selectRule(_rule.Id);
        }

        private void moveDownSelectedRuleMenuItem_Click(object sender, EventArgs e)
        {
            _moveAction(_rule.Id, 1);
        }

        private void moveUpSelectedRuleMenuItem_Click(object sender, EventArgs e)
        {
            _moveAction(_rule.Id, -1);
        }
        
        //Designer items
        private IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }

            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.panelMain = new System.Windows.Forms.Panel();
            this.panelTop = new System.Windows.Forms.Panel();
            this.enabledLbl = new System.Windows.Forms.Label();
            this.checkBox1 = new System.Windows.Forms.CheckBox();
            this.ruleNameLbl = new System.Windows.Forms.Label();
            this.ruleDropDownMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.moveUpSelectedRuleMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.moveDownSelectedRuleMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.deleteSelectedRuleMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.panelMain.SuspendLayout();
            this.ruleDropDownMenu.SuspendLayout();
            this.SuspendLayout();
            // 
            // panelMain
            // 
            this.panelMain.Controls.Add(this.enabledLbl);
            this.panelMain.Controls.Add(this.checkBox1);
            this.panelMain.Controls.Add(this.ruleNameLbl);
            this.panelMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelMain.Location = new System.Drawing.Point(0, 0);
            this.panelMain.Name = "panelMain";
            this.panelMain.Size = new System.Drawing.Size(150, 27);
            this.panelMain.TabIndex = 0;
            this.panelMain.Click += new System.EventHandler(this.SetActiveClick);
            this.panelMain.ContextMenuStrip = this.ruleDropDownMenu;
            // 
            // panelMain
            // 
            this.panelTop.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelTop.Location = new System.Drawing.Point(0, 0);
            this.panelTop.Name = "panelTop";
            this.panelTop.Size = new System.Drawing.Size(150, 2);
            this.panelTop.TabIndex = 0;
            this.panelTop.Click += new System.EventHandler(this.SetActiveClick);
            this.panelTop.Visible = false;
            
            // 
            // enabledLbl
            // 
            this.enabledLbl.Location = new System.Drawing.Point(106, 4);
            this.enabledLbl.Name = "enabledLbl";
            this.enabledLbl.Size = new System.Drawing.Size(17, 14);
            this.enabledLbl.TabIndex = 4;
            this.enabledLbl.Click += new System.EventHandler(this.SetActiveClick);
            this.enabledLbl.ContextMenuStrip = this.ruleDropDownMenu;
            // 
            // checkBox1
            // 
            this.checkBox1.Location = new System.Drawing.Point(129, 4);
            this.checkBox1.Name = "checkBox1";
            this.checkBox1.Size = new System.Drawing.Size(18, 14);
            this.checkBox1.TabIndex = 3;
            this.checkBox1.UseVisualStyleBackColor = true;
            // 
            // ruleNameLbl
            // 
            this.ruleNameLbl.Location = new System.Drawing.Point(3, 4);
            this.ruleNameLbl.Name = "ruleNameLbl";
            this.ruleNameLbl.Size = new System.Drawing.Size(97, 14);
            this.ruleNameLbl.TabIndex = 2;
            this.ruleNameLbl.Text = "label1";
            this.ruleNameLbl.Click += new System.EventHandler(this.SetActiveClick);
            
            this.ruleNameLbl.ContextMenuStrip = this.ruleDropDownMenu;
            // 
            // ruleDropDownMenu
            // 
            this.ruleDropDownMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { this.moveUpSelectedRuleMenuItem, this.moveDownSelectedRuleMenuItem, this.deleteSelectedRuleMenuItem });
            this.ruleDropDownMenu.Name = "ruleDropDownMenu";
            this.ruleDropDownMenu.Size = new System.Drawing.Size(155, 70);
            this.ruleDropDownMenu.Opened += new System.EventHandler(this.ruleDropDownMenu_Opened);
            this.ruleDropDownMenu.Closed += new System.Windows.Forms.ToolStripDropDownClosedEventHandler(this.ruleDropDownMenu_Closed);
            // 
            // moveUpSelectedRuleMenuItem
            // 
            this.moveUpSelectedRuleMenuItem.Name = "moveUpSelectedRuleMenuItem";
            this.moveUpSelectedRuleMenuItem.Size = new System.Drawing.Size(154, 22);
            this.moveUpSelectedRuleMenuItem.Text = "Move Up";
            this.moveUpSelectedRuleMenuItem.Click += new System.EventHandler(this.moveUpSelectedRuleMenuItem_Click);
            // 
            // moveDownSelectedRuleMenuItem
            // 
            this.moveDownSelectedRuleMenuItem.Name = "moveDownSelectedRuleMenuItem";
            this.moveDownSelectedRuleMenuItem.Size = new System.Drawing.Size(154, 22);
            this.moveDownSelectedRuleMenuItem.Text = "Move Down";
            this.moveDownSelectedRuleMenuItem.Click += new System.EventHandler(this.moveDownSelectedRuleMenuItem_Click);
            // 
            // deleteSelectedRuleMenuItem
            // 
            this.deleteSelectedRuleMenuItem.Name = "deleteSelectedRuleMenuItem";
            this.deleteSelectedRuleMenuItem.Size = new System.Drawing.Size(154, 22);
            this.deleteSelectedRuleMenuItem.Text = "Delete Rule";
            this.deleteSelectedRuleMenuItem.Click += new System.EventHandler(this.deleteSelectedRuleMenuItem_Click);
            // 
            // RuleController
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.panelMain);
            this.Name = "RuleController";
            this.Size = new System.Drawing.Size(150, 24);
            this.Padding = new System.Windows.Forms.Padding(2, 0, 0, 0);
            this.Margin = new System.Windows.Forms.Padding(0);
            this.panelMain.ResumeLayout(false);
            this.ruleDropDownMenu.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        private void ruleDropDownMenu_Closed(object sender, ToolStripDropDownClosedEventArgs e)
        {
            var tp = sender as ContextMenuStrip;
            var control = tp.SourceControl as Control;
            if (control is Panel panel)
            {
                panel.BorderStyle = BorderStyle.None;
            }
            else if(control is Label label)
            {
                (label.Parent as Panel).BorderStyle = BorderStyle.None;
            }
        }

        private void ruleDropDownMenu_Opened(object sender, EventArgs e)
        {
            var tp = sender as ContextMenuStrip;
            var control = tp.SourceControl as Control;
            if (control is Panel panel)
            {
                panel.BorderStyle = BorderStyle.FixedSingle;
            }
            else if(control is Label label)
            {
                (label.Parent as Panel).BorderStyle = BorderStyle.FixedSingle;
            }
        }

        private System.Windows.Forms.Label enabledLbl;

        private System.Windows.Forms.ContextMenuStrip ruleDropDownMenu;
        private System.Windows.Forms.ToolStripMenuItem moveUpSelectedRuleMenuItem;
        private System.Windows.Forms.ToolStripMenuItem moveDownSelectedRuleMenuItem;
        private System.Windows.Forms.ToolStripMenuItem deleteSelectedRuleMenuItem;
        private System.Windows.Forms.Label ruleNameLbl;
        private System.Windows.Forms.CheckBox checkBox1;

        private System.Windows.Forms.Panel panelMain;
        private System.Windows.Forms.Panel panelTop;

        #endregion
    }
    
    public partial class IdNameControl : UserControl
    {
        private Action<Guid,Type> _deleteAction;
        private Action<Guid,object> _editAction;
        private Guid _tempId = Guid.NewGuid();
        private ItemColorIdentifier _idcolor = null;
        private string _name;
        
        public ItemColorIdentifier Get() => _idcolor;
        public string GetName() => _name;
        public bool IsIdColor { get; set; }
        public Guid UniqueId => _tempId;


        public IdNameControl(string name, Action<Guid,Type> deleteAction, Action<Guid,object> editAction)
        {
            _name = name;
            IsIdColor = false;
            InitializeComponent();
            idLbl.Text = string.Empty;
            colorLbl.Text = string.Empty;
            nameLbl.Text = name;
            idLbl.Visible = false;
            colorLbl.Visible = false;
            _deleteAction = deleteAction;
            _editAction = editAction;
        }
        public IdNameControl(ItemColorIdentifier idcolor, Action<Guid,Type> deleteAction, Action<Guid,object> editAction)
        {
            _idcolor = idcolor;
            IsIdColor = true;
            _deleteAction = deleteAction;
            _editAction = editAction;
            _name = idcolor.Name;
            InitializeComponent();
            idLbl.Text = string.Format("0x{0:X}", idcolor.ItemId);
            colorLbl.Text = idcolor.Color != null ? string.Format("0x{0:X}", idcolor.Color) : "ANY";
            nameLbl.Text = idcolor.Name;
        }

        private void editBtn_Click(object sender, EventArgs e)
        {
            if(IsIdColor)
                _editAction(_tempId,_idcolor);
            else
                _editAction(_tempId,_name);
        }

        private void deleteBtn_Click(object sender, EventArgs e)
        {
            _deleteAction(_tempId, IsIdColor ? typeof(ItemColorIdentifier) : typeof(string));
        }
        
        
        //Designer items
        private IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }

            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.panelMain = new System.Windows.Forms.Panel();
            this.panelTop = new System.Windows.Forms.Panel();
            this.idLbl = new System.Windows.Forms.Label();
            this.colorLbl = new System.Windows.Forms.Label();
            this.nameLbl = new System.Windows.Forms.Label();
            this.editBtn = new System.Windows.Forms.Button();
            this.deleteBtn = new System.Windows.Forms.Button();
            this.panelMain.SuspendLayout();
            this.SuspendLayout();
            // 
            // panelMain
            // 
            this.panelMain.Controls.Add(this.idLbl);
            this.panelMain.Controls.Add(this.colorLbl);
            this.panelMain.Controls.Add(this.nameLbl);
            this.panelMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelMain.Location = new System.Drawing.Point(0, 0);
            this.panelMain.Name = "panelMain";
            this.panelMain.Size = new System.Drawing.Size(155, 36);
            this.panelMain.TabIndex = 0;
            // 
            // panelTop
            // 
            this.panelTop.Controls.Add(this.idLbl);
            this.panelTop.Controls.Add(this.colorLbl);
            this.panelTop.Controls.Add(this.nameLbl);
            this.panelTop.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelTop.Location = new System.Drawing.Point(0, 0);
            this.panelTop.Name = "panelTop";
            this.panelTop.Size = new System.Drawing.Size(155, 1);
            this.panelTop.TabIndex = 0;
            this.panelTop.BackColor = SystemColors.ControlDark;
            // 
            // panelMain
            // 
            this.panelMain.Controls.Add(this.idLbl);
            this.panelMain.Controls.Add(this.colorLbl);
            this.panelMain.Controls.Add(this.nameLbl);
            this.panelMain.Controls.Add(this.editBtn);
            this.panelMain.Controls.Add(this.deleteBtn);
            this.panelMain.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelMain.Location = new System.Drawing.Point(1, 0);
            this.panelMain.Name = "panelMain";
            this.panelMain.Size = new System.Drawing.Size(157, 35);
            this.panelMain.TabIndex = 0;
            // 
            // idLbl
            // 
            this.idLbl.Location = new System.Drawing.Point(3, 20);
            this.idLbl.Name = "idLbl";
            this.idLbl.Size = new System.Drawing.Size(55, 14);
            this.idLbl.TabIndex = 2;
            this.idLbl.Text = "label1";
            this.idLbl.BorderStyle = BorderStyle.FixedSingle;
            // 
            // colorLbl
            // 
            this.colorLbl.Location = new System.Drawing.Point(60, 20);
            this.colorLbl.Name = "colorLbl";
            this.colorLbl.Size = new System.Drawing.Size(55, 14);
            this.colorLbl.TabIndex = 2;
            this.colorLbl.Text = "label1";
            this.colorLbl.BorderStyle = BorderStyle.FixedSingle;
            // 
            // nameLbl
            // 
            this.nameLbl.Location = new System.Drawing.Point(3, 4);
            this.nameLbl.Name = "nameLbl";
            this.nameLbl.Size = new System.Drawing.Size(110, 14);
            this.nameLbl.TabIndex = 2;
            this.nameLbl.Text = "label1";
            // 
            // editBtn
            // 
            this.editBtn.Location = new System.Drawing.Point(115, 2);
            this.editBtn.Name = "editBtn";
            this.editBtn.Size = new System.Drawing.Size(18, 18);
            this.editBtn.TabIndex = 2;
            this.editBtn.Text = "/";
            this.editBtn.ForeColor = Color.DarkOrange;
            this.editBtn.Padding = Padding.Empty;
            this.editBtn.Margin = Padding.Empty;
            this.editBtn.Click += new System.EventHandler(this.editBtn_Click);
            // 
            // deleteBtn
            // 
            this.deleteBtn.Location = new System.Drawing.Point(135, 2);
            this.deleteBtn.Name = "deleteBtn";
            this.deleteBtn.Size = new System.Drawing.Size(18, 18);
            this.deleteBtn.TabIndex = 2;
            this.deleteBtn.Text = "X";
            this.deleteBtn.ForeColor = Color.Red;
            this.deleteBtn.Padding = Padding.Empty;
            this.deleteBtn.Margin = Padding.Empty;
            this.deleteBtn.Click += new System.EventHandler(this.deleteBtn_Click);
            // 
            // idControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.panelTop);
            this.Controls.Add(this.panelMain);
            this.Name = "RuleController";
            this.Size = new System.Drawing.Size(157, 36);
            this.Padding = new System.Windows.Forms.Padding(2, 0, 0, 0);
            this.Margin = new System.Windows.Forms.Padding(0);
            this.panelMain.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.Label idLbl;
        private System.Windows.Forms.Label colorLbl;
        private System.Windows.Forms.Label nameLbl;
        private System.Windows.Forms.Button editBtn;
        private System.Windows.Forms.Button deleteBtn;

        private System.Windows.Forms.Panel panelMain;
        private System.Windows.Forms.Panel panelTop;

        #endregion
    }
    
    public partial class EquipmentSlotControl : UserControl
    {
        private Action<Guid,Type> _deleteAction;
        private Guid _tempId = Guid.NewGuid();
        private EquipmentSlot _slot;
        public EquipmentSlot Get() => _slot;
        public string GetName() => _slot.ToString();
        public Guid UniqueId => _tempId;

        
        public EquipmentSlotControl(string slot, Action<Guid,Type> deleteAction)
        {
            if (Enum.TryParse(slot, out EquipmentSlot enumSlot))
            {
                InitializeComponent();
                nameLbl.Text = slot;
                _slot = enumSlot;
                _deleteAction = deleteAction;
            }
            else
            {
                throw new Exception();
            }
        }

        public EquipmentSlotControl(EquipmentSlot slot, Action<Guid,Type> deleteAction)
        {
            InitializeComponent();
            nameLbl.Text = slot.ToString();
            _slot = slot;
            _deleteAction = deleteAction;
        }

        private void deleteBtn_Click(object sender, EventArgs e)
        {
            _deleteAction(_tempId, typeof(EquipmentSlot));
        }
        
        
        //Designer items
        private IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }

            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.panelMain = new System.Windows.Forms.Panel();
            this.panelTop = new System.Windows.Forms.Panel();
            this.nameLbl = new System.Windows.Forms.Label();
            this.deleteBtn = new System.Windows.Forms.Button();
            this.panelMain.SuspendLayout();
            this.SuspendLayout();
            // 
            // panelMain
            // 
            this.panelMain.Controls.Add(this.nameLbl);
            this.panelMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelMain.Location = new System.Drawing.Point(0, 0);
            this.panelMain.Name = "panelMain";
            this.panelMain.Size = new System.Drawing.Size(155, 36);
            this.panelMain.TabIndex = 0;
            // 
            // panelTop
            // 
            this.panelTop.Controls.Add(this.nameLbl);
            this.panelTop.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelTop.Location = new System.Drawing.Point(0, 0);
            this.panelTop.Name = "panelTop";
            this.panelTop.Size = new System.Drawing.Size(155, 1);
            this.panelTop.TabIndex = 0;
            this.panelTop.BackColor = SystemColors.ControlDark;
            // 
            // panelMain
            // 
            this.panelMain.Controls.Add(this.nameLbl);
            this.panelMain.Controls.Add(this.deleteBtn);
            this.panelMain.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelMain.Location = new System.Drawing.Point(1, 0);
            this.panelMain.Name = "panelMain";
            this.panelMain.Size = new System.Drawing.Size(157, 23);
            this.panelMain.TabIndex = 0;
            // 
            // nameLbl
            // 
            this.nameLbl.Location = new System.Drawing.Point(3, 4);
            this.nameLbl.Name = "nameLbl";
            this.nameLbl.Size = new System.Drawing.Size(110, 14);
            this.nameLbl.TabIndex = 2;
            this.nameLbl.Text = "label1";
            // 
            // deleteBtn
            // 
            this.deleteBtn.Location = new System.Drawing.Point(135, 2);
            this.deleteBtn.Name = "deleteBtn";
            this.deleteBtn.Size = new System.Drawing.Size(18, 18);
            this.deleteBtn.TabIndex = 2;
            this.deleteBtn.Text = "X";
            this.deleteBtn.ForeColor = Color.Red;
            this.deleteBtn.Padding = Padding.Empty;
            this.deleteBtn.Margin = Padding.Empty;
            this.deleteBtn.Click += new System.EventHandler(this.deleteBtn_Click);
            // 
            // EquipmentSlotControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.panelTop);
            this.Controls.Add(this.panelMain);
            this.Name = "RuleController";
            this.Size = new System.Drawing.Size(157, 24);
            this.Padding = new System.Windows.Forms.Padding(2, 0, 0, 0);
            this.Margin = new System.Windows.Forms.Padding(0);
            this.panelMain.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.Label nameLbl;
        private System.Windows.Forms.Button deleteBtn;

        private System.Windows.Forms.Panel panelMain;
        private System.Windows.Forms.Panel panelTop;

        #endregion
    }
    
    public partial class PropertyControl : UserControl
    {
        private Action<Guid,Type> _deleteAction;
        private Action<Guid,object> _editAction;
        private Guid _tempId = Guid.NewGuid();
        private PropertyMatch _property;
        private bool _isIgnoreProperty = false;
        public PropertyMatch Get() => _property;
        public string GetName() => _property.DisplayName;
        public int? GetValue() => _property.Value;
        public Guid UniqueId => _tempId;
        public bool IsIgnoreProperty => _isIgnoreProperty;
        
        public PropertyControl(PropertyMatch property, Action<Guid,Type> deleteAction, Action<Guid,object> editAction, bool ignore = false)
        {
            if (property.DisplayName.Contains("Slayer") || property.DisplayName.Equals("Silver"))
            {
                property.Value = null;
            }
            
            InitializeComponent();
            nameLbl.Text = property.DisplayName;
            valueLbl.Text = property.Value?.ToString() ?? "ANY";
            _deleteAction = deleteAction;
            _editAction = editAction;
            _property = property;
            if (ignore)
            {
                _isIgnoreProperty = true;
                editBtn.Visible = false;
                valueLbl.Visible = false;
                deleteBtn.Left = editBtn.Left;
            }
        }

        private void deleteBtn_Click(object sender, EventArgs e)
        {
            _deleteAction(_tempId, typeof(PropertyMatch));
        }

        private void editBtn_Click(object sender, EventArgs e)
        {
            _editAction(_tempId, _property);
        }
        
        
        //Designer items
        private IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }

            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.panelMain = new System.Windows.Forms.Panel();
            this.panelTop = new System.Windows.Forms.Panel();
            this.nameLbl = new System.Windows.Forms.Label();
            this.valueLbl = new System.Windows.Forms.Label();
            this.deleteBtn = new System.Windows.Forms.Button();
            this.editBtn = new System.Windows.Forms.Button();
            this.panelMain.SuspendLayout();
            this.SuspendLayout();
            // 
            // panelMain
            // 
            this.panelMain.Controls.Add(this.nameLbl);
            this.panelMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelMain.Location = new System.Drawing.Point(0, 0);
            this.panelMain.Name = "panelMain";
            this.panelMain.Size = new System.Drawing.Size(185, 36);
            this.panelMain.TabIndex = 0;
            // 
            // panelTop
            // 
            this.panelTop.Controls.Add(this.nameLbl);
            this.panelTop.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelTop.Location = new System.Drawing.Point(0, 0);
            this.panelTop.Name = "panelTop";
            this.panelTop.Size = new System.Drawing.Size(185, 1);
            this.panelTop.TabIndex = 0;
            this.panelTop.BackColor = SystemColors.ControlDark;
            // 
            // panelMain
            // 
            this.panelMain.Controls.Add(this.nameLbl);
            this.panelMain.Controls.Add(this.valueLbl);
            this.panelMain.Controls.Add(this.deleteBtn);
            this.panelMain.Controls.Add(this.editBtn);
            this.panelMain.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelMain.Location = new System.Drawing.Point(0, 1);
            this.panelMain.Name = "panelMain";
            this.panelMain.Size = new System.Drawing.Size(185, 35);
            this.panelMain.TabIndex = 0;
            // 
            // nameLbl
            // 
            this.nameLbl.Location = new System.Drawing.Point(3, 4);
            this.nameLbl.Name = "nameLbl";
            this.nameLbl.Size = new System.Drawing.Size(140, 14);
            this.nameLbl.TabIndex = 2;
            this.nameLbl.Text = "label1";
            // 
            // valueLbl 
            // 
            this.valueLbl.Location = new System.Drawing.Point(3, 18);
            this.valueLbl.Name = "valueLbl";
            this.valueLbl.Size = new System.Drawing.Size(50, 14);
            this.valueLbl.TabIndex = 2;
            this.valueLbl.Text = "label2";
            // 
            // editBtn
            // 
            this.editBtn.Location = new System.Drawing.Point(145, 2);
            this.editBtn.Name = "editBtn";
            this.editBtn.Size = new System.Drawing.Size(18, 18);
            this.editBtn.TabIndex = 2;
            this.editBtn.Text = "/";
            this.editBtn.ForeColor = Color.DarkOrange;
            this.editBtn.Padding = Padding.Empty;
            this.editBtn.Margin = Padding.Empty;
            this.editBtn.Click += new System.EventHandler(this.editBtn_Click);
            // 
            // deleteBtn
            // 
            this.deleteBtn.Location = new System.Drawing.Point(165, 2);
            this.deleteBtn.Name = "deleteBtn";
            this.deleteBtn.Size = new System.Drawing.Size(18, 18);
            this.deleteBtn.TabIndex = 2;
            this.deleteBtn.Text = "X";
            this.deleteBtn.ForeColor = Color.Red;
            this.deleteBtn.Padding = Padding.Empty;
            this.deleteBtn.Margin = Padding.Empty;
            this.deleteBtn.Click += new System.EventHandler(this.deleteBtn_Click);
            // 
            // EquipmentSlotControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.panelTop);
            this.Controls.Add(this.panelMain);
            this.Name = "RuleController";
            this.Size = new System.Drawing.Size(185, 36);
            this.Padding = new System.Windows.Forms.Padding(2, 0, 0, 0);
            this.Margin = new System.Windows.Forms.Padding(0);
            this.panelMain.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.Label nameLbl;
        private System.Windows.Forms.Label valueLbl;
        private System.Windows.Forms.Button deleteBtn;
        private System.Windows.Forms.Button editBtn;

        private System.Windows.Forms.Panel panelMain;
        private System.Windows.Forms.Panel panelTop;

        #endregion
    }

    public class DropDownItem
    {
        public string Name { get; set; }
        public int Value { get; set; }
    }

    public enum OptionsItem
    {
        Reload = 10,
        Reset = 11,
        ManualRun = 12,
        OpenConfig = 13,
        LoadStarter = 14,
        About = 15,
        Wiki = 16,
        Coffee = 17,
    }

    public enum Hue
    {
        Idle = 591,
        Looting = 72,
        Paused = 914,
    }
    
    public static class ListExtension
    {
        public static string GetNameFromSet(this List<ItemColorIdentifier> items, int itemId, int? color)
        {
            return items.FirstOrDefault(k => k.ItemId == itemId && k.Color == color)?.Name;
        }
        public static string GetNameFromItem(this List<ItemColorIdentifier> items, ItemColorIdentifier item)
        {
            return items.FirstOrDefault(k => k.ItemId == item.ItemId && k.Color == item.Color)?.Name;
        }
        
        public static void AddUnique<T>(this List<T> list, T item)
        {
            if (list.Contains(item))
            {
                return;
            }

            if (typeof(T) == typeof(ItemColorIdentifier))
            {
                var ici = item as ItemColorIdentifier;
                if (list.Cast<ItemColorIdentifier>().Any(k => k.ItemId == ici.ItemId && k.Color == ici.Color))
                {
                    return;
                }
            }
            
            list.Add(item);
        }
    }
    
    
    
}