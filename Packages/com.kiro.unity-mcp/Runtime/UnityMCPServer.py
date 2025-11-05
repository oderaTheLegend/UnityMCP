#!/usr/bin/env python3
"""
Unity MCP Server - Simple Unity project integration for Kiro AI
"""

import asyncio
import json
import os
import sys
import re
import subprocess
from pathlib import Path
from typing import Any, Dict, List, Optional
from datetime import datetime

# MCP imports
from mcp.server import Server, NotificationOptions
from mcp.server.models import InitializationOptions
from mcp.server.stdio import stdio_server
from mcp.types import (
    Resource,
    Tool,
    TextContent,
)

# Initialize the MCP server
server = Server("unity-mcp")

class UnityProjectInspector:
    def __init__(self, project_root: str):
        self.project_root = Path(project_root)
        self.assets_path = self.project_root / "Assets"
        self.project_settings_path = self.project_root / "ProjectSettings"
        self.packages_path = self.project_root / "Packages"
        self.library_path = self.project_root / "Library"
    
    def find_unity_project_root(self, start_path: str) -> Optional[Path]:
        """Find Unity project root by looking for ProjectSettings folder"""
        current = Path(start_path).resolve()
        while current.parent != current:
            if (current / "ProjectSettings").exists() and (current / "Assets").exists():
                return current
            current = current.parent
        return None
    
    def get_project_info(self) -> Dict[str, Any]:
        """Get basic Unity project information"""
        info = {
            "project_root": str(self.project_root),
            "unity_version": "Unknown",
            "project_name": self.project_root.name
        }
        
        # Try to get Unity version from ProjectVersion.txt
        version_file = self.project_settings_path / "ProjectVersion.txt"
        if version_file.exists():
            try:
                content = version_file.read_text()
                for line in content.split('\n'):
                    if line.startswith('m_EditorVersion:'):
                        info["unity_version"] = line.split(':', 1)[1].strip()
                        break
            except Exception:
                pass
                
        return info
    
    def list_scenes(self) -> List[Dict[str, Any]]:
        """List all scenes in the project"""
        scenes = []
        
        for scene_file in self.assets_path.rglob("*.unity"):
            scenes.append({
                "name": scene_file.stem,
                "path": str(scene_file.relative_to(self.project_root)),
                "size": scene_file.stat().st_size if scene_file.exists() else 0
            })
        
        return scenes
    
    def list_scripts(self) -> List[Dict[str, Any]]:
        """List all C# scripts in the project"""
        scripts = []
        
        for script_file in self.assets_path.rglob("*.cs"):
            scripts.append({
                "name": script_file.stem,
                "path": str(script_file.relative_to(self.project_root)),
                "size": script_file.stat().st_size,
                "folder": str(script_file.parent.relative_to(self.assets_path))
            })
        
        return scripts
    
    def parse_scene_file(self, scene_path: Path) -> Dict[str, Any]:
        """Parse Unity scene file to extract GameObjects and components"""
        try:
            content = scene_path.read_text(encoding='utf-8')
            
            # Unity scene files are YAML-based but with custom format
            # We'll parse them manually to extract GameObject information
            scene_data = {
                "name": scene_path.stem,
                "path": str(scene_path.relative_to(self.project_root)),
                "gameObjects": [],
                "components": [],
                "error": None
            }
            
            # Split content into documents (Unity uses --- separators)
            documents = content.split('--- !u!')
            
            gameobjects = []
            transforms = []
            components = []
            
            # Debug: add document count to scene data
            scene_data["debug_document_count"] = len(documents)
            
            for doc in documents:
                if not doc.strip():
                    continue
                    
                lines = doc.strip().split('\n')
                if not lines:
                    continue
                    
                first_line = lines[0]
                
                # Parse GameObject entries (look for type 1 which is GameObject)
                if first_line.startswith('1 &') and 'GameObject:' in doc:
                    go_data = self._parse_gameobject_document(doc)
                    if go_data:
                        gameobjects.append(go_data)
                
                # Parse Transform components (look for type 4 which is Transform)
                elif first_line.startswith('4 &') and 'Transform:' in doc:
                    transform_data = self._parse_transform_document(doc)
                    if transform_data:
                        transforms.append(transform_data)
                
                # Parse other components (Camera is type 20, AudioListener is type 81, etc.)
                elif (first_line.startswith('20 &') or first_line.startswith('81 &') or first_line.startswith('114 &')) and any(comp_name in doc for comp_name in ['Camera:', 'AudioListener:', 'MonoBehaviour:']):
                    comp_data = self._parse_component_document(doc, first_line)
                    if comp_data:
                        components.append(comp_data)
            
            # Link GameObjects with their transforms and components
            scene_data["gameObjects"] = self._link_scene_objects(gameobjects, transforms, components)
            scene_data["summary"] = {
                "total_gameobjects": len(gameobjects),
                "total_components": len(components),
                "total_transforms": len(transforms)
            }
            
            return scene_data
            
        except Exception as e:
            return {
                "name": scene_path.stem,
                "path": str(scene_path.relative_to(self.project_root)),
                "error": f"Failed to parse scene: {str(e)}",
                "gameObjects": [],
                "components": []
            }
    
    def _parse_gameobject_document(self, doc: str) -> Optional[Dict[str, Any]]:
        """Parse a GameObject document from Unity scene"""
        try:
            lines = doc.strip().split('\n')
            go_data = {"type": "GameObject", "fileID": None, "name": "Unknown", "active": True, "components": []}
            
            for line in lines:
                line = line.strip()
                if line.startswith('&'):
                    # Extract fileID
                    go_data["fileID"] = line.split('&')[1].split()[0] if '&' in line else None
                elif 'm_Name:' in line:
                    go_data["name"] = line.split('m_Name:')[1].strip()
                elif 'm_IsActive:' in line:
                    go_data["active"] = '1' in line
                elif 'm_Component:' in line:
                    # Components will be linked later
                    pass
            
            return go_data if go_data["fileID"] else None
            
        except Exception:
            return None
    
    def _parse_transform_document(self, doc: str) -> Optional[Dict[str, Any]]:
        """Parse a Transform component document"""
        try:
            lines = doc.strip().split('\n')
            transform_data = {
                "type": "Transform", 
                "fileID": None, 
                "gameObject": None,
                "position": {"x": 0, "y": 0, "z": 0},
                "rotation": {"x": 0, "y": 0, "z": 0, "w": 1},
                "scale": {"x": 1, "y": 1, "z": 1},
                "parent": None,
                "children": []
            }
            
            for line in lines:
                line = line.strip()
                if line.startswith('&'):
                    transform_data["fileID"] = line.split('&')[1].split()[0] if '&' in line else None
                elif 'm_GameObject:' in line and 'fileID:' in line:
                    transform_data["gameObject"] = line.split('fileID:')[1].strip().split('}')[0]
                elif 'm_LocalPosition:' in line:
                    # Position parsing will be in next few lines
                    pass
                elif line.startswith('x:') and 'position' not in str(transform_data.get('_parsing', '')):
                    transform_data["position"]["x"] = float(line.split(':')[1].strip()) if ':' in line else 0
                elif line.startswith('y:') and 'position' not in str(transform_data.get('_parsing', '')):
                    transform_data["position"]["y"] = float(line.split(':')[1].strip()) if ':' in line else 0
                elif line.startswith('z:') and 'position' not in str(transform_data.get('_parsing', '')):
                    transform_data["position"]["z"] = float(line.split(':')[1].strip()) if ':' in line else 0
            
            return transform_data if transform_data["fileID"] else None
            
        except Exception:
            return None
    
    def _parse_component_document(self, doc: str, first_line: str) -> Optional[Dict[str, Any]]:
        """Parse a component document"""
        try:
            lines = doc.strip().split('\n')
            
            # Determine component type from first line
            comp_type = "Unknown"
            if 'MonoBehaviour' in first_line:
                comp_type = "MonoBehaviour"
            elif 'MeshRenderer' in first_line:
                comp_type = "MeshRenderer"
            elif 'Camera' in first_line:
                comp_type = "Camera"
            elif 'Light' in first_line:
                comp_type = "Light"
            
            comp_data = {
                "type": comp_type,
                "fileID": None,
                "gameObject": None,
                "enabled": True,
                "properties": {}
            }
            
            for line in lines:
                line = line.strip()
                if line.startswith('&'):
                    comp_data["fileID"] = line.split('&')[1].split()[0] if '&' in line else None
                elif 'm_GameObject:' in line and 'fileID:' in line:
                    comp_data["gameObject"] = line.split('fileID:')[1].strip().split('}')[0]
                elif 'm_Enabled:' in line:
                    comp_data["enabled"] = '1' in line
                elif 'm_Script:' in line and comp_type == "MonoBehaviour":
                    # Try to extract script name
                    if 'guid:' in line:
                        comp_data["properties"]["script_guid"] = line.split('guid:')[1].strip().split(',')[0]
            
            return comp_data if comp_data["fileID"] else None
            
        except Exception:
            return None
    
    def _link_scene_objects(self, gameobjects: List[Dict], transforms: List[Dict], components: List[Dict]) -> List[Dict[str, Any]]:
        """Link GameObjects with their transforms and components"""
        # Create lookup dictionaries
        transform_by_go = {t["gameObject"]: t for t in transforms if t.get("gameObject")}
        components_by_go = {}
        
        for comp in components:
            go_id = comp.get("gameObject")
            if go_id:
                if go_id not in components_by_go:
                    components_by_go[go_id] = []
                components_by_go[go_id].append(comp)
        
        # Link everything together
        linked_objects = []
        for go in gameobjects:
            go_id = go["fileID"]
            
            # Add transform
            if go_id in transform_by_go:
                go["transform"] = transform_by_go[go_id]
            
            # Add components
            if go_id in components_by_go:
                go["components"] = components_by_go[go_id]
            else:
                go["components"] = []
            
            linked_objects.append(go)
        
        return linked_objects
    
    def get_scene_hierarchy(self, scene_path: str) -> Dict[str, Any]:
        """Get detailed scene hierarchy and GameObject information"""
        full_path = self.project_root / scene_path
        
        if not full_path.exists():
            return {"error": f"Scene not found: {scene_path}"}
        
        return self.parse_scene_file(full_path)
    
    def list_prefabs(self) -> List[Dict[str, Any]]:
        """List all prefabs in the project"""
        prefabs = []
        
        for prefab_file in self.assets_path.rglob("*.prefab"):
            prefabs.append({
                "name": prefab_file.stem,
                "path": str(prefab_file.relative_to(self.project_root)),
                "size": prefab_file.stat().st_size,
                "folder": str(prefab_file.parent.relative_to(self.assets_path))
            })
        
        return prefabs
    
    def list_materials(self) -> List[Dict[str, Any]]:
        """List all materials in the project"""
        materials = []
        
        for mat_file in self.assets_path.rglob("*.mat"):
            materials.append({
                "name": mat_file.stem,
                "path": str(mat_file.relative_to(self.project_root)),
                "size": mat_file.stat().st_size,
                "folder": str(mat_file.parent.relative_to(self.assets_path))
            })
        
        return materials
    
    def get_project_structure(self) -> Dict[str, Any]:
        """Get complete project structure overview"""
        structure = {
            "project_info": self.get_project_info(),
            "assets": {
                "scripts": len(list(self.assets_path.rglob("*.cs"))),
                "scenes": len(list(self.assets_path.rglob("*.unity"))),
                "prefabs": len(list(self.assets_path.rglob("*.prefab"))),
                "materials": len(list(self.assets_path.rglob("*.mat"))),
                "textures": len(list(self.assets_path.rglob("*.png"))) + len(list(self.assets_path.rglob("*.jpg"))),
                "audio": len(list(self.assets_path.rglob("*.wav"))) + len(list(self.assets_path.rglob("*.mp3"))),
            },
            "folders": []
        }
        
        # Get folder structure
        for item in self.assets_path.iterdir():
            if item.is_dir() and not item.name.startswith('.'):
                folder_info = {
                    "name": item.name,
                    "path": str(item.relative_to(self.project_root)),
                    "files": len([f for f in item.rglob("*") if f.is_file()]),
                    "subfolders": len([f for f in item.rglob("*") if f.is_dir()])
                }
                structure["folders"].append(folder_info)
        
        return structure

