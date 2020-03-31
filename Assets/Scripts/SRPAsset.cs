using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Object = System.Object;

[CreateAssetMenu(menuName = "Rendering/SRPAsset")]
public sealed class SRPAsset : RenderPipelineAsset {

	public SRPipelineParams @params;

	protected override RenderPipeline CreatePipeline() => new SRPipeline(@params);
}

[Serializable]
public class SRPipelineParams {
	public bool enableDynamicBatching = true;
	public bool enableInstancing = true;
	public bool enableSRPBatching = true;
	//todo implement render scaling
	public bool enableDynamicScaling;
	public bool enableRenderScaling;
	public float renderScale = 1f;
	public int clusterGridX = 16;
	public int clusterGridY = 9;
	public int clusterGridZ = 24;
	public float alphaTestDepthCutoff = .001f;
	public ComputeShader clusterCullingComputeShader;
	public ComputeShader generalComputeShader;
	public DirectionalLightParams directionalLightParams;
	public PointLightParams pointLightParams;
	public SpotLightParams spotLightParams;
	public DitherTransparentParams ditherTransparentParams;
	public bool testMaterialOn;
	public Material testMaterial;
	public bool depthBoundOn;
	public bool gizmosOn;
	public int testInt;
}

[Serializable]
public class DirectionalLightParams {
	public bool enabled = true;
	public int maxPerFrame = 2;
	public bool shadowOn = true;
	public int shadowResolution = 1024;
	public int shadowCascades = 4;
	public Vector3 shadowCascadeSplits = new Vector3(.067f, .2f, .467f);
	public float shadowDistance = 100;
	public int maxShadowCount = 1;

}

[Serializable]
public class PointLightParams {
	public bool enabled = true;
	public int maxPerFrame = 200;
	public int maxPerCluster = 20;
	public bool shadowOn = true;
	public bool softShadow = true;
	public int shadowResolution = 1024;
	public int maxShadowCount = 10;
}

[Serializable]
public class SpotLightParams {
	public bool enabled = true;
	public int maxPerFrame = 100;
	public int maxPerCluster = 20;
	public bool shadowOn = true;
	public bool softShadow = true;
	public int shadowResolution = 1024;
	public int maxShadowCount = 10;
}

[Serializable]
public class DitherTransparentParams {
	public bool blurOn = true;
	public int downSamples = 2;
	public int iteration = 1;
	public float blurRadius;
	public Material blurMaterial;
}

[Serializable]
public struct Cone {
	public float4 vertex; // rgb - vertex, a - angle
	public float4 direction; // rgb - direction, a - height
	public float radius;

	public Cone(float3 vertex, float angle, float height, float3 direction) {
		this.vertex = new float4(vertex, angle);
		this.direction = new float4(direction, height);
		radius = Mathf.Tan(angle) * height;
	}
}

[Serializable]
public struct DirectionalLight {
	public float4 direction;
	public float4 color; // rgb - final color, a - shadow strength
	public uint shadowIndex;
}

[Serializable]
public struct PointLight {
	public float4 sphere;
	public float4 color; // rgb - final color, a - shadow strength
	public uint shadowIndex;
}

[Serializable]
public struct SpotLight {
	public Cone cone;
	public float4 color; // rgb - final color, a - shadow strength
	public uint shadowIndex;
}

public sealed unsafe class SRPipeline : RenderPipeline {

	public static SRPipeline current {
		get;
		private set;
	}

	public static bool usesReversedZBuffer {
		get;
		private set;
	}
	
