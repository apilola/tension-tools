# tension-tools
![caption](https://i.redd.it/1nbkmf9kteja1.gif)
<Details>
<Summary><b>Other Showcases<b></Summary>

![caption](./Documentation/Images/FaceShowcase.jpg)
</Details>

### Overview

The project contents contains the code nessesary to calculate tension data on inside Unity Shaders 3D. It also contains some other tools generic tools required to create the "Skin" material that you see in the showcase above. The following implementation will work with Rigged Geometry as well as BlendShapes/MorphTargets.

### Requirements
- Atleast Unity 2021.3.18f1
- This Noodle Demo in the showcase above requires the use of the Universal Render Pipeline although the package itself should work in any pipeline

### Installation
Edit your package manifest to include this package.
Your projects package manifest should be located at YOUR_PROJECT_NAME/Packages/manifest.json

    {
        "dependencies": {
            "com.ap.tension-tools": "https://github.com/apilola/tension-tools.git?path=/Packages/com.ap.tension-tools"
        }
    }




### Known Issues

- Unity may show warnings for undisposed Compute/Graphics Buffers.
    - Leak detection mode may be toggled through: Toolbar->TensionTools->LeakDetectionMode
    - On my machine, leak detection does not seem to be outputting any stack traces, feedback would be appreciated...
- Inspector preview support only functions in using the URP scriptable renderpipline. 
    - If anyone knows how to render a skinned mesh renderer with a replacement shader in a preview context that would be helpful.


### Contents

The following items are contained in the package located at: ./Packages/com.ap.tension-tools

Components:
- TensionData.cs

ShaderNodes:
- SampleTension
    - Samples the tension experienced by the mesh provided the renderer has a TensionData component.
- Subsurface Scattering
    - Calculates subsurface scattering 
- Simple Subsurface
    - Simplifies the process to calculate subsurface scattering but provides less inputs
- Simple Subsurface GI
    - Same as simple subsurface but applies global illumination
- Unpack Normal
    - Unpacks a normal map and applys a weight to its intensity.
- AO-Smoothness-Metalic
    - this node samples a texture and samples their weights.

Shaders
- Tension Visualizer
    - Used in the Inspector Preview to visualize tension activations in the editor
- Skin
    - A simple skin shader used in the preview's shader.


<Header><b>Quick Start</b></Header>

1. Add a 3D model skinned mesh renderer to the scene
1. Attach a "Tension Data" component to the skinned mesh renderer. 
1. Create a material that uses the shader "ShaderGraph/TensionVisualizer"
1. Set the skinned mesh renderer to use the newly created material.