# Global inspector instance
inspector: Optional[UnityProjectInspector] = None

def initialize_inspector():
    """Initialize the Unity project inspector"""
    global inspector
    
    # Try to find Unity project root from current working directory
    cwd = os.getcwd()
    project_root = None
    
    # Check if we're already in a Unity project
    if (Path(cwd) / "ProjectSettings").exists() and (Path(cwd) / "Assets").exists():
        project_root = cwd
    else:
        # Try to find Unity project in parent directories
        temp_inspector = UnityProjectInspector(cwd)
        found_root = temp_inspector.find_unity_project_root(cwd)
        if found_root:
            project_root = str(found_root)
    
    if project_root:
        inspector = UnityProjectInspector(project_root)
        return True
    
    return False

@server.list_resources()
async def handle_list_resources() -> List[Resource]:
    """List available Unity project resources"""
    if not inspector:
        return []
    
    return [
        Resource(
            uri="unity://project-info",
            name="Unity Project Information",
            description="Basic Unity project information",
            mimeType="application/json",
        ),
        Resource(
            uri="unity://project-structure",
            name="Unity Project Structure",
            description="Complete project structure overview",
            mimeType="application/json",
        ),
        Resource(
            uri="unity://scripts",
            name="Unity Scripts",
            description="List of C# scripts in the project",
            mimeType="application/json",
        ),
        Resource(
            uri="unity://scenes",
            name="Unity Scenes", 
            description="List of scenes in the project",
            mimeType="application/json",
        ),
        Resource(
            uri="unity://prefabs",
            name="Unity Prefabs",
            description="List of prefabs in the project",
            mimeType="application/json",
        ),
        Resource(
            uri="unity://materials",
            name="Unity Materials",
            description="List of materials in the project",
            mimeType="application/json",
        ),
    ]

