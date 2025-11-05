# Unity MCP Tool Capabilities

## GameObject Management

### unity_create_gameobject
Create new GameObjects with optional parent hierarchy.

### unity_delete_gameobject
Delete GameObjects by name.

### unity_set_property
Set GameObject properties:
- **position**: Set world position (x,y,z)
- **rotation**: Set euler angles (x,y,z)
- **scale**: Set local scale (x,y,z)
- **active**: Enable/disable GameObject (true/false)
- **name**: Rename GameObject
- **layer**: Set layer (Default, UI, etc.)
- **tag**: Set tag (Player, MainCamera, etc.)

## Component Management

### unity_add_component
Add components to GameObjects. Supports 70+ component types:

**UI Components:**
- Canvas, RectTransform, Image, RawImage, Text, Button, Toggle, Slider, Scrollbar, Dropdown, InputField, ScrollRect, Mask
- CanvasScaler, GraphicRaycaster, CanvasRenderer, CanvasGroup
- LayoutElement, HorizontalLayoutGroup, VerticalLayoutGroup, GridLayoutGroup
- ContentSizeFitter, AspectRatioFitter

**Event System:**
- EventSystem, StandaloneInputModule, EventTrigger

**Rendering:**
- Camera, Light, MeshRenderer, SkinnedMeshRenderer, SpriteRenderer, LineRenderer, TrailRenderer
- ParticleSystem, MeshFilter

**Physics 3D:**
- Rigidbody, BoxCollider, SphereCollider, CapsuleCollider, MeshCollider
- CharacterController, FixedJoint, HingeJoint, SpringJoint

**Physics 2D:**
- Rigidbody2D, BoxCollider2D, CircleCollider2D, PolygonCollider2D, EdgeCollider2D, CapsuleCollider2D

**Audio:**
- AudioSource, AudioListener, AudioReverbZone

**Animation:**
- Animator, Animation

**Misc:**
- Transform, Terrain, TerrainCollider, WindZone

### unity_remove_component
Remove components from GameObjects (same types as add_component).

### unity_set_component_property
Set component-specific properties:

**Canvas:**
- rendermode: ScreenSpaceOverlay, ScreenSpaceCamera, WorldSpace

**RectTransform:**
- anchormin: (x,y) anchor minimum
- anchormax: (x,y) anchor maximum
- sizedelta: (width,height) size
- anchoredposition: (x,y) position

**CanvasScaler:**
- uiscalemode: ConstantPixelSize, ScaleWithScreenSize, ConstantPhysicalSize
- referenceresolution: (width,height) e.g., 1920,1080
- screenmatchmode: MatchWidthOrHeight, Expand, Shrink
- matchwidthorheight: 0-1 value

**Image:**
- color: (r,g,b) or (r,g,b,a) in 0-1 range
- raycasttarget: true/false

**Text:**
- text: string content
- fontsize: integer size
- color: (r,g,b) or (r,g,b,a)
- alignment: UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight

**Camera:**
- fieldofview/fov: float value
- orthographic: true/false
- orthographicsize: float value
- depth: float value
- backgroundcolor: (r,g,b) or (r,g,b,a)

**Light:**
- color: (r,g,b) or (r,g,b,a)
- intensity: float value
- range: float value

**SpriteRenderer:**
- color: (r,g,b) or (r,g,b,a)
- flipx: true/false
- flipy: true/false
- sortingorder: integer value

**Rigidbody:**
- mass: float value
- drag: float value
- angulardrag: float value
- usegravity: true/false
- iskinematic: true/false

**Rigidbody2D:**
- mass: float value
- drag: float value
- angulardrag: float value
- gravityscale: float value
- iskinematic: true/false

**AudioSource:**
- volume: 0-1 float value
- pitch: float value
- loop: true/false
- playonawake: true/false

**Collider:**
- istrigger: true/false

**CanvasGroup:**
- alpha: 0-1 float value
- interactable: true/false
- blocksraycasts: true/false

## Scene Management

### unity_get_scene_hierarchy
Get complete scene hierarchy with all GameObjects, components, and properties.

### unity_get_scene_info
Get detailed scene information including cameras and lighting.

### unity_inspect_scene
Inspect a specific scene file.

### unity_list_gameobjects
List all GameObjects in a scene with simplified view.

## Project Management

### unity_get_project_overview
Get comprehensive project structure and asset counts.

### unity_search_assets
Search for assets by name or type (script, scene, prefab, material, texture, audio).

### unity_read_script
Read C# script content.

### unity_create_script
Create new C# MonoBehaviour scripts with auto-refresh.

## Screenshot & Visualization

### unity_capture_screenshot
Capture Scene View, Game View, or entire Unity Editor.

### unity_capture_camera_view
Capture from specific camera's perspective with custom resolution.

### unity_cleanup_screenshots
Clean up old screenshots to save disk space.

## Utilities

### unity_force_refresh
Force Unity to refresh and recompile immediately.

## Usage Examples

```python
# Create UI setup
await unity_create_gameobject(name="Canvas")
await unity_add_component(object_name="Canvas", component_type="Canvas")
await unity_add_component(object_name="Canvas", component_type="CanvasScaler")
await unity_add_component(object_name="Canvas", component_type="GraphicRaycaster")

# Configure for Full HD
await unity_set_component_property(
    object_name="Canvas",
    component_type="CanvasScaler",
    property="uiscalemode",
    value="scalewithscreensize"
)
await unity_set_component_property(
    object_name="Canvas",
    component_type="CanvasScaler",
    property="referenceresolution",
    value="1920,1080"
)

# Create button
await unity_create_gameobject(name="Button", parent="Canvas")
await unity_add_component(object_name="Button", component_type="Image")
await unity_add_component(object_name="Button", component_type="Button")
await unity_set_property(object_name="Button", property="layer", value="UI")

# Set button color
await unity_set_component_property(
    object_name="Button",
    component_type="Image",
    property="color",
    value="0.2,0.8,0.2,1"
)
```
