using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.IO;
using System;

namespace Kiro.Unity.MCP
{
    [InitializeOnLoad]
    public static class KiroCommandProcessor
    {
        private static string commandFile = "Temp/KiroCommands/command.txt";
        private static string resultFile = "Temp/KiroCommands/result.txt";
        
        static KiroCommandProcessor()
        {
            EditorApplication.update += CheckForCommands;
        }
        
        private static void CheckForCommands()
        {
            if (File.Exists(commandFile))
            {
                try
                {
                    string command = File.ReadAllText(commandFile).Trim();
                    File.Delete(commandFile);
                    
                    string result = ProcessCommand(command);
                    
                    Directory.CreateDirectory(Path.GetDirectoryName(resultFile));
                    File.WriteAllText(resultFile, result);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Kiro: Command processing error: {e.Message}");
                }
            }
        }
        
        private static string ProcessCommand(string command)
        {
            try
            {
                string[] parts = command.Split('|');
                string action = parts[0];
                
                switch (action)
                {
                    case "capture_scene":
                        int sceneDelay = parts.Length > 1 && int.TryParse(parts[1], out int sd) ? sd : 0;
                        if (sceneDelay > 0)
                        {
                            EditorApplication.delayCall += () => {
                                System.Threading.Thread.Sleep(sceneDelay * 1000);
                                UnityScreenshotCapture.CaptureSceneViewToFile();
                            };
                            return $"Scene capture scheduled in {sceneDelay} seconds";
                        }
                        return UnityScreenshotCapture.CaptureSceneViewToFile() ?? "Failed";
                        
                    case "capture_game":
                        int gameDelay = parts.Length > 1 && int.TryParse(parts[1], out int gd) ? gd : 0;
                        if (gameDelay > 0)
                        {
                            EditorApplication.delayCall += () => {
                                System.Threading.Thread.Sleep(gameDelay * 1000);
                                UnityScreenshotCapture.CaptureGameViewToFile();
                            };
                            return $"Game capture scheduled in {gameDelay} seconds";
                        }
                        return UnityScreenshotCapture.CaptureGameViewToFile() ?? "Failed";
                        
                    case "capture_editor":
                        int editorDelay = parts.Length > 1 && int.TryParse(parts[1], out int ed) ? ed : 0;
                        if (editorDelay > 0)
                        {
                            EditorApplication.delayCall += () => {
                                System.Threading.Thread.Sleep(editorDelay * 1000);
                                UnityScreenshotCapture.CaptureUnityEditorToFile();
                            };
                            return $"Editor capture scheduled in {editorDelay} seconds";
                        }
                        return UnityScreenshotCapture.CaptureUnityEditorToFile() ?? "Failed";
                        
                    case "capture_camera":
                        string cameraName = parts.Length > 1 ? parts[1] : "Main Camera";
                        int width = parts.Length > 2 && int.TryParse(parts[2], out int w) ? w : 0;
                        int height = parts.Length > 3 && int.TryParse(parts[3], out int h) ? h : 0;
                        
                        Camera camera = GameObject.Find(cameraName)?.GetComponent<Camera>();
                        if (camera == null)
                        {
                            GameObject camObj = GameObject.FindWithTag("MainCamera");
                            if (camObj != null) camera = camObj.GetComponent<Camera>();
                        }
                        return camera != null ? UnityScreenshotCapture.CaptureCameraView(camera, width, height) ?? "Failed" : "Camera not found";
                        
                    case "scene_info":
                        UnitySceneInspector.SaveSceneInfoToFile();
                        return "Scene info saved";
                        
                    case "get_hierarchy":
                        Debug.Log("Kiro: Processing get_hierarchy command");
                        string hierarchyResult = GetSimpleHierarchy();
                        Debug.Log($"Kiro: Hierarchy result: {hierarchyResult}");
                        return hierarchyResult;
                        
                    case "create_gameobject":
                        string objName = parts.Length > 1 ? parts[1] : "GameObject";
                        string parentName = parts.Length > 2 ? parts[2] : "";
                        return CreateGameObject(objName, parentName);
                        
                    case "delete_gameobject":
                        string deleteObjName = parts.Length > 1 ? parts[1] : "";
                        return DeleteGameObject(deleteObjName);
                        
                    case "add_component":
                        if (parts.Length >= 3)
                        {
                            string targetObj = parts[1];
                            string componentType = parts[2];
                            return AddComponent(targetObj, componentType);
                        }
                        return "Error: add_component requires object_name, component_type";
                        
                    case "set_component_property":
                        if (parts.Length >= 5)
                        {
                            string targetObj = parts[1];
                            string componentType = parts[2];
                            string propertyName = parts[3];
                            string propertyValue = parts[4];
                            return SetComponentProperty(targetObj, componentType, propertyName, propertyValue);
                        }
                        return "Error: set_component_property requires object_name, component_type, property, value";
                        
                    case "remove_component":
                        if (parts.Length >= 3)
                        {
                            string targetObj = parts[1];
                            string componentType = parts[2];
                            return RemoveComponent(targetObj, componentType);
                        }
                        return "Error: remove_component requires object_name, component_type";
                        
                    case "set_property":
                        if (parts.Length >= 4)
                        {
                            string targetObj = parts[1];
                            string property = parts[2];
                            string value = parts[3];
                            return SetProperty(targetObj, property, value);
                        }
                        return "Error: set_property requires object_name, property, value";
                        
                    case "move_gameobject":
                        if (parts.Length >= 3)
                        {
                            string objToMove = parts[1];
                            string newParent = parts[2];
                            return MoveGameObject(objToMove, newParent);
                        }
                        return "Error: move_gameobject requires object_name, new_parent";
                        

                        
                    case "cleanup_screenshots":
                        int keepCount = parts.Length > 1 && int.TryParse(parts[1], out int count) ? count : 5;
                        UnityScreenshotCapture.CleanupOldScreenshots(keepCount);
                        return $"Screenshots cleaned up, kept {keepCount} most recent";
                        
                    default:
                        return $"Unknown command: {action}";
                }
            }
            catch (Exception e)
            {
                return $"Error: {e.Message}";
            }
        }
        
