using UnityEngine;
using UnityEditor;
using System.IO;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using SysProcess = System.Diagnostics.Process;

namespace Kiro.Unity.MCP
{
    public static class UnityScreenshotCapture
    {
        private static string screenshotPath = "Temp/KiroScreenshots";
        
        // Windows API declarations for window capture
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        
        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        
        [DllImport("user32.dll")]
        private static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, int nFlags);
        
        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);
        
        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);
        
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
        
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        
        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);
        
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        
        // System metrics constants
        private const int SM_XVIRTUALSCREEN = 76;
        private const int SM_YVIRTUALSCREEN = 77;
        private const int SM_CXVIRTUALSCREEN = 78;
        private const int SM_CYVIRTUALSCREEN = 79;
        
        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        
        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);
        
        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);
        
        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);
        
        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);
        
        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDC(IntPtr hWnd);
        
        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        
        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, 
            IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);
        
        [DllImport("gdi32.dll")]
        private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, int uStartScan, int cScanLines, 
            byte[] lpvBits, ref BITMAPINFO lpbmi, int uUsage);
        
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFO
        {
            public BITMAPINFOHEADER bmiHeader;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
            public RGBQUAD[] bmiColors;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public int biSize;
            public int biWidth;
            public int biHeight;
            public short biPlanes;
            public short biBitCount;
            public int biCompression;
            public int biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public int biClrUsed;
            public int biClrImportant;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct RGBQUAD
        {
            public byte rgbBlue;
            public byte rgbGreen;
            public byte rgbRed;
            public byte rgbReserved;
        }
        
        [MenuItem("Kiro/Capture Scene View")]
        public static void CaptureSceneView()
        {
            CaptureSceneViewToFile();
        }
        
        [MenuItem("Kiro/Capture Game View")]
        public static void CaptureGameView()
        {
            CaptureGameViewToFile();
        }
        
        public static string CaptureSceneViewToFile()
        {
            try
            {
                // Ensure screenshot directory exists
                Directory.CreateDirectory(screenshotPath);
                
                // Get the scene view
                SceneView sceneView = SceneView.lastActiveSceneView;
                if (sceneView == null)
                {
                    UnityEngine.Debug.LogError("Kiro: No active Scene View found");
                    return null;
                }
                
                // Create filename with timestamp
                string filename = $"sceneview_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                string fullPath = Path.Combine(screenshotPath, filename);
                
                // Get scene view size for adaptive resolution
                var sceneViewRect = sceneView.position;
                int width = Mathf.Max(800, (int)sceneViewRect.width);
                int height = Mathf.Max(600, (int)sceneViewRect.height);
                
                // Capture the scene view
                var camera = sceneView.camera;
                var renderTexture = new RenderTexture(width, height, 24);
                var oldTarget = camera.targetTexture;
                
                camera.targetTexture = renderTexture;
                camera.Render();
                
                RenderTexture.active = renderTexture;
                var screenshot = new Texture2D(width, height, TextureFormat.RGB24, false);
                screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                screenshot.Apply();
                
                // Restore camera
                camera.targetTexture = oldTarget;
                RenderTexture.active = null;
                
                // Save to file
                byte[] data = screenshot.EncodeToPNG();
                File.WriteAllBytes(fullPath, data);
                
                // Cleanup
                UnityEngine.Object.DestroyImmediate(screenshot);
                renderTexture.Release();
                
                UnityEngine.Debug.Log($"Kiro: Scene view captured to {fullPath} ({width}x{height})");
                
                // Auto-cleanup old screenshots to save space
                CleanupOldScreenshots(5);
                
                return fullPath;
            }
            catch (Exception e)
            {
                Debug.LogError($"Kiro: Failed to capture scene view: {e.Message}");
                return null;
            }
        }
        
        public static string CaptureGameViewToFile()
        {
            try
            {
                // Ensure screenshot directory exists
                Directory.CreateDirectory(screenshotPath);
                
                // Create filename with timestamp
                string filename = $"gameview_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                string fullPath = Path.Combine(screenshotPath, filename);
                
                // Capture game view using ScreenCapture
                ScreenCapture.CaptureScreenshot(fullPath);
                
                Debug.Log($"Kiro: Game view captured to {fullPath}");
                
                // Auto-cleanup old screenshots to save space
                CleanupOldScreenshots(5);
                
                return fullPath;
            }
            catch (Exception e)
            {
                Debug.LogError($"Kiro: Failed to capture game view: {e.Message}");
                return null;
            }
        }
        
        public static string CaptureUnityEditorToFile()
        {
            try
            {
                // Ensure screenshot directory exists
                Directory.CreateDirectory(screenshotPath);
                
                // Create filename with timestamp
                string filename = $"unity_editor_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                string fullPath = Path.Combine(screenshotPath, filename);
                
                // Try to capture ALL Unity windows and combine them
                bool success = CaptureAllUnityWindows(fullPath);
                
                if (success)
                {
                    Debug.Log($"Kiro: All Unity windows captured to {fullPath}");
                    
                    // Auto-cleanup old screenshots to save space
                    CleanupOldScreenshots(5);
                    
                    return fullPath;
                }
                else
                {
                    Debug.LogWarning("Kiro: All Unity windows capture failed, trying single Unity window");
                    
                    // Fallback: try to capture single Unity window
                    success = CaptureUnityWindowWithWinAPI(fullPath);
                    
                    if (success)
                    {
                        Debug.Log($"Kiro: Unity Editor window captured to {fullPath}");
                    }
                    else
                    {
                        Debug.LogWarning("Kiro: Unity window capture failed, using Unity's screenshot");
                        // Final fallback: Unity's built-in screenshot (primary monitor only)
                        ScreenCapture.CaptureScreenshot(fullPath);
                        Debug.Log($"Kiro: Unity screenshot captured to {fullPath}");
                    }
                    
                    // Auto-cleanup old screenshots to save space
                    CleanupOldScreenshots(5);
                    
                    return fullPath;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Kiro: Failed to capture Unity Editor: {e.Message}");
                return null;
            }
        }
        
        private static bool CaptureUnityWindowWithWinAPI(string filePath)
        {
            try
            {
                // Find Unity Editor window
                IntPtr unityWindow = FindUnityEditorWindow();
                
                if (unityWindow == IntPtr.Zero)
                {
                    Debug.LogWarning("Kiro: Could not find Unity Editor window");
                    return false;
                }
                
                // Get window dimensions
                RECT windowRect;
                if (!GetWindowRect(unityWindow, out windowRect))
                {
                    Debug.LogWarning("Kiro: Could not get Unity window dimensions");
                    return false;
                }
                
                int width = windowRect.Right - windowRect.Left;
                int height = windowRect.Bottom - windowRect.Top;
                
                UnityEngine.Debug.Log($"Kiro: Found Unity window - {width}x{height}");
                
                // Capture the window
                IntPtr windowDC = GetWindowDC(unityWindow);
                IntPtr memoryDC = CreateCompatibleDC(windowDC);
                IntPtr bitmap = CreateCompatibleBitmap(windowDC, width, height);
                IntPtr oldBitmap = SelectObject(memoryDC, bitmap);
                
                // Copy window content to bitmap
                const int SRCCOPY = 0x00CC0020;
                bool success = BitBlt(memoryDC, 0, 0, width, height, windowDC, 0, 0, SRCCOPY);
                
                if (success)
                {
                    // Convert bitmap to Unity Texture2D and save
                    Texture2D texture = BitmapToTexture2D(bitmap, width, height, memoryDC);
                    if (texture != null)
                    {
                        byte[] pngData = texture.EncodeToPNG();
                        File.WriteAllBytes(filePath, pngData);
                        UnityEngine.Object.DestroyImmediate(texture);
                        
                        Debug.Log($"Kiro: Unity Editor window captured successfully - {width}x{height}");
                        success = true;
                    }
                    else
                    {
                        Debug.LogWarning("Kiro: Failed to convert bitmap to texture");
                        success = false;
                    }
                }
                else
                {
                    Debug.LogWarning("Kiro: BitBlt failed to capture window");
                }
                
                // Cleanup
                SelectObject(memoryDC, oldBitmap);
                DeleteObject(bitmap);
                DeleteDC(memoryDC);
                ReleaseDC(unityWindow, windowDC);
                
                return success;
            }
            catch (Exception e)
            {
                Debug.LogError($"Kiro: Windows API capture failed: {e.Message}");
                return false;
            }
        }
        
        private static IntPtr FindUnityEditorWindow()
        {
            try
            {
                // Get current Unity process
                SysProcess currentProcess = SysProcess.GetCurrentProcess();
                uint currentProcessId = (uint)currentProcess.Id;
                
                Debug.Log($"Kiro: Looking for Unity windows in process: {currentProcess.ProcessName} (PID: {currentProcessId})");
                
                // Find all windows belonging to Unity process
                IntPtr unityMainWindow = IntPtr.Zero;
                
                EnumWindows((hWnd, lParam) =>
                {
                    uint windowProcessId;
                    GetWindowThreadProcessId(hWnd, out windowProcessId);
                    
                    if (windowProcessId == currentProcessId)
                    {
                        // Get window title
                        int length = GetWindowTextLength(hWnd);
                        if (length > 0)
                        {
                            var sb = new System.Text.StringBuilder(length + 1);
                            GetWindowText(hWnd, sb, sb.Capacity);
                            string windowTitle = sb.ToString();
                            
                            Debug.Log($"Kiro: Found Unity window: '{windowTitle}'");
                            
                            // Look for main Unity Editor window (usually contains project name)
                            if (windowTitle.Contains("Unity") && !windowTitle.Contains("Game") && 
                                !windowTitle.Contains("Console") && !windowTitle.Contains("Inspector"))
                            {
                                // Get window size to find the largest window (likely main editor)
                                RECT rect;
                                if (GetWindowRect(hWnd, out rect))
                                {
                                    int width = rect.Right - rect.Left;
                                    int height = rect.Bottom - rect.Top;
                                    
                                    // Main Unity window is usually quite large
                                    if (width > 800 && height > 600)
                                    {
                                        Debug.Log($"Kiro: Selected Unity main window: '{windowTitle}' ({width}x{height})");
                                        unityMainWindow = hWnd;
                                        return false; // Stop enumeration
                                    }
                                }
                            }
                        }
                    }
                    return true; // Continue enumeration
                }, IntPtr.Zero);
                
                if (unityMainWindow != IntPtr.Zero)
                {
                    return unityMainWindow;
                }
                
                // Fallback: try the process main window handle
                IntPtr mainWindowHandle = currentProcess.MainWindowHandle;
                if (mainWindowHandle != IntPtr.Zero)
                {
                    Debug.Log("Kiro: Using process main window handle as fallback");
                    return mainWindowHandle;
                }
                
                Debug.LogWarning("Kiro: Could not find Unity Editor main window");
                return IntPtr.Zero;
            }
            catch (Exception e)
            {
                Debug.LogError($"Kiro: Error finding Unity window: {e.Message}");
                return IntPtr.Zero;
            }
        }
        
        private static Texture2D BitmapToTexture2D(IntPtr bitmap, int width, int height, IntPtr hdc)
        {
            try
            {
                // Prepare bitmap info
                BITMAPINFO bmi = new BITMAPINFO();
                bmi.bmiHeader.biSize = Marshal.SizeOf(typeof(BITMAPINFOHEADER));
                bmi.bmiHeader.biWidth = width;
                bmi.bmiHeader.biHeight = -height; // Negative for top-down bitmap
                bmi.bmiHeader.biPlanes = 1;
                bmi.bmiHeader.biBitCount = 24; // 24-bit RGB
                bmi.bmiHeader.biCompression = 0; // BI_RGB
                
                // Calculate image size
                int imageSize = width * height * 3; // 3 bytes per pixel (RGB)
                byte[] bitmapData = new byte[imageSize];
                
                // Get bitmap bits
                int result = GetDIBits(hdc, bitmap, 0, height, bitmapData, ref bmi, 0); // DIB_RGB_COLORS = 0
                
                if (result == 0)
                {
                    Debug.LogWarning("Kiro: GetDIBits failed");
                    return null;
                }
                
                // Create Unity texture
                Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                
                // Convert BGR to RGB and flip vertically
                Color[] pixels = new Color[width * height];
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int srcIndex = (y * width + x) * 3;
                        int dstIndex = ((height - 1 - y) * width + x); // Flip vertically
                        
                        if (srcIndex + 2 < bitmapData.Length)
                        {
                            // Convert BGR to RGB
                            float b = bitmapData[srcIndex] / 255f;
                            float g = bitmapData[srcIndex + 1] / 255f;
                            float r = bitmapData[srcIndex + 2] / 255f;
                            
                            pixels[dstIndex] = new Color(r, g, b, 1f);
                        }
                    }
                }
                
                texture.SetPixels(pixels);
                texture.Apply();
                
                return texture;
            }
            catch (Exception e)
            {
                Debug.LogError($"Kiro: Error converting bitmap to texture: {e.Message}");
                return null;
            }
        }
        
        private static bool CaptureMultiMonitorDesktop(string filePath)
        {
            try
            {
                // Get virtual screen dimensions (all monitors combined)
                int x = GetSystemMetrics(SM_XVIRTUALSCREEN);
                int y = GetSystemMetrics(SM_YVIRTUALSCREEN);
                int width = GetSystemMetrics(SM_CXVIRTUALSCREEN);
                int height = GetSystemMetrics(SM_CYVIRTUALSCREEN);
                
                Debug.Log($"Kiro: Virtual screen dimensions: {x},{y} {width}x{height}");
                
                // Get desktop DC
                IntPtr desktopDC = GetWindowDC(IntPtr.Zero);
                IntPtr memoryDC = CreateCompatibleDC(desktopDC);
                IntPtr bitmap = CreateCompatibleBitmap(desktopDC, width, height);
                IntPtr oldBitmap = SelectObject(memoryDC, bitmap);
                
                // Copy entire virtual screen to bitmap
                const int SRCCOPY = 0x00CC0020;
                bool success = BitBlt(memoryDC, 0, 0, width, height, desktopDC, x, y, SRCCOPY);
                
                if (success)
                {
                    // Convert bitmap to Unity Texture2D and save
                    Texture2D texture = BitmapToTexture2D(bitmap, width, height, memoryDC);
                    if (texture != null)
                    {
                        byte[] pngData = texture.EncodeToPNG();
                        File.WriteAllBytes(filePath, pngData);
                        UnityEngine.Object.DestroyImmediate(texture);
                        
                        Debug.Log($"Kiro: Multi-monitor desktop captured successfully - {width}x{height}");
                        success = true;
                    }
                    else
                    {
                        Debug.LogWarning("Kiro: Failed to convert desktop bitmap to texture");
                        success = false;
                    }
                }
                else
                {
                    Debug.LogWarning("Kiro: BitBlt failed to capture desktop");
                }
                
                // Cleanup
                SelectObject(memoryDC, oldBitmap);
                DeleteObject(bitmap);
                DeleteDC(memoryDC);
                ReleaseDC(IntPtr.Zero, desktopDC);
                
                return success;
            }
            catch (Exception e)
            {
                Debug.LogError($"Kiro: Multi-monitor desktop capture failed: {e.Message}");
                return false;
            }
        }
        
        private static bool CaptureAllUnityWindows(string filePath)
        {
            try
            {
                // Get current Unity process
                SysProcess currentProcess = SysProcess.GetCurrentProcess();
                uint currentProcessId = (uint)currentProcess.Id;
                
                Debug.Log($"Kiro: Finding ALL Unity windows in process: {currentProcess.ProcessName} (PID: {currentProcessId})");
                
                // Find all Unity windows
                var unityWindows = new System.Collections.Generic.List<UnityWindowInfo>();
                
                EnumWindows((hWnd, lParam) =>
                {
                    uint windowProcessId;
                    GetWindowThreadProcessId(hWnd, out windowProcessId);
                    
                    if (windowProcessId == currentProcessId)
                    {
                        // Get window rect first
                        RECT rect;
                        if (GetWindowRect(hWnd, out rect))
                        {
                            int width = rect.Right - rect.Left;
                            int height = rect.Bottom - rect.Top;
                            
                            // Only check windows that are reasonably sized (could be Unity panels)
                            if (width > 200 && height > 150)
                            {
                                // Get window title
                                string windowTitle = "";
                                int length = GetWindowTextLength(hWnd);
                                if (length > 0)
                                {
                                    var sb = new System.Text.StringBuilder(length + 1);
                                    GetWindowText(hWnd, sb, sb.Capacity);
                                    windowTitle = sb.ToString();
                                }
                                
                                // Since this window belongs to Unity process, capture it
                                // (This includes main editor, undocked panels, game view, etc.)
                                unityWindows.Add(new UnityWindowInfo
                                {
                                    Handle = hWnd,
                                    Title = string.IsNullOrEmpty(windowTitle) ? "Unity Window" : windowTitle,
                                    X = rect.Left,
                                    Y = rect.Top,
                                    Width = width,
                                    Height = height
                                });
                                
                                Debug.Log($"Kiro: Found Unity window: '{windowTitle}' at ({rect.Left},{rect.Top}) {width}x{height}");
                            }
                        }
                    }
                    return true; // Continue enumeration
                }, IntPtr.Zero);
                
                if (unityWindows.Count == 0)
                {
                    Debug.LogWarning("Kiro: No Unity windows found");
                    return false;
                }
                
                Debug.Log($"Kiro: Found {unityWindows.Count} Unity windows to capture");
                
                // Calculate combined bounds
                int minX = unityWindows.Min(w => w.X);
                int minY = unityWindows.Min(w => w.Y);
                int maxX = unityWindows.Max(w => w.X + w.Width);
                int maxY = unityWindows.Max(w => w.Y + w.Height);
                
                int combinedWidth = maxX - minX;
                int combinedHeight = maxY - minY;
                
                Debug.Log($"Kiro: Combined Unity workspace: ({minX},{minY}) {combinedWidth}x{combinedHeight}");
                
                // Create combined texture
                Texture2D combinedTexture = new Texture2D(combinedWidth, combinedHeight, TextureFormat.RGB24, false);
                
                // Fill with background color
                Color[] backgroundPixels = new Color[combinedWidth * combinedHeight];
                for (int i = 0; i < backgroundPixels.Length; i++)
                {
                    backgroundPixels[i] = new Color(0.2f, 0.2f, 0.2f, 1f); // Dark gray background
                }
                combinedTexture.SetPixels(backgroundPixels);
                
                // Capture each Unity window and composite them
                foreach (var window in unityWindows)
                {
                    Texture2D windowTexture = CaptureWindowToTexture(window.Handle, window.Width, window.Height);
                    if (windowTexture != null)
                    {
                        // Calculate position in combined texture
                        int offsetX = window.X - minX;
                        int offsetY = (maxY - window.Y - window.Height); // Flip Y coordinate
                        
                        // Copy window pixels to combined texture
                        Color[] windowPixels = windowTexture.GetPixels();
                        combinedTexture.SetPixels(offsetX, offsetY, window.Width, window.Height, windowPixels);
                        
                        UnityEngine.Object.DestroyImmediate(windowTexture);
                    }
                }
                
                combinedTexture.Apply();
                
                // Save combined texture
                byte[] pngData = combinedTexture.EncodeToPNG();
                File.WriteAllBytes(filePath, pngData);
                UnityEngine.Object.DestroyImmediate(combinedTexture);
                
                Debug.Log($"Kiro: All Unity windows captured and combined successfully");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Kiro: Failed to capture all Unity windows: {e.Message}");
                return false;
            }
        }
        
        private static Texture2D CaptureWindowToTexture(IntPtr windowHandle, int width, int height)
        {
            try
            {
                IntPtr windowDC = GetWindowDC(windowHandle);
                IntPtr memoryDC = CreateCompatibleDC(windowDC);
                IntPtr bitmap = CreateCompatibleBitmap(windowDC, width, height);
                IntPtr oldBitmap = SelectObject(memoryDC, bitmap);
                
                // Copy window content to bitmap
                const int SRCCOPY = 0x00CC0020;
                bool success = BitBlt(memoryDC, 0, 0, width, height, windowDC, 0, 0, SRCCOPY);
                
                Texture2D texture = null;
                if (success)
                {
                    texture = BitmapToTexture2D(bitmap, width, height, memoryDC);
                }
                
                // Cleanup
                SelectObject(memoryDC, oldBitmap);
                DeleteObject(bitmap);
                DeleteDC(memoryDC);
                ReleaseDC(windowHandle, windowDC);
                
                return texture;
            }
            catch (Exception e)
            {
                Debug.LogError($"Kiro: Failed to capture window to texture: {e.Message}");
                return null;
            }
        }
        
        private struct UnityWindowInfo
        {
            public IntPtr Handle;
            public string Title;
            public int X, Y, Width, Height;
        }
        
        public static string CaptureCameraView(Camera camera, int width = 0, int height = 0)
        {
            try
            {
                if (camera == null)
                {
                    Debug.LogError("Kiro: Camera is null");
                    return null;
                }
                
                // Ensure screenshot directory exists
                Directory.CreateDirectory(screenshotPath);
                
                // Create filename with timestamp and camera name
                string filename = $"camera_{camera.name.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                string fullPath = Path.Combine(screenshotPath, filename);
                
                // Use custom resolution if provided, otherwise try to get Game View resolution
                if (width <= 0 || height <= 0)
                {
                    // Try to get Game View resolution using reflection
                    var gameViewSize = GetGameViewSize();
                    if (gameViewSize.x > 0 && gameViewSize.y > 0)
                    {
                        width = (int)gameViewSize.x;
                        height = (int)gameViewSize.y;
                    }
                    else
                    {
                        // Fallback to high resolution default
                        width = 1920;
                        height = 1080;
                    }
                }
                
                // Clamp to reasonable bounds (support ultra-wide and high-res displays)
                width = Mathf.Clamp(width, 800, 8192);  // Support up to 8K+ ultra-wide
                height = Mathf.Clamp(height, 600, 4320); // Support up to 4K height
                
                // Create render texture
                var renderTexture = new RenderTexture(width, height, 24);
                var oldTarget = camera.targetTexture;
                
                camera.targetTexture = renderTexture;
                camera.Render();
                
                RenderTexture.active = renderTexture;
                var screenshot = new Texture2D(width, height, TextureFormat.RGB24, false);
                screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                screenshot.Apply();
                
                // Restore camera
                camera.targetTexture = oldTarget;
                RenderTexture.active = null;
                
                // Save to file
                byte[] data = screenshot.EncodeToPNG();
                File.WriteAllBytes(fullPath, data);
                
                // Cleanup
                UnityEngine.Object.DestroyImmediate(screenshot);
                renderTexture.Release();
                
                Debug.Log($"Kiro: Camera view captured to {fullPath} ({width}x{height})");
                
                // Auto-cleanup old screenshots to save space
                CleanupOldScreenshots(5);
                
                return fullPath;
            }
            catch (Exception e)
            {
                Debug.LogError($"Kiro: Failed to capture camera view: {e.Message}");
                return null;
            }
        }
        
        private static Vector2 GetGameViewSize()
        {
            try
            {
                // Use reflection to get Game View size
                var gameViewType = System.Type.GetType("UnityEditor.GameView,UnityEditor");
                if (gameViewType != null)
                {
                    var gameView = EditorWindow.GetWindow(gameViewType, false, null, false);
                    if (gameView != null)
                    {
                        var position = gameView.position;
                        return new Vector2(position.width, position.height);
                    }
                }
                
                // Alternative method: try to get from PlayerSettings
                // This gets the resolution from the current build target
                return new Vector2(PlayerSettings.defaultScreenWidth, PlayerSettings.defaultScreenHeight);
            }
            catch (Exception e)
            {
                Debug.Log($"Kiro: Could not get Game View size: {e.Message}");
                return Vector2.zero;
            }
        }
        
        public static void CleanupOldScreenshots(int keepCount = 5)
        {
            try
            {
                if (!Directory.Exists(screenshotPath))
                    return;
                
                var files = Directory.GetFiles(screenshotPath, "*.png")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTime)
                    .ToArray();
                
                if (files.Length <= keepCount)
                    return;
                
                // Delete old files, keep only the most recent ones
                for (int i = keepCount; i < files.Length; i++)
                {
                    files[i].Delete();
                    Debug.Log($"Kiro: Deleted old screenshot: {files[i].Name}");
                }
                
                Debug.Log($"Kiro: Cleaned up {files.Length - keepCount} old screenshots, kept {keepCount} most recent");
            }
            catch (Exception e)
            {
                Debug.LogError($"Kiro: Failed to cleanup screenshots: {e.Message}");
            }
        }
        
        public static string GetLatestScreenshot()
        {
            try
            {
                if (!Directory.Exists(screenshotPath))
                    return null;
                
                var files = Directory.GetFiles(screenshotPath, "*.png");
                if (files.Length == 0)
                    return null;
                
                // Get most recent file
                string latestFile = files[0];
                DateTime latestTime = File.GetCreationTime(latestFile);
                
                foreach (string file in files)
                {
                    DateTime fileTime = File.GetCreationTime(file);
                    if (fileTime > latestTime)
                    {
                        latestTime = fileTime;
                        latestFile = file;
                    }
                }
                
                return latestFile;
            }
            catch (Exception e)
            {
                Debug.LogError($"Kiro: Failed to get latest screenshot: {e.Message}");
                return null;
            }
        }
    }
}