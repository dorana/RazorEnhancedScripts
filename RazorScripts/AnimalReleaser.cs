using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using RazorEnhanced;

namespace RazorScripts
{
    public class AnimalReleaser
    {
        public void Run()
        {
            List<int> _friendlies =  Mobiles.ApplyFilter(new Mobiles.Filter
            {
                RangeMax = 100,
                RangeMin = 0,
                Notorieties = new List<byte> { 1,2},
            }).Select(m => m.Serial).ToList();
            
            while (true)
            {
                var friends = Mobiles.ApplyFilter(new Mobiles.Filter
                {
                    RangeMax = 100,
                    RangeMin = 0,
                    Notorieties = new List<byte> { 1,2},
                });
                
                friends.ForEach(f => Mobiles.WaitForProps(f, 3000));
                var pets = friends.Where(f => f.Properties.Any(p => p.Number == 502006));
                var newFriendsList = new List<int>();
                // check if any pet.Setial is ot in the _friendlies list
                foreach (var pet in pets)
                {
                    if (!_friendlies.Contains(pet.Serial))
                    {
                        Misc.WaitForContext(pet, 500);
                        Misc.PetRename(pet, "Tamed");
                        Misc.Pause(100);
                        Misc.ContextReply(pet, 9);
                        var ids = Gumps.AllGumpIDs();
                        var foundGunp = 0;
                        while(foundGunp == 0)
                        {
                            var newIds = Gumps.AllGumpIDs();
                            //get Id's not earlier existing
                            var diff = newIds.Except(ids).ToList();

                            foreach (var gid in diff)
                            {
                                var lines = Gumps.GetLineList(gid);
                                if (lines.Any(l => l.Contains("release your pet")))
                                {
                                    foundGunp = (int)gid;
                                    break;
                                }
                            }
                            Misc.Pause(50);
                        }
                        Gumps.SendAction((uint)foundGunp,2);
                        Misc.Pause(200);
                    }
                    newFriendsList.Add(pet.Serial);
                }
                
                _friendlies = newFriendsList;
                Misc.Pause(1000);
            }
        }
    }
}