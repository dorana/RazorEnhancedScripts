using System;
using System.Collections.Generic;
using System.Linq;
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
            {"Mysticism", 120},
            {"Spellweaving", 0}
        };
        

        private Dictionary<string, List<SpellSkill>> _castHolder = new Dictionary<string, List<SpellSkill>>();

        public void Run()
        {
            Setup();
            UpdateGump("");
            foreach (var caster in _castHolder)
            {
                TrainSkill(caster.Key);
            }

            UpdateGump("", true);
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

                UpdateGump(casterKey, false);
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

        private void UpdateGump(string current, bool finished = false)
        {
            var bar = Gumps.CreateGump();
            bar.buttonid = -1;
            bar.gumpId = 1239862396;
            bar.serial = (uint)Player.Serial;
            bar.x = 500;
            bar.y = 500;
            
            var schooltToTrain = _spellSchools.Where(ss => ss.Value > 0).ToList();
            
            Gumps.AddBackground(ref bar, 0, 0, 300, 50+(schooltToTrain.Count()*50), 1755);
            Gumps.AddLabel(ref bar,10,10,203, "Caster Training");

            var index = 0;
            foreach (var school in schooltToTrain)
            {
                var currentSkill = Player.GetRealSkillValue(school.Key);
                var cap = school.Value;
                var crystal = school.Key == current ? 2152 : (currentSkill >= cap ? 5826 : 5832);
                Gumps.AddImage(ref bar,10,30+(index*35),crystal);
                Gumps.AddLabel(ref bar,60,35+(index*35),203, $"{school.Key} - {currentSkill}/{cap}");
                index++;
            }

            if (finished)
            {
                Gumps.AddLabel(ref bar,75,35+(index*35),704, $"ALL DONE!");
            }
            
            Gumps.CloseGump(1239862396);
            Gumps.SendGump(bar, 500, 500);
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
                _castHolder.Add("Chivalry", spellList);
            }
            if(_spellSchools["Spellweaving"] > 0)
            {
                var spellList = new List<SpellSkill>();
                spellList.Add(new SpellSkill {SkillLevel = 20, SpellName = "Arcane Circle"});
                spellList.Add(new SpellSkill {SkillLevel = 33, SpellName = "Immolating Weapon"});
                spellList.Add(new SpellSkill {SkillLevel = 44, SpellName = "Reaper Form", DoNotEndOnBuff = "Reaper Form"});
                spellList.Add(new SpellSkill {SkillLevel = 55, SpellName = "Summon Fey"});
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