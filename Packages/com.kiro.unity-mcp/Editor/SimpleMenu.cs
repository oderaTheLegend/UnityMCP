using UnityEngine;
using UnityEditor;
using System.IO;

namespace Kiro.Unity.MCP
{
    public class SimpleMenu
    {
        [MenuItem("Kiro/Test Auto-Refresh")]
        public static void TestAutoRefresh()
        {
            string testPath = Path.Combine(Application.dataPath, "KiroAutoRefreshTest.cs");
            string testContent = $@"using UnityEngine;

// Auto-refresh test created at {System.DateTime.Now:HH:mm:ss}
public class KiroAutoRefreshTest : MonoBehaviour
{{
    void Start()
    {{
        Debug.Log(""Kiro auto-refresh is working!"");
    }}
}}";
            
            try
            {
                File.WriteAllText(testPath, testContent);
                Debug.Log("Kiro: Test file created - watch for auto-refresh message");
                
                // Clean up after 3 seconds
                EditorApplication.delayCall += () => {
                    var startTime = EditorApplication.timeSinceStartup;
                    EditorApplication.CallbackFunction cleanup = null;
                    cleanup = () => {
                        if (EditorApplication.timeSinceStartup - startTime > 3.0)
                        {
                            EditorApplication.update -= cleanup;
                            if (File.Exists(testPath))
                            {
                                File.Delete(testPath);
                                AssetDatabase.Refresh();
                                Debug.Log("Kiro: Test file cleaned up");
                            }
                        }
                    };
                    EditorApplication.update += cleanup;
                };
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Kiro: Test failed - {e.Message}");
            }
        }
        
        [MenuItem("Kiro/Toggle Unity Built-in Auto-Refresh")]
        public static void ToggleUnityAutoRefresh()
        {
            bool current = EditorPrefs.GetBool("kAutoRefresh", true);
            EditorPrefs.SetBool("kAutoRefresh", !current);
            
            string message = !current ? 
                "Unity's built-in auto-refresh ENABLED\n\nYou'll now get double-refresh (background + focus)" :
                "Unity's built-in auto-refresh DISABLED\n\nOnly Kiro's background refresh will work";
                
            Debug.Log($"Kiro: Unity built-in auto-refresh {(!current ? "enabled" : "disabled")}");
            EditorUtility.DisplayDialog("Auto-Refresh Toggle", message, "OK");
        }
    }
}