        private static string GetSimpleHierarchy()
        {
            try
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                var rootObjects = scene.GetRootGameObjects();
                
                // Get Unity state context
                bool isPlaying = EditorApplication.isPlaying;
                bool isPaused = EditorApplication.isPaused;
                string playModeState = isPlaying ? (isPaused ? "PAUSED" : "PLAYING") : "EDIT_MODE";
                
                string result = $"Scene: {scene.name} | Mode: {playModeState} | Root Objects: {rootObjects.Length}\n\n";
                
                // Add Unity-specific analysis
                result += "UNITY ANALYSIS:\n";
                result += AnalyzeSceneSetup(rootObjects, isPlaying);
                result += "\nHIERARCHY:\n";
                
                foreach (var rootObj in rootObjects)
                {
                    result += GetGameObjectString(rootObj, 0);
                }
                
                return result;
            }
            catch (System.Exception e)
            {
                return $"Error getting hierarchy: {e.Message}";
            }
        }
        
        private static string AnalyzeSceneSetup(GameObject[] rootObjects, bool isPlaying)
        {
            string analysis = "";
            
            // Check for Canvas setup issues
            foreach (var obj in rootObjects)
            {
                if (obj.GetComponent<Canvas>())
                {
                    var rectTransform = obj.GetComponent<RectTransform>();
                    var transform = obj.GetComponent<Transform>();
                    
                    if (rectTransform == null && transform != null)
                    {
                        analysis += "❌ ERROR: Canvas has Transform instead of RectTransform - BROKEN UI SETUP\n";
                    }
                    else if (rectTransform != null)
                    {
                        analysis += "✅ Canvas has correct RectTransform\n";
                    }
                }
                
                // Check EventSystem
                var eventSystem = obj.GetComponent<UnityEngine.EventSystems.EventSystem>();
                if (eventSystem != null)
                {
                    bool isCurrent = UnityEngine.EventSystems.EventSystem.current == eventSystem;
                    if (!isPlaying && !isCurrent)
                    {
                        analysis += "✅ EventSystem not current (normal in Edit Mode)\n";
                    }
                    else if (isPlaying && !isCurrent)
                    {
                        analysis += "❌ ERROR: EventSystem not current during Play Mode - UI BROKEN\n";
                    }
                    else if (isPlaying && isCurrent)
                    {
                        analysis += "✅ EventSystem is current (correct in Play Mode)\n";
                    }
                }
            }
            
            return analysis;
        }
        
