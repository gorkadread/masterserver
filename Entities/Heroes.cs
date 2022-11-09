using System;
using System.Collections.Generic;

namespace MasterServer
{
    public class Heroes
    {
        internal List<Hero> allHeroes = new List<Hero>();

        // The heroes should be from a database, but for now we set them up here
        void addHeroes(int numberOfHeroes)
        {
            for (int i = 0; i < numberOfHeroes - 1; i++)
            {
                Random r = new Random();
                int j = r.Next(0, 3);
                allHeroes.Add(new Hero("Conan_" + i, i + 1, (Hero.HeroClass)j));
            }
        }

        public Heroes(int numberOfAvailableHeroes)
        {
            addHeroes(numberOfAvailableHeroes);
        }
    }
}
    
