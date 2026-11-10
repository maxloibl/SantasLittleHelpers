using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SantasLittleHelpers.Savegames.Editor
{
    public static class SaveSlotSmokeRunner
    {
        public static void Run()
        {
            string testRoot = ResolveTestRoot();
            try
            {
                Directory.CreateDirectory(testRoot);
                Debug.Log($"SAVE_SLOT_SMOKE_START root={testRoot} unity={Application.unityVersion}");
                int passed = SaveSlotSmokeTests.RunAll(testRoot);
                Debug.Log($"SAVE_SLOT_SMOKE_PASS tests={passed} root={testRoot}");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(0);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"SAVE_SLOT_SMOKE_FAIL error={exception.GetType().Name} " +
                    $"message={exception.Message}\n{exception.StackTrace}");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }

                throw;
            }
        }

        private static string ResolveTestRoot()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length - 1; index++)
            {
                if (arguments[index] == "-slhSaveSlotTestRoot")
                {
                    return Path.GetFullPath(arguments[index + 1]);
                }
            }

            return Path.Combine(Path.GetTempPath(), "slh36-save-slot-smoke");
        }
    }
}
