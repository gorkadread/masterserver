namespace MasterServer
{
    internal class Hero
    {
        string _name;
        int _level = 1;
        HeroClass _heroClass;
        int _id;
        float _baseSpeed = 1;
        float _baseAttackSpeed = 1;
        int _cost = 1;
        int _fullHealth = 100;
        int _currentHealth;

        public enum HeroClass {
            Warrior,
            Rogue,
            Priest,
            Mage
        }


        public string Name {
            get {
                return _name;
            }

            set {
                _name = value;
            }
        }

        public int Level {
            get {
                return _level;
            }

            set {
                _level = value;
            }
        }

        public int Id {
            get {
                return _id;
            }

            set {
                _id = value;
            }
        }

        public float BaseSpeed { get => _baseSpeed; set => _baseSpeed = value; }
        public float BaseAttackSpeed { get => _baseAttackSpeed; set => _baseAttackSpeed = value; }
        public int Cost { get => _cost; set => _cost = value; }
        public HeroClass HeroClass1 { get => _heroClass; set => _heroClass = value; }
        public int FullHealth { get => _fullHealth; set => _fullHealth = value; }
        public int CurrentHealth { get => _currentHealth; set => _currentHealth = value; }

        public Hero(string name, int id, HeroClass hClass)
        {
            Name = name;
            Id = id;
            HeroClass1 = hClass;
            CurrentHealth = FullHealth;
        }
    }
}