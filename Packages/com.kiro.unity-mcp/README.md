# Kiro Unity MCP Server - Advanced Unity Integration

A comprehensive Model Context Protocol (MCP) server that provides deep Unity project integration for Kiro AI assistant. This advanced implementation offers real-time Unity Editor communication, comprehensive project analysis, and intelligent development assistance.

## 🚀 Advanced Features

### Core Capabilities
- **Real-time Unity Editor Bridge**: Direct communication with Unity Editor for live project manipulation
- **Comprehensive Project Analysis**: Deep inspection of scripts, assets, scenes, and dependencies
- **Performance Analysis**: Automated detection of performance issues and optimization suggestions
- **Code Intelligence**: Advanced script analysis with dependency tracking and documentation generation
- **Asset Management**: Smart asset discovery, dependency analysis, and unused asset detection
- **Build Integration**: Access to build settings, player configuration, and compilation status

### Unity Editor Integration
- **Live Project Monitoring**: Real-time status updates and project state tracking
- **Remote Control**: Play/pause/stop Unity Editor from Kiro
- **Asset Manipulation**: Create, modify, and organize project assets
- **Scene Management**: Analyze and manipulate Unity scenes
- **Console Integration**: Access Unity console logs and debugging information

## 📦 Installation

### Quick Setup
1. **Automatic Installation**:
   ```bash
   python Packages/com.kiro.unity-mcp/setup.py
   ```

2. **Unity Editor Integration**:
   - Open Unity Editor
   - Go to `Kiro > Setup MCP Server` (menu will appear after package import)
   - Click "Test Connection" to verify setup

### Manual Setup
1. **Install Python Dependencies**:
   ```bash
   pip install -r Packages/com.kiro.unity-mcp/Runtime/requirements.txt
   ```

2. **Verify Configuration**:
   - Check `.kiro/settings/mcp.json` is created
   - Restart Kiro to load the MCP server

3. **Unity Editor Setup**:
   - The bridge will automatically initialize when Unity starts
   - Check `Kiro > MCP Control Panel` for status

## 🛠️ Comprehensive Resources

### Project Resources
- `unity://project-info` - Complete project metadata and settings
- `unity://build-settings` - Build configuration and player settings
- `unity://statistics` - Comprehensive project statistics and metrics
- `unity://editor-status` - Real-time Unity Editor status

### Asset Resources
- `unity://scenes` - Detailed scene analysis with GameObject counts
- `unity://scripts` - C# scripts with metadata and analysis
- `unity://prefabs` - Prefab information with component details
- `unity://materials` - Materials with shader information
- `unity://textures` - Texture analysis with size and format data
- `unity://audio` - Audio file inventory and metadata
- `unity://animations` - Animation clips and controller information
- `unity://packages` - Package management and dependency info

### Development Resources
- `unity://console-logs` - Recent Unity console output
- `unity://assets` - Complete asset hierarchy and structure

## 🔧 Advanced Tools

### Asset Management
- `unity_find_assets` - Advanced asset search with size filtering and metadata
- `unity_get_asset_dependencies` - Comprehensive dependency analysis
- `unity_performance_analysis` - Automated performance issue detection

### Code Intelligence
- `unity_read_script` - Read C# script content with syntax highlighting
- `unity_analyze_script` - Deep script analysis (dependencies, methods, structure)
- `unity_search_code` - Regex-powered code search across all scripts
- `unity_create_script_template` - Generate scripts from templates

### Unity Editor Control
- `unity_editor_command` - Remote Unity Editor control (play/pause/stop/compile)
- `unity_analyze_scene` - Detailed scene analysis with component breakdown

### Documentation & Analysis
- `unity_generate_documentation` - Auto-generate project documentation
- `unity_performance_analysis` - Identify performance bottlenecks

## 💡 Example Usage with Kiro

### Project Analysis
```
"Analyze this Unity project's performance"
"Show me all large texture files"
"Find scripts that use the Update method"
"Generate documentation for the PlayerController script"
```

### Asset Management
```
"Find all materials using the Standard shader"
"Show me unused assets in the project"
"List all prefabs with Rigidbody components"
"What scripts reference the GameManager?"
```

### Development Assistance
```
"Create a new MonoBehaviour script called EnemyAI"
"Search for all TODO comments in the codebase"
"Show me the dependency tree for the Player prefab"
"What's the current Unity Editor status?"
```

### Real-time Control
```
"Start play mode in Unity"
"Refresh the asset database"
"Compile the project"
"Show me the current scene information"
```

## 🎮 Unity Editor Features