        private static string GetGameObjectString(GameObject go, int depth)
        {
            string indent = new string(' ', depth * 2);
            string result = $"{indent}- {go.name} (Active: {go.activeInHierarchy}, Tag: {go.tag}, Layer: {LayerMask.LayerToName(go.layer)})\n";
            result += $"{indent}  Position: {go.transform.position}, Rotation: {go.transform.eulerAngles}, Scale: {go.transform.localScale}\n";
            
            // Add detailed component information
            var components = go.GetComponents<Component>();
            foreach (var comp in components)
            {
                if (comp != null)
                {
                    result += GetComponentDetails(comp, indent + "  ");
                }
            }
            
            // Add children
            for (int i = 0; i < go.transform.childCount; i++)
            {
                var child = go.transform.GetChild(i).gameObject;
                result += GetGameObjectString(child, depth + 1);
            }
            
            return result;
        }
        
        private static string GetComponentDetails(Component comp, string indent)
        {
            string result = $"{indent}+ {comp.GetType().Name}";
            
            try
            {
                // Add specific component details
                if (comp is Camera camera)
                {
                    result += $" (FOV: {camera.fieldOfView}, Orthographic: {camera.orthographic}, ClearFlags: {camera.clearFlags}, Depth: {camera.depth})";
                }
                else if (comp is AudioListener audioListener)
                {
                    result += $" (Enabled: {audioListener.enabled})";
                }
                else if (comp is Canvas canvas)
                {
                    result += $" (RenderMode: {canvas.renderMode}, SortingOrder: {canvas.sortingOrder}, PixelPerfect: {canvas.pixelPerfect})";
                }
                else if (comp is UnityEngine.UI.Image image)
                {
                    result += $" (Sprite: {(image.sprite ? image.sprite.name : "None")}, Color: {image.color}, Type: {image.type}, Raycast: {image.raycastTarget})";
                }
                else if (comp is CanvasRenderer canvasRenderer)
                {
                    result += $" (CullTransparentMesh: {canvasRenderer.cullTransparentMesh})";
                }
                else if (comp is UnityEngine.EventSystems.EventSystem eventSystem)
                {
                    result += $" (Current: {UnityEngine.EventSystems.EventSystem.current == eventSystem}, FirstSelected: {(eventSystem.firstSelectedGameObject ? eventSystem.firstSelectedGameObject.name : "None")})";
                }
                else if (comp is UnityEngine.EventSystems.StandaloneInputModule inputModule)
                {
                    result += $" (HorizontalAxis: {inputModule.horizontalAxis}, VerticalAxis: {inputModule.verticalAxis}, SubmitButton: {inputModule.submitButton})";
                }
                else if (comp is RectTransform rectTransform)
                {
                    result += $" (AnchoredPos: {rectTransform.anchoredPosition}, SizeDelta: {rectTransform.sizeDelta}, AnchorMin: {rectTransform.anchorMin}, AnchorMax: {rectTransform.anchorMax})";
                }
                else if (comp is Transform transform)
                {
                    result += $" (LocalPos: {transform.localPosition}, LocalRot: {transform.localEulerAngles}, LocalScale: {transform.localScale})";
                }
                else if (comp is Renderer renderer)
                {
                    result += $" (Enabled: {renderer.enabled}, Material: {(renderer.material ? renderer.material.name : "None")}, SortingLayer: {renderer.sortingLayerName}, SortingOrder: {renderer.sortingOrder})";
                }
                else if (comp is Collider collider)
                {
                    result += $" (Enabled: {collider.enabled}, IsTrigger: {collider.isTrigger}, Material: {(collider.material ? collider.material.name : "None")})";
                }
                else if (comp is MonoBehaviour monoBehaviour)
                {
                    result += $" (Enabled: {monoBehaviour.enabled}, Script: {comp.GetType().Name})";
                }
                else
                {
                    // Generic component info
                    if (comp is Behaviour behaviour)
                    {
                        result += $" (Enabled: {behaviour.enabled})";
                    }
                }
            }
            catch (System.Exception e)
            {
                result += $" (Error reading properties: {e.Message})";
            }
            
            result += "\n";
            return result;
        }
        
        private static object GetGameObjectData(GameObject go, int depth)
        {
            var components = new System.Collections.Generic.List<string>();
            var comps = go.GetComponents<Component>();
            foreach (var comp in comps)
            {
                if (comp != null)
                    components.Add(comp.GetType().Name);
            }
            
            var children = new System.Collections.Generic.List<object>();
            for (int i = 0; i < go.transform.childCount; i++)
            {
                var child = go.transform.GetChild(i).gameObject;
                children.Add(GetGameObjectData(child, depth + 1));
            }
            
            return new {
                name = go.name,
                active = go.activeInHierarchy,
                tag = go.tag,
                layer = LayerMask.LayerToName(go.layer),
                position = go.transform.position.ToString(),
                components = components,
                childCount = go.transform.childCount,
                children = children,
                depth = depth
            };
        }
        
