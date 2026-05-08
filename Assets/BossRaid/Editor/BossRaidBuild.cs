using System.IO;
using UnityEditor;
using UnityEngine;

namespace BossRaid.Editor
{
    public static class BossRaidBuild
    {
        private const string BuildDirectory = "Build/BossRaidOverlay";
        private const string ExecutableName = "BossRaidOverlay.exe";

        public static void BuildWindows()
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var outputDirectory = Path.Combine(projectRoot, BuildDirectory);
            Directory.CreateDirectory(outputDirectory);

            var outputPath = Path.Combine(outputDirectory, ExecutableName);
            var report = BuildPipeline.BuildPlayer(
                new[] { "Assets/Scenes/SampleScene.unity" },
                outputPath,
                BuildTarget.StandaloneWindows64,
                BuildOptions.None);

            var settingSource = Path.Combine(projectRoot, "Setting.Json");
            var settingTarget = Path.Combine(outputDirectory, "Setting.Json");
            if (File.Exists(settingSource))
            {
                File.Copy(settingSource, settingTarget, true);
            }

            CopyFileIfMissing(Path.Combine(projectRoot, "API.Json"), Path.Combine(outputDirectory, "API.Json"));
            CopyFileIfExists(Path.Combine(projectRoot, "API.Json.example"), Path.Combine(outputDirectory, "API.Json.example"));
            CopyDirectoryIfExists(Path.Combine(projectRoot, "Bridge"), Path.Combine(outputDirectory, "Bridge"));
            CopyFileIfExists(Path.Combine(projectRoot, "README.md"), Path.Combine(outputDirectory, "README.md"));
            RemoveDoNotShipFolders(outputDirectory);

            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                throw new System.Exception($"BossRaidOverlay build failed: {report.summary.result}");
            }

            Debug.Log($"BossRaidOverlay build complete: {outputPath}");
        }

        private static void CopyFileIfExists(string source, string target)
        {
            if (!File.Exists(source))
            {
                return;
            }

            File.Copy(source, target, true);
        }

        private static void CopyFileIfMissing(string source, string target)
        {
            if (!File.Exists(source) || File.Exists(target))
            {
                return;
            }

            File.Copy(source, target, false);
        }

        private static void CopyDirectoryIfExists(string source, string target)
        {
            if (!Directory.Exists(source))
            {
                return;
            }

            if (Directory.Exists(target))
            {
                Directory.Delete(target, true);
            }

            Directory.CreateDirectory(target);
            foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            {
                if (directory.Contains("__pycache__"))
                {
                    continue;
                }

                Directory.CreateDirectory(directory.Replace(source, target));
            }

            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                if (file.Contains("__pycache__"))
                {
                    continue;
                }

                File.Copy(file, file.Replace(source, target), true);
            }
        }

        private static void RemoveDoNotShipFolders(string outputDirectory)
        {
            foreach (var directory in Directory.GetDirectories(outputDirectory, "*_BurstDebugInformation_DoNotShip", SearchOption.TopDirectoryOnly))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
