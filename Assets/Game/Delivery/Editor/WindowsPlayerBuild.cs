using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SantasLittleHelpers.Editor
{
    /// <summary>Builds the shipping mission first while keeping the controller sandbox available.</summary>
    public static class WindowsPlayerBuild
    {
        private static readonly string[] Scenes =
        {
            "Assets/Game/Scenes/DeliveryHouse.unity",
            "Assets/Game/Scenes/MovementSandbox.unity"
        };

        public static void Build()
        {
            string outputPath = GetOutputPath();
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });

            Debug.Log($"WINDOWS_BUILD result={report.summary.result} errors={report.summary.totalErrors} warnings={report.summary.totalWarnings} path={outputPath}");
            if (report.summary.result != BuildResult.Succeeded || report.summary.totalErrors > 0)
                throw new InvalidOperationException($"Windows player build failed: {report.summary.result}, errors={report.summary.totalErrors}");
        }

        private static string GetOutputPath()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-slhBuildPath") return Path.GetFullPath(args[i + 1]);

            return Path.GetFullPath("C:/tmp/slh13-windows/DeliveryHouse.exe");
        }
    }
}
