using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Xml.Schema;
using RazorEnhanced;

namespace RazorScripts
{
    public class CasterTrain
    {
        private Mobile _player;
        
        private Dictionary<string, int> _spellSchools = new Dictionary<string, int>
        {
            {"Magery", 0},
            {"Necromancy", 0},
            {"Chivalry", 0},
            {"Mysticism", 0},
            {"Spellweaving", 0}
        };
        private uint _gumpId = 1239862396;
        private bool _running = false;
        private Item _targetWeapon = null;
        

        private Dictionary<string, List<SpellSkill>> _castHolder = new Dictionary<string, List<SpellSkill>>();

        public void Run()
        {
            try
            {
                UpdateGump("");
                while (!_running)
                {
                    var reply = Gumps.GetGumpData(_gumpId);
                    if (reply.buttonid == 1)
                    {
                        var changes = new Dictionary<string, int>();
                        var index = 0;
                        foreach (var sc in _spellSchools)
                        {
                            var valueString = reply.text[index];
                            if (int.TryParse(valueString, out var value))
                            {
                                changes[sc.Key] = value;
                            }

                            index++;
                        }

                        foreach (var change in changes)
                        {
                            _spellSchools[change.Key] = change.Value;
                        }

                        UpdateGump("");
                        reply.buttonid = -1;
                        _running = true;
                    }

                    Misc.Pause(500);
                }


                Setup();
                
                if(_spellSchools["Chivalry"] > 0)
                {
                    var currentSkill = Player.GetSkillValue("Chivalry");
                    if (currentSkill < 45)
                    {
                        Misc.SendMessage("You need to have a weapon to train Chivalry, please target one in your backpack", 0x22);
                        var tar = new Target();
                        int tarSerial = 0;
                        while (tarSerial == 0)
                        {
                            tarSerial = tar.PromptTarget("Select Weapon");
                        }
                        var tarItem = Items.FindBySerial(tarSerial);
                        
                        _targetWeapon = tarItem;
                    }
                }
                if(_spellSchools["Spellweaving"] > 20 && _targetWeapon == null)
                {
                    var currentSkill = Player.GetSkillValue("Spellweaving");
                    if (currentSkill < 44)
                    {
                        Misc.SendMessage("You need to have a weapon to train Spellweaving, please target one in your backpack", 0x22);
                        var tar = new Target();
                        int tarSerial = 0;
                        while (tarSerial == 0)
                        {
                            tarSerial = tar.PromptTarget("Select Weapon");
                        }
                        var tarItem = Items.FindBySerial(tarSerial);
                        
                        _targetWeapon = tarItem;
                    }
                }

                foreach (var caster in _castHolder)
                {
                    var needsWeapon = true;
                    if(caster.Key == "Spellweaving")
                    {
                        if(Player.GetSkillValue("Spellweaving") >= 44)
                        {
                            needsWeapon = false;
                        }
                        if (needsWeapon)
                        {
                            Player.EquipItem(_targetWeapon);
                            Misc.Pause(500);
                        }
                    }
                    if(caster.Key == "Chivalry")
                    {
                        if(Player.GetSkillValue("Chivalry") >= 45)
                        {
                            needsWeapon = false;
                        }
                        if (needsWeapon)
                        {
                            Player.EquipItem(_targetWeapon);
                            Misc.Pause(500);
                        }
                    }
                    
                    TrainSkill(caster.Key);
                    UpdateGump("");
                }
            }
            catch (ThreadAbortException)
            {
                Gumps.CloseGump(_gumpId);
            }
            catch (Exception e)
            {
                Misc.SendMessage(e);
                throw;
            }
        }

        private Action<string,Mobile,bool> GetCastFunction(string casterKey)
        {
            switch (casterKey)
            {
                case "Magery":
                    return Spells.CastMagery;
                case "Necromancy":
                    return Spells.CastNecro;
                case "Chivalry":
                    return Spells.CastChivalry;
                case "Mysticism":
                    return Spells.CastMysticism;
                case "Spellweaving":
                    return Spells.CastSpellweaving;
                default:
                    return null;
            }
        }

