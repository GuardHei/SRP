# SRP

## Description
A customized forward+ render pipeline for Unity

## Planned Features
1. Supports forward+ render path (Implemented)
2. Supports tile-based light culling with transparent objects (Implemented - Both dither transparent & transparent)
3. Supports realtime directional light / spot light / point light shadows (Implemented - Both hard shadows & soft shadows)
4. Supports cascaded shadowmap for directional light (Implemented)
5. Supports volumetric lighting
6. Supports Mie-scattering skylight
7. Supoorts screen space decals
8. Supports global illumination
9. Supports stochastic screen space reflection
10. Supports MSAA / FXAA / SMAA / TAA

## Possible Features
1. Supports GPU culling
2. Supports nonphotorealistic render pipeline
3. Supports in-pipeline GPU grass
4. Supports in-pipeline post-processing stack
5. Supports groundtruth ambient occulsion

## Graphic API
1. DX11+ on Windows
2. Metal on Mac
3. OpenGL 4.5+ on Linux

PS: Graphic APIs that support Compute Shaders.

## Docs
Not ready yet

## Previews
![Still Under Development.png](https://i.loli.net/2019/10/08/IryF3zL2GwCnMxd.png)

Parameters
+ 2k resolution
+ 59 point lights
+ 22 spot lights, 2 of them have soft shadows, 1k resolution
+ 1 directional light with soft shadow, 4 cascades, 2k resolution
+ 586 fps

Configs
+ MacBook Pro (15-inch, 2017)
+ macOS Mojave 10.14.4
+ 3.1 GHz Intel Core i7
+ 16 GB 2133 MHz LPDDR3
+ Radeon Pro 560 4 GB