        private static string CreateGameObject(string name, string parentName = "")
        {
            try
            {
                var go = new GameObject(name);
                
                if (!string.IsNullOrEmpty(parentName))
                {
                    var parent = GameObject.Find(parentName);
                    if (parent != null)
                    {
                        go.transform.SetParent(parent.transform);
                    }
                }
                
                return $"✅ Created GameObject: {name} at {go.transform.position}";
            }
            catch (System.Exception e)
            {
                return $"❌ Error creating GameObject: {e.Message}";
            }
        }
        
        private static string DeleteGameObject(string name)
        {
            try
            {
                var go = GameObject.Find(name);
                if (go != null)
                {
                    GameObject.DestroyImmediate(go);
                    return $"✅ Deleted GameObject: {name}";
                }
                return $"❌ GameObject not found: {name}";
            }
            catch (System.Exception e)
            {
                return $"❌ Error deleting GameObject: {e.Message}";
            }
        }
        
        private static string AddComponent(string objectName, string componentType)
        {
            try
            {
                var go = GameObject.Find(objectName);
                if (go == null)
                {
                    return $"❌ GameObject not found: {objectName}";
                }
                
                System.Type type = GetComponentType(componentType);
                if (type == null)
                {
                    return $"❌ Unknown component type: {componentType}";
                }
                
                var component = go.AddComponent(type);
                return $"✅ Added {componentType} to {objectName}";
            }
            catch (System.Exception e)
            {
                return $"❌ Error adding component: {e.Message}";
            }
        }
        
        private static string RemoveComponent(string objectName, string componentType)
        {
            try
            {
                var go = GameObject.Find(objectName);
                if (go == null)
                {
                    return $"❌ GameObject not found: {objectName}";
                }
                
                System.Type type = GetComponentType(componentType);
                if (type == null)
                {
                    return $"❌ Unknown component type: {componentType}";
                }
                
                var component = go.GetComponent(type);
                if (component != null)
                {
                    GameObject.DestroyImmediate(component);
                    return $"✅ Removed {componentType} from {objectName}";
                }
                
                return $"❌ Component {componentType} not found on {objectName}";
            }
            catch (System.Exception e)
            {
                return $"❌ Error removing component: {e.Message}";
            }
        }
        
        private static string SetProperty(string objectName, string property, string value)
        {
            try
            {
                var go = GameObject.Find(objectName);
                if (go == null)
                {
                    return $"❌ GameObject not found: {objectName}";
                }
                
                switch (property.ToLower())
                {
                    case "position":
                        go.transform.position = ParseVector3(value);
                        return $"✅ Set {objectName} position to {value}";
                        
                    case "rotation":
                        go.transform.eulerAngles = ParseVector3(value);
                        return $"✅ Set {objectName} rotation to {value}";
                        
                    case "scale":
                        go.transform.localScale = ParseVector3(value);
                        return $"✅ Set {objectName} scale to {value}";
                        
                    case "active":
                        go.SetActive(bool.Parse(value));
                        return $"✅ Set {objectName} active to {value}";
                        
                    case "name":
                        go.name = value;
                        return $"✅ Renamed GameObject to {value}";
                        
                    case "layer":
                        int layerIndex = LayerMask.NameToLayer(value);
                        if (layerIndex == -1)
                        {
                            return $"❌ Layer not found: {value}";
                        }
                        go.layer = layerIndex;
                        return $"✅ Set {objectName} layer to {value}";
                        
                    case "tag":
                        try
                        {
                            go.tag = value;
                            return $"✅ Set {objectName} tag to {value}";
                        }
                        catch
                        {
                            return $"❌ Tag not found: {value}";
                        }
                        
                    default:
                        return $"❌ Unknown property: {property}";
                }
            }
            catch (System.Exception e)
            {
                return $"❌ Error setting property: {e.Message}";
            }
        }
        
        private static string MoveGameObject(string objectName, string newParentName)
        {
            try
            {
                var go = GameObject.Find(objectName);
                if (go == null)
                {
                    return $"❌ GameObject not found: {objectName}";
                }
                
                Transform newParent = null;
                if (!string.IsNullOrEmpty(newParentName))
                {
                    var parentGO = GameObject.Find(newParentName);
                    if (parentGO == null)
                    {
                        return $"❌ Parent GameObject not found: {newParentName}";
                    }
                    newParent = parentGO.transform;
                }
                
                go.transform.SetParent(newParent);
                return $"✅ Moved {objectName} to {(newParent ? newParent.name : "root")}";
            }
            catch (System.Exception e)
            {
                return $"❌ Error moving GameObject: {e.Message}";
            }
        }
        