        private void TrainSkill(string casterKey)
        {
            var skillList = _castHolder[casterKey];
            var skillCap = _spellSchools[casterKey];
            var skill = Player.GetRealSkillValue(casterKey);
            if (skill >= skillCap)
            {
                return;
            }

            var castFunc = GetCastFunction(casterKey);
            while (skill <= skillCap)
            {
                skillCap = _spellSchools[casterKey];
                skill = Player.GetRealSkillValue(casterKey);
                if (_player.Hits < 30)
                {
                    while (_player.Hits < Player.HitsMax)
                    {
                        var magerySkill = Player.GetRealSkillValue("Magery");
                        if (magerySkill >= 50)
                        {
                            CheckMana();
                            Spells.CastMagery("Greater Heal", _player);
                            Misc.Pause(4000);
                            continue;
                        }

                        if (magerySkill >= 30)
                        {
                            CheckMana();
                            Spells.CastMagery("Heal", _player);
                            Misc.Pause(4000);
                            continue;
                        }

                        var chivalrySkill = Player.GetRealSkillValue("Chivalry");
                        if (chivalrySkill >= 30)
                        {
                            CheckMana();
                            Spells.CastChivalry("Close Wounds", _player);
                            Misc.Pause(4000);
                            continue;
                        }

                        var spiritSpeakSkill = Player.GetRealSkillValue("Spirit Speak");
                        if (spiritSpeakSkill >= 30)
                        {
                            Player.UseSkill("Spirit Speak");
                            Misc.Pause(7000);
                            continue;
                        }

                        var bandages = _player.Backpack.Contains.FirstOrDefault(i => i.ItemID == 0x0E21);
                        if (bandages != null)
                        {
                            Items.UseItem(bandages, _player);
                            Misc.Pause(7000);
                            continue;
                        }
                    }
                }

                UpdateGump(casterKey);
                if (skill >= skillCap)
                {
                    break;
                }

                CheckMana();


                foreach (var spell in skillList)
                {
                    if (skill < spell.SkillLevel)
                    {
                        castFunc.Invoke(spell.SpellName, _player, true);
                        Misc.Pause(spell.WaitTime);
                        UpdateGump(casterKey);
                        break;
                    }
                }
            }
        }

        private void CheckMana()
        {
            if (Player.Mana < 30)
            {
                while (Player.Mana < Player.ManaMax)
                {
                    if (Player.Buffs.Any(b => b.Contains("Medit")))
                    {
                        while (Player.Mana < Player.ManaMax)
                        {
                            Misc.Pause(1000);
                        }
                    }
                    else
                    {
                        Player.UseSkill("Mediation");
                        Misc.Pause(3000);
                    }
                }
            }
        }

        private void UpdateGump(string current)
        {
            var schoolstToTrain = _spellSchools.Where(ss => ss.Value > 0).ToList();
            var gump = Gumps.CreateGump();
            gump.buttonid = -1;
            gump.gumpId = _gumpId;
            gump.serial = (uint)Player.Serial;
            gump.x = 500;
            gump.y = 500;
            
            var height = schoolstToTrain.Any() ? 35 + (schoolstToTrain.Count() * 35) : 100 + (_spellSchools.Count() * 35);
            
            Gumps.AddBackground(ref gump, 0, 0, 200, height, 1755);
            Gumps.AddLabel(ref gump,10,10,0x7b, "Caster Training by Dorana");

            var index = 0;
            if (schoolstToTrain.Any())
            {
                foreach (var school in schoolstToTrain)
                {
                    var currentSkill = Player.GetRealSkillValue(school.Key);
                    var cap = school.Value;
                    var crystal = school.Key == current ? 2152 : (currentSkill >= cap ? 5826 : 5832);
                    Gumps.AddImage(ref gump, 10, 30 + (index * 35), crystal);
                    Gumps.AddLabel(ref gump, 45, 35 + (index * 35), 203, $"{school.Key} - {currentSkill}/{cap}");
                    index++;
                }
            }
            else
            {
                Gumps.AddLabel(ref gump,15,30,0x7f, $"Skill name");
                Gumps.AddLabel(ref gump,100,30,0x7f, $"Target Skill");
                foreach (var school in _spellSchools)
                {
                    var name = school.Key;
                    var value = school.Value;
                    Gumps.AddLabel(ref gump,15,55+(index*35),0x7b, $"{name}");
                    Gumps.AddImageTiled(ref gump, 100, 59+(index*35), 75, 16,1803);
                    Gumps.AddTextEntry(ref gump, 100,59+(index*35),75,32,0x16a,index+1,value > 0 ? value.ToString() : "");
                    index++;
                }
                Gumps.AddButton(ref gump, 100,height-50, 247,248, 1,1,1);
            }
            Gumps.CloseGump(1239862396);
            Gumps.SendGump(gump, 500, 500);
        }