	public static readonly RenderTargetIdentifier ColorBufferId = new RenderTargetIdentifier(ShaderManager.COLOR_BUFFER);
	public static readonly RenderTargetIdentifier TemporaryTexture1Id = new RenderTargetIdentifier(ShaderManager.TEMPORARY_TEXTURE_1);
	public static readonly RenderTargetIdentifier TemporaryTexture2Id = new RenderTargetIdentifier(ShaderManager.TEMPORARY_TEXTURE_2);
	public static readonly RenderTargetIdentifier BlitTemporaryTexture1Id = new RenderTargetIdentifier(ShaderManager.BLIT_TEMPORARY_TEXTURE_1);
	public static readonly RenderTargetIdentifier DepthId = new RenderTargetIdentifier(ShaderManager.DEPTH_TEXTURE);
	public static readonly RenderTargetIdentifier OpaqueDepthId = new RenderTargetIdentifier(ShaderManager.OPAQUE_DEPTH_TEXTURE);
	public static readonly RenderTargetIdentifier OpaqueNormalId = new RenderTargetIdentifier(ShaderManager.OPAQUE_NORMAL_TEXTURE);
	public static readonly RenderTargetIdentifier SunlightShadowmapId = new RenderTargetIdentifier(ShaderManager.SUNLIGHT_SHADOWMAP);
	public static readonly RenderTargetIdentifier SunlightShadowmapArrayId = new RenderTargetIdentifier(ShaderManager.SUNLIGHT_SHADOWMAP_ARRAY);
	public static readonly RenderTargetIdentifier PointLightShadowmapId = new RenderTargetIdentifier(ShaderManager.POINT_LIGHT_SHADOWMAP);
	public static readonly RenderTargetIdentifier PointLightShadowmapArrayId = new RenderTargetIdentifier(ShaderManager.POINT_LIGHT_SHADOWMAP_ARRAY);
	public static readonly RenderTargetIdentifier SpotLightShadowmapArrayId = new RenderTargetIdentifier(ShaderManager.SPOT_LIGHT_SHADOWMAP_ARRAY);
	public static readonly RenderTargetIdentifier DepthBoundId = new RenderTargetIdentifier(ShaderManager.DEPTH_BOUND_TEXTURE);
	public static readonly RenderTargetIdentifier DepthMaskId = new RenderTargetIdentifier(ShaderManager.DEPTH_MASK_TEXTURE);
	public static readonly RenderTargetIdentifier DepthFrustumId = new RenderTargetIdentifier(ShaderManager.DEPTH_FRUSTUM_TEXTURE);
	public static readonly RenderTargetIdentifier CulledPointLightId = new RenderTargetIdentifier(ShaderManager.CULLED_POINT_LIGHT_TEXTURE);
	public static readonly RenderTargetIdentifier CulledSpotLightId = new RenderTargetIdentifier(ShaderManager.CULLED_SPOT_LIGHT_TEXTURE);

	public SRPipelineParams @params;
	
	private ScriptableRenderContext _context;
	private CommandBuffer _mainBuffer = new CommandBuffer { name = "Main Buffer" };
	private CommandBuffer _computeBuffer = new CommandBuffer { name = "Compute Buffer" };
	private Camera _camera;
	private DirectionalLight[] _dirLights;
	private PointLight[] _pointLights;
	private SpotLight[] _spotLights;
	private int[] _shadowDirLights;
	private int[] _shadowPointLights;
	private int[] _shadowSpotLights;
	private ComputeBuffer _dirLightBuffer;
	private ComputeBuffer _pointLightBuffer;
	private ComputeBuffer _spotLightBuffer;
	private ComputeBuffer _pointLightIndexBuffer;
	private ComputeBuffer _spotLightIndexBuffer;
	private ComputeBuffer _dirLightInverseVPBuffer;
	private ComputeBuffer _spotLightInverseVPBuffer;
	private int _pixelWidth;
	private int _pixelHeight;

	public SRPipeline(SRPipelineParams @params) {
		this.@params = @params;
		Init();
	}

	private void Init() {
		current = this;

		GraphicsSettings.lightsUseLinearIntensity = true;
		usesReversedZBuffer = SystemInfo.usesReversedZBuffer;

		foreach (var camera in Camera.allCameras) camera.forceIntoRenderTexture = true;
		
		_dirLights = new DirectionalLight[@params.directionalLightParams.maxPerFrame];
		_pointLights = new PointLight[@params.pointLightParams.maxPerFrame];
		_spotLights = new SpotLight[@params.spotLightParams.maxPerFrame];
		_shadowDirLights = new int[@params.directionalLightParams.maxShadowCount];
		_shadowPointLights = new int[@params.pointLightParams.maxShadowCount];
		_shadowSpotLights = new int[@params.spotLightParams.maxShadowCount];
		
		GenerateComputeBuffers();
	}