@server.read_resource()
async def handle_read_resource(uri: str) -> str:
    """Read Unity project resource content"""
    if not inspector:
        return json.dumps({"error": "Unity project not found"})
    
    try:
        if uri == "unity://project-info":
            return json.dumps(inspector.get_project_info(), indent=2)
        elif uri == "unity://project-structure":
            return json.dumps(inspector.get_project_structure(), indent=2)
        elif uri == "unity://scripts":
            return json.dumps(inspector.list_scripts(), indent=2)
        elif uri == "unity://scenes":
            return json.dumps(inspector.list_scenes(), indent=2)
        elif uri == "unity://prefabs":
            return json.dumps(inspector.list_prefabs(), indent=2)
        elif uri == "unity://materials":
            return json.dumps(inspector.list_materials(), indent=2)
        else:
            return json.dumps({"error": f"Unknown resource: {uri}"})
    except Exception as e:
        return json.dumps({"error": f"Failed to read resource {uri}: {str(e)}"})

@server.list_tools()
async def handle_list_tools() -> List[Tool]:
    """List available Unity tools"""
    return [
        Tool(
            name="unity_read_script",
            description="Read the content of a Unity C# script",
            inputSchema={
                "type": "object",
                "properties": {
                    "script_path": {
                        "type": "string",
                        "description": "Path to the script file (relative to project root)",
                    },
                },
                "required": ["script_path"],
            },
        ),
        Tool(
            name="unity_create_script",
            description="Create a new C# script",
            inputSchema={
                "type": "object",
                "properties": {
                    "script_name": {
                        "type": "string",
                        "description": "Name of the new script (without .cs extension)",
                    },
                    "folder_path": {
                        "type": "string",
                        "description": "Folder path relative to Assets (default: Scripts)",
                    },
                },
                "required": ["script_name"],
            },
        ),
        Tool(
            name="unity_inspect_scene",
            description="Inspect a Unity scene to see GameObjects, components, and hierarchy",
            inputSchema={
                "type": "object",
                "properties": {
                    "scene_path": {
                        "type": "string",
                        "description": "Path to the scene file (relative to project root, e.g., 'Assets/Scenes/SampleScene.unity')",
                    },
                },
                "required": ["scene_path"],
            },
        ),
        Tool(
            name="unity_list_gameobjects",
            description="List all GameObjects in a specific scene with their components",
            inputSchema={
                "type": "object",
                "properties": {
                    "scene_path": {
                        "type": "string",
                        "description": "Path to the scene file (relative to project root)",
                    },
                },
                "required": ["scene_path"],
            },
        ),
        Tool(
            name="unity_search_assets",
            description="Search for assets by name or type in the project",
            inputSchema={
                "type": "object",
                "properties": {
                    "search_term": {
                        "type": "string",
                        "description": "Term to search for in asset names",
                    },
                    "asset_type": {
                        "type": "string",
                        "description": "Asset type to filter by (script, scene, prefab, material, texture, audio)",
                    },
                },
                "required": ["search_term"],
            },
        ),
        Tool(
            name="unity_get_project_overview",
            description="Get a comprehensive overview of the Unity project structure and contents",
            inputSchema={
                "type": "object",
                "properties": {},
            },
        ),
        Tool(
            name="unity_capture_screenshot",
            description="Capture a screenshot of the Unity Scene View, Game View, or entire Unity Editor",
            inputSchema={
                "type": "object",
                "properties": {
                    "view_type": {
                        "type": "string",
                        "description": "Type of view to capture: 'scene', 'game', or 'editor'",
                        "enum": ["scene", "game", "editor"]
                    },
                    "delay_seconds": {
                        "type": "number",
                        "description": "Optional delay in seconds before capture (useful for arranging windows)",
                    },
                },
                "required": ["view_type"],
            },
        ),
        Tool(
            name="unity_capture_camera_view",
            description="Capture a screenshot from a specific camera's perspective",
            inputSchema={
                "type": "object",
                "properties": {
                    "camera_name": {
                        "type": "string",
                        "description": "Name of the camera to capture from (e.g., 'Main Camera')",
                    },
                    "width": {
                        "type": "number",
                        "description": "Optional width for the screenshot (defaults to Game View resolution)",
                    },
                    "height": {
                        "type": "number",
                        "description": "Optional height for the screenshot (defaults to Game View resolution)",
                    },
                },
                "required": ["camera_name"],
            },
        ),
        Tool(
            name="unity_get_scene_info",
            description="Get detailed information about the current scene including GameObjects, cameras, and lighting",
            inputSchema={
                "type": "object",
                "properties": {},
            },
        ),
        Tool(
            name="unity_get_scene_hierarchy",
            description="Get the exact hierarchy of GameObjects in the current scene with names and structure",
            inputSchema={
                "type": "object",
                "properties": {},
            },
        ),
        Tool(
            name="unity_create_gameobject",
            description="Create a new GameObject with optional parent",
            inputSchema={
                "type": "object",
                "properties": {
                    "name": {
                        "type": "string",
                        "description": "Name of the new GameObject",
                    },
                    "parent": {
                        "type": "string", 
                        "description": "Optional parent GameObject name",
                    },
                },
                "required": ["name"],
            },
        ),
        Tool(
            name="unity_delete_gameobject",
            description="Delete a GameObject by name",
            inputSchema={
                "type": "object",
                "properties": {
                    "name": {
                        "type": "string",
                        "description": "Name of the GameObject to delete",
                    },
                },
                "required": ["name"],
            },
        ),
        Tool(
            name="unity_set_property",
            description="Set GameObject properties (position, rotation, scale, active, name, layer, tag)",
            inputSchema={
                "type": "object",
                "properties": {
                    "object_name": {
                        "type": "string",
                        "description": "Name of the GameObject",
                    },
                    "property": {
                        "type": "string",
                        "description": "Property to set: position, rotation, scale, active, name, layer, tag",
                    },
                    "value": {
                        "type": "string",
                        "description": "New value (e.g., '0,0,0' for vectors, 'true' for boolean, 'UI' for layer, 'Player' for tag)",
                    },
                },
                "required": ["object_name", "property", "value"],
            },
        ),
        Tool(
            name="unity_add_component",
            description="Add a component to a GameObject. Supports UI (Canvas, Image, Button, Text, Slider, etc.), Physics (Rigidbody, Collider, etc.), Rendering (Camera, Light, SpriteRenderer, etc.), Audio (AudioSource, AudioListener), and more.",
            inputSchema={
                "type": "object",
                "properties": {
                    "object_name": {
                        "type": "string",
                        "description": "Name of the GameObject",
                    },
                    "component_type": {
                        "type": "string",
                        "description": "Type of component to add. Examples: Canvas, RectTransform, Image, RawImage, Text, Button, Toggle, Slider, Dropdown, InputField, ScrollRect, Mask, CanvasScaler, GraphicRaycaster, CanvasGroup, LayoutElement, HorizontalLayoutGroup, VerticalLayoutGroup, GridLayoutGroup, Camera, Light, MeshRenderer, SpriteRenderer, ParticleSystem, Rigidbody, Rigidbody2D, BoxCollider, SphereCollider, BoxCollider2D, CircleCollider2D, AudioSource, AudioListener, Animator, Animation",
                    },
                },
                "required": ["object_name", "component_type"],
            },
        ),
        Tool(
            name="unity_set_component_property",
            description="Set component-specific properties for any Unity component. Supports Canvas (rendermode), RectTransform (anchormin, anchormax, sizedelta, anchoredposition), CanvasScaler (uiscalemode, referenceresolution, screenmatchmode, matchwidthorheight), Image (color, raycasttarget), Text (text, fontsize, color, alignment), Camera (fieldofview/fov, orthographic, orthographicsize, depth, backgroundcolor), Light (color, intensity, range), SpriteRenderer (color, flipx, flipy, sortingorder), Rigidbody (mass, drag, angulardrag, usegravity, iskinematic), Rigidbody2D (mass, drag, angulardrag, gravityscale, iskinematic), AudioSource (volume, pitch, loop, playonawake), Collider (istrigger), CanvasGroup (alpha, interactable, blocksraycasts)",
            inputSchema={
                "type": "object",
                "properties": {
                    "object_name": {
                        "type": "string",
                        "description": "Name of the GameObject",
                    },
                    "component_type": {
                        "type": "string",
                        "description": "Type of component (Canvas, RectTransform, CanvasScaler, Image, Text, Camera, Light, SpriteRenderer, Rigidbody, Rigidbody2D, AudioSource, Collider, CanvasGroup, etc.)",
                    },
                    "property": {
                        "type": "string",
                        "description": "Property name to set (depends on component type - see description for full list)",
                    },
                    "value": {
                        "type": "string",
                        "description": "Property value. Format depends on property type: vectors as 'x,y' or 'x,y,z', colors as 'r,g,b' or 'r,g,b,a' (0-1 range), booleans as 'true'/'false', numbers as strings",
                    },
                },
                "required": ["object_name", "component_type", "property", "value"],
            },
        ),
        Tool(
            name="unity_remove_component",
            description="Remove a component from a GameObject",
            inputSchema={
                "type": "object",
                "properties": {
                    "object_name": {
                        "type": "string",
                        "description": "Name of the GameObject",
                    },
                    "component_type": {
                        "type": "string",
                        "description": "Type of component to remove (same types as add_component)",
                    },
                },
                "required": ["object_name", "component_type"],
            },
        ),
        Tool(
            name="unity_cleanup_screenshots",
            description="Clean up old screenshots to save disk space",
            inputSchema={
                "type": "object",
                "properties": {
                    "keep_count": {
                        "type": "number",
                        "description": "Number of recent screenshots to keep (default: 5)",
                    },
                },
            },
        ),
        Tool(
            name="unity_force_refresh",
            description="Force Unity to refresh and recompile immediately",
            inputSchema={
                "type": "object",
                "properties": {
                    "reason": {
                        "type": "string",
                        "description": "Optional reason for the refresh",
                    },
                },
            },
        ),
    ]

