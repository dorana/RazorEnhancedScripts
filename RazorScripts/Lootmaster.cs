using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Engine = Assistant.Engine;

namespace RazorEnhanced
{
    public class Lootmaster
    {
        public static readonly bool Debug = false;
        private readonly string _version = "v1.0.6";
        public static readonly bool IsOSI = false;
        
        private Target _tar = new Target();
        private readonly List<int> _gems = new List<int>();
        private readonly List<int> ignoreList = new List<int>();
        private LootMasterConfig _config = new LootMasterConfig();
        private Journal.JournalEntry _lastEntry = null;

        private Mobile _player;
        private int _lootDelay =  IsOSI ? 800 : 200;
        private DateTime? DeathClock = null;
        readonly Journal _journal = new Journal();
        
        public void Run()
        {
            try
            {
                if (Player.IsGhost)
                {
                    Handler.SendMessage(MessageType.Error, "You are a ghost, please ressurrect before running Lootmaster");
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

                _config.Init();
                
                Misc.RemoveSharedValue("Lootmaster:DirectContainer");
                Misc.RemoveSharedValue("Lootmaster:ReconfigureBags");
                Misc.RemoveSharedValue("Lootmaster:ClearCurrentCharacter");
                Misc.RemoveSharedValue("Lootmaster:DirectContainerRule");
                //SetSpecialRules();
                Setup();
                UpdateLootMasterGump(Hue.Idle);
                if (firstRun)
                {
                    ShowWelcomeGump();
                }
                _config.ItemLookup[3821] = "Gold Coin";
                _config.ItemLookup[41777] =  "Coin Purse";
                _config.ItemLookup[41779] = "Gem Purse";

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
                        Gumps.SendGump(lm, 150, 150);
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
                                ShowConfigurator();
                                break;
                            case OptionsItem.LoadStarter:
                                var confirmResult = MessageBox.Show("Are you sure you want to reset config to Starter Rules? \r\n\r\nIt's recomended to have at least 4 target bags ready.",
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
                    
                    if (JustRessed())
                    {
                        Handler.SendMessage(MessageType.Info,"Just Ressed");
                        Misc.Pause(2000);
                        continue;
                    }

                    if (Misc.ReadSharedValue("Lootmaster:ReconfigureBags") is bool reconfigure && reconfigure)
                    {
                        ReconfigureBags();
                    }

                    if (Misc.ReadSharedValue("Lootmaster:ClearCurrentCharacter") is bool clearCurrentCharacterConfig && clearCurrentCharacterConfig)
                    {
                        ClearCurrentCharacterConfig();
                    }

                    if (Misc.ReadSharedValue("Lootmaster:DirectContainer") is int directContainerSerial && directContainerSerial != 0)
                    {
                        if (directContainerSerial == -1)
                        {
                            continue;
                        }

                        LootRule rule = null;
                        if (Misc.ReadSharedValue("Lootmaster:DirectContainerRule") is string directContainerRule && !string.IsNullOrEmpty(directContainerRule))
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
                        UpdateLootMasterGump(Hue.Looting);
                        foreach (var corpse in corpses.Where(c => !ignoreList.Contains(c.Serial)))
                        {
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
                            var filtered = rows.Where(r => r.Type == "System");
                            if (filtered.Any(r => r.Text == "You must wait to perform another action."))
                            {
                                if (_lootDelay >= 700)
                                {
                                    _lootDelay = 400;
                                    Handler.SendMessage(MessageType.Log, $"Resetting loot delay to {_lootDelay}");
                                }
                                else
                                {
                                    _lootDelay += 10;
                                    Handler.SendMessage(MessageType.Log, $"Adjusting loot delay with 10ms, currently at {_lootDelay}");
                                }
                            }
                        }

                        UpdateLootMasterGump(Hue.Idle);
                    }

                    Misc.Pause(100);
                }
            }
            catch (Exception e)
            {
                if (e is ThreadAbortException)
                {
                    return;
                }

                if (!Debug)
                {
                    Handler.SendMessage(MessageType.Error, "Lootmaster encountered an error, and was forced to shut down");
                    var logFile = Path.Combine(Engine.RootPath, "Lootmaster.log");
                    File.AppendAllText(logFile, e.ToString());
                }
                else
                {
                    Handler.SendMessage(MessageType.Debug, e.ToString());
                }
               
                throw;
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
                UpdateLootMasterGump(Hue.Looting);
                LootContainer(directContainer, rule);
                Misc.Pause(500);
                UpdateLootMasterGump(Hue.Idle);
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


        private void UpdateLootMasterGump(Hue color)
        {
            var controller = Gumps.CreateGump();
            controller.x = 300;
            controller.y = 300;
            Gumps.AddPage(ref controller, 0);
            Gumps.AddBackground(ref controller, 0, 0, 140, 45, 1755);
            Gumps.AddButton(ref controller, 10, 8, 2152, 2151, 500, 1, 0);
            Gumps.AddLabel(ref controller, 50, 12, (int)color, "Lootmaster");
            Gumps.CloseGump(13659823);
            controller.serial = (uint)Player.Serial;
            controller.gumpId = 13659823;
            Gumps.SendGump(controller, 150, 150);
        }

        private void LootContainer(Item container) => LootContainer(container, null);
        private void LootContainer(Item container, LootRule rule)
        {
            Handler.SendMessage(MessageType.Debug, $"Waiting for contents of {container.Name}");
            Items.WaitForContents(container, 10000);
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
            Misc.Pause(_lootDelay);

            var entries = _journal.GetJournalEntry(_lastEntry);
            if (entries != null && entries.Any(e => e.Type == "system" && (e.Text.ToLower() == "you may not loot this corpse." || e.Text.ToLower() == "you did not earn the right to loot this creature!")))
            {
                IgnoreCorpse(container);
                return;
            }

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
                Items.SetColor(container.Serial,0x3F6);
            }
        }

        private int Loot(Item container, LootRule singleRule)
        {
            List <GrabTarget> lootItems = new List<GrabTarget>();
            
            var sum = 0;

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
                        return int.MinValue;
                    }
                    
                    if (rule.TargetBag == null)
                    {
                        continue;
                    }
                    
                    if (rule.Match(item))
                    {
                        if (rule.TargetBag == container.Serial)
                        {
                            // don't loot to the same container
                            continue;
                        }
                        if (rule.TargetBag == item.Serial)
                        {
                            // Don't loot into itself
                            continue;
                        }
                        
                        lootItems.Add(new GrabTarget
                        {
                            Item = item,
                            Rule = rule
                        });
                        break;
                    }
                }
            }

            var overLimit = false;
            
            foreach (var li in lootItems)
            {
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

            var gems = Enum.GetValues(typeof(Gem)).Cast<Gem>().ToList();

            foreach (var gem in gems) _gems.Add((int)gem);

            Misc.RemoveSharedValue("Lootmaster:ReconfigureBags");

            Handler.SendMessage(MessageType.Info, "Lootmaster is ready to loot");
            
            _config.Save();

            return true;
        }