	protected override void Render(ScriptableRenderContext context, Camera[] cameras) {

		_context = context;
		GraphicsSettings.useScriptableRenderPipelineBatching = @params.enableSRPBatching;

		foreach (var camera in cameras) {
			_camera = camera;
			RenderCurrentCamera();
		}
	}

	private void RenderCurrentCamera() {
		
		_pixelWidth = _camera.pixelWidth;
		_pixelHeight = _camera.pixelHeight;
		
		var clearFlags = _camera.clearFlags;
		
		_mainBuffer.ClearRenderTarget((clearFlags & CameraClearFlags.Depth) != 0, (clearFlags & CameraClearFlags.Color) != 0, _camera.backgroundColor);
		
		_context.SetupCameraProperties(_camera);
		
		var farClipPlane = _camera.farClipPlane;
		var nearClipPlane = _camera.nearClipPlane;
		var clipDistance = farClipPlane - nearClipPlane;
		
		var zBufferParams = new Vector4(clipDistance / nearClipPlane, 1, clipDistance / (farClipPlane * nearClipPlane), 1 / farClipPlane);

		_mainBuffer.SetGlobalFloat(ShaderManager.ALPHA_TEST_DEPTH_CUTOFF, @params.alphaTestDepthCutoff);
		_mainBuffer.SetGlobalVector(ShaderManager.Z_BUFFER_PARAMS, zBufferParams);
		
		ExecuteMainBuffer();
		
#if UNITY_EDITOR
		if (_camera.cameraType == CameraType.SceneView) ScriptableRenderContext.EmitWorldGeometryForSceneView(_camera);
#endif
		
		// todo add gpu culling (cs based AABB/OBB tests)
		if (!_camera.TryGetCullingParameters(out var cullingParameters)) return;
		var cull = _context.Cull(ref cullingParameters);
		
		GenerateRTs();

		var dirLightCount = 0u;
		var pointLightCount = 0u;
		var spotLightCount = 0u;
		var shadowDirLightCount = 0u;
		var shadowPointLightCount = 0u;
		var shadowSpotLightCount = 0u;

		var dirLightMax = @params.directionalLightParams.enabled ? @params.directionalLightParams.maxPerFrame : 0;
		var pointLightMax = @params.pointLightParams.enabled ? @params.pointLightParams.maxPerFrame : 0;
		var spotLightMax = @params.spotLightParams.enabled ? @params.spotLightParams.maxPerFrame : 0;
		var shadowDirLightMax = @params.directionalLightParams.enabled ? @params.directionalLightParams.maxShadowCount : 0;
		var shadowPointLightMax = @params.pointLightParams.enabled ? @params.pointLightParams.maxShadowCount : 0;
		var shadowSpotLightMax = @params.spotLightParams.enabled ? @params.spotLightParams.maxShadowCount : 0;

		if (_dirLights.Length < dirLightMax) _dirLights = new DirectionalLight[dirLightMax];
		if (_pointLights.Length < pointLightMax) _pointLights = new PointLight[pointLightMax];
		if (_spotLights.Length < spotLightMax) _spotLights = new SpotLight[spotLightMax];
		if (_shadowDirLights.Length < shadowDirLightMax) _shadowDirLights = new int[shadowDirLightMax];
		if (_shadowPointLights.Length < shadowPointLightMax) _shadowPointLights = new int[shadowPointLightMax];
		if (_shadowSpotLights.Length < shadowSpotLightMax) _shadowSpotLights = new int[shadowSpotLightMax];

		var visibleLights = cull.visibleLights;

		for (int i = 0; i < visibleLights.Length; i++) {
			var visibleLight = visibleLights[i];
			var originalLight = visibleLight.light;
			switch (visibleLight.lightType) {
				case LightType.Directional:
					if (dirLightCount >= dirLightMax) continue;
					DirectionalLight dirLight = new DirectionalLight {
						direction = new float4(visibleLight.localToWorldMatrix.GetDirectionFromLocalTransform()),
						color = new float4(visibleLight.finalColor.ToFloat3(), originalLight.shadowStrength)
					};

					if (originalLight.shadows != LightShadows.None && shadowDirLightCount < shadowDirLightMax) {
						_shadowDirLights[shadowDirLightCount] = i;
						shadowDirLightCount++;
						dirLight.shadowIndex = shadowDirLightCount;
					} else dirLight.shadowIndex = 0;
					
					_dirLights[dirLightCount] = dirLight;
					dirLightCount++;
					break;
				case LightType.Point:
					if (pointLightCount >= pointLightMax) continue;
					PointLight pointLight = new PointLight {
						sphere = new float4(originalLight.transform.position, originalLight.range),
						color = new float4(visibleLight.finalColor.ToFloat3(), originalLight.shadowStrength)
					};

					if (originalLight.shadows != LightShadows.None && shadowPointLightCount < shadowPointLightMax) {
						_shadowPointLights[shadowPointLightCount] = i;
						shadowPointLightCount++;
						pointLight.shadowIndex = shadowPointLightCount;
					} else pointLight.shadowIndex = 0;
					
					_pointLights[pointLightCount] = pointLight;
					pointLightCount++;
					break;
				case LightType.Spot:
					if (spotLightCount >= spotLightMax) continue;
					SpotLight spotLight = new SpotLight {
						cone = new Cone(visibleLight.localToWorldMatrix.GetPositionFromLocalTransform(), Mathf.Deg2Rad * visibleLight.spotAngle * .5f, visibleLight.range, visibleLight.localToWorldMatrix.GetDirectionFromLocalTransform().ToFloat3()),
						color = new float4(visibleLight.finalColor.ToFloat3(), originalLight.shadowStrength)
					};

					if (originalLight.shadows != LightShadows.None && shadowSpotLightCount < shadowSpotLightMax) {
						_shadowSpotLights[shadowSpotLightCount] = i;
						shadowSpotLightCount++;
						spotLight.shadowIndex = shadowSpotLightCount;
					} else spotLight.shadowIndex = 0;
					
					_spotLights[spotLightCount] = spotLight;
					spotLightCount++;
					break;
			}
		}
		
		// Cluster Cull
		var clusterCullFence = ClusterCull();
		
		// Depth Prepass
		var opaqueSortSettings = new SortingSettings(_camera) { criteria = SortingCriteria.QuantizedFrontToBack | SortingCriteria.OptimizeStateChanges };
		var depthDrawSettings = new DrawingSettings(ShaderTagManager.DEPTH, opaqueSortSettings) {
			enableDynamicBatching = @params.enableDynamicBatching,
			enableInstancing = @params.enableInstancing
		};
		
		var filterSettings = FilteringSettings.defaultValue;
		filterSettings.layerMask = _camera.cullingMask;
		filterSettings.renderQueueRange = ShaderManager.OPAQUE_RENDER_QUEUE_RANGE;
		
		ResetRenderTarget(ColorBufferId, DepthId, false, true, 1, Color.black);
		
		ExecuteMainBuffer();
		
		_context.DrawRenderers(cull, ref depthDrawSettings, ref filterSettings);
		
		// Stencil Prepass
		var stencilDrawSettings = new DrawingSettings(ShaderTagManager.STENCIL, opaqueSortSettings) {
			enableDynamicBatching = @params.enableDynamicBatching,
			enableInstancing = @params.enableInstancing
		};
		
		filterSettings.renderQueueRange = RenderQueueRange.all;
		
		ResetRenderTarget(ColorBufferId, DepthId, false, false, 1, Color.black);
		
		ExecuteMainBuffer();

		_context.DrawRenderers(cull, ref stencilDrawSettings, ref filterSettings);

		// Editor Pass
#if UNITY_EDITOR
		if (@params.gizmosOn) _context.DrawGizmos(_camera, GizmoSubset.PostImageEffects);
#endif

		ReleaseRTs();
		
		ExecuteMainBuffer();
		
		_context.Submit();
	}