@server.call_tool()
async def handle_call_tool(name: str, arguments: Dict[str, Any]) -> List[TextContent]:
    """Handle Unity tool calls"""
    if not inspector:
        return [TextContent(type="text", text="Unity project not found")]
    
    try:
        if name == "unity_read_script":
            return await read_script(arguments)
        elif name == "unity_create_script":
            return await create_script(arguments)
        elif name == "unity_inspect_scene":
            return await inspect_scene(arguments)
        elif name == "unity_list_gameobjects":
            return await list_gameobjects(arguments)
        elif name == "unity_search_assets":
            return await search_assets(arguments)
        elif name == "unity_get_project_overview":
            return await get_project_overview(arguments)
        elif name == "unity_capture_screenshot":
            return await capture_screenshot(arguments)
        elif name == "unity_capture_camera_view":
            return await capture_camera_view(arguments)
        elif name == "unity_get_scene_info":
            return await get_scene_info(arguments)
        elif name == "unity_get_scene_hierarchy":
            return await get_scene_hierarchy(arguments)
        elif name == "unity_create_gameobject":
            return await create_gameobject(arguments)
        elif name == "unity_delete_gameobject":
            return await delete_gameobject(arguments)
        elif name == "unity_set_property":
            return await set_property(arguments)
        elif name == "unity_add_component":
            return await add_component(arguments)
        elif name == "unity_remove_component":
            return await remove_component(arguments)
        elif name == "unity_set_component_property":
            return await set_component_property(arguments)
        elif name == "unity_cleanup_screenshots":
            return await cleanup_screenshots(arguments)
        elif name == "unity_force_refresh":
            return await force_refresh(arguments)
        else:
            return [TextContent(type="text", text=f"Unknown tool: {name}")]
    except Exception as e:
        return [TextContent(type="text", text=f"Error executing tool {name}: {str(e)}")]