        private int GrabItem(Item item,LootRule rule)
        {
            MoveToBag(item, rule.GetTargetBag());
            Misc.Pause(200);
            
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
        public string RuleName { get; set; }
        public List<string> ItemNames { get; set; }
        public List<EquipmentSlot> EquipmentSlots { get; set; }
        public List<PropertyMatch> Properties { get; set; }
        public ItemRarity? MinimumRarity { get; set; }
        public bool IgnoreWeightCurse { get; set; }
        public List<int> ItemIds { get; set; }
        public List<ItemProperty> BlackListedProperties { get; set; }

        public bool Alert { get; set; }


        public bool Disabled { get; set; }

        public int? TargetBag { get; set; }
        
        public int? PropertyMatchRequirement { get; set; }
        
        public Item GetTargetBag() => Items.FindBySerial(TargetBag ?? -1);

        public static LootRule Gold =>
            new LootRule
            {
                RuleName = "Gold",
                ItemIds = new List<int> { 3821, 41777 }, //GoldStacks and GoldBags
                IgnoreWeightCurse = true
            };

        public static LootRule Gems =>
            new LootRule
            {
                RuleName = "Gems",
                ItemIds = Enum.GetValues(typeof(Gem)).Cast<Gem>().Select(g => (int)g).ToList().Union(new List<int> { 41779 }).ToList(),
                IgnoreWeightCurse = true
            };


        public static LootRule Ammo =>
            new LootRule
            {
                RuleName = "Bolts and Arrows",
                ItemIds = new List<int> { 3903, 7163 } //Arrows and Bolts
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

        public LootRule(string ruleName, params string[] itemName)
        {
            RuleName = ruleName;
            ItemNames = itemName.ToList();
            TargetBag = null;
        }

        public LootRule(string ruleName, string itemName, bool alert = false)
        {
            RuleName = ruleName;
            ItemNames = new List<string> { itemName };
            Alert = alert;
            TargetBag = null;
        }

        public LootRule()
        {
            TargetBag = null;
        }

        public LootRule(string ruleName, ItemProperty property, int amount, ItemRarity? minimumRarity, EquipmentSlot? slot, bool alert = false)
        {
            RuleName = ruleName;
            EquipmentSlots = slot == null ? null : new List<EquipmentSlot>
            {
                slot.Value
            };
            Properties = new List<PropertyMatch>
            {
                new PropertyMatch
                {
                    Property = property,
                    Value = amount
                }
            };
            MinimumRarity = minimumRarity;
            Alert = alert;
            TargetBag = null;
        }



        public bool Match(Item item)
        {
            var match = CheckItemIdOrName(item);
            Handler.SendMessage(MessageType.Debug,$"CheckItemId / CheckItemName : {match}");
            //match = match && CheckBlackListProperties(item);
            
            match = match && CheckWeightCursed(item);
            Handler.SendMessage(MessageType.Debug,$"CheckWeightCursed : {match}");

            match = match && CheckRarityProps(item);
            Handler.SendMessage(MessageType.Debug,$"CheckRarityProps : {match}");

            match = match && CheckEquipmentSlot(item);
            Handler.SendMessage(MessageType.Debug,$"CheckEquipmentSlot : {match}");

            match = match && CheckSpecialProps(item);
            Handler.SendMessage(MessageType.Debug,$"CheckSpecialProps : {match}");
                
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
                matchFound = matchFound || item.Layer.Equals(EquipmentSlot.Ring.ToString(), StringComparison.OrdinalIgnoreCase) ||
                             item.Layer.Equals(EquipmentSlot.Bracelet.ToString(), StringComparison.OrdinalIgnoreCase);
            }

            return matchFound || EquipmentSlots.Any(s => s.ToString().Equals(item.Layer, StringComparison.OrdinalIgnoreCase));
        }

        private bool CheckItemIdOrName(Item item)
        {
            if ((ItemIds == null || !ItemIds.Any()) && (ItemNames == null || !ItemNames.Any()))
            {
                return true;
            }
            
            if (ItemIds == null || !ItemIds.Any())
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
            if (ItemIds == null || !ItemIds.Any())
            {
                return true;
            }

            return ItemIds.Contains(item.ItemID);
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
                    var propString = Handler.ResolvePropertyName(ruleProp);
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
            if (!IgnoreWeightCurse)
            {
                return item.Weight < 50;
            }

            return true;
        }

        private bool CheckRarityProps(Item item)
        {
            if (MinimumRarity == null)
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


            var checkRarities = Enum.GetValues(typeof(ItemRarity)).Cast<ItemRarity>().ToList().Where(r => (int)r >= (int)MinimumRarity).ToList();

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
                        var stringVal = prop.ToString().Replace("%", "").Replace("+","");
                        var reMatch = re.Match(stringVal);
                        var numIndex = reMatch.Success ? reMatch.Index : stringVal.Length;
                        if (checkProps.Any(propString => propString.Equals(stringVal.Substring(0, numIndex).Trim(), StringComparison.InvariantCultureIgnoreCase)))
                        {
                            var numstring = stringVal.Substring(numIndex);
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
                        var stringVal = prop.ToString().Replace("%", "").Replace("+","");
                        var reMatch = re.Match(stringVal);
                        var numIndex = reMatch.Success ? reMatch.Index : stringVal.Length;
                        var propString = Handler.ResolvePropertyName(ruleProp.Property);
                        if (propString.Equals(stringVal.Substring(0, numIndex).Trim(), StringComparison.InvariantCultureIgnoreCase))
                        {
                            var numstring = stringVal.Substring(numIndex);
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
    }


    public class LootMasterConfig
    {

        public List<LootMasterCharacter> Characters { get; set; }
        public bool ColorCorpses { get; set; }
        
        public Dictionary<int, string> ItemLookup { get; set; }


        public LootMasterConfig()
        {
            Characters = new List<LootMasterCharacter>();
            ColorCorpses = true;
            ItemLookup = new Dictionary<int, string>();
        }
        
        public void Init()
        {
            ReadConfig();
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
        
        private void ReadConfig()
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
                foreach(Type type in ns.GetExportedTypes())
                {
                    if (type.Name == "JsonConvert")
                    {
                        var funcs = type.GetMethods(BindingFlags.InvokeMethod | BindingFlags.Static | BindingFlags.Public).Where(f => f.Name == "DeserializeObject" && f.IsGenericMethodDefinition);
                        var func = funcs.FirstOrDefault(f => f.Name == "DeserializeObject" && f.GetParameters().Length == 1 && f.GetParameters()[0].ParameterType == typeof(string)).MakeGenericMethod(typeof(LootMasterConfig));
                        var readConfig = func.Invoke(type, BindingFlags.InvokeMethod, null, new object[] { data },null) as LootMasterConfig;
                        
                        Characters = new List<LootMasterCharacter>();
                        foreach (var rcc in readConfig?.Characters ?? new List<LootMasterCharacter>())
                        {
                            Characters.Add(new LootMasterCharacter
                            {
                                PlayerName = rcc.PlayerName,
                                Rules = rcc.Rules.Select(r => new LootRule
                                {
                                    RuleName = r.RuleName,
                                    ItemIds = r.ItemIds ?? new List<int>(),
                                    ItemNames = r.ItemNames ?? new List<string>(),
                                    Properties = r.Properties ?? new List<PropertyMatch>(),
                                    EquipmentSlots = r.EquipmentSlots ?? new List<EquipmentSlot>(),
                                    Alert = r.Alert,
                                    MinimumRarity = r.MinimumRarity,
                                    TargetBag = r.TargetBag,
                                    BlackListedProperties = r.BlackListedProperties ?? new List<ItemProperty>(),
                                    IgnoreWeightCurse = r.IgnoreWeightCurse,
                                    Disabled = r.Disabled
                                }).ToList()
                            });
                        }
                        ItemLookup = readConfig?.ItemLookup ?? new Dictionary<int, string>();
                        break;
                    }
                }
            }
            catch
            {
                // ignored
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

    public class PropertyMatch
    {
        public ItemProperty Property { get; set; }
        public int? Value { get; set; }

        public string DisplayName => $"{Property.ToString()} : {Value}";
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
    }

    public class Configurator : Form
    {
        private Target _tar = new Target();
        private List<DropDownItem> Rarities = new List<DropDownItem>();
        private List<DropDownItem> EquipmentSlots = new List<DropDownItem>();
        private List<DropDownItem> Properties = new List<DropDownItem>();
        private LootMasterConfig Config;
        private int _lastSelectedRuleIndex = -1;
        
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
            rulesList.ClearSelected();
            ruleNameTextBox.Text = string.Empty;
            rarityDropDown.SelectedIndex = 0;
            slotDropDown.SelectedIndex = 0;
            propertyDropDown.SelectedIndex = 0;
            presetDropDown.SelectedIndex = 0;
            itemNamesList.Items.Clear();
            propertiesList.Items.Clear();
            eqipmentSlotList.Items.Clear();
            weightCurseCheckbox.Checked = false;
            enabledCheckbox.Checked = true;
            ruleDownButton.Enabled = false;
            ruleUpButton.Enabled = false;
            deleteButton.Enabled = false;
            clearTargetBagButton.Enabled = false;
            
        }
        
        private void colorCorpseCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Config.ColorCorpses = colorCorpseCheckbox.Checked;
            Config.Save();
        }

        private bool DetectChanges()
        {
            if (_lastSelectedRuleIndex != -1)
            {
                var nameList = new List<string>();
                var idList = new List<int>();
                foreach (var val in itemNamesList.Items.Cast<string>())
                {
                    if (val.StartsWith("0x"))
                    {
                        var idString = val.Split('|').First().Trim();
                        var parseVal = Convert.ToInt32(idString, 16);
                        idList.Add(parseVal);
                    }
                    else
                    {
                        nameList.Add(val);
                    }
                }

                var currentValues = new LootRule
                {
                    RuleName = ruleNameTextBox.Text,
                    EquipmentSlots = eqipmentSlotList.Items.Cast<DropDownItem>().Select(x => (EquipmentSlot)x.Value).ToList(),
                    MinimumRarity = rarityDropDown.SelectedIndex == 0 ? null : (ItemRarity?)(rarityDropDown.SelectedItem as DropDownItem).Value,
                    ItemNames = nameList,
                    ItemIds = idList,
                    Properties = propertiesList.Items.Cast<PropertyMatch>().ToList(),
                    IgnoreWeightCurse = weightCurseCheckbox.Checked,
                    Alert = alertCheckbox.Checked,
                    Disabled = !enabledCheckbox.Checked
                };

                var originalRule = rulesList.Items[_lastSelectedRuleIndex] as LootRule;

                // check if currentRule and originalRule differ on any property
                return currentValues.EquipmentSlots.Count != originalRule.EquipmentSlots.Count ||
                       currentValues.EquipmentSlots.Except(originalRule.EquipmentSlots).Any() ||
                       currentValues.MinimumRarity != originalRule.MinimumRarity ||
                       currentValues.ItemNames.Count != originalRule.ItemNames.Count ||
                       currentValues.ItemNames.Except(originalRule.ItemNames).Any() ||
                       currentValues.ItemIds.Count != originalRule.ItemIds.Count ||
                       currentValues.ItemIds.Except(originalRule.ItemIds).Any() ||
                       currentValues.Properties.Count != originalRule.Properties.Count ||
                       currentValues.Properties.Except(originalRule.Properties).Any() ||
                       currentValues.IgnoreWeightCurse != originalRule.IgnoreWeightCurse ||
                       currentValues.Alert != originalRule.Alert ||
                       currentValues.Disabled != originalRule.Disabled;
            }

            return false;
        }

        private bool DetectChangeAndPromptSave(bool moving)
        {
            if (!moving && rulesList.SelectedIndex == _lastSelectedRuleIndex)
            {
                return false;
            }

            if (DetectChanges())
            {
                var result = MessageBox.Show("Save changes to rule?", "Save Changes", MessageBoxButtons.YesNoCancel);
                switch (result)
                {
                    case DialogResult.Yes:
                        SaveRule();
                        break;
                    case DialogResult.Cancel:
                        // if cancel, don't change the selected index
                        rulesList.SelectedIndex = _lastSelectedRuleIndex;
                        return false;
                }
            }

            return true;
        }

        private void rulesList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (DetectChangeAndPromptSave(false))
            {
                var rule = (LootRule)rulesList.SelectedItem;
                if (rule == null)
                {
                    return;
                }

                LoadRule(rule);
                presetDropDown.SelectedIndex = 0;
                _lastSelectedRuleIndex = rulesList.SelectedIndex;
            }
        }

        private void LoadRule(LootRule rule)
        {
            slotDropDown.SelectedIndex = 0;
            ruleNameTextBox.Text = rule.RuleName;
            rarityDropDown.SelectedIndex = rule.MinimumRarity == null ? 0 : Rarities.IndexOf(Rarities.First(r => r.Name == rule.MinimumRarity.ToString()));
            
            eqipmentSlotList.Items.Clear();
            var slotList = rule.EquipmentSlots?.OrderBy(x => x.ToString()).Select(x => new DropDownItem
            {
                Name = x.ToString(),
                Value = (int)x
            }) ?? new List<DropDownItem>();
            
            eqipmentSlotList.Items.AddRange(slotList.ToArray());
            
            itemNamesList.Items.Clear();
            
            var idList = rule.ItemIds?.OrderBy(x => x).ToList() ?? new List<int>();
            if (rule.ItemIds != null)
            {
                foreach (var itemId in rule.ItemIds)
                {
                    if (!Config.ItemLookup.ContainsKey(itemId))
                    {
                        var item = Items.FindByID(itemId, -1, -1, false, false);
                        if (item != null)
                        {
                            Config.ItemLookup.Add(itemId, item.Name.Replace(item.Amount.ToString(), string.Empty).Trim());    
                        }
                    }

                    Config.ItemLookup.TryGetValue(itemId, out var lookupName);
                    
                    itemNamesList.Items.Add($"0x{Convert.ToString(itemId, 16)} | {lookupName ?? string.Empty}");
                }
            }
            
            var nameList = rule.ItemNames ?? new List<string>();
            if (rule.ItemNames != null) itemNamesList.Items.AddRange(nameList.OrderBy(x => x).ToArray());

            propertiesList.Items.Clear();
            
            propertiesList.Items.AddRange(rule.Properties?.ToArray() ?? new List<PropertyMatch>().ToArray());
            
            weightCurseCheckbox.Checked = rule.IgnoreWeightCurse;
            alertCheckbox.Checked = rule.Alert;
            enabledCheckbox.Checked = !rule.Disabled;
            
            ruleDownButton.Enabled = true;
            ruleUpButton.Enabled = true;
            deleteButton.Enabled = true;
            clearTargetBagButton.Enabled = rule.TargetBag != null;
        }

        private void addSlotButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (!eqipmentSlotList.Items.Cast<DropDownItem>().Any(s => s.Name == (slotDropDown.SelectedItem as DropDownItem).Name))
                {
                    eqipmentSlotList.Items.Add(slotDropDown.SelectedItem);
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
                        if (!Config.ItemLookup.ContainsKey(intVal))
                        {
                            var item = Items.FindByID(intVal, -1, -1, false, false);
                            if (item != null)
                            {
                                Config.ItemLookup.Add(intVal, item.Name.Replace(item.Amount.ToString(), string.Empty).Trim());
                            }
                        }

                        Config.ItemLookup.TryGetValue(intVal, out var lookupName);
                        
                        itemNamesList.Items.Add($"0x{Convert.ToString(intVal, 16)} | {lookupName ?? string.Empty}");
                    }
                    else
                    {
                        try
                        {
                            intVal = Convert.ToInt32(itemIdAddTextBox.Text , 16);
                            if (!Config.ItemLookup.ContainsKey(intVal))
                            {
                                var item = Items.FindByID(intVal, -1, -1, false, false);
                                if (item != null)
                                {
                                    Config.ItemLookup.Add(intVal, item.Name.Replace(item.Amount.ToString(), string.Empty).Trim());
                                }
                            }

                            Config.ItemLookup.TryGetValue(intVal, out var lookupName);
                        
                            itemNamesList.Items.Add($"0x{Convert.ToString(intVal, 16)} | {lookupName ?? string.Empty}");
                        }
                        catch
                        {
                            itemNamesList.Items.Add(itemIdAddTextBox.Text);
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
                        if (!Config.ItemLookup.ContainsKey(item.ItemID))
                        {
                            Config.ItemLookup.Add(item.ItemID, item.Name.Replace(item.Amount.ToString(), string.Empty).Trim());
                        }

                        Config.ItemLookup.TryGetValue(item.ItemID, out var lookupName);
                    
                        itemNamesList.Items.Add($"0x{Convert.ToString(item.ItemID, 16)} | {lookupName ?? string.Empty}");
                    }
                }
            }
            catch
            {
                // ignored
            }
        }

        private int SaveRule()
        {
            var nameList = new List<string>();
                var idList = new List<int>();
                foreach (var val in itemNamesList.Items.Cast<string>())
                {
                    if (val.StartsWith("0x"))
                    {
                        var idString = val.Split('|').First().Trim();
                        var parseVal = Convert.ToInt32(idString, 16);
                        idList.Add(parseVal);
                    }
                    else
                    {
                        nameList.Add(val);
                    }
                }

                
                var rule = new LootRule
                {
                    RuleName = ruleNameTextBox.Text,
                    EquipmentSlots = eqipmentSlotList.Items.Cast<DropDownItem>().Select(x => (EquipmentSlot)x.Value).ToList(),
                    MinimumRarity = rarityDropDown.SelectedIndex == 0 ? null : (ItemRarity?)(rarityDropDown.SelectedItem as DropDownItem).Value,
                    ItemNames = nameList,
                    ItemIds = idList,
                    Properties = propertiesList.Items.Cast<PropertyMatch>().ToList(),
                    IgnoreWeightCurse = weightCurseCheckbox.Checked,
                    Alert = alertCheckbox.Checked,
                    Disabled = !enabledCheckbox.Checked
                };

                if (string.IsNullOrEmpty(rule.RuleName))
                {
                    MessageBox.Show("Please enter a rule name.");
                    return -1;
                }
                
                var blockSave = rule.EquipmentSlots.Count == 0 && rule.MinimumRarity == null && rule.ItemNames.Count == 0 && rule.ItemIds.Count == 0 && rule.Properties.Count == 0;
                if (blockSave)
                {
                    MessageBox.Show("This rule will match all items. Please adjust the rule to be more specific.");
                    return -1;
                }
                
                var existing =  rulesList.Items.Cast<LootRule>().FirstOrDefault(x => x.RuleName == ruleNameTextBox.Text);
                if (existing != null)
                {
                    existing.EquipmentSlots = eqipmentSlotList.Items.Cast<DropDownItem>().Select(x => (EquipmentSlot)x.Value).ToList();
                    existing.MinimumRarity = rarityDropDown.SelectedIndex == 0 ? null : (ItemRarity?)(rarityDropDown.SelectedItem as DropDownItem).Value;
                    existing.ItemNames = nameList;
                    existing.ItemIds = idList;
                    existing.Properties = propertiesList.Items.Cast<PropertyMatch>().ToList();
                    existing.IgnoreWeightCurse = weightCurseCheckbox.Checked;
                    existing.Alert = alertCheckbox.Checked;
                    existing.Disabled = !enabledCheckbox.Checked;

                }
                else
                {
                    rulesList.Items.Add(rule);
                    Config.GetCharacter().Rules.Clear();
                    Config.GetCharacter().Rules.AddRange(rulesList.Items.Cast<LootRule>());
                }

                _lastSelectedRuleIndex = -1;
                return rulesList.Items.Cast<LootRule>().ToList().IndexOf(existing ?? rule);
        }

        private void saveButton_Click(object sender, EventArgs e)
        {
            try
            {

                var index = SaveRule();
                if (index == -1)
                {
                    return;
                }
                
                rulesList.ClearSelected();
                rulesList.SelectedIndex = index;
                
                Config.Save();
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }
        
        

        private void presetDropDown_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (presetDropDown.SelectedIndex == 0)
            {
                return;
            }
            
            LootRule rule = presetDropDown.SelectedItem as LootRule;
            LoadRule(rule);
        }
        
        private void characterDropdown_SelectedIndexChanged(object sender, EventArgs e)
        {
            var name = characterDropdown.SelectedItem as string;
            if (!string.IsNullOrEmpty(name))
            {
                rulesList.Items.Clear();
                rulesList.Items.AddRange(Config.GetCharacter(name).Rules.ToArray());
            }
        }

        private void propertiesList_DoubleClick(object sender, EventArgs e)
        {
            if (propertiesList.SelectedItem is PropertyMatch propertyItem)
            {
                var propList = propertyDropDown.Items.Cast<DropDownItem>().ToList();
                propertyDropDown.SelectedIndex = propList.IndexOf(propList.First(p => p.Name == Handler.ResolvePropertyName(propertyItem.Property)));
                propertyValueTextBox.Text = propertyItem.Value?.ToString() ?? string.Empty;
            }
            else
            {
                propertyDropDown.SelectedIndex = 0;
                propertyValueTextBox.Text = string.Empty;
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

                var index = propertiesList.Items.Count;
                
                var existing = propertiesList.Items.Cast<PropertyMatch>().FirstOrDefault(x => x.Property == (ItemProperty)selectedProp.Value);
                if (existing != null)
                {
                    index = propertiesList.Items.IndexOf(existing);
                    propertiesList.Items.Remove(existing);
                }

                propertiesList.Items.Insert(index, new PropertyMatch
                {
                    Property = (ItemProperty)selectedProp.Value,
                    Value = value
                });
                propertyDropDown.SelectedIndex = 0;
                propertyValueTextBox.Text = string.Empty;

            }
            catch
            {
                // ignored
            }
        }

        private void deleteSelectedNameMenuItem_Click(object sender, EventArgs e)
        {
            var selected = itemNamesList.SelectedItem;
            if (selected != null)
            {
                itemNamesList.Items.Remove(selected);
            }
        }

        private void deleteSelectedSlotMenuItem_Click(object sender, EventArgs e)
        {
            var selected = eqipmentSlotList.SelectedItem;
            if (selected != null)
            {
                eqipmentSlotList.Items.Remove(selected);
            }
        }

        private void deleteSelectedPropertyMenuItem_Click(object sender, EventArgs e)
        {
            var selected = propertiesList.SelectedItem;
            if (selected != null)
            {
                propertiesList.Items.Remove(selected);
            }
        }

        private void deleteSelectedRuleMenuItem_Click(object sender, EventArgs e)
        {
            var selected = rulesList.SelectedItem;
            if (selected != null)
            {
                _lastSelectedRuleIndex = -1;
                rulesList.Items.Remove(selected);
            }

            Config.GetCharacter().Rules.Clear();
            Config.GetCharacter().Rules.AddRange(rulesList.Items.Cast<LootRule>());
            ClearConfig();
        }

        private void clearTargetBagButton_Click(object sender, EventArgs e)
        {
            if (rulesList.SelectedItem is LootRule selected)
            {
                selected.TargetBag = null;
                clearTargetBagButton.Enabled = selected.TargetBag != null;
            }
            
            
            Config.GetCharacter().Rules.Clear();
            Config.GetCharacter().Rules.AddRange(rulesList.Items.Cast<LootRule>());
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
                    rulesList.Items.Clear();
                    rulesList.Items.AddRange(Config.GetCharacter(name).Rules.ToArray());
                }
            }
        }