        private static System.Type GetComponentType(string componentType)
        {
            // Map common component names to types
            switch (componentType.ToLower())
            {
                // UI Components
                case "canvas": return typeof(Canvas);
                case "recttransform": return typeof(RectTransform);
                case "image": return typeof(UnityEngine.UI.Image);
                case "rawimage": return typeof(UnityEngine.UI.RawImage);
                case "text": return typeof(UnityEngine.UI.Text);
                case "button": return typeof(UnityEngine.UI.Button);
                case "toggle": return typeof(UnityEngine.UI.Toggle);
                case "slider": return typeof(UnityEngine.UI.Slider);
                case "scrollbar": return typeof(UnityEngine.UI.Scrollbar);
                case "dropdown": return typeof(UnityEngine.UI.Dropdown);
                case "inputfield": return typeof(UnityEngine.UI.InputField);
                case "scrollrect": return typeof(UnityEngine.UI.ScrollRect);
                case "mask": return typeof(UnityEngine.UI.Mask);
                case "canvasscaler": return typeof(CanvasScaler);
                case "graphicraycaster": return typeof(GraphicRaycaster);
                case "canvasrenderer": return typeof(CanvasRenderer);
                case "canvasgroup": return typeof(CanvasGroup);
                case "layoutelement": return typeof(UnityEngine.UI.LayoutElement);
                case "horizontallayoutgroup": return typeof(UnityEngine.UI.HorizontalLayoutGroup);
                case "verticallayoutgroup": return typeof(UnityEngine.UI.VerticalLayoutGroup);
                case "gridlayoutgroup": return typeof(UnityEngine.UI.GridLayoutGroup);
                case "contentsizefitter": return typeof(UnityEngine.UI.ContentSizeFitter);
                case "aspectratiofitter": return typeof(UnityEngine.UI.AspectRatioFitter);
                
                // Event System
                case "eventsystem": return typeof(UnityEngine.EventSystems.EventSystem);
                case "standaloneinputmodule": return typeof(UnityEngine.EventSystems.StandaloneInputModule);
                case "eventrigger": return typeof(UnityEngine.EventSystems.EventTrigger);
                
                // Rendering
                case "camera": return typeof(Camera);
                case "light": return typeof(Light);
                case "meshrenderer": return typeof(MeshRenderer);
                case "skinnedmeshrenderer": return typeof(SkinnedMeshRenderer);
                case "spriterenderer": return typeof(SpriteRenderer);
                case "linerenderer": return typeof(LineRenderer);
                case "trailrenderer": return typeof(TrailRenderer);
                case "particlesystem": return typeof(ParticleSystem);
                case "meshfilter": return typeof(MeshFilter);
                
                // Physics 3D
                case "rigidbody": return typeof(Rigidbody);
                case "collider": return typeof(Collider);
                case "boxcollider": return typeof(BoxCollider);
                case "spherecollider": return typeof(SphereCollider);
                case "capsulecollider": return typeof(CapsuleCollider);
                case "meshcollider": return typeof(MeshCollider);
                case "charactercontroller": return typeof(CharacterController);
                case "fixedjoint": return typeof(FixedJoint);
                case "hingejoint": return typeof(HingeJoint);
                case "springjoint": return typeof(SpringJoint);
                
                // Physics 2D
                case "rigidbody2d": return typeof(Rigidbody2D);
                case "boxcollider2d": return typeof(BoxCollider2D);
                case "circlecollider2d": return typeof(CircleCollider2D);
                case "polygoncollider2d": return typeof(PolygonCollider2D);
                case "edgecollider2d": return typeof(EdgeCollider2D);
                case "capsulecollider2d": return typeof(CapsuleCollider2D);
                
                // Audio
                case "audiosource": return typeof(AudioSource);
                case "audiolistener": return typeof(AudioListener);
                case "audioreverbzone": return typeof(AudioReverbZone);
                
                // Animation
                case "animator": return typeof(Animator);
                case "animation": return typeof(Animation);
                
                // Misc
                case "transform": return typeof(Transform);
                case "terrain": return typeof(Terrain);
                case "terraincolider": return typeof(TerrainCollider);
                case "windzone": return typeof(WindZone);
                
                default: return null;
            }
        }
        
