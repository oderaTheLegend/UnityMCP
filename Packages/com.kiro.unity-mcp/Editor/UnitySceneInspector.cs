using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;

namespace Kiro.Unity.MCP
{
    public static class UnitySceneInspector
    {
        [MenuItem("Kiro/Export Scene Info")]
        public static void ExportSceneInfo()
        {
            var sceneInfo = GetCurrentSceneInfo();
            string json = JsonUtility.ToJson(sceneInfo, true);
            
            string path = "Temp/KiroSceneInfo/current_scene.json";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, json);
            
            Debug.Log($"Kiro: Scene info exported to {path}");
        }
        
        public static SceneInfo GetCurrentSceneInfo()
        {
            var sceneInfo = new SceneInfo();
            sceneInfo.sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            sceneInfo.scenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            sceneInfo.timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            
            // Get all GameObjects in the scene
            GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>();
            sceneInfo.gameObjects = new List<GameObjectInfo>();
            
            foreach (GameObject go in allObjects)
            {
                // Only include root objects and their immediate children for now
                if (go.transform.parent == null || go.transform.parent.parent == null)
                {
                    var goInfo = CreateGameObjectInfo(go);
                    sceneInfo.gameObjects.Add(goInfo);
                }
            }
            
            // Get camera information
            Camera[] cameras = GameObject.FindObjectsOfType<Camera>();
            sceneInfo.cameras = new List<CameraInfo>();
            
            foreach (Camera cam in cameras)
            {
                var camInfo = new CameraInfo
                {
                    name = cam.name,
                    position = cam.transform.position,
                    rotation = cam.transform.rotation.eulerAngles,
                    fieldOfView = cam.fieldOfView,
                    isOrthographic = cam.orthographic,
                    orthographicSize = cam.orthographicSize,
                    nearClipPlane = cam.nearClipPlane,
                    farClipPlane = cam.farClipPlane,
                    isMainCamera = cam.CompareTag("MainCamera")
                };
                sceneInfo.cameras.Add(camInfo);
            }
            
            // Get lighting information
            sceneInfo.lightingInfo = new LightingInfo
            {
                ambientMode = RenderSettings.ambientMode.ToString(),
                ambientColor = RenderSettings.ambientSkyColor,
                fogEnabled = RenderSettings.fog,
                fogColor = RenderSettings.fogColor
            };
            
            return sceneInfo;
        }
        
        private static GameObjectInfo CreateGameObjectInfo(GameObject go)
        {
            var goInfo = new GameObjectInfo
            {
                name = go.name,
                isActive = go.activeInHierarchy,
                position = go.transform.position,
                rotation = go.transform.rotation.eulerAngles,
                scale = go.transform.localScale,
                tag = go.tag,
                layer = LayerMask.LayerToName(go.layer),
                components = new List<ComponentInfo>()
            };
            
            // Get component information
            Component[] components = go.GetComponents<Component>();
            foreach (Component comp in components)
            {
                if (comp != null)
                {
                    var compInfo = new ComponentInfo
                    {
                        type = comp.GetType().Name,
                        enabled = true
                    };
                    
                    // Add specific component details
                    if (comp is Renderer renderer)
                    {
                        compInfo.enabled = renderer.enabled;
                        compInfo.details = $"Material: {(renderer.material ? renderer.material.name : "None")}";
                    }
                    else if (comp is Collider collider)
                    {
                        compInfo.enabled = collider.enabled;
                        compInfo.details = $"IsTrigger: {collider.isTrigger}";
                    }
                    else if (comp is MonoBehaviour monoBehaviour)
                    {
                        compInfo.enabled = monoBehaviour.enabled;
                    }
                    
                    goInfo.components.Add(compInfo);
                }
            }
            
            // Get children (one level deep)
            goInfo.children = new List<GameObjectInfo>();
            for (int i = 0; i < go.transform.childCount; i++)
            {
                Transform child = go.transform.GetChild(i);
                var childInfo = CreateGameObjectInfo(child.gameObject);
                goInfo.children.Add(childInfo);
            }
            
            return goInfo;
        }
        
        public static void SaveSceneInfoToFile()
        {
            var sceneInfo = GetCurrentSceneInfo();
            string json = JsonUtility.ToJson(sceneInfo, true);
            
            string directory = "Temp/KiroSceneInfo";
            Directory.CreateDirectory(directory);
            
            string filename = $"scene_{sceneInfo.sceneName}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            string fullPath = Path.Combine(directory, filename);
            
            File.WriteAllText(fullPath, json);
            Debug.Log($"Kiro: Scene info saved to {fullPath}");
        }
    }
    
    [System.Serializable]
    public class SceneInfo
    {
        public string sceneName;
        public string scenePath;
        public string timestamp;
        public List<GameObjectInfo> gameObjects;
        public List<CameraInfo> cameras;
        public LightingInfo lightingInfo;
    }
    
    [System.Serializable]
    public class GameObjectInfo
    {
        public string name;
        public bool isActive;
        public Vector3 position;
        public Vector3 rotation;
        public Vector3 scale;
        public string tag;
        public string layer;
        public List<ComponentInfo> components;
        public List<GameObjectInfo> children;
    }
    
    [System.Serializable]
    public class ComponentInfo
    {
        public string type;
        public bool enabled;
        public string details;
    }
    
    [System.Serializable]
    public class CameraInfo
    {
        public string name;
        public Vector3 position;
        public Vector3 rotation;
        public float fieldOfView;
        public bool isOrthographic;
        public float orthographicSize;
        public float nearClipPlane;
        public float farClipPlane;
        public bool isMainCamera;
    }
    
    [System.Serializable]
    public class LightingInfo
    {
        public string ambientMode;
        public Color ambientColor;
        public bool fogEnabled;
        public Color fogColor;
    }
}