### Control Panel
Access the comprehensive control panel via `Kiro > MCP Control Panel`:
- **Server Status**: Real-time connection status and Unity Editor state
- **Quick Actions**: One-click operations (setup, test, refresh, build)
- **Project Information**: Live project statistics and metadata
- **Search & Analysis**: Built-in asset search and performance analysis
- **Operation Logs**: Track all MCP operations and results

### Menu Integration
- `Kiro > Setup MCP Server` - Automated setup and configuration
- `Kiro > Test MCP Connection` - Verify server connectivity
- `Kiro > Open MCP Configuration` - Edit MCP settings
- `Kiro > MCP Control Panel` - Open the comprehensive control interface
- `Kiro > About Kiro Unity MCP` - Package information and help

## 🔄 Real-time Bridge System

The Unity Editor Bridge provides:
- **Live Status Updates**: Continuous monitoring of Unity Editor state
- **Command Processing**: Real-time command execution and response
- **File System Integration**: Automatic file watching and synchronization
- **Error Handling**: Robust error reporting and recovery

### Bridge Commands
- `play/pause/stop` - Control play mode
- `compile/refresh` - Trigger compilation and asset refresh
- `get_selection` - Get currently selected objects
- `get_scene_info` - Current scene information
- `select_object` - Select specific GameObjects
- `create_gameobject` - Create new GameObjects

## 📊 Performance Analysis

### Automated Detection
- **Large Assets**: Identify oversized textures, audio, and models
- **Script Performance**: Find scripts with expensive Update methods
- **Scene Complexity**: Analyze scene GameObject counts and hierarchy depth
- **Memory Usage**: Estimate memory consumption by asset type

### Optimization Suggestions
- Texture compression recommendations
- Script optimization advice
- Scene organization suggestions
- Asset cleanup recommendations

## 🚀 Advanced Workflows

### AI-Assisted Development
1. **Code Review**: "Review the PlayerMovement script for performance issues"
2. **Asset Optimization**: "Find all textures larger than 2MB and suggest optimizations"
3. **Architecture Analysis**: "Show me the dependency relationships in my scripts"
4. **Documentation**: "Generate comprehensive documentation for my project"

### Project Maintenance
1. **Cleanup**: "Find unused assets and suggest removal"
2. **Organization**: "Analyze my project structure and suggest improvements"
3. **Performance**: "Identify performance bottlenecks in my scenes"
4. **Dependencies**: "Show me what would break if I remove this script"

## 📋 Requirements

- **Python**: 3.7+ with pip
- **Unity**: 2022.3+ (2023.x recommended)
- **Kiro**: Latest version with MCP support
- **Dependencies**: Automatically installed via setup script
  - `mcp>=1.0.0` - Model Context Protocol
  - `pyyaml>=6.0` - YAML parsing
  - `watchdog>=3.0.0` - File system monitoring
  - `pillow>=9.0.0` - Image processing

## 🔧 Troubleshooting

### Common Issues

**Server Not Starting**
- Verify Python installation: `python --version`
- Check dependencies: `pip list | grep mcp`
- Review MCP configuration in `.kiro/settings/mcp.json`

**Unity Bridge Inactive**
- Ensure Unity Editor is running
- Check `Temp/KiroMCPBridge` folder exists
- Verify no firewall blocking file system access

**Performance Issues**
- Large projects may take time to analyze
- Use specific search terms to limit scope
- Consider excluding large asset folders

### Debug Mode
Enable detailed logging by setting environment variable:
```bash
export FASTMCP_LOG_LEVEL=DEBUG
```

## 🏗️ Architecture

### Core Components
- **UnityMCPServer.py**: Main MCP server implementation
- **KiroMCPBridge.cs**: Unity Editor integration bridge
- **KiroMCPWindow.cs**: Unity Editor control panel
- **KiroMCPMenu.cs**: Unity Editor menu integration

### Data Flow
1. Kiro sends MCP requests to Python server
2. Server analyzes Unity project files
3. For real-time operations, server communicates with Unity Editor bridge
4. Results are formatted and returned to Kiro

## 📦 Package Distribution

### Creating Unity Package
1. **Export Package**:
   - Select `Packages/com.kiro.unity-mcp` in Project window
   - Right-click → Export Package
   - Include all files and dependencies

2. **Distribution**:
   - Share `.unitypackage` file
   - Recipients import via `Assets > Import Package > Custom Package`

### Version Management
- Update `package.json` version field
- Update README with changelog
- Test with multiple Unity versions

## 🤝 Contributing

This package is designed for extensibility:
- Add new MCP resources in `handle_list_resources()`
- Implement new tools in `handle_call_tool()`
- Extend Unity Editor bridge with additional commands
- Enhance analysis capabilities in `UnityProjectInspector`

## 📄 License

This package is provided as-is for use with Kiro AI Assistant. Designed for maximum compatibility and extensibility across Unity projects.