using System;
using System.IO;
using Newtonsoft.Json;

namespace SmartGooseAI
{
    public class Config
    {
        public bool EnableAI { get; set; } = false;
        public string OllamaUrl { get; set; } = "http://localhost:11434/api/chat";
        public string Model { get; set; } = "qwen3-vl:2b";
        public string SystemPrompt { get; set; } = "Ты — Nexus, дружелюбный гусь-помощник.";
    }

    public static class ConfigManager
    {
        private static readonly string ModFolder = Path.Combine("Assets", "Mods", "SmartGooseAI");
        private static readonly string ConfigPath = Path.Combine(ModFolder, "config.json");

        public static Config Load()
        {
            try
            {
                if (!Directory.Exists(ModFolder))
                    Directory.CreateDirectory(ModFolder);

                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    return JsonConvert.DeserializeObject<Config>(json) ?? new Config();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки конфига: {ex.Message}");
            }

            var defaultConfig = new Config();
            Save(defaultConfig);
            return defaultConfig;
        }

        public static void Save(Config config)
        {
            try
            {
                string json = JsonConvert.SerializeObject(config, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка сохранения конфига: {ex.Message}");
            }
        }
    }
}
