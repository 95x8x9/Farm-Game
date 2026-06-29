#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FarmGame.Editor
{
    public static class WebBuildCommand
    {
        private const string OutputDirectory = "Builds/Web";

        [MenuItem("Farm Game/Build/Web/Development")]
        public static void BuildDevelopment()
        {
            Build(BuildOptions.Development);
        }

        [MenuItem("Farm Game/Build/Web/Release")]
        public static void BuildRelease()
        {
            Build(BuildOptions.None);
        }

        private static void Build(BuildOptions options)
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled && File.Exists(scene.path))
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                throw new InvalidOperationException("No enabled scenes are configured in Build Profiles.");
            }

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string outputPath = Path.Combine(projectRoot, OutputDirectory);
            Directory.CreateDirectory(outputPath);

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.WebGL,
                options = options
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Web build failed with result {report.summary.result} and {report.summary.totalErrors} errors.");
            }

            Debug.Log(
                $"Web build completed: {outputPath} ({report.summary.totalSize} bytes, {report.summary.totalTime}).");
        }
    }
}
#endif
