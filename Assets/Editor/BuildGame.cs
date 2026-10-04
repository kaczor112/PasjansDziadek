using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pasjans.Editor
{
    /// <summary>Buduje tylko włączone sceny gry, z menu Unity lub w trybie wsadowym.</summary>
    public static class BuildGame
    {
        public static void AllPlatforms()
        {
            Windows();
            Android();
        }

        [MenuItem("Pasjans/Zbuduj/Windows (64-bit)")]
        public static void Windows()
        {
            Build(BuildTarget.StandaloneWindows64, "Builds/Windows/Pasjans.exe");
        }

        [MenuItem("Pasjans/Zbuduj/Android (APK)")]
        public static void Android()
        {
            bool previousBundleSetting = EditorUserBuildSettings.buildAppBundle;
            try
            {
                EditorUserBuildSettings.buildAppBundle = false;
                Build(BuildTarget.Android, "Builds/Android/Pasjans.apk");
            }
            finally
            {
                EditorUserBuildSettings.buildAppBundle = previousBundleSetting;
            }
        }


        private static void Build(BuildTarget target, string outputPath)
        {
            if (target == BuildTarget.Android)
            {
                // Zwykłe Activity jest stabilniejsze na szerszej gamie urządzeń.
                PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
                // Jeden zgodny interfejs omija czarne ekrany wadliwych sterowników Vulkan.
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            }
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/AppIcon.png");
            if (icon != null)
                PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            // Unity 6 pozwala wyłączyć ekran startowy także w bezpłatnej licencji Personal.
            PlayerSettings.SplashScreen.show = false;
            AssetDatabase.SaveAssets();
            BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(target);
            if (!BuildPipeline.IsBuildTargetSupported(group, target))
                throw new BuildFailedException("Brakuje modułu platformy " + target +
                                               ". Dodaj go w Unity Hub dla bieżącej wersji Unity.");

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled && File.Exists(scene.path))
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
                throw new BuildFailedException("Nie znaleziono włączonej sceny gry w Build Profiles.");

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? "Builds");
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = target,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Budowanie " + target + " nie powiodło się: " +
                                               report.summary.result + ", błędów: " + report.summary.totalErrors);
            Debug.Log("Gotowa gra: " + Path.GetFullPath(outputPath) + " (" +
                      Math.Round(report.summary.totalSize / (1024d * 1024d), 1) + " MB).");
        }
    }
}