        private void moveUpSelectedRuleMenuItem_Click(object sender, EventArgs e)
        {
            if (DetectChangeAndPromptSave(true))
            {
                if (rulesList.SelectedItem is LootRule rule)
                {
                    var index = rulesList.SelectedIndex;
                    if (index == 0)
                    {
                        return;
                    }

                    _lastSelectedRuleIndex = -1;

                    rulesList.Items.Remove(rule);
                    rulesList.Items.Insert(index - 1, rule);
                    rulesList.SelectedIndex = index - 1;
                
                    _lastSelectedRuleIndex = index - 1;
                    Config.GetCharacter().Rules.Clear();
                    Config.GetCharacter().Rules.AddRange(rulesList.Items.Cast<LootRule>());
                    Config.Save();
                }
            }
        }

        private void moveDownSelectedRuleMenuItem_Click(object sender, EventArgs e)
        {
            if (rulesList.SelectedItem is LootRule rule)
            {
                var index = rulesList.SelectedIndex;
                if (index == rulesList.Items.Count - 1)
                {
                    return;
                }
                _lastSelectedRuleIndex = -1;
                
                rulesList.Items.Remove(rule);
                rulesList.Items.Insert(index + 1, rule);
                rulesList.SelectedIndex = index + 1;
                _lastSelectedRuleIndex = index + 1;
                Config.GetCharacter().Rules.Clear();
                Config.GetCharacter().Rules.AddRange(rulesList.Items.Cast<LootRule>());
                Config.Save();
            }
        }
        