async def read_script(args: Dict[str, Any]) -> List[TextContent]:
    """Read a Unity C# script file"""
    script_path = args.get("script_path")
    if not script_path:
        return [TextContent(type="text", text="script_path is required")]
    
    full_path = inspector.project_root / script_path
    
    try:
        if not full_path.exists():
            return [TextContent(type="text", text=f"Script not found: {script_path}")]
        
        content = full_path.read_text(encoding='utf-8')
        return [TextContent(type="text", text=content)]
    
    except Exception as e:
        return [TextContent(type="text", text=f"Error reading script: {str(e)}")]

async def create_script(args: Dict[str, Any]) -> List[TextContent]:
    """Create a new C# script"""
    script_name = args.get("script_name")
    folder_path = args.get("folder_path", "Scripts")
    
    if not script_name:
        return [TextContent(type="text", text="script_name is required")]
    
    # Ensure script name is valid
    script_name = re.sub(r'[^a-zA-Z0-9_]', '', script_name)
    if not script_name or script_name[0].isdigit():
        return [TextContent(type="text", text="Invalid script name")]
    
    # Create target directory
    target_dir = inspector.assets_path / folder_path
    target_dir.mkdir(parents=True, exist_ok=True)
    
    script_file = target_dir / f"{script_name}.cs"
    
    if script_file.exists():
        return [TextContent(type="text", text=f"Script already exists: {script_file.relative_to(inspector.project_root)}")]
    
    # Simple MonoBehaviour template
    script_content = f"""using UnityEngine;

public class {script_name} : MonoBehaviour
{{
    void Start()
    {{
        
    }}
    
    void Update()
    {{
        
    }}
}}"""
    
    try:
        script_file.write_text(script_content, encoding='utf-8')
        
        # Force Unity refresh immediately after creating script
        await force_refresh({"reason": f"Created script: {script_name}"})
        
        result = {
            "success": True,
            "script_path": str(script_file.relative_to(inspector.project_root)),
            "lines": len(script_content.split('\n')),
            "auto_refresh": "forced"
        }
        
        return [TextContent(type="text", text=json.dumps(result, indent=2))]
        
    except Exception as e:
        return [TextContent(type="text", text=f"Error creating script: {str(e)}")]

async def inspect_scene(args: Dict[str, Any]) -> List[TextContent]:
    """Inspect a Unity scene to see GameObjects and components"""
    scene_path = args.get("scene_path")
    if not scene_path:
        return [TextContent(type="text", text="scene_path is required")]
    
    try:
        scene_data = inspector.get_scene_hierarchy(scene_path)
        return [TextContent(type="text", text=json.dumps(scene_data, indent=2))]
    except Exception as e:
        return [TextContent(type="text", text=f"Error inspecting scene: {str(e)}")]

async def list_gameobjects(args: Dict[str, Any]) -> List[TextContent]:
    """List GameObjects in a scene with simplified view"""
    scene_path = args.get("scene_path")
    if not scene_path:
        return [TextContent(type="text", text="scene_path is required")]
    
    try:
        scene_data = inspector.get_scene_hierarchy(scene_path)
        
        if "error" in scene_data:
            return [TextContent(type="text", text=json.dumps(scene_data, indent=2))]
        
        # Create simplified GameObject list
        gameobjects_summary = []
        for go in scene_data.get("gameObjects", []):
            go_summary = {
                "name": go.get("name", "Unknown"),
                "active": go.get("active", True),
                "components": [comp.get("type", "Unknown") for comp in go.get("components", [])],
                "position": go.get("transform", {}).get("position", {"x": 0, "y": 0, "z": 0}) if go.get("transform") else None
            }
            gameobjects_summary.append(go_summary)
        
        result = {
            "scene": scene_data.get("name", "Unknown"),
            "path": scene_data.get("path", ""),
            "gameObjects": gameobjects_summary,
            "summary": scene_data.get("summary", {})
        }
        
        return [TextContent(type="text", text=json.dumps(result, indent=2))]
        
    except Exception as e:
        return [TextContent(type="text", text=f"Error listing GameObjects: {str(e)}")]

async def search_assets(args: Dict[str, Any]) -> List[TextContent]:
    """Search for assets by name or type"""
    search_term = args.get("search_term", "").lower()
    asset_type = args.get("asset_type", "").lower()
    
    try:
        results = {"matches": [], "search_term": search_term, "asset_type": asset_type}
        
        # Define search patterns based on asset type
        patterns = {
            "script": "*.cs",
            "scene": "*.unity", 
            "prefab": "*.prefab",
            "material": "*.mat",
            "texture": ["*.png", "*.jpg", "*.jpeg", "*.tga"],
            "audio": ["*.wav", "*.mp3", "*.ogg"]
        }
        
        # Search in appropriate file types
        if asset_type and asset_type in patterns:
            pattern_list = patterns[asset_type] if isinstance(patterns[asset_type], list) else [patterns[asset_type]]
        else:
            # Search all types
            pattern_list = []
            for p in patterns.values():
                if isinstance(p, list):
                    pattern_list.extend(p)
                else:
                    pattern_list.append(p)
        
        # Perform search
        for pattern in pattern_list:
            for file_path in inspector.assets_path.rglob(pattern):
                if search_term in file_path.stem.lower():
                    results["matches"].append({
                        "name": file_path.stem,
                        "path": str(file_path.relative_to(inspector.project_root)),
                        "type": file_path.suffix[1:],  # Remove the dot
                        "size": file_path.stat().st_size,
                        "folder": str(file_path.parent.relative_to(inspector.assets_path))
                    })
        
        # Sort by relevance (exact matches first, then partial)
        results["matches"].sort(key=lambda x: (
            0 if search_term == x["name"].lower() else 1,
            x["name"].lower()
        ))
        
        return [TextContent(type="text", text=json.dumps(results, indent=2))]
        
    except Exception as e:
        return [TextContent(type="text", text=f"Error searching assets: {str(e)}")]

async def get_project_overview(args: Dict[str, Any]) -> List[TextContent]:
    """Get comprehensive project overview"""
    try:
        overview = inspector.get_project_structure()
        return [TextContent(type="text", text=json.dumps(overview, indent=2))]
    except Exception as e:
        return [TextContent(type="text", text=f"Error getting project overview: {str(e)}")]

