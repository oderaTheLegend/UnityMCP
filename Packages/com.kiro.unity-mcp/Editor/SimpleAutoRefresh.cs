using UnityEngine;
using UnityEditor;
using UnityEditor.Compilation;
using System.IO;
using System;
using System.Reflection;

namespace Kiro.Unity.MCP
{
    [InitializeOnLoad]
    public class SimpleAutoRefresh
    {
        private static FileSystemWatcher watcher;
        private static DateTime lastRefresh = DateTime.MinValue;
        private static bool pendingRefresh = false;
        private static string pendingFile = "";
        
        static SimpleAutoRefresh()
        {
            // Disable Unity's built-in auto-refresh to prevent double-refresh
            EditorPrefs.SetBool("kAutoRefresh", false);
            Debug.Log("Kiro: Disabled Unity's built-in auto-refresh to prevent double-refresh");
            
            SetupAutoRefresh();
            // Also hook into Unity's update loop for aggressive refresh
            EditorApplication.update += OnEditorUpdate;
        }
        
        private static void SetupAutoRefresh()
        {
            try
        {
                string assetsPath = Application.dataPath;
                watcher = new FileSystemWatcher(assetsPath, "*.cs");
                watcher.IncludeSubdirectories = true;
                watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime;
                watcher.Changed += OnFileChanged;
                watcher.Created += OnFileChanged;
                watcher.Deleted += OnFileChanged;
                watcher.Renamed += OnFileChanged;
                watcher.EnableRaisingEvents = true;
                
                Debug.Log("Kiro: Auto-refresh enabled for C# files");
            }
            catch (Exception e)
            {
                Debug.LogError($"Kiro: Failed to setup auto-refresh: {e.Message}");
            }
        }
        
        private static void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            // Cooldown to prevent spam
            if (DateTime.Now - lastRefresh < TimeSpan.FromSeconds(1))
                return;
            
            // Mark that we need a refresh and let the update loop handle it
            pendingRefresh = true;
            pendingFile = Path.GetFileName(e.FullPath);
            
            // Also try immediate refresh
            EditorApplication.delayCall += () => {
                PerformAggressiveRefresh(pendingFile);
            };
        }
        
        private static void OnEditorUpdate()
        {
            // Continuously try to refresh if we have a pending refresh
            if (pendingRefresh && DateTime.Now - lastRefresh > TimeSpan.FromSeconds(0.5))
            {
                PerformAggressiveRefresh(pendingFile);
                pendingRefresh = false;
            }
        }
        
        private static void PerformAggressiveRefresh(string fileName)
        {
            try
            {
                Debug.Log($"Kiro: AGGRESSIVE refresh for {fileName}");
                
                // Strategy 1: Force synchronous import
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                
                // Strategy 2: Force compilation pipeline
                CompilationPipeline.RequestScriptCompilation();
                
                // Strategy 3: Force asset database operations
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                
                // Strategy 4: Try to trigger Unity's internal refresh
                EditorApplication.QueuePlayerLoopUpdate();
                
                // Strategy 5: Force repaint of project window (sometimes triggers refresh)
                EditorApplication.RepaintProjectWindow();
                
                // Strategy 6: Try to access Unity's internal refresh via reflection
                TryInternalRefresh();
                
                lastRefresh = DateTime.Now;
                Debug.Log("Kiro: Aggressive refresh completed - compilation should start!");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Kiro: Auto-refresh failed: {ex.Message}");
            }
        }
        
        private static void TryInternalRefresh()
        {
            try
            {
                // Try to access Unity's internal AssetDatabase refresh methods
                var assetDatabaseType = typeof(AssetDatabase);
                
                // Method 1: Try ForceReserializeAssets (forces Unity to process assets)
                var forceReserializeMethod = assetDatabaseType.GetMethod("ForceReserializeAssets", 
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                if (forceReserializeMethod != null)
                {
                    forceReserializeMethod.Invoke(null, new object[] { new string[0], ForceReserializeAssetsOptions.ReserializeAssetsAndMetadata });
                    Debug.Log("Kiro: ForceReserializeAssets called");
                }
                
                // Method 2: Try to trigger compilation directly
                var compilationPipelineType = typeof(CompilationPipeline);
                var assemblyReloadEventsType = compilationPipelineType.Assembly.GetType("UnityEditor.Compilation.AssemblyReloadEvents");
                if (assemblyReloadEventsType != null)
                {
                    var beforeAssemblyReloadField = assemblyReloadEventsType.GetField("beforeAssemblyReload", BindingFlags.Static | BindingFlags.Public);
                    if (beforeAssemblyReloadField != null)
                    {
                        Debug.Log("Kiro: Attempting to trigger assembly reload");
                    }
                }
                
                // Method 3: Force asset import
                AssetDatabase.ImportAsset("Assets", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
                
            }
            catch (Exception ex)
            {
                Debug.Log($"Kiro: Internal refresh attempt failed (this is normal): {ex.Message}");
            }
        }
    }
}