        public void Open(LootMasterConfig config)
        {
            
            Config = config;
            
            characterDropdown.Items.Clear();
            characterDropdown.Items.AddRange(Config.Characters.Select(c => c.PlayerName).OrderBy(n => n).ToArray());
            characterDropdown.SelectedIndex = characterDropdown.Items.IndexOf(Player.Name);
            
            
            rulesList.Items.Clear();
            rulesList.Items.AddRange(Config.GetCharacter((string)characterDropdown.SelectedValue).Rules.ToArray());
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
                LootRule.Ammo,
                LootRule.PureElementalWeapons,
                LootRule.PureColdWeapon,
                LootRule.PureFireWeapon,
                LootRule.PureEnergyWeapon,
                LootRule.PurePoisonWeapon,
                LootRule.Slayers
            }.ToArray();

            presetDropDown.Items.Clear();
            presetDropDown.Items.AddRange(presets);
            
            
            rarityDropDown.Items.AddRange(Rarities.ToArray());
            slotDropDown.Items.AddRange(EquipmentSlots.ToArray());
            propertyDropDown.Items.AddRange(Properties.ToArray());
            
            
            
            
            rarityDropDown.SelectedIndex = 0;
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
            this.rulesList = new ListBox();
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
            this.rarityDropDown = new ComboBox();
            this.label3 = new Label();
            this.slotDropDown = new ComboBox();
            this.weightCurseCheckbox = new CheckBox();
            this.alertCheckbox = new CheckBox();
            this.enabledCheckbox = new CheckBox();
            this.settingContainer = new GroupBox();
            this.itemNameContainer = new GroupBox();
            this.slotContainer = new GroupBox();
            this.propertiesContainer = new GroupBox();
            this.idAddButton = new Button();
            this.itemIdAddTextBox = new TextBox();
            this.itemNamesList = new ListBox();
            this.eqipmentSlotList = new ListBox();
            this.propertiesList = new ListBox();
            this.propertyDropDown = new ComboBox();
            this.addPropButton = new Button();
            this.propertyValueTextBox = new TextBox();
            this.label5 = new Label();
            this.nameDropDownMenu = new ContextMenuStrip(this.components);
            this.slotDropDownMenu = new ContextMenuStrip(this.components);
            this.propertyDropDownMenu = new ContextMenuStrip(this.components);
            this.ruleDropDownMenu = new ContextMenuStrip(this.components);
            this.deleteSelectedNameMenuItem = new ToolStripMenuItem();
            this.deleteSelectedSlotMenuItem = new ToolStripMenuItem();
            this.deleteSelectedPropertyMenuItem = new ToolStripMenuItem();
            this.deleteSelectedRuleMenuItem = new ToolStripMenuItem();
            this.moveUpSelectedRuleMenuItem = new ToolStripMenuItem();
            this.moveDownSelectedRuleMenuItem = new ToolStripMenuItem();
            slotDropDown = new ComboBox();
            slotAddButton = new Button();
            
            
            this.listContainer.SuspendLayout();
            this.ruleContainer.SuspendLayout();
            this.settingContainer.SuspendLayout();
            this.itemNameContainer.SuspendLayout();
            this.slotContainer.SuspendLayout();
            this.propertiesContainer.SuspendLayout();
            this.SuspendLayout();
            // 
            // characterDropDown
            // 
            this.characterDropdown.FormattingEnabled = true;
            this.characterDropdown.Location = new System.Drawing.Point(20, 9);
            this.characterDropdown.Name = "characterDropdown";
            this.characterDropdown.Size = new System.Drawing.Size(160, 24);
            this.characterDropdown.TabIndex = 0;
            this.characterDropdown.SelectedIndexChanged += new System.EventHandler(this.characterDropdown_SelectedIndexChanged);
            // 
            // colorCorpseCheckbox
            // 
            this.colorCorpseCheckbox.AutoSize = true;
            this.colorCorpseCheckbox.Location = new System.Drawing.Point(550, 9);
            this.colorCorpseCheckbox.Name = "colorCorpseCheckbox";
            this.colorCorpseCheckbox.Size = new System.Drawing.Size(150, 18);
            this.colorCorpseCheckbox.TabIndex = 0;
            this.colorCorpseCheckbox.Text = "Color Corpses after looting";
            colorCorpseCheckbox.CheckedChanged += new EventHandler(colorCorpseCheckbox_CheckedChanged);
            // 
            // listContainer
            // 
            this.listContainer.Controls.Add(this.rulesList);
            this.listContainer.Controls.Add(this.ruleUpButton);
            this.listContainer.Controls.Add(this.ruleDownButton);
            this.listContainer.Controls.Add(this.addButton);
            this.listContainer.Controls.Add(this.deleteButton);
            this.listContainer.Controls.Add(this.clearTargetBagButton);
            this.listContainer.Location = new System.Drawing.Point(12, 36);
            this.listContainer.Name = "listContainer";
            this.listContainer.Size = new System.Drawing.Size(158, 557);
            this.listContainer.TabIndex = 2;
            this.listContainer.TabStop = false;
            this.listContainer.Text = "Current Rules";
            // 
            // rulesList
            // 
            this.rulesList.ContextMenuStrip = ruleDropDownMenu;
            this.rulesList.Dock = System.Windows.Forms.DockStyle.Top;
            this.rulesList.Location = new System.Drawing.Point(3, 19);
            this.rulesList.Name = "rulesList";
            this.rulesList.Size = new System.Drawing.Size(152, 445);
            this.rulesList.TabIndex = 0;
            this.rulesList.SelectedIndexChanged += new System.EventHandler(this.rulesList_SelectedIndexChanged);
            rulesList.DisplayMember = "RuleName";
            // 
            // ruleUpButton
            // 
            this.exportCharacterButton.Location = new System.Drawing.Point(200, 9);
            this.exportCharacterButton.Name = "exportCharacterButton";
            this.exportCharacterButton.Size = new System.Drawing.Size(85, 25);
            this.exportCharacterButton.TabIndex = 0;
            this.exportCharacterButton.Text = "Export";
            this.exportCharacterButton.UseVisualStyleBackColor = true;
            this.exportCharacterButton.Click += new System.EventHandler(exportCharacterButton_Click);
            // 
            // ruleUpButton
            // 
            this.importCharacterButton.Location = new System.Drawing.Point(315, 9);
            this.importCharacterButton.Name = "importCharacterButton";
            this.importCharacterButton.Size = new System.Drawing.Size(85, 25);
            this.importCharacterButton.TabIndex = 0;
            this.importCharacterButton.Text = "Import";
            this.importCharacterButton.UseVisualStyleBackColor = true;
            this.importCharacterButton.Click += new System.EventHandler(importCharacterButton_Click);
            // 
            // ruleUpButton
            // 
            this.ruleUpButton.Location = new System.Drawing.Point(6, 449);
            this.ruleUpButton.Name = "ruleUpButton";
            this.ruleUpButton.Size = new System.Drawing.Size(85, 25);
            this.ruleUpButton.TabIndex = 0;
            this.ruleUpButton.Text = "Move Up";
            this.ruleUpButton.UseVisualStyleBackColor = true;
            this.ruleUpButton.Click += new System.EventHandler(moveUpSelectedRuleMenuItem_Click);
            // 
            // ruleDownButton
            // 
            this.ruleDownButton.Location = new System.Drawing.Point(6, 478);
            this.ruleDownButton.Name = "ruleDownButton";
            this.ruleDownButton.Size = new System.Drawing.Size(85, 25);
            this.ruleDownButton.TabIndex = 0;
            this.ruleDownButton.Text = "Move Down";
            this.ruleDownButton.UseVisualStyleBackColor = true;
            this.ruleDownButton.Click += new System.EventHandler(moveDownSelectedRuleMenuItem_Click);
            // 
            // addButton
            // 
            this.addButton.Location = new System.Drawing.Point(93, 449);
            this.addButton.Name = "addButton";
            this.addButton.Size = new System.Drawing.Size(55, 25);
            this.addButton.TabIndex = 1;
            this.addButton.Text = "New";
            this.addButton.UseVisualStyleBackColor = true;
            this.addButton.Click += new System.EventHandler(this.addButton_Click);
            // 
            // addButton
            // 
            this.deleteButton.Location = new System.Drawing.Point(93, 479);
            this.deleteButton.Name = "deleteButton";
            this.deleteButton.Size = new System.Drawing.Size(55, 25);
            this.deleteButton.TabIndex = 1;
            this.deleteButton.Text = "Delete";
            this.deleteButton.UseVisualStyleBackColor = true;
            this.deleteButton.Click += new System.EventHandler(deleteSelectedRuleMenuItem_Click);
            // 
            // addButton
            // 
            this.clearTargetBagButton.Location = new System.Drawing.Point(6, 509);
            this.clearTargetBagButton.Name = "clearTargetBagButton";
            this.clearTargetBagButton.Size = new System.Drawing.Size(142, 25);
            this.clearTargetBagButton.TabIndex = 1;
            this.clearTargetBagButton.Text = "Clear Target Bag";
            this.clearTargetBagButton.UseVisualStyleBackColor = true;
            this.clearTargetBagButton.Click += new System.EventHandler(clearTargetBagButton_Click);
            // 
            // ruleContainer
            // 
            this.ruleContainer.Controls.Add(this.settingContainer);
            this.ruleContainer.Controls.Add(this.label1);
            this.ruleContainer.Controls.Add(this.presetDropDown);
            this.ruleContainer.Controls.Add(this.saveButton);
            this.ruleContainer.Location = new System.Drawing.Point(176, 41);
            this.ruleContainer.Name = "ruleContainer";
            this.ruleContainer.Size = new System.Drawing.Size(680, 552);
            this.ruleContainer.TabIndex = 3;
            this.ruleContainer.TabStop = false;
            this.ruleContainer.Text = "Rule Settings";
            // 
            // presetDropDown
            // 
            this.presetDropDown.FormattingEnabled = true;
            this.presetDropDown.Location = new System.Drawing.Point(51, 19);
            this.presetDropDown.Name = "presetDropDown";
            this.presetDropDown.Size = new System.Drawing.Size(416, 23);
            this.presetDropDown.TabIndex = 0;
            this.presetDropDown.SelectedIndexChanged += new System.EventHandler(this.presetDropDown_SelectedIndexChanged);
            presetDropDown.DisplayMember = "RuleName";
            // 
            // saveButton
            // 
            this.saveButton.Location = new System.Drawing.Point(478, 17);
            this.saveButton.Name = "saveButton";
            this.saveButton.Size = new System.Drawing.Size(99, 27);
            this.saveButton.TabIndex = 1;
            this.saveButton.Text = "Save Rule";
            this.saveButton.UseVisualStyleBackColor = true;
            this.saveButton.Click += new System.EventHandler(this.saveButton_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(6, 22);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(39, 15);
            this.label1.TabIndex = 1;
            this.label1.Text = "Preset";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(9, 25);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(65, 15);
            this.label2.TabIndex = 2;
            this.label2.Text = "Rule Name";
            // 
            // ruleNameTextBox
            // 
            this.ruleNameTextBox.Location = new System.Drawing.Point(80, 22);
            this.ruleNameTextBox.Name = "ruleNameTextBox";
            this.ruleNameTextBox.Size = new System.Drawing.Size(344, 23);
            this.ruleNameTextBox.TabIndex = 3;
            // 
            // rarityDropDown
            // 
            this.rarityDropDown.FormattingEnabled = true;
            this.rarityDropDown.Location = new System.Drawing.Point(80, 51);
            this.rarityDropDown.Name = "rarityDropDown";
            this.rarityDropDown.Size = new System.Drawing.Size(121, 23);
            this.rarityDropDown.TabIndex = 4;
            rarityDropDown.DisplayMember = "Name";
            rarityDropDown.ValueMember = "Value";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(9, 54);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(61, 15);
            this.label3.TabIndex = 5;
            this.label3.Text = "Min Rarity";
            // 
            // slotDropDown
            // 
            this.slotDropDown.FormattingEnabled = true;
            this.slotDropDown.Location = new System.Drawing.Point(6, 23);
            this.slotDropDown.Name = "slotDropDown";
            this.slotDropDown.Size = new System.Drawing.Size(133, 23);
            this.slotDropDown.TabIndex = 6;
            slotDropDown.DisplayMember = "Name";
            slotDropDown.ValueMember = "Value";
            // 
            // weightCurseCheckbox
            // 
            this.weightCurseCheckbox.AutoSize = true;
            this.weightCurseCheckbox.Location = new System.Drawing.Point(446, 64);
            this.weightCurseCheckbox.Name = "weightCurseCheckbox";
            this.weightCurseCheckbox.Size = new System.Drawing.Size(134, 19);
            this.weightCurseCheckbox.TabIndex = 9;
            this.weightCurseCheckbox.Text = "Loot Weighted Items";
            this.weightCurseCheckbox.UseVisualStyleBackColor = true;
            // 
            // settingContainer
            // 
            this.settingContainer.Controls.Add(this.enabledCheckbox);
            this.settingContainer.Controls.Add(this.alertCheckbox);
            this.settingContainer.Controls.Add(this.propertiesContainer);
            this.settingContainer.Controls.Add(this.slotContainer);
            this.settingContainer.Controls.Add(this.itemNameContainer);
            this.settingContainer.Controls.Add(this.ruleNameTextBox);
            this.settingContainer.Controls.Add(this.weightCurseCheckbox);
            this.settingContainer.Controls.Add(this.label2);
            this.settingContainer.Controls.Add(this.rarityDropDown);
            this.settingContainer.Controls.Add(this.label3);
            this.settingContainer.Controls.Add(this.slotDropDown);
            this.settingContainer.Location = new System.Drawing.Point(6, 48);
            this.settingContainer.Name = "settingContainer";
            this.settingContainer.Size = new System.Drawing.Size(665, 498);
            this.settingContainer.TabIndex = 10;
            this.settingContainer.TabStop = false;
            this.settingContainer.Text = "Settings";
            // 
            // itemNameContainer
            // 
            this.itemNameContainer.Controls.Add(this.itemNamesList);
            this.itemNameContainer.Controls.Add(this.itemIdAddTextBox);
            this.itemNameContainer.Controls.Add(this.idAddButton);
            this.itemNameContainer.Location = new System.Drawing.Point(9, 80);
            this.itemNameContainer.Name = "itemNameContainer";
            this.itemNameContainer.Size = new System.Drawing.Size(192, 412);
            this.itemNameContainer.TabIndex = 10;
            this.itemNameContainer.TabStop = false;
            this.itemNameContainer.Text = "Names / Id\'s";
            // 
            // slotContainer
            // 
            this.slotContainer.Controls.Add(this.slotDropDown);
            this.slotContainer.Controls.Add(this.slotAddButton);
            this.slotContainer.Controls.Add(this.eqipmentSlotList);
            this.slotContainer.Location = new System.Drawing.Point(209, 80);
            this.slotContainer.Name = "slotContainer";
            this.slotContainer.Size = new System.Drawing.Size(206, 412);
            this.slotContainer.TabIndex = 11;
            this.slotContainer.TabStop = false;
            this.slotContainer.Text = "Equipment Slots";
            // 
            // propertiesContainer
            // 
            this.propertiesContainer.Controls.Add(this.label5);
            this.propertiesContainer.Controls.Add(this.propertyValueTextBox);
            this.propertiesContainer.Controls.Add(this.addPropButton);
            this.propertiesContainer.Controls.Add(this.propertyDropDown);
            this.propertiesContainer.Controls.Add(this.propertiesList);
            this.propertiesContainer.Location = new System.Drawing.Point(421, 80);
            this.propertiesContainer.Name = "propertiesContainer";
            this.propertiesContainer.Size = new System.Drawing.Size(231, 412);
            this.propertiesContainer.TabIndex = 12;
            this.propertiesContainer.TabStop = false;
            this.propertiesContainer.Text = "Properties";
            // 
            // idAddButton
            // 
            this.idAddButton.Location = new System.Drawing.Point(130, 22);
            this.idAddButton.Name = "idAddButton";
            this.idAddButton.Size = new System.Drawing.Size(56, 25);
            this.idAddButton.TabIndex = 0;
            this.idAddButton.Text = "Add";
            this.idAddButton.UseVisualStyleBackColor = true;
            this.idAddButton.Click += new System.EventHandler(this.idAddButton_Click);
            // 
            // itemIdAddTextBox
            // 
            this.itemIdAddTextBox.Location = new System.Drawing.Point(6, 22);
            this.itemIdAddTextBox.Name = "itemIdAddTextBox";
            this.itemIdAddTextBox.Size = new System.Drawing.Size(118, 23);
            this.itemIdAddTextBox.TabIndex = 1;
            // 
            // slotAddButton
            // 
            this.slotAddButton.Location = new System.Drawing.Point(142, 22);
            this.slotAddButton.Name = "addNameButton";
            this.slotAddButton.Size = new System.Drawing.Size(56, 25);
            this.slotAddButton.TabIndex = 2;
            this.slotAddButton.Text = "Add";
            this.slotAddButton.UseVisualStyleBackColor = true;
            this.slotAddButton.Click += new System.EventHandler(this.addSlotButton_Click);
            // 
            // itemNamesList
            // 
            this.itemNamesList.ContextMenuStrip = nameDropDownMenu;
            this.itemNamesList.FormattingEnabled = true;
            this.itemNamesList.ItemHeight = 15;
            this.itemNamesList.Location = new System.Drawing.Point(6, 55);
            this.itemNamesList.Name = "itemNamesList";
            this.itemNamesList.Size = new System.Drawing.Size(180, 349);
            this.itemNamesList.TabIndex = 4;
            // 
            // eqipmentSlotList
            // 
            this.eqipmentSlotList.ContextMenuStrip = slotDropDownMenu;
            this.eqipmentSlotList.FormattingEnabled = true;
            this.eqipmentSlotList.ItemHeight = 15;
            this.eqipmentSlotList.Location = new System.Drawing.Point(6, 55);
            this.eqipmentSlotList.Name = "eqipmentSlotList";
            this.eqipmentSlotList.Size = new System.Drawing.Size(190, 349);
            this.eqipmentSlotList.TabIndex = 4;
            eqipmentSlotList.DisplayMember = "Name";
            // 
            // propertiesList
            // 
            this.propertiesList.ContextMenuStrip = propertyDropDownMenu;
            this.propertiesList.FormattingEnabled = true;
            this.propertiesList.ItemHeight = 15;
            this.propertiesList.Location = new System.Drawing.Point(7, 80);
            this.propertiesList.Name = "propertiesList";
            this.propertiesList.Size = new System.Drawing.Size(215, 319);
            this.propertiesList.TabIndex = 0;
            propertiesList.DisplayMember = "DisplayName";
            propertiesList.DoubleClick += new System.EventHandler(propertiesList_DoubleClick);
            // 
            // comboBox1
            // 
            this.propertyDropDown.FormattingEnabled = true;
            this.propertyDropDown.Location = new System.Drawing.Point(6, 22);
            this.propertyDropDown.Name = "propertyDropDown";
            this.propertyDropDown.Size = new System.Drawing.Size(154, 23);
            this.propertyDropDown.TabIndex = 1;
            propertyDropDown.DisplayMember = "Name";
            propertyDropDown.ValueMember = "Value";
            // 
            // addPropButton
            // 
            this.addPropButton.Location = new System.Drawing.Point(166, 21);
            this.addPropButton.Name = "addPropButton";
            this.addPropButton.Size = new System.Drawing.Size(56, 25);
            this.addPropButton.TabIndex = 3;
            this.addPropButton.Text = "Add";
            this.addPropButton.UseVisualStyleBackColor = true;
            this.addPropButton.Click += new System.EventHandler(this.addPropButton_Click);
            // 
            // textBox1
            // 
            this.propertyValueTextBox.Location = new System.Drawing.Point(110, 51);
            this.propertyValueTextBox.Name = "propertyValueTextBox";
            this.propertyValueTextBox.Size = new System.Drawing.Size(50, 23);
            this.propertyValueTextBox.TabIndex = 5;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(6, 55);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(59, 15);
            this.label5.TabIndex = 13;
            this.label5.Text = "Min Value";
            // 
            // nameDropDownMenu
            // 
            this.nameDropDownMenu.Items.AddRange(new ToolStripItem[] {
            this.deleteSelectedNameMenuItem});
            this.nameDropDownMenu.Name = "nameDropDownMenu";
            this.nameDropDownMenu.Size = new System.Drawing.Size(181, 48);
            // 
            // idDropDownMenu
            // 
            this.slotDropDownMenu.Items.AddRange(new ToolStripItem[] {
            this.deleteSelectedSlotMenuItem});
            this.slotDropDownMenu.Name = "slotDropDownMenu";
            this.slotDropDownMenu.Size = new System.Drawing.Size(155, 26);
            // 
            // propertyDropDownMenu
            // 
            this.propertyDropDownMenu.Items.AddRange(new ToolStripItem[] {
            this.deleteSelectedPropertyMenuItem});
            this.propertyDropDownMenu.Name = "propertyDropDownMenu";
            this.propertyDropDownMenu.Size = new System.Drawing.Size(155, 26);
            // 
            // propertyDropDownMenu
            // 
            this.ruleDropDownMenu.Items.AddRange(new ToolStripItem[] {moveUpSelectedRuleMenuItem,
                moveDownSelectedRuleMenuItem,
                deleteSelectedRuleMenuItem});
            this.ruleDropDownMenu.Name = "propertyDropDownMenu";
            this.ruleDropDownMenu.Size = new System.Drawing.Size(155, 26);
            // 
            // deleteSelectedToolStripMenuItem
            // 
            this.deleteSelectedNameMenuItem.Name = "deleteSelectedNameMenuItem";
            this.deleteSelectedNameMenuItem.Size = new System.Drawing.Size(180, 22);
            this.deleteSelectedNameMenuItem.Text = "Delete Selected";
            this.deleteSelectedNameMenuItem.Click += new System.EventHandler(this.deleteSelectedNameMenuItem_Click);
            // 
            // deleteSelectedToolStripMenuItem1
            // 
            this.deleteSelectedSlotMenuItem.Name = "deleteSelectedSlotMenuItem";
            this.deleteSelectedSlotMenuItem.Size = new System.Drawing.Size(154, 22);
            this.deleteSelectedSlotMenuItem.Text = "Delete Selected";
            this.deleteSelectedSlotMenuItem.Click += new System.EventHandler(this.deleteSelectedSlotMenuItem_Click);
            // 
            // deleteSelectedToolStripMenuItem2
            // 
            this.deleteSelectedPropertyMenuItem.Name = "deleteSelectedPropertyMenuItem";
            this.deleteSelectedPropertyMenuItem.Size = new System.Drawing.Size(154, 22);
            this.deleteSelectedPropertyMenuItem.Text = "Delete Selected";
            this.deleteSelectedPropertyMenuItem.Click += new System.EventHandler(this.deleteSelectedPropertyMenuItem_Click);
            // 
            // deleteSelectedRuleMenuItem
            // 
            this.deleteSelectedRuleMenuItem.Name = "deleteSelectedRuleMenuItem";
            this.deleteSelectedRuleMenuItem.Size = new System.Drawing.Size(154, 22);
            this.deleteSelectedRuleMenuItem.Text = "Delete Selected";
            this.deleteSelectedRuleMenuItem.Click += new System.EventHandler(this.deleteSelectedRuleMenuItem_Click);
            // 
            // deleteSelectedRuleMenuItem
            // 
            this.moveUpSelectedRuleMenuItem.Name = "moveUpSelectedRuleMenuItem";
            this.moveUpSelectedRuleMenuItem.Size = new System.Drawing.Size(154, 22);
            this.moveUpSelectedRuleMenuItem.Text = "Move Up";
            this.moveUpSelectedRuleMenuItem.Click += new System.EventHandler(this.moveUpSelectedRuleMenuItem_Click);
            // 
            // deleteSelectedRuleMenuItem
            // 
            this.moveDownSelectedRuleMenuItem.Name = "moveDownSelectedRuleMenuItem";
            this.moveDownSelectedRuleMenuItem.Size = new System.Drawing.Size(154, 22);
            this.moveDownSelectedRuleMenuItem.Text = "Move Down";
            this.moveDownSelectedRuleMenuItem.Click += new System.EventHandler(this.moveDownSelectedRuleMenuItem_Click);
            // 
            // alertCheckbox
            // 
            this.alertCheckbox.AutoSize = true;
            this.alertCheckbox.Location = new System.Drawing.Point(446, 44);
            this.alertCheckbox.Name = "alertCheckbox";
            this.alertCheckbox.Size = new System.Drawing.Size(137, 19);
            this.alertCheckbox.TabIndex = 13;
            this.alertCheckbox.Text = "Notify When Looting";
            this.alertCheckbox.UseVisualStyleBackColor = true;
            // 
            // alertCheckbox
            // 
            this.enabledCheckbox.AutoSize = true;
            this.enabledCheckbox.Location = new System.Drawing.Point(446, 24);
            this.enabledCheckbox.Name = "enabledCheckbox";
            this.enabledCheckbox.Size = new System.Drawing.Size(137, 19);
            this.enabledCheckbox.TabIndex = 13;
            this.enabledCheckbox.Text = "Enabled";
            this.enabledCheckbox.UseVisualStyleBackColor = true;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(855, 605);
            this.Controls.Add(this.ruleContainer);
            this.Controls.Add(this.listContainer);
            this.Controls.Add(this.characterDropdown);
            this.Controls.Add(this.exportCharacterButton);
            this.Controls.Add(this.importCharacterButton);
            this.Controls.Add(this.colorCorpseCheckbox);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "LootmasterConfigurator";
            this.ShowIcon = false;
            this.Text = "Lootmaster Configurator";
            this.listContainer.ResumeLayout(false);
            this.ruleContainer.ResumeLayout(false);
            this.ruleContainer.PerformLayout();
            this.settingContainer.ResumeLayout(false);
            this.settingContainer.PerformLayout();
            this.itemNameContainer.ResumeLayout(false);
            this.itemNameContainer.PerformLayout();
            this.slotContainer.ResumeLayout(false);
            this.slotContainer.PerformLayout();
            this.propertiesContainer.ResumeLayout(false);
            this.propertiesContainer.PerformLayout();
            
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private CheckBox colorCorpseCheckbox;
        private ComboBox characterDropdown;
        private Button exportCharacterButton;
        private Button importCharacterButton;
        private GroupBox listContainer;
        private ListBox rulesList;
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
        private ListBox itemNamesList;
        private ListBox eqipmentSlotList;
        private TextBox ruleNameTextBox;
        private CheckBox weightCurseCheckbox;
        private Label label2;
        private ComboBox rarityDropDown;
        private Label label3;
        private ComboBox slotDropDown;
        private Label label1;
        private ComboBox presetDropDown;
        private Label label5;
        private TextBox propertyValueTextBox;
        private Button addPropButton;
        private ComboBox propertyDropDown;
        private ListBox propertiesList;
        private ContextMenuStrip nameDropDownMenu;
        private ContextMenuStrip slotDropDownMenu;
        private ContextMenuStrip propertyDropDownMenu;
        private ContextMenuStrip ruleDropDownMenu;
        private ToolStripMenuItem deleteSelectedNameMenuItem;
        private ToolStripMenuItem deleteSelectedSlotMenuItem;
        private ToolStripMenuItem deleteSelectedPropertyMenuItem;
        private ToolStripMenuItem deleteSelectedRuleMenuItem;
        private ToolStripMenuItem moveUpSelectedRuleMenuItem;
        private ToolStripMenuItem moveDownSelectedRuleMenuItem;
        private CheckBox alertCheckbox;
        private CheckBox enabledCheckbox;

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
        Looting = 72
    }
}