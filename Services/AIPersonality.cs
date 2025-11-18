using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EmpireGame
{
    public enum AIPlaystyle
    {
        Balanced,
        Aggressive,
        Defensive,
        Buildup,
        Naval,
        Aerial
    }

    public class AIPersonality
    {
        public string Name { get; set; }
        public AIPlaystyle Playstyle { get; set; }

        public AIPersonality(string name, AIPlaystyle playstyle)
        {
            Name = name;
            Playstyle = playstyle;
        }

        public override string ToString()
        {
            return $"{Name} ({Playstyle})";
        }

        public static List<AIPersonality> LoadPersonalities()
        {
            var personalities = new List<AIPersonality>();
            string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string filePath = Path.Combine(appDirectory, "AIPersonalities.txt");

            if (!File.Exists(filePath))
            {
                CreateDefaultPersonalitiesFile(filePath);
            }

            try
            {
                var lines = File.ReadAllLines(filePath);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                        continue;

                    var parts = line.Split('|');
                    if (parts.Length == 2)
                    {
                        string name = parts[0].Trim();
                        if (Enum.TryParse<AIPlaystyle>(parts[1].Trim(), out var playstyle))
                        {
                            personalities.Add(new AIPersonality(name, playstyle));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading AI personalities: {ex.Message}");
            }

            if (personalities.Count == 0)
            {
                personalities.Add(new AIPersonality("Default AI", AIPlaystyle.Balanced));
            }

            return personalities;
        }

        private static void CreateDefaultPersonalitiesFile(string filePath)
        {
            var defaultContent = @"# AI Personalities for Empire
# Format: Name | Playstyle
# Playstyles: Balanced, Aggressive, Defensive, Buildup, Naval, Aerial

# Balanced AIs
Alexander | Balanced
Caesar | Balanced
Napoleon | Balanced

# Aggressive AIs
Genghis Khan | Aggressive
Attila | Aggressive
Hannibal | Aggressive
Patton | Aggressive

# Defensive AIs
Leonidas | Defensive
Washington | Defensive
Eisenhower | Defensive

# Buildup AIs (Economy focus)
Mansa Musa | Buildup
Croesus | Buildup
Carnegie | Buildup

# Naval Specialists
Nelson | Naval
Yamamoto | Naval
Drake | Naval

# Aerial Specialists
Richthofen | Aerial
Yeager | Aerial
Mitchell | Aerial
";

            try
            {
                File.WriteAllText(filePath, defaultContent);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating AI personalities file: {ex.Message}");
            }
        }

        public static List<AIPersonality> GetUniqueRandomPersonalities(int count)
        {
            var allPersonalities = LoadPersonalities();
            var random = new Random();
            
            var shuffled = allPersonalities.OrderBy(x => random.Next()).ToList();
            return shuffled.Take(Math.Min(count, shuffled.Count)).ToList();
        }
    }
}