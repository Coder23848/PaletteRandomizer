﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using BepInEx;
using UnityEngine;

namespace PaletteRandomizer
{
    [BepInPlugin("com.coder23848.paletterandomizer", PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
#pragma warning disable IDE0051 // Visual Studio is whiny
        private void OnEnable()
#pragma warning restore IDE0051
        {
            On.RoomCamera.LoadPalette += RoomCamera_LoadPalette;
            On.RoomCamera.LoadGhostPalette += RoomCamera_LoadGhostPalette;

            On.RoomCamera.ApplyEffectColorsToPaletteTexture += RoomCamera_ApplyEffectColorsToPaletteTexture;

            On.RainWorld.OnModsInit += RainWorld_OnModsInit;
            On.RainWorldGame.ctor += RainWorldGame_ctor;
        }

        public struct PaletteInfo
        {
            public int Index { get; set; }
            public bool IsDark { get; set; }
            public PaletteInfo(int index, bool isDark)
            {
                Index = index;
                IsDark = isDark;
            }

            public override string ToString()
            {
                return $"[Palette {Index}" + (IsDark ? " (DARK)" : "") + "]";
            }
        }

        public static List<PaletteInfo> allPalettes;
        public static readonly ConditionalWeakTable<RainWorldGame, Dictionary<int, int>> paletteMaps = new();
        /// <summary>
        /// A palette is considered "dark" if the brightness channel and all of the normal unlit channels are below this value.
        /// </summary>
        public const float DARKNESS_THRESHOLD = 0.05f;

        public static List<PaletteInfo> GetAllPalettes()
        {
            List<PaletteInfo> ret = new();
            string[] files = AssetManager.ListDirectory("Palettes");
            foreach (string i in files)
            {
                string file = Path.GetFileName(i);
                try
                {
                    if (file.StartsWith("palette") && file.EndsWith(".png") && int.TryParse(file.Substring(7, file.Length - 4 - 7), out int num))
                    {
                        if (num >= 0)
                        {
                            string str = AssetManager.ResolveFilePath(string.Concat(new string[]
                            {
                                "Palettes",
                                Path.DirectorySeparatorChar.ToString(),
                                "palette",
                                num.ToString(),
                                ".png"
                            }));
                            Debug.Log("Palette Randomizer: Found palette " + num + " at " + str);
                            try
                            {
                                Texture2D tex = new(32, 16, TextureFormat.ARGB32, false);
                                AssetManager.SafeWWWLoadTexture(ref tex, "file:///" + str, false, true);

                                bool isDark = tex.GetPixel(30, 7).r < DARKNESS_THRESHOLD;
                                if (isDark)
                                {
                                    // Check the unlit set of colors to make sure it's actually a dark palette.
                                    for (int d = 0; d < 30; d++)
                                    {
                                        float maxColor = Mathf.Max(tex.GetPixel(d, 8).maxColorComponent, tex.GetPixel(d, 9).maxColorComponent, tex.GetPixel(d, 10).maxColorComponent);
                                        if (maxColor >= DARKNESS_THRESHOLD)
                                        {
                                            Debug.Log("Palette Randomizer: Palette " + num + " is a false dark palette! Sublayer " + d + " has value " + maxColor);
                                            isDark = false;
                                            break;
                                        }
                                    }
                                }
                                
                                if (isDark)
                                {
                                    Debug.Log("Palette Randomizer: Palette " + num + " is a dark palette.");
                                }
                                ret.Add(new(num, isDark));
                            }
                            catch (System.Exception ex)
                            {
                                Debug.Log("Palette Randomizer: Could not load palette " + num + ": " + ex);
                                continue;
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.Log("Palette Randomizer: Could not load file " + i + ": " + ex);
                }
            }
            ret = ret.OrderBy(x => x.Index).ToList();
            return ret;
        }
        public static Dictionary<int, int> GeneratePaletteMap(int seed)
        {
            Random.State randomState = Random.state;
            Random.InitState(seed);

            Debug.Log("Palette Randomizer: Randomizing palettes with seed " + seed);

            List<PaletteInfo> unusedPalettes = new();
            foreach (var i in allPalettes)
            {
                unusedPalettes.Add(i);
            }

            Dictionary<int, int> ret = new();

            foreach (var oldPal in allPalettes)
            {
                PaletteInfo newPal;
                if (PluginOptions.DarkTreatment.Value == PluginOptions.DARKTREATMENT_DONOTRANDOMIZE && oldPal.IsDark)
                {
                    newPal = oldPal;
                }
                else
                {
                    List<PaletteInfo> choices = unusedPalettes.Where(x =>
                    {
                        if (PluginOptions.DarkTreatment.Value == PluginOptions.DARKTREATMENT_DONOTRANDOMIZE && x.IsDark)
                        {
                            return false;
                        }
                        if (PluginOptions.DarkTreatment.Value == PluginOptions.DARKTREATMENT_RANDOMIZETODARK && x.IsDark != oldPal.IsDark)
                        {
                            return false;
                        }
                        return true;
                    }
                    ).ToList();
                    if (choices.Count <= 0) // I'm pretty sure this can't happen, but just in case...
                    {
                        Debug.Log("Palette Randomizer: no good match for palette " + oldPal.Index + "! Selecting randomly...");
                        newPal = unusedPalettes[Random.Range(0, unusedPalettes.Count)];
                    }
                    else
                    {
                        newPal = choices[Random.Range(0, choices.Count)];
                    }
                }

                Debug.Log("Palette Randomizer: Palette " + oldPal.Index + " -> " + newPal.Index);
                ret.Add(oldPal.Index, newPal.Index);
                unusedPalettes.Remove(newPal);
            }

            Random.state = randomState;

            return ret;
        }

        public int RandomizePalette(int pal, RainWorldGame game)
        {
            if (paletteMaps.TryGetValue(game, out var paletteMap))
            {
                if (paletteMap.TryGetValue(pal, out var ret))
                {
                    return ret;
                }
            }
            return pal;
        }
        private void RoomCamera_LoadPalette(On.RoomCamera.orig_LoadPalette orig, RoomCamera self, int pal, ref Texture2D texture)
        {
            if (inLoadGhostPalette && PluginOptions.LeaveEchoes.Value)
            {
                orig(self, pal, ref texture);
            }
            else
            {
                orig(self, RandomizePalette(pal, self.game), ref texture);
            }
        }
        private static bool inLoadGhostPalette = false;
        private void RoomCamera_LoadGhostPalette(On.RoomCamera.orig_LoadGhostPalette orig, RoomCamera self, int gPal)
        {
            inLoadGhostPalette = true;
            orig(self, gPal);
            inLoadGhostPalette = false;
        }



        public static List<int> allEffectColors;
        public static readonly ConditionalWeakTable<RainWorldGame, Dictionary<int, int>> effectColorMaps = new();

        public static List<int> GetAllEffectColors()
        {
            string str = AssetManager.ResolveFilePath(string.Concat(new string[]
            {
                "Palettes",
                Path.DirectorySeparatorChar.ToString(),
                "effectcolors.png"
            }));
            Texture2D tex = new(32, 16, TextureFormat.ARGB32, false);
            AssetManager.SafeWWWLoadTexture(ref tex, "file:///" + str, false, true);

            Debug.Log("Palette Randomizer: Found " + tex.width / 2 + " effect colors at " + str);

            List<int> ret = new();
            for (int i = 0; i < tex.width / 2; i++)
            {
                ret.Add(i);
            }

            return ret;
        }
        public static Dictionary<int, int> GenerateEffectColorMap(int seed)
        {
            Random.State randomState = Random.state;
            Random.InitState(seed);

            Debug.Log("Palette Randomizer: Randomizing effect colors with seed " + seed);

            List<int> unusedEffectColors = new();
            foreach (var i in allEffectColors)
            {
                unusedEffectColors.Add(i);
            }

            Dictionary<int, int> ret = new();

            foreach (var oldCol in allEffectColors)
            {
                int newCol = unusedEffectColors[Random.Range(0, unusedEffectColors.Count)];

                Debug.Log("Palette Randomizer: Effect Color " + oldCol + " -> " + newCol);
                ret.Add(oldCol, newCol);
                unusedEffectColors.Remove(newCol);
            }

            Random.state = randomState;

            return ret;
        }
        public int RandomizeEffectColor(int col, RainWorldGame game)
        {
            if (effectColorMaps.TryGetValue(game, out var effectColorMap))
            {
                if (effectColorMap.TryGetValue(col, out var ret))
                {
                    return ret;
                }
            }
            return col;
        }
        private void RoomCamera_ApplyEffectColorsToPaletteTexture(On.RoomCamera.orig_ApplyEffectColorsToPaletteTexture orig, RoomCamera self, ref Texture2D texture, int color1, int color2)
        {
            if (PluginOptions.RandomEffectColors.Value)
            {
                orig(self, ref texture, RandomizeEffectColor(color1, self.game), RandomizeEffectColor(color2, self.game));
            }
            else
            {
                orig(self, ref texture, color1, color2);
            }
        }

        private void RainWorldGame_ctor(On.RainWorldGame.orig_ctor orig, RainWorldGame self, ProcessManager manager)
        {
            orig(self, manager);

            int moreRandomness = (int)System.DateTime.Now.Ticks & 65535;
            int seed;
            if (self.session is StoryGameSession session)
            {
                seed = PluginOptions.RandomizeFrequency.Value switch
                {
                    PluginOptions.RANDOMIZEFREQUENCY_SEEDED => PluginOptions.Seed.Value,
                    PluginOptions.RANDOMIZEFREQUENCY_PERPLAYTHROUGH => PluginOptions.Seed.Value + session.saveState.seed,
                    PluginOptions.RANDOMIZEFREQUENCY_PERCYCLE => PluginOptions.Seed.Value + session.saveState.seed * 250 + session.saveState.cycleNumber + 10000,
                    PluginOptions.RANDOMIZEFREQUENCY_PERATTEMPT => moreRandomness,
                    _ => PluginOptions.Seed.Value
                };
            }
            else
            {
                seed = PluginOptions.RandomizeFrequency.Value switch
                {
                    PluginOptions.RANDOMIZEFREQUENCY_SEEDED => PluginOptions.Seed.Value,
                    PluginOptions.RANDOMIZEFREQUENCY_PERPLAYTHROUGH or PluginOptions.RANDOMIZEFREQUENCY_PERCYCLE or PluginOptions.RANDOMIZEFREQUENCY_PERATTEMPT => moreRandomness,
                    _ => PluginOptions.Seed.Value
                };
            }
            
            paletteMaps.Add(self, GeneratePaletteMap(seed));
            if (PluginOptions.RandomEffectColors.Value)
            {
                effectColorMaps.Add(self, GenerateEffectColorMap(seed));
            }

            // These load during RainWorldGame.ctor, so I'm just going to reload them again afterwards. I hope this doesn't break anything...
            self.cameras[0].LoadGhostPalette(32);
            self.cameras[0].ChangeMainPalette(0);
        }

        private void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
        {
            orig(self);

            Debug.Log("Palette Randomizer config setup: " + MachineConnector.SetRegisteredOI(PluginInfo.PLUGIN_GUID, PluginOptions.Instance));

            allPalettes = GetAllPalettes();
            allEffectColors = GetAllEffectColors();
        }
    }
}