	private GraphicsFence ClusterCull() {
		_computeBuffer.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);
		var fence = _computeBuffer.CreateAsyncGraphicsFence();
		ExecuteComputeBufferAsync(ComputeQueueType.Default);
		return fence;
	}

	private void DrawShadows() {
		
	}
	
	private void ExecuteMainBuffer() {
		_context.ExecuteCommandBuffer(_mainBuffer);
		_mainBuffer.Clear();
	}
	
	private void ExecuteComputeBuffer() {
		_context.ExecuteCommandBuffer(_computeBuffer);
		_computeBuffer.Clear();
	}

	private void ExecuteCommandBuffer(CommandBuffer cmd) {
		_context.ExecuteCommandBuffer(cmd);
		cmd.Clear();
	}

	private void ExecuteMainBufferAsync(ComputeQueueType queueType) {
		_context.ExecuteCommandBufferAsync(_mainBuffer, queueType);
		_mainBuffer.Clear();
	}
	
	private void ExecuteComputeBufferAsync(ComputeQueueType queueType) {
		_context.ExecuteCommandBufferAsync(_computeBuffer, queueType);
		_computeBuffer.Clear();
	}
	
	private void ExecuteCommandBufferAsync(CommandBuffer cmd, ComputeQueueType queueType) {
		_context.ExecuteCommandBufferAsync(cmd, queueType);
		cmd.Clear();
	}
	
	private void ResetRenderTarget(RenderTargetIdentifier colorBuffer, bool clearDepth, bool clearColor, float depth, Color color) {
		_mainBuffer.SetRenderTarget(colorBuffer);
		_mainBuffer.ClearRenderTarget(clearDepth, clearColor, color, depth);
	}

	private void ResetRenderTarget(RenderTargetIdentifier colorBuffer, RenderTargetIdentifier depthBuffer, bool clearDepth, bool clearColor, float depth, Color color) {
		_mainBuffer.SetRenderTarget(colorBuffer, depthBuffer);
		_mainBuffer.ClearRenderTarget(clearDepth, clearColor, color, depth);
	}

	private void ResetRenderTarget(RenderTargetIdentifier colorBuffer, CubemapFace cubemapFace, int depthSlice, bool clearDepth, bool clearColor, float depth, Color color) {
		_mainBuffer.SetRenderTarget(colorBuffer, 0, cubemapFace, depthSlice);
		_mainBuffer.ClearRenderTarget(clearDepth, clearColor, color, depth);
	}
	
	private void GenerateComputeBuffers() {
		_dirLightBuffer = new ComputeBuffer(@params.directionalLightParams.maxPerFrame, sizeof(DirectionalLight));
		_pointLightBuffer = new ComputeBuffer(@params.pointLightParams.maxPerFrame, sizeof(PointLight));
		_spotLightBuffer = new ComputeBuffer(@params.spotLightParams.maxPerFrame, sizeof(SpotLight));
		
		_dirLightInverseVPBuffer = new ComputeBuffer(@params.directionalLightParams.maxShadowCount, sizeof(Matrix4x4));
		_spotLightInverseVPBuffer = new ComputeBuffer(@params.spotLightParams.maxShadowCount, sizeof(Matrix4x4));
	}

	private void DisposeComputeBuffers() {
		_dirLightBuffer.Dispose();
		_pointLightBuffer.Dispose();
		_spotLightBuffer.Dispose();
		_dirLightInverseVPBuffer.Dispose();
		_spotLightInverseVPBuffer.Dispose();
	}

	private void GenerateRTs() {
		int width = _pixelWidth;
		int height = _pixelHeight;
		
		_mainBuffer.GetTemporaryRT(ShaderManager.COLOR_BUFFER, width, height, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR, RenderTextureReadWrite.Default);
		_mainBuffer.GetTemporaryRT(ShaderManager.DEPTH_TEXTURE, width, height, 32, FilterMode.Bilinear, RenderTextureFormat.Depth, RenderTextureReadWrite.Linear);
		_mainBuffer.GetTemporaryRT(ShaderManager.OPAQUE_NORMAL_TEXTURE, width, height, 0, FilterMode.Bilinear, GraphicsFormat.R16G16_SFloat);
	}

	private void ReleaseRTs() {
		_mainBuffer.ReleaseTemporaryRT(ShaderManager.COLOR_BUFFER);
		_mainBuffer.ReleaseTemporaryRT(ShaderManager.DEPTH_TEXTURE);
	}
	
	protected override void Dispose(bool disposing) {
		base.Dispose(disposing);
		if (!disposing) return;
		DisposeComputeBuffers();
	}
}
