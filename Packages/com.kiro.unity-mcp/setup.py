#!/usr/bin/env python3
"""
Setup script for Unity MCP Server
"""

import subprocess
import sys
import os
from pathlib import Path

def install_requirements():
    """Install required Python packages"""
    requirements_file = Path(__file__).parent / "Runtime" / "requirements.txt"
    
    try:
        subprocess.check_call([
            sys.executable, "-m", "pip", "install", "-r", str(requirements_file)
        ])
        print("✅ Successfully installed Python requirements")
        return True
    except subprocess.CalledProcessError as e:
        print(f"❌ Failed to install requirements: {e}")
        return False

def test_mcp_server():
    """Test if the MCP server can be imported and run"""
    server_file = Path(__file__).parent / "Runtime" / "UnityMCPServer.py"
    
    try:
        # Test import
        result = subprocess.run([
            sys.executable, "-c", 
            f"import sys; sys.path.insert(0, '{server_file.parent}'); import UnityMCPServer"
        ], capture_output=True, text=True)
        
        if result.returncode == 0:
            print("✅ MCP server can be imported successfully")
            return True
        else:
            print(f"❌ Failed to import MCP server: {result.stderr}")
            return False
    except Exception as e:
        print(f"❌ Error testing MCP server: {e}")
        return False

def main():
    """Main setup function"""
    print("🚀 Setting up Unity MCP Server...")
    
    # Install requirements
    if not install_requirements():
        sys.exit(1)
    
    # Test server
    if not test_mcp_server():
        sys.exit(1)
    
    print("\n✅ Unity MCP Server setup complete!")
    print("\nNext steps:")
    print("1. Restart Kiro to load the new MCP server")
    print("2. The server will automatically detect your Unity project")
    print("3. Use resources like 'unity://project-info' to inspect your project")
    print("4. Use tools like 'unity_find_assets' to search for assets")

if __name__ == "__main__":
    main()