        private void Setup()
        {
            _player = Mobiles.FindBySerial(Player.Serial);
            if (_spellSchools["Magery"] > 0)
            {
                var spellList = new List<SpellSkill>();
                spellList.Add(new SpellSkill {SkillLevel = 45, SpellName = "Fireball",});
                spellList.Add(new SpellSkill {SkillLevel = 55, SpellName = "Lightning",});
                spellList.Add(new SpellSkill {SkillLevel = 65, SpellName = "Paralyse",});
                spellList.Add(new SpellSkill {SkillLevel = 75, SpellName = "Reveal",});
                spellList.Add(new SpellSkill {SkillLevel = 90, SpellName = "Flame Strike",});
                spellList.Add(new SpellSkill {SkillLevel = 120, SpellName = "Earthquake",WaitTime = 5000});
                _castHolder.Add("Magery", spellList);
            }
            if (_spellSchools["Mysticism"] > 0)
            {
                var spellList = new List<SpellSkill>();
                spellList.Add(new SpellSkill {SkillLevel = 60, SpellName = "Stone Form"});
                spellList.Add(new SpellSkill {SkillLevel = 80, SpellName = "Cleansing Winds"});
                spellList.Add(new SpellSkill {SkillLevel = 95, SpellName = "Hail Storm"});
                spellList.Add(new SpellSkill {SkillLevel = 120, SpellName = "Nether Cyclone"});
                _castHolder.Add("Mysticism", spellList);
            }
            if(_spellSchools["Necromancy"] > 0)
            {
                var spellList = new List<SpellSkill>();
                spellList.Add(new SpellSkill {SkillLevel = 50, SpellName = "Pain Spike"});
                spellList.Add(new SpellSkill {SkillLevel = 70, SpellName = "Horrific Beast", DoNotEndOnBuff = "Horrific Beast"});
                spellList.Add(new SpellSkill {SkillLevel = 90, SpellName = "Wither"});
                spellList.Add(new SpellSkill {SkillLevel = 120, SpellName = "Vampiric Embrace"});
                _castHolder.Add("Necromancy", spellList);
            }
            if(_spellSchools["Chivalry"] > 0)
            {
                var spellList = new List<SpellSkill>();
                spellList.Add(new SpellSkill {SkillLevel = 45, SpellName = "Consecrate Weapon"});
                spellList.Add(new SpellSkill {SkillLevel = 60, SpellName = "Divine Fury"});
                spellList.Add(new SpellSkill {SkillLevel = 70, SpellName = "Enemy of One"});
                spellList.Add(new SpellSkill {SkillLevel = 90, SpellName = "Holy Light"});
                spellList.Add(new SpellSkill {SkillLevel = 120, SpellName = "Noble Sacrifice"});
                _castHolder.Add("Chivalry", spellList);
            }
            if(_spellSchools["Spellweaving"] > 0)
            {
                var spellList = new List<SpellSkill>();
                spellList.Add(new SpellSkill {SkillLevel = 20, SpellName = "Arcane Circle"});
                spellList.Add(new SpellSkill {SkillLevel = 33, SpellName = "Immolating Weapon", WaitTime = 9000});
                spellList.Add(new SpellSkill {SkillLevel = 52, SpellName = "Reaper Form", DoNotEndOnBuff = "Reaper Form"});
                // spellList.Add(new SpellSkill {SkillLevel = 55, SpellName = "Summon Fey"});
                spellList.Add(new SpellSkill {SkillLevel = 74, SpellName = "Essence of Wind"});
                spellList.Add(new SpellSkill {SkillLevel = 90, SpellName = "Wildfire", WaitTime = 3000});
                spellList.Add(new SpellSkill {SkillLevel = 120, SpellName = "Word of Death"});
                _castHolder.Add("Spellweaving", spellList);
            }
        }
    }

    public class SpellSkill
    {
        public int SkillLevel { get; set; }
        public string SpellName { get; set; }
        public int WaitTime { get; set; } = 4000;
        public string DoNotEndOnBuff { get; set; }
    }
}