async def capture_screenshot(args: Dict[str, Any]) -> List[TextContent]:
    """Capture a screenshot of Unity Scene View or Game View"""
    view_type = args.get("view_type", "scene")
    
    try:
        # Create command file for Unity to process
        command_dir = inspector.project_root / "Temp" / "KiroCommands"
        command_dir.mkdir(parents=True, exist_ok=True)
        
        command_file = command_dir / "command.txt"
        result_file = command_dir / "result.txt"
        
        # Remove old result file
        if result_file.exists():
            result_file.unlink()
        
        # Write command with optional delay
        delay_seconds = args.get("delay_seconds", 0)
        
        if view_type == "scene":
            command = "capture_scene"
        elif view_type == "game":
            command = "capture_game"
        elif view_type == "editor":
            command = "capture_editor"
        else:
            command = "capture_scene"  # default fallback
        
        # Add delay if specified
        if delay_seconds > 0:
            command += f"|{int(delay_seconds)}"
        
        command_file.write_text(command)
        
        # Wait for Unity to process the command
        import time
        for i in range(10):  # Wait up to 5 seconds
            time.sleep(0.5)
            if result_file.exists():
                result_text = result_file.read_text().strip()
                result_file.unlink()  # Clean up
                
                if result_text != "Failed":
                    result = {
                        "success": True,
                        "action": "capture_screenshot",
                        "view_type": view_type,
                        "screenshot_path": result_text.replace(str(inspector.project_root) + "\\", "").replace("\\", "/"),
                        "timestamp": datetime.now().isoformat()
                    }
                else:
                    result = {
                        "success": False,
                        "action": "capture_screenshot",
                        "view_type": view_type,
                        "error": "Screenshot capture failed",
                        "timestamp": datetime.now().isoformat()
                    }
                
                return [TextContent(type="text", text=json.dumps(result, indent=2))]
        
        # Timeout
        result = {
            "success": False,
            "action": "capture_screenshot",
            "view_type": view_type,
            "error": "Timeout waiting for Unity to process command",
            "timestamp": datetime.now().isoformat()
        }
        
        return [TextContent(type="text", text=json.dumps(result, indent=2))]
        
    except Exception as e:
        return [TextContent(type="text", text=f"Error capturing screenshot: {str(e)}")]

async def capture_camera_view(args: Dict[str, Any]) -> List[TextContent]:
    """Capture a screenshot from a specific camera"""
    camera_name = args.get("camera_name", "Main Camera")
    width = args.get("width", 0)
    height = args.get("height", 0)
    
    try:
        # Create command file for Unity to process
        command_dir = inspector.project_root / "Temp" / "KiroCommands"
        command_dir.mkdir(parents=True, exist_ok=True)
        
        command_file = command_dir / "command.txt"
        result_file = command_dir / "result.txt"
        
        # Remove old result file
        if result_file.exists():
            result_file.unlink()
        
        # Write command with optional resolution
        command = f"capture_camera|{camera_name}|{int(width)}|{int(height)}"
        command_file.write_text(command)
        
        # Wait for Unity to process the command
        import time
        for i in range(10):  # Wait up to 5 seconds
            time.sleep(0.5)
            if result_file.exists():
                result_text = result_file.read_text().strip()
                result_file.unlink()  # Clean up
                
                if result_text not in ["Failed", "Camera not found"]:
                    result = {
                        "success": True,
                        "action": "capture_camera_view",
                        "camera_name": camera_name,
                        "screenshot_path": result_text.replace(str(inspector.project_root) + "\\", "").replace("\\", "/"),
                        "timestamp": datetime.now().isoformat()
                    }
                else:
                    result = {
                        "success": False,
                        "action": "capture_camera_view",
                        "camera_name": camera_name,
                        "error": result_text,
                        "timestamp": datetime.now().isoformat()
                    }
                
                return [TextContent(type="text", text=json.dumps(result, indent=2))]
        
        # Timeout
        result = {
            "success": False,
            "action": "capture_camera_view",
            "camera_name": camera_name,
            "error": "Timeout waiting for Unity to process command",
            "timestamp": datetime.now().isoformat()
        }
        
        return [TextContent(type="text", text=json.dumps(result, indent=2))]
        
    except Exception as e:
        return [TextContent(type="text", text=f"Error capturing camera view: {str(e)}")]

async def get_scene_info(args: Dict[str, Any]) -> List[TextContent]:
    """Get detailed scene information from Unity"""
    try:
        # Create a trigger script that will export scene info
        trigger_file = inspector.project_root / "Temp" / "kiro_scene_info_trigger.cs"
        trigger_content = f"""// KIRO SCENE INFO TRIGGER - {datetime.now()}
using UnityEngine;
using UnityEditor;
using System.IO;
using System;
using System.Collections.Generic;

[InitializeOnLoad]
public class KiroSceneInfoTrigger_{int(datetime.now().timestamp())}
{{
    static KiroSceneInfoTrigger_{int(datetime.now().timestamp())}()
    {{
        EditorApplication.delayCall += () => {{
            SaveSceneInfoToFile();
            
            // Clean up this trigger file
            string thisFile = "Temp/kiro_scene_info_trigger.cs";
            if (File.Exists(thisFile))
            {{
                File.Delete(thisFile);
            }}
        }};
    }}
    
    static void SaveSceneInfoToFile()
    {{
        try
        {{
            string directory = "Temp/KiroSceneInfo";
            Directory.CreateDirectory(directory);
            
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var sceneInfo = new Dictionary<string, object>();
            
            sceneInfo["sceneName"] = scene.name;
            sceneInfo["scenePath"] = scene.path;
            sceneInfo["timestamp"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            
            // Get GameObjects
            var gameObjects = new List<Dictionary<string, object>>();
            GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>();
            
            foreach (GameObject go in allObjects)
            {{
                if (go.transform.parent == null) // Only root objects
                {{
                    var goInfo = new Dictionary<string, object>();
                    goInfo["name"] = go.name;
                    goInfo["isActive"] = go.activeInHierarchy;
                    goInfo["position"] = go.transform.position.ToString();
                    goInfo["rotation"] = go.transform.rotation.eulerAngles.ToString();
                    goInfo["scale"] = go.transform.localScale.ToString();
                    goInfo["tag"] = go.tag;
                    goInfo["layer"] = LayerMask.LayerToName(go.layer);
                    
                    var components = new List<string>();
                    Component[] comps = go.GetComponents<Component>();
                    foreach (Component comp in comps)
                    {{
                        if (comp != null)
                            components.Add(comp.GetType().Name);
                    }}
                    goInfo["components"] = components;
                    
                    gameObjects.Add(goInfo);
                }}
            }}
            sceneInfo["gameObjects"] = gameObjects;
            
            // Get cameras
            var cameras = new List<Dictionary<string, object>>();
            Camera[] cams = GameObject.FindObjectsOfType<Camera>();
            foreach (Camera cam in cams)
            {{
                var camInfo = new Dictionary<string, object>();
                camInfo["name"] = cam.name;
                camInfo["position"] = cam.transform.position.ToString();
                camInfo["rotation"] = cam.transform.rotation.eulerAngles.ToString();
                camInfo["fieldOfView"] = cam.fieldOfView;
                camInfo["isOrthographic"] = cam.orthographic;
                camInfo["isMainCamera"] = cam.CompareTag("MainCamera");
                cameras.Add(camInfo);
            }}
            sceneInfo["cameras"] = cameras;
            
            string json = JsonUtility.ToJson(sceneInfo, true);
            string filename = $"scene_{{scene.name}}_{{DateTime.Now:yyyyMMdd_HHmmss}}.json";
            string fullPath = Path.Combine(directory, filename);
            
            File.WriteAllText(fullPath, json);
            Debug.Log($"Kiro: Scene info saved to {{fullPath}}");
        }}
        catch (Exception e)
        {{
            Debug.LogError($"Kiro: Failed to save scene info: {{e.Message}}");
        }}
    }}
}}"""
        
        trigger_file.write_text(trigger_content, encoding='utf-8')
        
        # Wait a moment for Unity to process
        import time
        time.sleep(2)
        
        # Try to read the generated scene info
        scene_info_dir = inspector.project_root / "Temp" / "KiroSceneInfo"
        if scene_info_dir.exists():
            json_files = list(scene_info_dir.glob("*.json"))
            if json_files:
                latest_info = max(json_files, key=lambda p: p.stat().st_mtime)
                try:
                    scene_data = json.loads(latest_info.read_text())
                    return [TextContent(type="text", text=json.dumps(scene_data, indent=2))]
                except Exception as e:
                    pass
        
        # If no scene info found, return basic info
        result = {
            "success": True,
            "action": "get_scene_info",
            "message": "Scene info export initiated - check Temp/KiroSceneInfo folder",
            "timestamp": datetime.now().isoformat()
        }
        
        return [TextContent(type="text", text=json.dumps(result, indent=2))]
        
    except Exception as e:
        return [TextContent(type="text", text=f"Error getting scene info: {str(e)}")]