        private static string SetComponentProperty(string objectName, string componentType, string propertyName, string value)
        {
            try
            {
                var go = GameObject.Find(objectName);
                if (go == null)
                {
                    return $"❌ GameObject not found: {objectName}";
                }
                
                System.Type type = GetComponentType(componentType);
                if (type == null)
                {
                    return $"❌ Unknown component type: {componentType}";
                }
                
                var component = go.GetComponent(type);
                if (component == null)
                {
                    return $"❌ Component {componentType} not found on {objectName}";
                }
                
                // Handle specific component properties
                if (component is Canvas canvas)
                {
                    if (propertyName.ToLower() == "rendermode")
                    {
                        if (value.ToLower() == "screenspaceoverlay")
                            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                        else if (value.ToLower() == "screenspacecamera")
                            canvas.renderMode = RenderMode.ScreenSpaceCamera;
                        else if (value.ToLower() == "worldspace")
                            canvas.renderMode = RenderMode.WorldSpace;
                        return $"✅ Set Canvas RenderMode to {canvas.renderMode}";
                    }
                }
                else if (component is RectTransform rectTransform)
                {
                    if (propertyName.ToLower() == "anchormin")
                    {
                        rectTransform.anchorMin = ParseVector2(value);
                        return $"✅ Set RectTransform anchorMin to {rectTransform.anchorMin}";
                    }
                    else if (propertyName.ToLower() == "anchormax")
                    {
                        rectTransform.anchorMax = ParseVector2(value);
                        return $"✅ Set RectTransform anchorMax to {rectTransform.anchorMax}";
                    }
                    else if (propertyName.ToLower() == "sizedelta")
                    {
                        rectTransform.sizeDelta = ParseVector2(value);
                        return $"✅ Set RectTransform sizeDelta to {rectTransform.sizeDelta}";
                    }
                    else if (propertyName.ToLower() == "anchoredposition")
                    {
                        rectTransform.anchoredPosition = ParseVector2(value);
                        return $"✅ Set RectTransform anchoredPosition to {rectTransform.anchoredPosition}";
                    }
                }
                else if (component is CanvasScaler canvasScaler)
                {
                    if (propertyName.ToLower() == "uiscalemode")
                    {
                        if (value.ToLower() == "constantpixelsize")
                            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                        else if (value.ToLower() == "scalewithscreensize")
                            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                        else if (value.ToLower() == "constantphysicalsize")
                            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPhysicalSize;
                        return $"✅ Set CanvasScaler uiScaleMode to {canvasScaler.uiScaleMode}";
                    }
                    else if (propertyName.ToLower() == "referenceresolution")
                    {
                        canvasScaler.referenceResolution = ParseVector2(value);
                        return $"✅ Set CanvasScaler referenceResolution to {canvasScaler.referenceResolution}";
                    }
                    else if (propertyName.ToLower() == "screenmatchmode")
                    {
                        if (value.ToLower() == "matchwidthorheight")
                            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                        else if (value.ToLower() == "expand")
                            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                        else if (value.ToLower() == "shrink")
                            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Shrink;
                        return $"✅ Set CanvasScaler screenMatchMode to {canvasScaler.screenMatchMode}";
                    }
                    else if (propertyName.ToLower() == "matchwidthorheight")
                    {
                        canvasScaler.matchWidthOrHeight = float.Parse(value);
                        return $"✅ Set CanvasScaler matchWidthOrHeight to {canvasScaler.matchWidthOrHeight}";
                    }
                }
                
                else if (component is UnityEngine.UI.Image image)
                {
                    if (propertyName.ToLower() == "color")
                    {
                        image.color = ParseColor(value);
                        return $"✅ Set Image color to {image.color}";
                    }
                    else if (propertyName.ToLower() == "raycasttarget")
                    {
                        image.raycastTarget = bool.Parse(value);
                        return $"✅ Set Image raycastTarget to {image.raycastTarget}";
                    }
                }
                else if (component is UnityEngine.UI.Text text)
                {
                    if (propertyName.ToLower() == "text")
                    {
                        text.text = value;
                        return $"✅ Set Text to: {value}";
                    }
                    else if (propertyName.ToLower() == "fontsize")
                    {
                        text.fontSize = int.Parse(value);
                        return $"✅ Set Text fontSize to {text.fontSize}";
                    }
                    else if (propertyName.ToLower() == "color")
                    {
                        text.color = ParseColor(value);
                        return $"✅ Set Text color to {text.color}";
                    }
                    else if (propertyName.ToLower() == "alignment")
                    {
                        text.alignment = ParseTextAnchor(value);
                        return $"✅ Set Text alignment to {text.alignment}";
                    }
                }
                else if (component is Camera camera)
                {
                    if (propertyName.ToLower() == "fieldofview" || propertyName.ToLower() == "fov")
                    {
                        camera.fieldOfView = float.Parse(value);
                        return $"✅ Set Camera fieldOfView to {camera.fieldOfView}";
                    }
                    else if (propertyName.ToLower() == "orthographic")
                    {
                        camera.orthographic = bool.Parse(value);
                        return $"✅ Set Camera orthographic to {camera.orthographic}";
                    }
                    else if (propertyName.ToLower() == "orthographicsize")
                    {
                        camera.orthographicSize = float.Parse(value);
                        return $"✅ Set Camera orthographicSize to {camera.orthographicSize}";
                    }
                    else if (propertyName.ToLower() == "depth")
                    {
                        camera.depth = float.Parse(value);
                        return $"✅ Set Camera depth to {camera.depth}";
                    }
                    else if (propertyName.ToLower() == "backgroundcolor")
                    {
                        camera.backgroundColor = ParseColor(value);
                        return $"✅ Set Camera backgroundColor to {camera.backgroundColor}";
                    }
                }
                else if (component is Light light)
                {
                    if (propertyName.ToLower() == "color")
                    {
                        light.color = ParseColor(value);
                        return $"✅ Set Light color to {light.color}";
                    }
                    else if (propertyName.ToLower() == "intensity")
                    {
                        light.intensity = float.Parse(value);
                        return $"✅ Set Light intensity to {light.intensity}";
                    }
                    else if (propertyName.ToLower() == "range")
                    {
                        light.range = float.Parse(value);
                        return $"✅ Set Light range to {light.range}";
                    }
                }
                else if (component is SpriteRenderer spriteRenderer)
                {
                    if (propertyName.ToLower() == "color")
                    {
                        spriteRenderer.color = ParseColor(value);
                        return $"✅ Set SpriteRenderer color to {spriteRenderer.color}";
                    }
                    else if (propertyName.ToLower() == "flipx")
                    {
                        spriteRenderer.flipX = bool.Parse(value);
                        return $"✅ Set SpriteRenderer flipX to {spriteRenderer.flipX}";
                    }
                    else if (propertyName.ToLower() == "flipy")
                    {
                        spriteRenderer.flipY = bool.Parse(value);
                        return $"✅ Set SpriteRenderer flipY to {spriteRenderer.flipY}";
                    }
                    else if (propertyName.ToLower() == "sortingorder")
                    {
                        spriteRenderer.sortingOrder = int.Parse(value);
                        return $"✅ Set SpriteRenderer sortingOrder to {spriteRenderer.sortingOrder}";
                    }
                }
                else if (component is Rigidbody rigidbody)
                {
                    if (propertyName.ToLower() == "mass")
                    {
                        rigidbody.mass = float.Parse(value);
                        return $"✅ Set Rigidbody mass to {rigidbody.mass}";
                    }
                    else if (propertyName.ToLower() == "drag")
                    {
                        rigidbody.drag = float.Parse(value);
                        return $"✅ Set Rigidbody drag to {rigidbody.drag}";
                    }
                    else if (propertyName.ToLower() == "angulardrag")
                    {
                        rigidbody.angularDrag = float.Parse(value);
                        return $"✅ Set Rigidbody angularDrag to {rigidbody.angularDrag}";
                    }
                    else if (propertyName.ToLower() == "usegravity")
                    {
                        rigidbody.useGravity = bool.Parse(value);
                        return $"✅ Set Rigidbody useGravity to {rigidbody.useGravity}";
                    }
                    else if (propertyName.ToLower() == "iskinematic")
                    {
                        rigidbody.isKinematic = bool.Parse(value);
                        return $"✅ Set Rigidbody isKinematic to {rigidbody.isKinematic}";
                    }
                }
                else if (component is Rigidbody2D rigidbody2D)
                {
                    if (propertyName.ToLower() == "mass")
                    {
                        rigidbody2D.mass = float.Parse(value);
                        return $"✅ Set Rigidbody2D mass to {rigidbody2D.mass}";
                    }
                    else if (propertyName.ToLower() == "drag")
                    {
                        rigidbody2D.drag = float.Parse(value);
                        return $"✅ Set Rigidbody2D drag to {rigidbody2D.drag}";
                    }
                    else if (propertyName.ToLower() == "angulardrag")
                    {
                        rigidbody2D.angularDrag = float.Parse(value);
                        return $"✅ Set Rigidbody2D angularDrag to {rigidbody2D.angularDrag}";
                    }
                    else if (propertyName.ToLower() == "gravityscale")
                    {
                        rigidbody2D.gravityScale = float.Parse(value);
                        return $"✅ Set Rigidbody2D gravityScale to {rigidbody2D.gravityScale}";
                    }
                    else if (propertyName.ToLower() == "iskinematic")
                    {
                        rigidbody2D.isKinematic = bool.Parse(value);
                        return $"✅ Set Rigidbody2D isKinematic to {rigidbody2D.isKinematic}";
                    }
                }
                else if (component is AudioSource audioSource)
                {
                    if (propertyName.ToLower() == "volume")
                    {
                        audioSource.volume = float.Parse(value);
                        return $"✅ Set AudioSource volume to {audioSource.volume}";
                    }
                    else if (propertyName.ToLower() == "pitch")
                    {
                        audioSource.pitch = float.Parse(value);
                        return $"✅ Set AudioSource pitch to {audioSource.pitch}";
                    }
                    else if (propertyName.ToLower() == "loop")
                    {
                        audioSource.loop = bool.Parse(value);
                        return $"✅ Set AudioSource loop to {audioSource.loop}";
                    }
                    else if (propertyName.ToLower() == "playonawake")
                    {
                        audioSource.playOnAwake = bool.Parse(value);
                        return $"✅ Set AudioSource playOnAwake to {audioSource.playOnAwake}";
                    }
                }
                else if (component is Collider collider)
                {
                    if (propertyName.ToLower() == "istrigger")
                    {
                        collider.isTrigger = bool.Parse(value);
                        return $"✅ Set Collider isTrigger to {collider.isTrigger}";
                    }
                }
                else if (component is CanvasGroup canvasGroup)
                {
                    if (propertyName.ToLower() == "alpha")
                    {
                        canvasGroup.alpha = float.Parse(value);
                        return $"✅ Set CanvasGroup alpha to {canvasGroup.alpha}";
                    }
                    else if (propertyName.ToLower() == "interactable")
                    {
                        canvasGroup.interactable = bool.Parse(value);
                        return $"✅ Set CanvasGroup interactable to {canvasGroup.interactable}";
                    }
                    else if (propertyName.ToLower() == "blocksraycasts")
                    {
                        canvasGroup.blocksRaycasts = bool.Parse(value);
                        return $"✅ Set CanvasGroup blocksRaycasts to {canvasGroup.blocksRaycasts}";
                    }
                }
                
                return $"❌ Property {propertyName} not supported for {componentType}";
            }
            catch (System.Exception e)
            {
                return $"❌ Error setting component property: {e.Message}";
            }
        }
        
