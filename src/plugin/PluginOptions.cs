using System.Collections.Generic;
using Menu.Remix.MixedUI;
using UnityEngine;

namespace PaletteRandomizer
{
    public class PluginOptions : OptionInterface
    {
        public static PluginOptions Instance = new();

        public static Configurable<int> Seed = Instance.config.Bind("Seed", 0, new ConfigurableInfo("The seed to use for randomization.", new ConfigAcceptableRange<int>(0, 1000)));
        public static Configurable<bool> LeaveEchoes = Instance.config.Bind("LeaveEchoes", true, new ConfigurableInfo("Prevent the echo palette from being randomized."));
        public static Configurable<string> DarkTreatment = Instance.config.Bind("DarkTreatment", DARKTREATMENT_RANDOMIZETODARK, new ConfigurableInfo("How very dark palettes should be affected by the randomizer."));
        public const string DARKTREATMENT_RANDOMIZE = "RANDOM";
        public const string DARKTREATMENT_RANDOMIZETODARK = "KEEPDARK";
        public const string DARKTREATMENT_DONOTRANDOMIZE = "NOTRANDOM";
        public static Configurable<string> RandomizeFrequency = Instance.config.Bind("RandomizeFrequency", RANDOMIZEFREQUENCY_PERPLAYTHROUGH, new ConfigurableInfo("How often the palettes should be rerandomized."));
        public const string RANDOMIZEFREQUENCY_SEEDED = "SEEDED";
        public const string RANDOMIZEFREQUENCY_PERPLAYTHROUGH = "PERPLAYTHROUGH";
        public const string RANDOMIZEFREQUENCY_PERCYCLE = "PERCYCLE";
        public const string RANDOMIZEFREQUENCY_PERATTEMPT = "PERATTEMPT";

        public override void Initialize()
        {
            base.Initialize();
            Tabs = new OpTab[1];

            Tabs[0] = new(Instance, "Options");

            IntBoxOption(Seed, 100, 0, "Seed");
            CheckBoxOption(LeaveEchoes, 1, "Do not randomize echoes");
            DropDownOption(DarkTreatment, 2, 300, new() {
                new(DARKTREATMENT_RANDOMIZE, "Fully randomize", 0) { desc = "Randomize all palettes together, regardless of brightness." },
                new(DARKTREATMENT_RANDOMIZETODARK, "Randomize to other dark palettes", 1) { desc = "Randomize dark palettes separately from brighter ones, to ensure that dark areas stay dark." },
                new(DARKTREATMENT_DONOTRANDOMIZE, "Do not randomize", 2) { desc = "Do not randomize dark palettes, to ensure that dark areas stay dark." }
            }, "Dark Palette Treatment");
            DropDownOption(RandomizeFrequency, 6, 300, new() { new(RANDOMIZEFREQUENCY_SEEDED, "Seed only", 0) { desc = "Randomize the palettes according to a seed." },
                new(RANDOMIZEFREQUENCY_PERPLAYTHROUGH, "Once per save file", 1) { desc = "Randomize the palettes on a per-save-file basis." },
                new(RANDOMIZEFREQUENCY_PERCYCLE, "Once per cycle", 2) { desc = "Randomize the palettes every successful cycle." },
                new(RANDOMIZEFREQUENCY_PERATTEMPT, "Once per attempted cycle") { desc = "Randomize the palettes every cycle and every death." }
            }, "Randomization Frequency");
        }

        private void CheckBoxOption(Configurable<bool> setting, float pos, string label)
        {
            Tabs[0].AddItems(new OpCheckBox(setting, new(50, 550 - pos * 30)) { description = setting.info.description }, new OpLabel(new Vector2(90, 550 - pos * 30), new Vector2(), label, FLabelAlignment.Left));
        }
        private void DropDownOption(Configurable<string> setting, float pos, int size, List<ListItem> options, string label)
        {
            Tabs[0].AddItems(new OpComboBox(setting, new(50, 550 - pos * 30), size, options) { description = setting.info.description }, new OpLabel(new Vector2(60 + size, 550 - pos * 30), new Vector2(), label, FLabelAlignment.Left));
        }
        private void IntBoxOption(Configurable<int> setting, int size, float pos, string label)
        {
            Tabs[0].AddItems(new OpUpdown(setting, new(50, 545 - pos * 30), size) { description = setting.info.description }, new OpLabel(new Vector2(60 + size, 550 - pos * 30), new Vector2(), label, FLabelAlignment.Left));
        }
        private void SliderOption(Configurable<float> setting, int size, float pos, string label)
        {
            Tabs[0].AddItems(new OpFloatSlider(setting, new(50, 545 - pos * 30), size) { description = setting.info.description }, new OpLabel(new Vector2(60 + size, 550 - pos * 30), new Vector2(), label, FLabelAlignment.Left));
        }
    }
}