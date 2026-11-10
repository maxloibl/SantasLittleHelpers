using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SantasLittleHelpers.Home.Editor
{
    public static class HomeMenuSmokeRunner
    {
        [MenuItem("Tools/Home/Run Home Menu Editor Smoke")]
        public static void Run()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string root = Path.Combine(
                projectRoot ?? Directory.GetCurrentDirectory(),
                "Logs",
                "home-menu-tests",
                Guid.NewGuid().ToString("N"));

            try
            {
                int cases = HomeMenuSmokeTests.RunAll(root);
                Debug.Log($"HOME_MENU_EDITOR_SMOKE_PASS cases={cases} root={root}");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(0);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"HOME_MENU_EDITOR_SMOKE_FAIL root={root} " +
                    $"error={exception.GetType().Name} message={exception.Message}\n{exception.StackTrace}");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }

                throw;
            }
        }
    }
}
