# Gaussian Splat Portal Effect Guide

This guide explains how to add a portal-like fade effect to your Gaussian Splat objects, where the edges fade to transparent even as the object moves through the scene.

## What Was Modified

### 1. Shader Changes
**File:** `c:\Users\unico\Documents\Projects\Unity\UnityGaussianSplatting\package\Shaders\RenderGaussianSplats.shader`

The shader now includes:
- **Properties** for controlling the portal fade:
  - `_PortalFadeEnabled` - Toggle the effect on/off
  - `_PortalCenter` - Center point in object space
  - `_PortalInnerRadius` - Inner radius (no fade)
  - `_PortalOuterRadius` - Outer radius (fully transparent)
  - `_PortalFalloff` - Fade curve sharpness

- **Vertex Shader** modifications:
  - Loads each splat's position in object space
  - Transforms to world space for distance calculations
  - Passes world position to fragment shader

- **Fragment Shader** modifications:
  - Calculates distance from portal center in world space
  - Applies smooth fade between inner and outer radius
  - Uses power function for customizable falloff curve

### 2. Controller Script
**File:** `Assets/Scripts/GaussianSplatPortalController.cs`

A Unity MonoBehaviour that provides:
- Easy-to-use inspector controls for all portal parameters
- Real-time parameter updates
- Visual gizmos in the Scene view showing fade radii
- Validation to ensure outer radius > inner radius

## How to Use

### Step 1: Add the Controller Script

1. Select your Gaussian Splat GameObject in the hierarchy
2. In the Inspector, click "Add Component"
3. Search for and add `GaussianSplatPortalController`

### Step 2: Configure Portal Settings

In the Inspector, you'll see these parameters:

- **Enable Portal Fade**: Check this to enable the effect
- **Portal Center**: The center point of the portal in object-space coordinates (usually `0, 0, 0`)
- **Inner Radius**: Distance from center where fade begins (default: 0.5)
- **Outer Radius**: Distance from center where splats become fully transparent (default: 1.0)
- **Falloff**: Controls how sharp the fade transition is (default: 2.0)
  - Lower values (0.1-1.0): Gradual, soft fade
  - Higher values (2.0-5.0): Sharp, dramatic fade

### Step 3: Adjust in Real-Time

- The effect updates in real-time as you adjust parameters
- In the Scene view, select the object to see visual gizmos:
  - **Green sphere**: Inner radius (no fade)
  - **Red sphere**: Outer radius (fully transparent)
  - **Yellow point**: Portal center
  - **Yellow circles**: Intermediate fade levels

### Step 4: Test with Movement

The portal effect is calculated in **world space**, so:
- Move your Gaussian Splat object around the scene
- Rotate it
- Scale it
- The fade effect will move with the object correctly!

## Technical Details

### How It Works

1. **Object Space → World Space**: Each splat's position is loaded in object space and transformed to world space in the vertex shader
2. **Distance Calculation**: The fragment shader calculates the world-space distance between each splat and the portal center
3. **Fade Application**: A smooth fade is applied based on the distance:
   ```
   if distance < innerRadius:
       fade = 1.0 (fully opaque)
   else if distance < outerRadius:
       fade = smooth interpolation using power curve
   else:
       fade = 0.0 (fully transparent)
   ```

### Performance Considerations

- The portal fade adds minimal performance overhead:
  - One vector distance calculation per fragment
  - One conditional branch
  - One power operation
- The effect only runs when `_PortalFadeEnabled` is set to 1.0

### Shader Properties

The shader properties are set as **global shader properties** via `Shader.SetGlobalX()`. This means:
- All Gaussian Splat objects in the scene will use the same portal settings
- If you need different settings per object, you'll need to modify the approach to use MaterialPropertyBlocks or per-material properties

## Troubleshooting

### Issue: Effect not visible
**Solutions:**
- Ensure `Enable Portal Fade` is checked
- Check that `Outer Radius` is greater than `Inner Radius`
- Verify the Gaussian Splat shader has been updated
- Make sure the splat object has geometry within the portal radii

### Issue: Effect doesn't move with object
**Solutions:**
- This should work automatically. If not, ensure:
  - The shader was saved after modifications
  - Unity has recompiled the shader (check console for errors)

### Issue: Sharp edges instead of smooth fade
**Solutions:**
- Increase the distance between `Inner Radius` and `Outer Radius`
- Decrease the `Falloff` value for a more gradual transition

### Issue: Effect too subtle
**Solutions:**
- Increase the `Falloff` value
- Move the `Inner Radius` closer to the center
- Expand the `Outer Radius`

## Examples

### Tight Portal Effect
```
Enable Portal Fade: ✓
Portal Center: (0, 0, 0)
Inner Radius: 0.2
Outer Radius: 0.5
Falloff: 3.0
```
Result: A sharp, tight portal with quick fade

### Soft Vignette Effect
```
Enable Portal Fade: ✓
Portal Center: (0, 0, 0)
Inner Radius: 0.8
Outer Radius: 2.0
Falloff: 1.0
```
Result: Gentle, wide fade around edges

### Offset Portal
```
Enable Portal Fade: ✓
Portal Center: (0.5, 0, 0)
Inner Radius: 0.3
Outer Radius: 1.0
Falloff: 2.0
```
Result: Portal centered off to one side

## Advanced Customization

### Animating the Portal

You can animate the portal effect by:
1. Creating an animation curve in your own script
2. Modifying the `GaussianSplatPortalController` properties from code:
```csharp
var portalController = GetComponent<GaussianSplatPortalController>();
portalController.innerRadius = Mathf.Sin(Time.time) * 0.5f + 0.5f;
```

### Per-Object Portal Settings

To have different portal settings for multiple Gaussian Splats:
1. Modify the shader to use per-material properties instead of global properties
2. Update `GaussianSplatPortalController` to set properties per material
3. Access the material via the GaussianSplatRenderer component

## Files Modified

- `c:\Users\unico\Documents\Projects\Unity\UnityGaussianSplatting\package\Shaders\RenderGaussianSplats.shader`
- `Assets/Scripts/GaussianSplatPortalController.cs` (new file)

## Backup Note

⚠️ **Important**: The shader file is in the UnityGaussianSplatting package directory. If you update the package, these changes may be overwritten. Consider:
- Keeping a backup of the modified shader
- Creating a custom shader variant
- Documenting the changes for future reference

