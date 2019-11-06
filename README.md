# SRP

## Description
A customized forward+ render pipeline for Unity

## Planned Features
1. Supports forward+ render path (Implemented)
2. Supports tile-based light culling with transparent objects (Implemented)
3. Supports realtime directional light (Implemented) \ spot light (Implemented) \ point light shadows
4. Supports cascaded shadowmap for directional light (Implemented)
5. Supports volumetric lighting

## Possible Features
1. Built-in nonphotorealistic render pipeline
2. Built-in GPU grass
3. Built-in post-process stack

## Graphic API
1. DX11+ on Windows
2. Metal on Mac
3. OpenGL 4.5+ on Linux

PS: Graphic APIs that support Compute Shaders. Geometry shader is not necessary.

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