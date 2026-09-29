using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pasjans.UI
{
    /// <summary>Oryginalne grafiki generowane w kodzie i tworzone tylko raz.</summary>
    public sealed class GameArt : IDisposable
    {
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        public Sprite Card { get; }
        public Sprite Back { get; }
        public Sprite RedBack { get; }
        public Sprite Panel { get; }
        public Sprite[] Suits { get; }
        public Texture2D Felt { get; }

        public GameArt()
        {
            Card = Rounded("Ivory card", 96, 136, 9, new Color32(255, 252, 242, 255), new Color32(209, 213, 204, 255));
            Panel = Rounded("Rounded panel", 48, 48, 10, Color.white, Color.white);
            Back = MakeBack("Granatowy rewers", new Color32(23, 33, 62, 255), new Color32(28, 43, 83, 255), new Color32(68, 86, 128, 255));
            RedBack = MakeBack("Ciemnoczerwony rewers", new Color32(66, 19, 28, 255), new Color32(94, 25, 37, 255), new Color32(145, 62, 74, 255));
            Suits = new Sprite[4];
            for (int suit = 0; suit < 4; suit++)
            {
                int s = suit;
                Suits[s] = MakeSprite("Suit " + s, 64, 64, (x, y) =>
                {
                    // Cztery próbki piksela wygładzają symbole w małym rozmiarze.
                    int hit = 0;
                    for (int a = 0; a < 2; a++)
                        for (int b = 0; b < 2; b++)
                            if (InsideSuit(s, (x + .25f + a * .5f - 32) / 29f, (y + .25f + b * .5f - 32) / 29f)) hit++;
                    return new Color(1, 1, 1, hit * .25f);
                }, Vector4.zero);
            }
            Felt = new Texture2D(128, 128, TextureFormat.RGBA32, false) { name = "Original felt", wrapMode = TextureWrapMode.Repeat };
            var pixels = new Color32[128 * 128];
            var random = new System.Random(2026);
            for (int i = 0; i < pixels.Length; i++)
            {
                int n = random.Next(-3, 4);
                pixels[i] = new Color32((byte)(24 + n), (byte)(77 + n), (byte)(64 + n), 255);
            }
            Felt.SetPixels32(pixels);
            Felt.Apply(false, true);
            owned.Add(Felt);
        }

        // Wspólny wzór pozwala rozróżnić talie samym kolorem rewersu.
        Sprite MakeBack(string name, Color edgeColor, Color fill, Color pattern)
        {
            return MakeSprite(name, 96, 136, (x, y) =>
            {
                float edge = RoundedDistance(x, y, 96, 136, 9);
                if (edge > 0) return Color.clear;
                if (edge > -3) return new Color32(227, 216, 177, 255);
                if (x < 8 || x > 87 || y < 8 || y > 127) return edgeColor;
                return (x + y) % 16 < 2 || (x - y + 160) % 16 < 2 ? pattern : fill;
            }, new Vector4(10, 10, 10, 10));
        }

        public Sprite BackFor(int deckIndex) => deckIndex == 1 ? RedBack : Back;

        Sprite Rounded(string name, int w, int h, int radius, Color fill, Color border)
        {
            return MakeSprite(name, w, h, (x, y) =>
            {
                float d = RoundedDistance(x, y, w, h, radius);
                Color c = d > -1.7f ? border : fill;
                c.a *= Mathf.Clamp01(.5f - d);
                return c;
            }, new Vector4(radius + 2, radius + 2, radius + 2, radius + 2));
        }

        static float RoundedDistance(float x, float y, int w, int h, float radius)
        {
            float dx = Mathf.Abs(x + .5f - w * .5f) - (w * .5f - radius);
            float dy = Mathf.Abs(y + .5f - h * .5f) - (h * .5f - radius);
            return new Vector2(Mathf.Max(dx, 0), Mathf.Max(dy, 0)).magnitude + Mathf.Min(Mathf.Max(dx, dy), 0) - radius;
        }

        static bool Heart(float x, float y)
        {
            float a = x * x + y * y - .65f;
            return a * a * a - x * x * y * y * y <= 0;
        }

        static bool InsideSuit(int suit, float x, float y)
        {
            switch (suit)
            {
                case 0: // Trefl.
                    return Circle(x, y - .40f, .38f) || Circle(x - .36f, y + .03f, .38f) || Circle(x + .36f, y + .03f, .38f)
                        || (y < .05f && y > -.85f && Mathf.Abs(x) < .10f + (-y) * .18f);
                case 1: return Mathf.Abs(x) / .74f + Mathf.Abs(y) / .94f <= 1;
                case 2: return Heart(x, y);
                default: return Heart(x, -y + .12f) || (y < 0 && y > -.94f && Mathf.Abs(x) < .09f + (-y) * .19f);
            }
        }

        static bool Circle(float x, float y, float r) => x * x + y * y <= r * r;

        Sprite MakeSprite(string name, int w, int h, Func<int, int, Color> pixel, Vector4 border)
        {
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var colors = new Color[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) colors[y * w + x] = pixel(x, y);
            texture.SetPixels(colors);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, border);
            sprite.name = name;
            owned.Add(sprite);
            owned.Add(texture);
            return sprite;
        }

        public void Dispose()
        {
            foreach (var item in owned) if (item != null) UnityEngine.Object.Destroy(item);
            owned.Clear();
        }
    }
}