async def get_scene_hierarchy(args: Dict[str, Any]) -> List[TextContent]:
    """Get the exact hierarchy of GameObjects in the current scene"""
    try:
        # Create command file for Unity to process
        command_dir = inspector.project_root / "Temp" / "KiroCommands"
        command_dir.mkdir(parents=True, exist_ok=True)
        
        command_file = command_dir / "command.txt"
        result_file = command_dir / "result.txt"
        
        # Remove old result file
        if result_file.exists():
            result_file.unlink()
        
        # Write command
        command = "get_hierarchy"
        command_file.write_text(command)
        
        # Wait for Unity to process the command
        import time
        for i in range(10):  # Wait up to 5 seconds
            time.sleep(0.5)
            if result_file.exists():
                result_text = result_file.read_text().strip()
                result_file.unlink()  # Clean up
                
                try:
                    # Try to parse as JSON
                    hierarchy_data = json.loads(result_text)
                    return [TextContent(type="text", text=json.dumps(hierarchy_data, indent=2))]
                except json.JSONDecodeError:
                    # If not JSON, return as text
                    return [TextContent(type="text", text=result_text)]
        
        # Timeout
        result = {
            "success": False,
            "action": "get_scene_hierarchy",
            "error": "Timeout waiting for Unity to process command",
            "timestamp": datetime.now().isoformat()
        }
        
        return [TextContent(type="text", text=json.dumps(result, indent=2))]
        
    except Exception as e:
        return [TextContent(type="text", text=f"Error getting scene hierarchy: {str(e)}")]

async def create_gameobject(args: Dict[str, Any]) -> List[TextContent]:
    """Create a new GameObject"""
    name = args.get("name", "GameObject")
    parent = args.get("parent", "")
    
    try:
        command_dir = inspector.project_root / "Temp" / "KiroCommands"
        command_dir.mkdir(parents=True, exist_ok=True)
        
        command_file = command_dir / "command.txt"
        result_file = command_dir / "result.txt"
        
        if result_file.exists():
            result_file.unlink()
        
        command = f"create_gameobject|{name}|{parent}"
        command_file.write_text(command)
        
        import time
        for i in range(10):
            time.sleep(0.5)
            if result_file.exists():
                result_text = result_file.read_text().strip()
                result_file.unlink()
                return [TextContent(type="text", text=result_text)]
        
        return [TextContent(type="text", text="Timeout waiting for Unity")]
        
    except Exception as e:
        return [TextContent(type="text", text=f"Error: {str(e)}")]

async def delete_gameobject(args: Dict[str, Any]) -> List[TextContent]:
    """Delete a GameObject"""
    name = args.get("name", "")
    
    try:
        command_dir = inspector.project_root / "Temp" / "KiroCommands"
        command_dir.mkdir(parents=True, exist_ok=True)
        
        command_file = command_dir / "command.txt"
        result_file = command_dir / "result.txt"
        
        if result_file.exists():
            result_file.unlink()
        
        command = f"delete_gameobject|{name}"
        command_file.write_text(command)
        
        import time
        for i in range(10):
            time.sleep(0.5)
            if result_file.exists():
                result_text = result_file.read_text().strip()
                result_file.unlink()
                return [TextContent(type="text", text=result_text)]
        
        return [TextContent(type="text", text="Timeout waiting for Unity")]
        
    except Exception as e:
        return [TextContent(type="text", text=f"Error: {str(e)}")]

async def set_property(args: Dict[str, Any]) -> List[TextContent]:
    """Set GameObject property"""
    object_name = args.get("object_name", "")
    property_name = args.get("property", "")
    value = args.get("value", "")
    
    try:
        command_dir = inspector.project_root / "Temp" / "KiroCommands"
        command_dir.mkdir(parents=True, exist_ok=True)
        
        command_file = command_dir / "command.txt"
        result_file = command_dir / "result.txt"
        
        if result_file.exists():
            result_file.unlink()
        
        command = f"set_property|{object_name}|{property_name}|{value}"
        command_file.write_text(command)
        
        import time
        for i in range(10):
            time.sleep(0.5)
            if result_file.exists():
                result_text = result_file.read_text().strip()
                result_file.unlink()
                return [TextContent(type="text", text=result_text)]
        
        return [TextContent(type="text", text="Timeout waiting for Unity")]
        
    except Exception as e:
        return [TextContent(type="text", text=f"Error: {str(e)}")]

