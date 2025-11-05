#!/usr/bin/env python3
"""
Unity MCP Server - Simple Unity project integration for Kiro AI
"""

import asyncio
import json
import os
import sys
import re
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
    ]

@server.read_resource()
async def handle_read_resource(uri: str) -> str:
    """Read Unity project resource content"""
    if not inspector:
        return json.dumps({"error": "Unity project not found"})
    
    try:
        if uri == "unity://project-info":
            return json.dumps(inspector.get_project_info(), indent=2)
        elif uri == "unity://scripts":
            return json.dumps(inspector.list_scripts(), indent=2)
        elif uri == "unity://scenes":
            return json.dumps(inspector.list_scenes(), indent=2)
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