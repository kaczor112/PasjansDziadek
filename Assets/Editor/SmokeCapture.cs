using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pasjans.Editor
{
    /// <summary>
    /// Test uruchamiany w edytorze z grafiką. Użyj -batchmode -executeMethod
    /// Pasjans.Editor.SmokeCapture.Run --pasjans-save-dir &lt;project&gt;/Temp/SmokeSaves.
    /// Nie dodawaj -quit ani -nographics; narzędzie kończy się po wykonaniu zrzutów.
    /// Zrzuty obejmują rzeczywisty interfejs renderowany przez widok Game.
    /// </summary>
    [InitializeOnLoad]
    public static class SmokeCapture
    {
        const string Prefix = "Pasjans.SmokeCapture.";
        const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static EditorWindow gameView;

        static SmokeCapture()
        {
            EditorApplication.update += Update;
        }

        public static void Run()
        {
            try
            {
                if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                    throw new InvalidOperationException("Screenshot smoke run requires graphics; remove -nographics.");
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("Start the capture in edit mode.");
                string[] args = Environment.GetCommandLineArgs();
                bool hasIsolatedSaveDirectory = false;
                for (int i = 0; i + 1 < args.Length; i++)
                {
                    if (args[i] != "--pasjans-save-dir") continue;
                    string isolatedPath = Path.GetFullPath(args[i + 1]);
                    string tempRoot = Path.GetFullPath("Temp") + Path.DirectorySeparatorChar;
                    if (!isolatedPath.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Smoke saves must be inside the project's Temp directory.");
                    hasIsolatedSaveDirectory = true;
                    break;
                }
                if (!hasIsolatedSaveDirectory)
                    throw new InvalidOperationException("Pass --pasjans-save-dir <project>/Temp/SmokeSaves to protect personal saves.");

                string output = Path.GetFullPath(Path.Combine("Logs", "Screenshots", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")));
                Directory.CreateDirectory(output);
                SessionState.SetString(Prefix + "Output", output);
                SessionState.SetFloat(Prefix + "Deadline", (float)EditorApplication.timeSinceStartup + 120f);
                SessionState.SetInt(Prefix + "Stage", 1);
                EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
                SetGameViewSize(1280, 800);
                Debug.Log("Pasjans smoke screenshots: " + output);
                EditorApplication.isPlaying = true;
            }
            catch (Exception exception)
            {
                Finish(false, exception.ToString());
            }
        }

        static void Update()
        {
            int stage = SessionState.GetInt(Prefix + "Stage", 0);
            if (stage == 0) return;
            try
            {
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Prefix + "Deadline", 0))
                    throw new TimeoutException("Timed out waiting for Unity frames or screenshot output (stage " + stage + ").");
                EditorApplication.QueuePlayerLoopUpdate();
                GetGameView().Repaint();
                if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;

                switch (stage)
                {
                    case 1:
                        if (FindApp() == null) return;
                        SetGameViewSize(1280, 800);
                        SetStage(2);
                        break;
                    case 2:
                        if (!Ready(1280, 800)) return;
                        FindApp().SendMessage("Draw", SendMessageOptions.RequireReceiver);
                        SetStage(3);
                        break;
                    case 3:
                        if (!Ready(1280, 800)) return;
                        Capture("landscape.png", 4);
                        break;
                    case 4:
                        if (!CaptureComplete("landscape.png", 1280, 800)) return;
                        FindApp().SendMessage("ShowAbout", SendMessageOptions.RequireReceiver);
                        SetStage(5);
                        break;
                    case 5:
                        if (!Ready(1280, 800)) return;
                        Capture("about.png", 6);
                        break;
                    case 6:
                        if (!CaptureComplete("about.png", 1280, 800)) return;
                        FindApp().SendMessage("CloseModal", SendMessageOptions.RequireReceiver);
                        SetGameViewSize(720, 1280);
                        SetStage(7);
                        break;
                    case 7:
                        if (!Ready(720, 1280)) return;
                        Capture("portrait.png", 8);
                        break;
                    case 8:
                        if (!CaptureComplete("portrait.png", 720, 1280)) return;
                        FindApp().SendMessage("ShowRanking", SendMessageOptions.RequireReceiver);
                        SetStage(9);
                        break;
                    case 9:
                        if (!Ready(720, 1280)) return;
                        Capture("ranking-empty.png", 10);
                        break;
                    case 10:
                        if (!CaptureComplete("ranking-empty.png", 720, 1280)) return;
                        PopulateSampleRanking();
                        FindApp().SendMessage("ShowRanking", SendMessageOptions.RequireReceiver);
                        SetStage(11);
                        break;
                    case 11:
                        if (!Ready(720, 1280)) return;
                        Capture("ranking.png", 12);
                        break;
                    case 12:
                        if (!CaptureComplete("ranking.png", 720, 1280)) return;
                        Finish(true, "Captured game, About and ranking dialogs in " + SessionState.GetString(Prefix + "Output", ""));
                        break;
                }
            }
            catch (Exception exception)
            {
                Finish(false, exception.ToString());
            }
        }

        static void SetStage(int stage)
        {
            SessionState.SetInt(Prefix + "Stage", stage);
            SessionState.SetInt(Prefix + "StartFrame", Time.frameCount);
            SessionState.SetFloat(Prefix + "StartTime", (float)EditorApplication.timeSinceStartup);
        }

        static void PopulateSampleRanking()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--pasjans-save-dir");
            var store = new Pasjans.Persistence.RankingStore(args[index + 1]);
            for (int i = 0; i < 10; i++)
            {
                var state = new Pasjans.Core.GameState { moveCount = 72 + i * 9, finalMoves = 72 + i * 9, completedUtcTicks = DateTime.UtcNow.AddHours(-i).Ticks };
                for (int s = 0; s < 4; s++) for (int r = 1; r <= 13; r++) state.stock.Add(new Pasjans.Core.CardData((Pasjans.Core.Suit)s, r));
                if (!store.TryRecord(state, out string error)) throw new IOException(error);
            }
        }

        static bool Ready(int width, int height)
        {
            return Screen.width == width && Screen.height == height &&
                   Time.frameCount - SessionState.GetInt(Prefix + "StartFrame", 0) >= 8 &&
                   EditorApplication.timeSinceStartup - SessionState.GetFloat(Prefix + "StartTime", 0) >= .75;
        }

        static void Capture(string filename, int nextStage)
        {
            Canvas.ForceUpdateCanvases();
            ScreenCapture.CaptureScreenshot(Path.Combine(SessionState.GetString(Prefix + "Output", ""), filename));
            SetStage(nextStage);
        }

        static bool CaptureComplete(string filename, int width, int height)
        {
            string path = Path.Combine(SessionState.GetString(Prefix + "Output", ""), filename);
            // Niektóre tryby wsadowe pomijają zrzut na końcu klatki.
            // Wtedy odczytaj teksturę już wyrenderowanego widoku Game.
            if (!File.Exists(path) && EditorApplication.timeSinceStartup - SessionState.GetFloat(Prefix + "StartTime", 0) > 3)
                ReadGameViewTexture(path, width, height);
            if (!File.Exists(path)) return false;
            byte[] bytes;
            try { bytes = File.ReadAllBytes(path); }
            catch (IOException) { return false; }
            if (bytes.Length < 36) return false;
            // Poczekaj na znacznik IEND, a nie tylko nagłówek zapisywanego pliku.
            int tail = bytes.Length - 8;
            if (bytes[tail] != 73 || bytes[tail + 1] != 69 || bytes[tail + 2] != 78 || bytes[tail + 3] != 68)
                return false;
            if (bytes[0] != 137 || bytes[1] != 80 || bytes[2] != 78 || bytes[3] != 71)
                throw new InvalidDataException("Screenshot is not a PNG: " + path);
            int actualWidth = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
            int actualHeight = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
            if (actualWidth != width || actualHeight != height)
                throw new InvalidDataException("Unexpected screenshot dimensions: " + actualWidth + "x" + actualHeight);
            Debug.Log("Verified smoke screenshot: " + path + " (" + width + "x" + height + ")");
            return true;
        }

        static void ReadGameViewTexture(string path, int width, int height)
        {
            var textureField = GetGameView().GetType().GetField("m_RenderTexture", InstanceFlags);
            var rendered = textureField == null ? null : textureField.GetValue(gameView) as RenderTexture;
            if (rendered == null || rendered.width != width || rendered.height != height) return;
            RenderTexture previous = RenderTexture.active;
            var screenshot = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = rendered;
                screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                screenshot.Apply(false);
                File.WriteAllBytes(path, screenshot.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(screenshot);
            }
        }

        static MonoBehaviour FindApp()
        {
            foreach (var behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (behaviour.GetType().FullName == "Pasjans.UI.PasjansApp") return behaviour;
            return null;
        }

        static EditorWindow GetGameView()
        {
            if (gameView == null)
                gameView = EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView", true));
            return gameView;
        }

        static void SetGameViewSize(int width, int height)
        {
            var assembly = typeof(EditorWindow).Assembly;
            Type sizesType = assembly.GetType("UnityEditor.GameViewSizes", true);
            Type sizeType = assembly.GetType("UnityEditor.GameViewSize", true);
            Type sizeKindType = assembly.GetType("UnityEditor.GameViewSizeType", true);
            Type singletonType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            object sizes = singletonType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            object group = sizesType.GetProperty("currentGroup", InstanceFlags).GetValue(sizes);
            Type groupType = group.GetType();
            object fixedResolution = Enum.Parse(sizeKindType, "FixedResolution");
            object customSize = Activator.CreateInstance(sizeType, InstanceFlags, null,
                new object[] { fixedResolution, width, height, "Pasjans QA " + width + "x" + height }, null);
            groupType.GetMethod("AddCustomSize", InstanceFlags).Invoke(group, new[] { customSize });
            int total = (int)groupType.GetMethod("GetTotalCount", InstanceFlags).Invoke(group, null);
            EditorWindow view = GetGameView();
            view.GetType().GetMethod("SizeSelectionCallback", InstanceFlags).Invoke(view, new object[] { total - 1, null });
            view.Focus();
            view.Repaint();
            EditorApplication.QueuePlayerLoopUpdate();
        }

        static void Finish(bool success, string message)
        {
            SessionState.SetInt(Prefix + "Stage", 0);
            if (success) Debug.Log("Pasjans smoke capture PASSED: " + message);
            else Debug.LogError("Pasjans smoke capture FAILED: " + message);
            if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);
            else EditorApplication.isPlaying = false;
        }
    }
}