async def add_component(args: Dict[str, Any]) -> List[TextContent]:
    """Add component to GameObject"""
    object_name = args.get("object_name", "")
    component_type = args.get("component_type", "")
    
    try:
        command_dir = inspector.project_root / "Temp" / "KiroCommands"
        command_dir.mkdir(parents=True, exist_ok=True)
        
        command_file = command_dir / "command.txt"
        result_file = command_dir / "result.txt"
        
        if result_file.exists():
            result_file.unlink()
        
        command = f"add_component|{object_name}|{component_type}"
        command_file.write_text(command)
        
        import time
        for i in range(10):
            time.sleep(0.5)
            if result_file.exists():
                result_text = result_file.read_text().strip()
                result_file.unlink()
                return [TextContent(type="text", text=result_text)]
        
        return [TextContent(type="text", text="Timeout waiting for Unity")]
        
    except Exception as e:
        return [TextContent(type="text", text=f"Error: {str(e)}")]

async def remove_component(args: Dict[str, Any]) -> List[TextContent]:
    """Remove component from GameObject"""
    object_name = args.get("object_name", "")
    component_type = args.get("component_type", "")
    
    try:
        command_dir = inspector.project_root / "Temp" / "KiroCommands"
        command_dir.mkdir(parents=True, exist_ok=True)
        
        command_file = command_dir / "command.txt"
        result_file = command_dir / "result.txt"
        
        if result_file.exists():
            result_file.unlink()
        
        command = f"remove_component|{object_name}|{component_type}"
        command_file.write_text(command)
        
        import time
        for i in range(10):
            time.sleep(0.5)
            if result_file.exists():
                result_text = result_file.read_text().strip()
                result_file.unlink()
                return [TextContent(type="text", text=result_text)]
        
        return [TextContent(type="text", text="Timeout waiting for Unity")]
        
    except Exception as e:
        return [TextContent(type="text", text=f"Error: {str(e)}")]

async def set_component_property(args: Dict[str, Any]) -> List[TextContent]:
    """Set component property"""
    object_name = args.get("object_name", "")
    component_type = args.get("component_type", "")
    property_name = args.get("property", "")
    value = args.get("value", "")
    
    try:
        command_dir = inspector.project_root / "Temp" / "KiroCommands"
        command_dir.mkdir(parents=True, exist_ok=True)
        
        command_file = command_dir / "command.txt"
        result_file = command_dir / "result.txt"
        
        if result_file.exists():
            result_file.unlink()
        
        command = f"set_component_property|{object_name}|{component_type}|{property_name}|{value}"
        command_file.write_text(command)
        
        import time
        for i in range(10):
            time.sleep(0.5)
            if result_file.exists():
                result_text = result_file.read_text().strip()
                result_file.unlink()
                return [TextContent(type="text", text=result_text)]
        
        return [TextContent(type="text", text="Timeout waiting for Unity")]
        
    except Exception as e:
        return [TextContent(type="text", text=f"Error: {str(e)}")]

async def cleanup_screenshots(args: Dict[str, Any]) -> List[TextContent]:
    """Clean up old screenshots to save disk space"""
    keep_count = args.get("keep_count", 5)
    
    try:
        # Create command file for Unity to process
        command_dir = inspector.project_root / "Temp" / "KiroCommands"
        command_dir.mkdir(parents=True, exist_ok=True)
        
        command_file = command_dir / "command.txt"
        result_file = command_dir / "result.txt"
        
        # Remove old result file
        if result_file.exists():
            result_file.unlink()
        
        # Write command
        command = f"cleanup_screenshots|{int(keep_count)}"
        command_file.write_text(command)
        
        # Wait for Unity to process the command
        import time
        for i in range(10):  # Wait up to 5 seconds
            time.sleep(0.5)
            if result_file.exists():
                result_text = result_file.read_text().strip()
                result_file.unlink()  # Clean up
                
                result = {
                    "success": True,
                    "action": "cleanup_screenshots",
                    "keep_count": keep_count,
                    "message": result_text,
                    "timestamp": datetime.now().isoformat()
                }
                
                return [TextContent(type="text", text=json.dumps(result, indent=2))]
        
        # Timeout
        result = {
            "success": False,
            "action": "cleanup_screenshots",
            "error": "Timeout waiting for Unity to process command",
            "timestamp": datetime.now().isoformat()
        }
        
        return [TextContent(type="text", text=json.dumps(result, indent=2))]
        
    except Exception as e:
        return [TextContent(type="text", text=f"Error cleaning up screenshots: {str(e)}")]

async def force_refresh(args: Dict[str, Any]) -> List[TextContent]:
    """Force Unity to refresh by creating a trigger file"""
    reason = args.get("reason", "Manual refresh request")
    
    try:
        # Create a trigger file that Unity's auto-refresh will detect
        trigger_file = inspector.project_root / "Assets" / "kiro_refresh_trigger.cs"
        trigger_content = f"""// KIRO REFRESH TRIGGER - {datetime.now()}
// This file forces Unity to refresh - it will be auto-deleted
// Reason: {reason}
using UnityEngine;
public class KiroRefreshTrigger_{int(datetime.now().timestamp())} : MonoBehaviour {{ }}
"""
        
        trigger_file.write_text(trigger_content, encoding='utf-8')
        
        # Immediately delete it to clean up
        import time
        time.sleep(0.1)  # Small delay to ensure Unity detects it
        if trigger_file.exists():
            trigger_file.unlink()
        
        result = {
            "success": True,
            "action": "force_refresh",
            "reason": reason,
            "timestamp": datetime.now().isoformat()
        }
        
        return [TextContent(type="text", text=json.dumps(result, indent=2))]
        
    except Exception as e:
        return [TextContent(type="text", text=f"Error forcing refresh: {str(e)}")]

async def main():
    """Main entry point for the MCP server"""
    # Initialize the Unity project inspector
    if not initialize_inspector():
        print("Warning: Unity project not detected. Some features may not work.", file=sys.stderr)
    
    # Run the server
    async with stdio_server() as (read_stream, write_stream):
        await server.run(
            read_stream,
            write_stream,
            InitializationOptions(
                server_name="unity-mcp",
                server_version="1.0.0",
                capabilities=server.get_capabilities(
                    notification_options=NotificationOptions(),
                    experimental_capabilities={}
                ),
            ),
        )

if __name__ == "__main__":
    asyncio.run(main())