        private static Color ParseColor(string value)
        {
            value = value.Trim('(', ')');
            var parts = value.Split(',');
            if (parts.Length == 3)
            {
                return new Color(
                    float.Parse(parts[0].Trim()),
                    float.Parse(parts[1].Trim()),
                    float.Parse(parts[2].Trim())
                );
            }
            else if (parts.Length == 4)
            {
                return new Color(
                    float.Parse(parts[0].Trim()),
                    float.Parse(parts[1].Trim()),
                    float.Parse(parts[2].Trim()),
                    float.Parse(parts[3].Trim())
                );
            }
            return Color.white;
        }
        
        private static TextAnchor ParseTextAnchor(string value)
        {
            switch (value.ToLower())
            {
                case "upperleft": return TextAnchor.UpperLeft;
                case "uppercenter": return TextAnchor.UpperCenter;
                case "upperright": return TextAnchor.UpperRight;
                case "middleleft": return TextAnchor.MiddleLeft;
                case "middlecenter": return TextAnchor.MiddleCenter;
                case "middleright": return TextAnchor.MiddleRight;
                case "lowerleft": return TextAnchor.LowerLeft;
                case "lowercenter": return TextAnchor.LowerCenter;
                case "lowerright": return TextAnchor.LowerRight;
                default: return TextAnchor.MiddleCenter;
            }
        }
        
        private static Vector3 ParseVector3(string value)
        {
            value = value.Trim('(', ')');
            var parts = value.Split(',');
            return new Vector3(
                float.Parse(parts[0].Trim()),
                float.Parse(parts[1].Trim()),
                float.Parse(parts[2].Trim())
            );
        }
        
        private static Vector2 ParseVector2(string value)
        {
            value = value.Trim('(', ')');
            var parts = value.Split(',');
            return new Vector2(
                float.Parse(parts[0].Trim()),
                float.Parse(parts[1].Trim())
            );
        }
    }
}