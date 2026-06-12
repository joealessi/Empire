using System;
using System.Collections.Generic;
using System.Linq;

namespace EmpireGame.Services
{
    /// <summary>
    /// Maps legendary commanders to their historically themed city name lists.
    /// Each player is assigned one commander at game start; their structures are
    /// automatically named from that commander's list in order.
    /// </summary>
    public static class CommanderCityNames
    {
        // Commander name → ordered city list (first entry is the "capital" name)
        public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> All =
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Alexander"]     = new[] { "Pella", "Alexandria", "Babylon", "Persepolis", "Memphis", "Susa", "Ecbatana", "Gordium", "Tyre", "Issus" },
            ["Caesar"]        = new[] { "Rome", "Ravenna", "Ariminum", "Massilia", "Alesia", "Alexandria", "Pharsalus", "Utica", "Tarraco", "Londinium" },
            ["Napoleon"]      = new[] { "Paris", "Ajaccio", "Toulon", "Milan", "Vienna", "Austerlitz", "Jena", "Moscow", "Waterloo", "Elba" },
            ["Genghis Khan"]  = new[] { "Karakorum", "Avarga", "Burkhan Khaldun", "Bukhara", "Samarkand", "Nishapur", "Merv", "Urgench", "Zhongdu", "Yinchuan" },
            ["Attila"]        = new[] { "Budapest", "Szeged", "Aquincum", "Sirmium", "Margus", "Naissus", "Metz", "Orléans", "Châlons", "Ravenna" },
            ["Hannibal"]      = new[] { "Carthage", "Cartagena", "Saguntum", "Cannae", "Capua", "Tarentum", "Zama", "Gades", "Syracuse", "Croton" },
            ["Patton"]        = new[] { "Fort Knox", "San Gabriel", "Boston", "Casablanca", "Palermo", "Messina", "Avranches", "Bastogne", "Metz", "Hammelburg" },
            ["Leonidas"]      = new[] { "Sparta", "Thermopylae", "Corinth", "Athens", "Plataea", "Tegea", "Olympia", "Argos", "Mystras", "Lacedaemon" },
            ["Washington"]    = new[] { "Mount Vernon", "Philadelphia", "New York", "Boston", "Trenton", "Princeton", "Valley Forge", "Yorktown", "Alexandria", "Annapolis" },
            ["Eisenhower"]    = new[] { "Abilene", "Gettysburg", "Washington", "London", "Paris", "Reims", "Normandy", "Kansas City", "West Point", "Denison" },
            ["Mansa Musa"]    = new[] { "Niani", "Timbuktu", "Gao", "Djenne", "Walata", "Koumbi Saleh", "Taghaza", "Cairo", "Mecca", "Medina" },
            ["Croesus"]       = new[] { "Sardis", "Lydia", "Ephesus", "Smyrna", "Pergamon", "Halicarnassus", "Miletus", "Colophon", "Magnesia", "Thyatira" },
            ["Carnegie"]      = new[] { "Pittsburgh", "Dunfermline", "New York", "Homestead", "Braddock", "Johnstown", "Altoona", "Cleveland", "Chicago", "Bethlehem" },
            ["Nelson"]        = new[] { "Burnham Thorpe", "Portsmouth", "London", "Chatham", "Gibraltar", "Cádiz", "Naples", "Copenhagen", "Alexandria", "Trafalgar" },
            ["Yamamoto"]      = new[] { "Nagaoka", "Tokyo", "Hiroshima", "Kure", "Yokosuka", "Pearl Harbor", "Midway", "Truk", "Rabaul", "Bougainville" },
            ["Drake"]         = new[] { "Plymouth", "Tavistock", "London", "Cádiz", "Nombre de Dios", "Cartagena", "San Juan", "Panama", "Portobelo", "Deptford" },
            ["Richthofen"]    = new[] { "Wrocław", "Schweidnitz", "Berlin", "Cambrai", "Douai", "Lille", "Arras", "Amiens", "Vaux-sur-Somme", "Wiesbaden" },
            ["Yeager"]        = new[] { "Hamlin", "Charleston", "Victorville", "Edwards", "Muroc", "Roswell", "Wright-Patterson", "Washington", "Grass Valley", "Bridgeport" },
            ["Mitchell"]      = new[] { "Milwaukee", "Washington", "Langley", "San Antonio", "Newport News", "Hampton", "Dayton", "New York", "Paris", "St. Mihiel" },
        };

        private static readonly Random _rng = new Random();

        /// <summary>
        /// Assigns commander city name sets to a list of players.
        /// The human player (index 0) gets the set matching their name if it exists,
        /// otherwise a random unused one. AI players each get a random unused set.
        /// </summary>
        public static void AssignToPlayers(List<Player> players)
        {
            var available = All.Keys.ToList();
            _rng.Shuffle(available);

            // Try to give the human player a set matching their name
            string humanName = players[0].Name ?? "";
            string humanMatch = available.FirstOrDefault(k =>
                humanName.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0 ||
                k.IndexOf(humanName, StringComparison.OrdinalIgnoreCase) >= 0);

            if (humanMatch != null)
            {
                available.Remove(humanMatch);
                players[0].CommanderName = humanMatch;
            }
            else
            {
                players[0].CommanderName = available[0];
                available.RemoveAt(0);
            }

            for (int i = 1; i < players.Count; i++)
            {
                if (available.Count == 0)
                    available = All.Keys.ToList(); // wrap if more players than commanders

                // Prefer a set matching the AI personality name
                string aiName = players[i].Name ?? "";
                string aiMatch = available.FirstOrDefault(k =>
                    aiName.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    k.IndexOf(aiName, StringComparison.OrdinalIgnoreCase) >= 0);

                if (aiMatch != null)
                {
                    available.Remove(aiMatch);
                    players[i].CommanderName = aiMatch;
                }
                else
                {
                    players[i].CommanderName = available[0];
                    available.RemoveAt(0);
                }
            }
        }

        /// <summary>
        /// Returns the next city name for the given player and advances their index.
        /// Falls back to "City N" if the list is exhausted.
        /// </summary>
        public static string NextCityName(Player player)
        {
            if (string.IsNullOrEmpty(player.CommanderName) ||
                !All.TryGetValue(player.CommanderName, out var names))
                return $"City {player.CityNameIndex + 1}";

            string name = player.CityNameIndex < names.Count
                ? names[player.CityNameIndex]
                : $"{names[names.Count - 1]} {player.CityNameIndex - names.Count + 2}"; // "Elba 2" etc.

            player.CityNameIndex++;
            return name;
        }
    }

    // Extension to shuffle a list in place
    internal static class ListExtensions
    {
        private static readonly Random _rng = new Random();
        public static void Shuffle<T>(this Random rng, List<T> list)
        {
            for (int n = list.Count - 1; n > 0; n--)
            {
                int k = rng.Next(n + 1);
                (list[k], list[n]) = (list[n], list[k]);
            }
        }
    }
}
