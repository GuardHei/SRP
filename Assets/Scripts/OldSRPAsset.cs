using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using RenderPipeline = UnityEngine.Rendering.RenderPipeline;

[CreateAssetMenu(menuName = "Rendering/OldSRPAsset")]
public sealed class OldSRPAsset : RenderPipelineAsset {

	public OldSRPipelineParams @params;

	protected override RenderPipeline CreatePipeline() => new OldSRPipeline(@params);
}

[Serializable]
public class OldSRPipelineParams {
	public bool enableDynamicBatching = true;
	public bool enableInstancing = true;
	public bool enableSRPBatching = true;
	//todo implement render scaling
	public bool enableDynamicScaling;
	public bool enableRenderScaling;
	public float renderScale = 1f;
	public int depthTileResolution = 16;
	public float alphaTestDepthCutoff = .001f;
	public ComputeShader tbrComputeShader;
	public ComputeShader generalComputeShader;
	public SunlightParams sunlightParams;
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
public class SunlightParams {
	public bool shadowOn = true;
	public int shadowResolution = 1024;
	public int shadowCascades = 4;
	public Vector3 shadowCascadeSplits = new Vector3(.067f, .2f, .467f);
	public float shadowDistance = 100;
	
}

// Kill extra cost of virtual functions using "sealed" keyword
public sealed unsafe class OldSRPipeline : RenderPipeline {

	public const int MAX_LIGHT_PER_TILE = 16;
	public const int MAX_DECAL_PER_TILE = 4;

	public static OldSRPipeline current {
		get;
		private set;
	}

	public OldSRPipelineParams @params;
	public Light sunlight;

	private static readonly RenderTargetIdentifier ColorBufferId = new RenderTargetIdentifier(ShaderManager.COLOR_BUFFER);
	private static readonly RenderTargetIdentifier TemporaryTexture1Id = new RenderTargetIdentifier(ShaderManager.TEMPORARY_TEXTURE_1);
	private static readonly RenderTargetIdentifier TemporaryTexture2Id = new RenderTargetIdentifier(ShaderManager.TEMPORARY_TEXTURE_2);
	private static readonly RenderTargetIdentifier BlitTemporaryTexture1Id = new RenderTargetIdentifier(ShaderManager.BLIT_TEMPORARY_TEXTURE_1);
	private static readonly RenderTargetIdentifier DepthId = new RenderTargetIdentifier(ShaderManager.DEPTH_TEXTURE);
	private static readonly RenderTargetIdentifier OpaqueDepthId = new RenderTargetIdentifier(ShaderManager.OPAQUE_DEPTH_TEXTURE);
	private static readonly RenderTargetIdentifier OpaqueNormalId = new RenderTargetIdentifier(ShaderManager.OPAQUE_NORMAL_TEXTURE);
	private static readonly RenderTargetIdentifier SunlightShadowmapId = new RenderTargetIdentifier(ShaderManager.SUNLIGHT_SHADOWMAP);
	private static readonly RenderTargetIdentifier SunlightShadowmapArrayId = new RenderTargetIdentifier(ShaderManager.SUNLIGHT_SHADOWMAP_ARRAY);
	private static readonly RenderTargetIdentifier PointLightShadowmapId = new RenderTargetIdentifier(ShaderManager.POINT_LIGHT_SHADOWMAP);
	private static readonly RenderTargetIdentifier PointLightShadowmapArrayId = new RenderTargetIdentifier(ShaderManager.POINT_LIGHT_SHADOWMAP_ARRAY);
	private static readonly RenderTargetIdentifier SpotLightShadowmapArrayId = new RenderTargetIdentifier(ShaderManager.SPOT_LIGHT_SHADOWMAP_ARRAY);
	private static readonly RenderTargetIdentifier DepthBoundId = new RenderTargetIdentifier(ShaderManager.DEPTH_BOUND_TEXTURE);
	private static readonly RenderTargetIdentifier DepthMaskId = new RenderTargetIdentifier(ShaderManager.DEPTH_MASK_TEXTURE);
	private static readonly RenderTargetIdentifier DepthFrustumId = new RenderTargetIdentifier(ShaderManager.DEPTH_FRUSTUM_TEXTURE);
	private static readonly RenderTargetIdentifier CulledPointLightId = new RenderTargetIdentifier(ShaderManager.CULLED_POINT_LIGHT_TEXTURE);
	private static readonly RenderTargetIdentifier CulledSpotLightId = new RenderTargetIdentifier(ShaderManager.CULLED_SPOT_LIGHT_TEXTURE);

	private ScriptableRenderContext _context;
	private PointLight[] _pointLights;
	private SpotLight[] _spotLights;
	private ComputeBuffer _pointLightBuffer;
	private ComputeBuffer _spotLightBuffer;
	private ComputeBuffer _pointLightInverseVPBuffer;
	private ComputeBuffer _spotLightInverseVPBuffer;

	private readonly CommandBuffer _currentBuffer = new CommandBuffer { name = "Render Camera" };
	
	private bool _reversedZBuffer;

	public OldSRPipeline(OldSRPipelineParams @params) {
		this.@params = @params;
		Init();
	}

	private void Init() {
		current = this;

		GraphicsSettings.lightsUseLinearIntensity = true;
		_reversedZBuffer = SystemInfo.usesReversedZBuffer;
		
		_pointLights = new PointLight[@params.pointLightParams.maxPerFrame];
		_spotLights = new SpotLight[@params.spotLightParams.maxPerFrame];

		foreach (var camera in Camera.allCameras) camera.forceIntoRenderTexture = true;

		foreach (var light in Object.FindObjectsOfType<Light>()) {
			if (light.type != LightType.Directional) continue;
			sunlight = light;
			break;
		}
		
		GenerateComputeBuffers();
	}
	
	protected override void Render(ScriptableRenderContext context, Camera[] cameras) {
		
		GraphicsSettings.useScriptableRenderPipelineBatching = @params.enableSRPBatching;

		_context = context;

		foreach (var camera in cameras) RenderScene(camera);
	}

	private void RenderDirectionalShadow(CullingResults cull, int sunlightIndex, Light sunlight) {
		ResetRenderTarget(SunlightShadowmapId, true, false, 1, Color.black);
		
		var shadowSettings = new ShadowDrawingSettings(cull, sunlightIndex);
		if (!cull.ComputeDirectionalShadowMatricesAndCullingPrimitives(sunlightIndex, 0, 1, new Vector3(1, 0, 0), @params.sunlightParams.shadowResolution, sunlight.shadowNearPlane, out var viewMatrix, out var projectionMatrix, out var splitData)) {
			ExecuteCurrentBuffer();
			return;
		}
		
		shadowSettings.splitData = splitData;
		
		_currentBuffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
		
		if (_reversedZBuffer) {
			projectionMatrix.m20 = -projectionMatrix.m20;
			projectionMatrix.m21 = -projectionMatrix.m21;
			projectionMatrix.m22 = -projectionMatrix.m22;
			projectionMatrix.m23 = -projectionMatrix.m23;
		}
		
		var scaleOffset = Matrix4x4.identity;
		scaleOffset.m00 = scaleOffset.m11 = scaleOffset.m22 = scaleOffset.m03 = scaleOffset.m13 = scaleOffset.m23 = 0.5f;
		
		var sunlightInverseVP = scaleOffset * (projectionMatrix * viewMatrix);
		
		_currentBuffer.SetGlobalMatrix(ShaderManager.SUNLIGHT_INVERSE_VP, sunlightInverseVP);
		
		ExecuteCurrentBuffer();
		
		_context.DrawShadows(ref shadowSettings);
	}

	private void RenderCascadedDirectionalShadow(CullingResults cull, int sunlightIndex, Light sunlight, float shadowDistance) {
		_currentBuffer.GetTemporaryRTArray(ShaderManager.SUNLIGHT_SHADOWMAP_ARRAY, @params.sunlightParams.shadowResolution, @params.sunlightParams.shadowResolution, @params.sunlightParams.shadowCascades, 16, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
		
		var shadowCascades = @params.sunlightParams.shadowCascades;
		
		var shadowSettings = new ShadowDrawingSettings(cull, sunlightIndex);
		var sunlightInverseVPArray = new Matrix4x4[shadowCascades];
		var sunlightShadowSplitBoundArray = new Vector4[shadowCascades];
		var scaleOffset = Matrix4x4.identity;
		scaleOffset.m00 = scaleOffset.m11 = scaleOffset.m22 =scaleOffset.m03 = scaleOffset.m13 = scaleOffset.m23 = 0.5f;
		
		_currentBuffer.SetGlobalFloat(ShaderManager.SHADOW_BIAS, sunlight.shadowBias);
		_currentBuffer.SetGlobalFloat(ShaderManager.SHADOW_NORMAL_BIAS, sunlight.shadowNormalBias);

		for (var i = 0; i < shadowCascades; i++) {
			ResetRenderTarget(SunlightShadowmapArrayId, CubemapFace.Unknown, i, true, false, 1, Color.black);

			if (!cull.ComputeDirectionalShadowMatricesAndCullingPrimitives(sunlightIndex, i, shadowCascades, @params.sunlightParams.shadowCascadeSplits, @params.sunlightParams.shadowResolution, sunlight.shadowNearPlane, out var viewMatrix, out var projectionMatrix, out var splitData)) {
				ExecuteCurrentBuffer();
				continue;
			}
			
			shadowSettings.splitData = splitData;
			
			_currentBuffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
		
			if (_reversedZBuffer) {
				projectionMatrix.m20 = -projectionMatrix.m20;
				projectionMatrix.m21 = -projectionMatrix.m21;
				projectionMatrix.m22 = -projectionMatrix.m22;
				projectionMatrix.m23 = -projectionMatrix.m23;
			}
		
			sunlightInverseVPArray[i] = scaleOffset * (projectionMatrix * viewMatrix);
			sunlightShadowSplitBoundArray[i] = splitData.cullingSphere;
			sunlightShadowSplitBoundArray[i].w *= splitData.cullingSphere.w;
			
			ExecuteCurrentBuffer();
			
			_context.DrawShadows(ref shadowSettings);
		}

		var inverseShadowmapSize = 1f / @params.sunlightParams.shadowResolution;
		
		_currentBuffer.SetGlobalFloat(ShaderManager.SUNLIGHT_SHADOW_STRENGTH, sunlight.shadowStrength);
		_currentBuffer.SetGlobalFloat(ShaderManager.SUNLIGHT_SHADOW_DISTANCE, shadowDistance);
		_currentBuffer.SetGlobalVector(ShaderManager.SUNLIGHT_SHADOWMAP_SIZE, new Vector4(inverseShadowmapSize, inverseShadowmapSize, @params.sunlightParams.shadowResolution, @params.sunlightParams.shadowResolution));
		_currentBuffer.SetGlobalMatrixArray(ShaderManager.SUNLIGHT_INVERSE_VP_ARRAY, sunlightInverseVPArray);
		_currentBuffer.SetGlobalVectorArray(ShaderManager.SUNLIGHT_SHADOW_SPLIT_BOUND_ARRAY, sunlightShadowSplitBoundArray);
		
		if (sunlight.shadows == LightShadows.Soft) _currentBuffer.EnableShaderKeyword(ShaderManager.SUNLIGHT_SOFT_SHADOWS);
		else _currentBuffer.DisableShaderKeyword(ShaderManager.SUNLIGHT_SOFT_SHADOWS);
		
		ExecuteCurrentBuffer();
	}

	private void RenderPointLightShadow(CullingResults cull, int shadowLightCount, Light[] shadowLights, int[] shadowLightIndices) {
		var pointLightShadowmapDescriptor = new RenderTextureDescriptor(@params.pointLightParams.shadowResolution, @params.pointLightParams.shadowResolution, RenderTextureFormat.RHalf, 16) {
			autoGenerateMips = false,
			bindMS = false,
			dimension = TextureDimension.CubeArray,
			volumeDepth = shadowLightCount * 6,
			enableRandomWrite = false,
			msaaSamples = 1,
			shadowSamplingMode = ShadowSamplingMode.None,
			sRGB = false,
			useMipMap = false,
			vrUsage = VRTextureUsage.None
		};
		
		_currentBuffer.GetTemporaryRT(ShaderManager.POINT_LIGHT_SHADOWMAP_ARRAY, pointLightShadowmapDescriptor, FilterMode.Bilinear);

		for (var i = 0; i < shadowLightCount; i++) {
			if (!cull.GetShadowCasterBounds(shadowLightIndices[i], out var shadowBounds)) continue;
			var light = shadowLights[i];
			_currentBuffer.SetGlobalFloat(ShaderManager.SHADOW_BIAS, light.shadowBias);
			_currentBuffer.SetGlobalFloat(ShaderManager.SHADOW_NORMAL_BIAS, light.shadowNormalBias);

			var shadowSettings = new ShadowDrawingSettings(cull, shadowLightIndices[i]);

			for (var j = 0; j < 6; j++) {
				var shadowSlice = i * 6 + j;
				ResetRenderTarget(PointLightShadowmapArrayId, CubemapFace.Unknown, shadowSlice, true, true, 1, Color.white);
				if (!cull.ComputePointShadowMatricesAndCullingPrimitives(shadowLightIndices[i], (CubemapFace) j, 0, out var viewMatrix, out var projectionMatrix, out var splitData)) {
					ExecuteCurrentBuffer();
					continue;
				}

				shadowSettings.splitData = splitData;
				
				Vector3 position = light.transform.position;
				_currentBuffer.SetGlobalVector(ShaderManager.LIGHT_POS, new Vector4(position.x, position.y, position.z, light.range));
				_currentBuffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
				ExecuteCurrentBuffer();

				_context.DrawShadows(ref shadowSettings);
			}
		}

		var inverseShadowmapSize = 1f / @params.pointLightParams.shadowResolution;
		
		_currentBuffer.SetGlobalVector(ShaderManager.POINT_LIGHT_SHADOWMAP_SIZE, new Vector4(inverseShadowmapSize, inverseShadowmapSize, @params.pointLightParams.shadowResolution, @params.pointLightParams.shadowResolution));

		if (@params.pointLightParams.softShadow) _currentBuffer.EnableShaderKeyword(ShaderManager.POINT_LIGHT_SOFT_SHADOWS);
		else _currentBuffer.DisableShaderKeyword(ShaderManager.POINT_LIGHT_SOFT_SHADOWS);
		
		ExecuteCurrentBuffer();
	}

	private void RenderSpotLightShadow(CullingResults cull, int shadowLightCount, Light[] shadowLights, int[] shadowLightIndices) {
		_currentBuffer.GetTemporaryRTArray(ShaderManager.SPOT_LIGHT_SHADOWMAP_ARRAY, @params.spotLightParams.shadowResolution, @params.spotLightParams.shadowResolution, shadowLightCount, 16, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
		var shadowLightInverseVPArray = new Matrix4x4[shadowLightCount];
		var scaleOffset = Matrix4x4.identity;
		scaleOffset.m00 = scaleOffset.m11 = scaleOffset.m22 = scaleOffset.m03 = scaleOffset.m13 = scaleOffset.m23 = 0.5f;
		
		for (var i = 0; i < shadowLightCount; i++) {
			var light = shadowLights[i];
			
			_currentBuffer.SetGlobalFloat(ShaderManager.SHADOW_BIAS, light.shadowBias);
			_currentBuffer.SetGlobalFloat(ShaderManager.SHADOW_NORMAL_BIAS, light.shadowNormalBias);
			
			var shadowSettings = new ShadowDrawingSettings(cull, shadowLightIndices[i]);
			ResetRenderTarget(SpotLightShadowmapArrayId, CubemapFace.Unknown, i, true, false, 1, Color.black);

			if (!cull.ComputeSpotShadowMatricesAndCullingPrimitives(shadowLightIndices[i], out var viewMatrix, out var projectionMatrix, out var splitData)) {
				ExecuteCurrentBuffer();
				continue;
			}

			shadowSettings.splitData = splitData;
				
			_currentBuffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);

			if (_reversedZBuffer) {
				projectionMatrix.m20 = -projectionMatrix.m20;
				projectionMatrix.m21 = -projectionMatrix.m21;
				projectionMatrix.m22 = -projectionMatrix.m22;
				projectionMatrix.m23 = -projectionMatrix.m23;
			}
			
			shadowLightInverseVPArray[i] = scaleOffset * (projectionMatrix * viewMatrix);
			
			ExecuteCurrentBuffer();
			
			_context.DrawShadows(ref shadowSettings);
		}
		
		Extensions.Resize(ref _spotLightInverseVPBuffer, shadowLightCount);
		_spotLightInverseVPBuffer.SetData(shadowLightInverseVPArray);
		
		var inverseShadowmapSize = 1f / @params.spotLightParams.shadowResolution;
		
		_currentBuffer.SetGlobalBuffer(ShaderManager.SPOT_LIGHT_INVERSE_VP_BUFFER, _spotLightInverseVPBuffer);
		_currentBuffer.SetGlobalVector(ShaderManager.SPOT_LIGHT_SHADOWMAP_SIZE, new Vector4(inverseShadowmapSize, inverseShadowmapSize, @params.spotLightParams.shadowResolution, @params.spotLightParams.shadowResolution));
		
		if (@params.spotLightParams.softShadow) _currentBuffer.EnableShaderKeyword(ShaderManager.SPOT_LIGHT_SOFT_SHADOWS);
		else _currentBuffer.DisableShaderKeyword(ShaderManager.SPOT_LIGHT_SOFT_SHADOWS);
		
		ExecuteCurrentBuffer();
	}

	private void DitherTransparentBlur(int width, int height) {
		_currentBuffer.GetTemporaryRT(ShaderManager.TEMPORARY_TEXTURE_1, width, height, 0, FilterMode.Bilinear, RenderTextureFormat.Default);
		_currentBuffer.GetTemporaryRT(ShaderManager.TEMPORARY_TEXTURE_2, width, height, 0, FilterMode.Bilinear, RenderTextureFormat.Default);
		_currentBuffer.Blit(ColorBufferId, TemporaryTexture1Id);
		
		@params.ditherTransparentParams.blurMaterial.SetFloat(ShaderManager.BLUR_RADIUS, @params.ditherTransparentParams.blurRadius);

		for (int i = 0; i < @params.ditherTransparentParams.iteration; i++) {
			_currentBuffer.Blit(TemporaryTexture1Id, TemporaryTexture2Id, @params.ditherTransparentParams.blurMaterial, 0);
			_currentBuffer.Blit(TemporaryTexture2Id, TemporaryTexture1Id, @params.ditherTransparentParams.blurMaterial, 1);
		}
		
		_currentBuffer.BlitWithDepth(TemporaryTexture1Id, ColorBufferId, DepthId, @params.ditherTransparentParams.blurMaterial, 2);
		_currentBuffer.ReleaseTemporaryRT(ShaderManager.TEMPORARY_TEXTURE_1);
		_currentBuffer.ReleaseTemporaryRT(ShaderManager.TEMPORARY_TEXTURE_2);
		ExecuteCurrentBuffer();
	}

	private void RenderScene(Camera camera) {
		// Clear the screen
		var clearFlags = camera.clearFlags;
		
		_currentBuffer.ClearRenderTarget((clearFlags & CameraClearFlags.Depth) != 0, (clearFlags & CameraClearFlags.Color) != 0, camera.backgroundColor);
		
		// Set up view port, view matrix and projection matrix
		// context.SetupCameraProperties(camera);
		
		var cameraTransform = camera.transform;
		var cameraForward = cameraTransform.forward;
		var cameraPosition = cameraTransform.position;

		var viewPort = camera.pixelRect;
		var viewMatrix = camera.worldToCameraMatrix;
		var projectionMatrix = camera.projectionMatrix;
		
		_currentBuffer.SetViewport(viewPort);
		_currentBuffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);

		var farClipPlane = camera.farClipPlane;
		var nearClipPlane = camera.nearClipPlane;
		var clipDistance = farClipPlane - nearClipPlane;
		
		var zBufferParams = new Vector4(clipDistance / nearClipPlane, 1, clipDistance / (farClipPlane * nearClipPlane), 1 / farClipPlane);

		_currentBuffer.SetGlobalFloat(ShaderManager.ALPHA_TEST_DEPTH_CUTOFF, @params.alphaTestDepthCutoff);
		_currentBuffer.SetGlobalVector(ShaderManager.Z_BUFFER_PARAMS, zBufferParams);
		_currentBuffer.SetComputeVectorParam(@params.tbrComputeShader, ShaderManager.Z_BUFFER_PARAMS, zBufferParams);
		_currentBuffer.SetComputeMatrixParam(@params.tbrComputeShader, ShaderManager.UNITY_MATRIX_V, viewMatrix);
		_currentBuffer.SetComputeMatrixParam(@params.tbrComputeShader, ShaderManager.UNITY_INVERSE_P, projectionMatrix.inverse);

		ExecuteCurrentBuffer();

// Only need to construct UI meshes under Editor mode
#if UNITY_EDITOR
		if (camera.cameraType == CameraType.SceneView) ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
#endif
		
		// Object culling
		// todo maybe add gpu culling pipeline in the future (compute shader based, AABB/OBB intersection tests)
		if (!camera.TryGetCullingParameters(out var cullingParameters)) return;
		cullingParameters.shadowDistance = Mathf.Min(@params.sunlightParams.shadowDistance, farClipPlane);
		var cull = _context.Cull(ref cullingParameters);
		
		var sortingSettings = new SortingSettings(camera) { criteria = SortingCriteria.QuantizedFrontToBack | SortingCriteria.OptimizeStateChanges };

		// Render depth and normal textures
		var drawSettings = new DrawingSettings(ShaderTagManager.SRP_DEFAULT_UNLIT, sortingSettings) {
			enableDynamicBatching = @params.enableDynamicBatching,
			enableInstancing = @params.enableInstancing
		};

		var filterSettings = FilteringSettings.defaultValue;
		filterSettings.layerMask = camera.cullingMask;
		filterSettings.renderQueueRange = RenderQueueRange.transparent;
		
		var pixelWidth = camera.pixelWidth;
		var pixelHeight = camera.pixelHeight;
		
		GenerateRTs(pixelWidth, pixelHeight);
		
		ResetRenderTarget(OpaqueNormalId, DepthId, true, true, 1, Color.black);
		
		ExecuteCurrentBuffer();
		
		// Depth prepass (with opaque normal rendered)
		var depthNormalDrawSettings = new DrawingSettings(ShaderTagManager.DEPTH_NORMAL, sortingSettings) {
			enableDynamicBatching = @params.enableDynamicBatching,
			enableInstancing = @params.enableInstancing
		};

		filterSettings.renderQueueRange = ShaderManager.OPAQUE_RENDER_QUEUE_RANGE;
		_context.DrawRenderers(cull, ref depthNormalDrawSettings, ref filterSettings);

		sortingSettings.criteria = SortingCriteria.OptimizeStateChanges;
		depthNormalDrawSettings.sortingSettings = sortingSettings;
		filterSettings.renderQueueRange = ShaderManager.ALPHA_TEST_QUEUE_RANGE;
		_context.DrawRenderers(cull, ref depthNormalDrawSettings, ref filterSettings);

		var screenThreadGroupsX = pixelWidth / 8;
		var screenThreadGroupsY = pixelHeight / 8;
		if (pixelWidth % 8 != 0) screenThreadGroupsX++;
		if (pixelHeight % 8 != 0) screenThreadGroupsY++;

		var depthCopyKernel = @params.generalComputeShader.FindKernel("CopyDepth");
		_currentBuffer.SetComputeTextureParam(@params.generalComputeShader, depthCopyKernel, ShaderManager.DEPTH_TEXTURE, DepthId);
		_currentBuffer.SetComputeTextureParam(@params.generalComputeShader, depthCopyKernel, ShaderManager.OPAQUE_DEPTH_TEXTURE, OpaqueDepthId);
		_currentBuffer.DispatchCompute(@params.generalComputeShader, depthCopyKernel, screenThreadGroupsX, screenThreadGroupsY, 1);
		
		_currentBuffer.SetGlobalTexture(ShaderManager.OPAQUE_DEPTH_TEXTURE, OpaqueDepthId);

		// Stencil prepass
		ResetRenderTarget(OpaqueNormalId, DepthId, false, false, 1, Color.black);
		
		ExecuteCurrentBuffer();
		
		var stencilDrawSettings = new DrawingSettings(ShaderTagManager.STENCIL, sortingSettings) {
			enableDynamicBatching = @params.enableDynamicBatching,
			enableInstancing = @params.enableInstancing
		};
		
		filterSettings.renderQueueRange = RenderQueueRange.all;

		_context.DrawRenderers(cull, ref stencilDrawSettings, ref filterSettings);

		var transparentDepthDrawSettings = new DrawingSettings(ShaderTagManager.DEPTH, sortingSettings) {
			enableDynamicBatching = @params.enableDynamicBatching,
			enableInstancing = @params.enableInstancing
		};
		
		filterSettings.renderQueueRange = RenderQueueRange.transparent;
		_context.DrawRenderers(cull, ref transparentDepthDrawSettings, ref filterSettings);

		// Tile-based light culling
		var depthBoundTextureWidth = pixelWidth / @params.depthTileResolution;
		var depthBoundTextureHeight = pixelHeight / @params.depthTileResolution;
		if (pixelWidth % @params.depthTileResolution != 0) depthBoundTextureWidth++;
		if (pixelHeight % @params.depthTileResolution != 0) depthBoundTextureHeight++;
		var tileThreadGroupsX = depthBoundTextureWidth / 16;
		var tileThreadGroupsY = depthBoundTextureHeight / 9;
		if (depthBoundTextureWidth % 16 != 0) tileThreadGroupsX++;
		if (depthBoundTextureHeight % 9 != 0) tileThreadGroupsY++;
		
		// Debug.Log(pixelWidth + ", " + pixelHeight);

		var depthBoundKernel = @params.tbrComputeShader.FindKernel("GenerateDepthBound");
		_currentBuffer.SetComputeFloatParams(@params.tbrComputeShader, ShaderManager.TILE_NUMBER, (float) depthBoundTextureWidth, (float) depthBoundTextureHeight);
		_currentBuffer.SetComputeTextureParam(@params.tbrComputeShader, depthBoundKernel, ShaderManager.DEPTH_TEXTURE, DepthId);
		_currentBuffer.SetComputeTextureParam(@params.tbrComputeShader, depthBoundKernel, ShaderManager.OPAQUE_DEPTH_TEXTURE, OpaqueDepthId);
		_currentBuffer.SetComputeTextureParam(@params.tbrComputeShader, depthBoundKernel, ShaderManager.DEPTH_BOUND_TEXTURE, DepthBoundId);
		_currentBuffer.DispatchCompute(@params.tbrComputeShader, depthBoundKernel, tileThreadGroupsX, tileThreadGroupsY, 1);

		ExecuteCurrentBuffer();

		var depthMaskKernel = @params.tbrComputeShader.FindKernel("GenerateDepthMask");
		_currentBuffer.SetComputeTextureParam(@params.tbrComputeShader, depthMaskKernel, ShaderManager.DEPTH_TEXTURE, DepthId);
		_currentBuffer.SetComputeTextureParam(@params.tbrComputeShader, depthMaskKernel, ShaderManager.OPAQUE_DEPTH_TEXTURE, OpaqueDepthId);
		_currentBuffer.SetComputeTextureParam(@params.tbrComputeShader, depthMaskKernel, ShaderManager.DEPTH_BOUND_TEXTURE, DepthBoundId);
		_currentBuffer.SetComputeTextureParam(@params.tbrComputeShader, depthMaskKernel, ShaderManager.DEPTH_MASK_TEXTURE, DepthMaskId);
		_currentBuffer.DispatchCompute(@params.tbrComputeShader, depthMaskKernel, tileThreadGroupsX, tileThreadGroupsY, 1);
		
		ExecuteCurrentBuffer();

		var depthFrustumKernel = @params.tbrComputeShader.FindKernel("GenerateDepthFrustum");
		_currentBuffer.SetComputeFloatParams(@params.tbrComputeShader, ShaderManager.CAMERA_FORWARD, cameraForward.x, cameraForward.y, cameraForward.z);
		_currentBuffer.SetComputeFloatParams(@params.tbrComputeShader, ShaderManager.CAMERA_POSITION, cameraPosition.x, cameraPosition.y, cameraPosition.z);
		// _currentBuffer.SetComputeVectorParam(@params.tbrComputeShader, ShaderManager.Z_BUFFER_PARAMS, zBufferParams);
		_currentBuffer.SetComputeMatrixParam(@params.tbrComputeShader, ShaderManager.UNITY_INVERSE_VP, (camera.projectionMatrix * camera.worldToCameraMatrix).inverse);
		_currentBuffer.SetComputeTextureParam(@params.tbrComputeShader, depthFrustumKernel, ShaderManager.DEPTH_BOUND_TEXTURE, DepthBoundId);
		_currentBuffer.SetComputeTextureParam(@params.tbrComputeShader, depthFrustumKernel, ShaderManager.DEPTH_FRUSTUM_TEXTURE, DepthFrustumId);
		_currentBuffer.DispatchCompute(@params.tbrComputeShader, depthFrustumKernel, tileThreadGroupsX, tileThreadGroupsY, 1);
		
		ExecuteCurrentBuffer();
		
		var allLights = cull.visibleLights;
		var lightIndexMap = cull.GetLightIndexMap(Allocator.Temp);
		
		var sunlightColor = new Vector4(0, 0, 0);
		var sunlightDirection = new Vector4(0, 0, 0);

		if (sunlight.Exists()) {
			sunlightDirection = sunlight.transform.localToWorldMatrix.GetDirectionFromLocalTransform();
			sunlightColor = sunlight.color * sunlight.intensity;
		}
		
		_currentBuffer.SetGlobalVector(ShaderManager.SUNLIGHT_COLOR, sunlightColor);
		_currentBuffer.SetGlobalVector(ShaderManager.SUNLIGHT_DIRECTION, sunlightDirection);

		var pointLightCountMax = @params.pointLightParams.enabled ? @params.pointLightParams.maxPerFrame : 0;
		var spotLightCountMax = @params.spotLightParams.enabled ? @params.spotLightParams.maxPerFrame : 0;

		var pointLightShadowMax = @params.pointLightParams.enabled ? @params.pointLightParams.maxShadowCount : 0;
		var spotLightShadowMax = @params.spotLightParams.enabled ? @params.spotLightParams.maxShadowCount : 0;

		var sunlightIndex = 0;

		var pointLightIndices = new int[pointLightShadowMax];
		var spotLightIndices = new int[spotLightShadowMax];

		var shadowPointLights = new Light[pointLightShadowMax];
		var shadowSpotLights = new Light[spotLightShadowMax];

		var pointLightIndex = 0;
		var spotLightIndex = 0;

		var pointLightShadowIndex = 0u;
		var spotLightShadowIndex = 0u;

		for (int i = 0, l = allLights.Length; i < l; i++) {
			var visibleLight = allLights[i];
			var lightType = allLights[i].lightType;
			switch (lightType) {
				case LightType.Point:
					if (pointLightIndex >= pointLightCountMax) continue;
					var originalPointLight = visibleLight.light;
					var pointLightColor = visibleLight.finalColor;
					var pointLight = new PointLight {
						// color = new float3(pointLightColor.r, pointLightColor.g, pointLightColor.b),
						sphere = new float4(visibleLight.light.transform.position, visibleLight.range)
					};

					if (originalPointLight.shadows != LightShadows.None && pointLightShadowIndex < pointLightShadowMax) {
						// pointLight.shadowStrength = originalPointLight.shadowStrength;
						pointLight.shadowIndex = pointLightShadowIndex + 1;
						pointLightIndices[pointLightShadowIndex] = lightIndexMap[i];
						shadowPointLights[pointLightShadowIndex] = originalPointLight;
						pointLightShadowIndex++;
					} else pointLight.shadowIndex = 0;

					_pointLights[pointLightIndex] = pointLight;
					pointLightIndex++;
					break;
				
				case LightType.Spot:
					if (spotLightIndex >= spotLightCountMax) continue;
					var originalSpotLight = visibleLight.light;
					var spotLightColor = visibleLight.finalColor;
					var spotLightDirection = visibleLight.localToWorldMatrix.GetDirectionFromLocalTransform();
					var spotLightAngle = Mathf.Deg2Rad * visibleLight.spotAngle * .5f;
					var spotLight = new SpotLight {
						// color = new float3(spotLightColor.r, spotLightColor.g, spotLightColor.b),
						cone = new Cone(visibleLight.localToWorldMatrix.GetPositionFromLocalTransform(), spotLightAngle, visibleLight.range, new float3(spotLightDirection.x, spotLightDirection.y, spotLightDirection.z)),
						// innerAngle = Mathf.Deg2Rad * originalSpotLight.innerSpotAngle * .5f,
						// nearClip = originalSpotLight.shadowNearPlane
					};

					if (originalSpotLight.shadows != LightShadows.None && spotLightShadowIndex < spotLightShadowMax) {
						// spotLight.shadowStrength = originalSpotLight.shadowStrength;
						spotLight.shadowIndex = spotLightShadowIndex + 1;
						spotLightIndices[spotLightShadowIndex] = lightIndexMap[i];
						shadowSpotLights[spotLightShadowIndex] = originalSpotLight;
						spotLightShadowIndex++;
					} else spotLight.shadowIndex = 0;

					_spotLights[spotLightIndex] = spotLight;
					spotLightIndex++;
					break;
				
				case LightType.Directional:
					if (allLights[i].light == sunlight) sunlightIndex = lightIndexMap[i];
					break;
			}
		}
		
		if (@params.sunlightParams.shadowOn && sunlight.Exists() && sunlight.shadows != LightShadows.None) {
			_currentBuffer.EnableShaderKeyword(ShaderManager.SUNLIGHT_SHADOWS);
			RenderCascadedDirectionalShadow(cull, sunlightIndex, sunlight, cullingParameters.shadowDistance);
		} else _currentBuffer.DisableShaderKeyword(ShaderManager.SUNLIGHT_SHADOWS);

		if (@params.pointLightParams.shadowOn) {
			_currentBuffer.EnableShaderKeyword(ShaderManager.POINT_LIGHT_SHADOWS);
			RenderPointLightShadow(cull, (int) pointLightShadowIndex, shadowPointLights, pointLightIndices);
			// if (pointLightShadowCount > 0) RenderPointLightShadow(context, cull, shadowPointLights[0], pointLightIndices[0]);
		} else _currentBuffer.DisableShaderKeyword(ShaderManager.POINT_LIGHT_SHADOWS);

		if (@params.spotLightParams.shadowOn) {
			_currentBuffer.EnableShaderKeyword(ShaderManager.SPOT_LIGHT_SHADOWS);
			RenderSpotLightShadow(cull, (int) spotLightShadowIndex, shadowSpotLights, spotLightIndices);
		} else _currentBuffer.DisableShaderKeyword(ShaderManager.SPOT_LIGHT_SHADOWS);
		
		Extensions.Resize(ref _pointLightBuffer, pointLightIndex);
		Extensions.Resize(ref _spotLightBuffer, spotLightIndex);
		
		_pointLightBuffer.SetData(_pointLights, 0, 0, pointLightIndex);
		_spotLightBuffer.SetData(_spotLights, 0, 0, spotLightIndex);

		var pointLightKernel = @params.tbrComputeShader.FindKernel("CullPointLight");
		_currentBuffer.SetComputeIntParam(@params.tbrComputeShader, ShaderManager.POINT_LIGHT_COUNT, pointLightIndex);
		_currentBuffer.SetComputeBufferParam(@params.tbrComputeShader, pointLightKernel, ShaderManager.POINT_LIGHT_BUFFER, _pointLightBuffer);
		_currentBuffer.SetComputeTextureParam(@params.tbrComputeShader, pointLightKernel, ShaderManager.DEPTH_BOUND_TEXTURE, DepthBoundId);
		_currentBuffer.SetComputeTextureParam(@params.tbrComputeShader, pointLightKernel, ShaderManager.DEPTH_MASK_TEXTURE, DepthMaskId);
		_currentBuffer.SetComputeTextureParam(@params.tbrComputeShader, pointLightKernel, ShaderManager.DEPTH_FRUSTUM_TEXTURE, DepthFrustumId);
		_currentBuffer.SetComputeTextureParam(@params.tbrComputeShader, pointLightKernel, ShaderManager.CULLED_POINT_LIGHT_TEXTURE, CulledPointLightId);
		_currentBuffer.DispatchCompute(@params.tbrComputeShader, pointLightKernel, tileThreadGroupsX, tileThreadGroupsY, 1);
		
		var spotLightKernel = @params.tbrComputeShader.FindKernel("CullSpotLight");
		_currentBuffer.SetComputeIntParam(@params.tbrComputeShader, ShaderManager.SPOT_LIGHT_COUNT, spotLightIndex);
		_currentBuffer.SetComputeBufferParam(@params.tbrComputeShader, spotLightKernel, ShaderManager.SPOT_LIGHT_BUFFER, _spotLightBuffer);
		_currentBuffer.SetComputeTextureParam(@params.tbrComputeShader, spotLightKernel, ShaderManager.DEPTH_FRUSTUM_TEXTURE, DepthFrustumId);
		_currentBuffer.SetComputeTextureParam(@params.tbrComputeShader, spotLightKernel, ShaderManager.CULLED_SPOT_LIGHT_TEXTURE, CulledSpotLightId);
		_currentBuffer.DispatchCompute(@params.tbrComputeShader, spotLightKernel, tileThreadGroupsX, tileThreadGroupsY, 1);
		
		ExecuteCurrentBuffer();
		
		_currentBuffer.SetGlobalTexture(ShaderManager.CULLED_POINT_LIGHT_TEXTURE, CulledPointLightId);
		_currentBuffer.SetGlobalTexture(ShaderManager.CULLED_SPOT_LIGHT_TEXTURE, CulledSpotLightId);
		_currentBuffer.SetGlobalBuffer(ShaderManager.POINT_LIGHT_BUFFER, _pointLightBuffer);
		_currentBuffer.SetGlobalBuffer(ShaderManager.SPOT_LIGHT_BUFFER, _spotLightBuffer);
		
		ExecuteCurrentBuffer();

		_context.SetupCameraProperties(camera);
		
		ResetRenderTarget(ColorBufferId, OpaqueDepthId, false, true, 0, Color.black);
		
		ExecuteCurrentBuffer();
		
		// Opaque pass
		sortingSettings.criteria = SortingCriteria.OptimizeStateChanges;
		drawSettings.overrideMaterial = null;
		filterSettings.renderQueueRange = ShaderManager.OPAQUE_RENDER_QUEUE_RANGE;
		_context.DrawRenderers(cull, ref drawSettings, ref filterSettings);

		// Alpha test pass
		sortingSettings.criteria = SortingCriteria.OptimizeStateChanges;
		filterSettings.renderQueueRange = ShaderManager.ALPHA_TEST_QUEUE_RANGE;
		_context.DrawRenderers(cull, ref drawSettings, ref filterSettings);
		
		// Skybox Pass
		if ((camera.clearFlags & CameraClearFlags.Skybox) != 0) _context.DrawSkybox(camera);
		
		ResetRenderTarget(ColorBufferId, DepthId, false, false, 1, Color.black);
		
		ExecuteCurrentBuffer();

		// Transparent Pass
		filterSettings.renderQueueRange = RenderQueueRange.transparent;
		_context.DrawRenderers(cull, ref drawSettings, ref filterSettings);

		if (@params.ditherTransparentParams.blurOn && @params.ditherTransparentParams.blurMaterial != null) DitherTransparentBlur(pixelWidth >> @params.ditherTransparentParams.downSamples, pixelHeight >> @params.ditherTransparentParams.downSamples);
		
		// Blit color buffer to camera target (normally screen)
		_currentBuffer.Blit(ColorBufferId, BuiltinRenderTextureType.CameraTarget);

#if UNITY_EDITOR
		if (@params.testMaterialOn) {
			@params.testMaterial.SetInt("_TestInt", @params.testInt);
			_currentBuffer.Blit(BuiltinRenderTextureType.CurrentActive, BuiltinRenderTextureType.CurrentActive, @params.testMaterial);
			// _currentBuffer.Blit(PointLightShadowmapId, BuiltinRenderTextureType.CurrentActive);
			if (@params.depthBoundOn) _currentBuffer.Blit(DepthBoundId, BuiltinRenderTextureType.CurrentActive);
		}
#endif
		
		ExecuteCurrentBuffer();

#if UNITY_EDITOR
		if (@params.gizmosOn) _context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
#endif
		
		// Release temporary render textures
		ReleaseRTs();
		
		// Release unmanaged objects
		// DisposeComputeBuffers();
		
		ExecuteCurrentBuffer();

		_context.Submit();
		
		// allLights.Dispose();
		lightIndexMap.Dispose();
	}

	private void ExecuteCurrentBuffer() {
		_context.ExecuteCommandBuffer(_currentBuffer);
		_currentBuffer.Clear();
	}

	private void ExecuteCommandBuffer(CommandBuffer cmd) {
		_context.ExecuteCommandBuffer(cmd);
		cmd.Clear();
	}

	private void ResetRenderTarget(RenderTargetIdentifier colorBuffer, bool clearDepth, bool clearColor, float depth, Color color) {
		_currentBuffer.SetRenderTarget(colorBuffer);
		_currentBuffer.ClearRenderTarget(clearDepth, clearColor, color, depth);
	}

	private void ResetRenderTarget(RenderTargetIdentifier colorBuffer, RenderTargetIdentifier depthBuffer, bool clearDepth, bool clearColor, float depth, Color color) {
		_currentBuffer.SetRenderTarget(colorBuffer, depthBuffer);
		_currentBuffer.ClearRenderTarget(clearDepth, clearColor, color, depth);
	}

	private void ResetRenderTarget(RenderTargetIdentifier colorBuffer, CubemapFace cubemapFace, int depthSlice, bool clearDepth, bool clearColor, float depth, Color color) {
		_currentBuffer.SetRenderTarget(colorBuffer, 0, cubemapFace, depthSlice);
		_currentBuffer.ClearRenderTarget(clearDepth, clearColor, color, depth);
	}

	private void GenerateRTs(int pixelWidth, int pixelHeight) {
		var tileWidth = pixelWidth / @params.depthTileResolution;
		var tileHeight = pixelHeight / @params.depthTileResolution;
		
		/*
		var sunlightShadowmapDescriptor = new RenderTextureDescriptor(@params.sunlightParams.shadowResolution, @params.sunlightParams.shadowResolution, RenderTextureFormat.Shadowmap, 16) {
			dimension = TextureDimension.Tex2D
		};
		*/
		
		var depthFrustumDescriptor = new RenderTextureDescriptor(tileWidth, tileHeight, GraphicsFormat.R16G16B16A16_SFloat, 0) {
			enableRandomWrite = true,
			dimension = TextureDimension.Tex3D,
			volumeDepth = 6
		};

		var culledPointLightDescriptor = new RenderTextureDescriptor(tileWidth, tileHeight, GraphicsFormat.R16_UInt, 0) {
			enableRandomWrite = true,
			dimension = TextureDimension.Tex3D,
			volumeDepth = 17
		};
		
		var culledSpotLightDescriptor = new RenderTextureDescriptor(tileWidth, tileHeight, GraphicsFormat.R16_UInt, 0) {
			enableRandomWrite = true,
			dimension = TextureDimension.Tex3D,
			volumeDepth = 9
		};
		
		var culledDecalDescriptor = new RenderTextureDescriptor(tileWidth, tileHeight, GraphicsFormat.R16_UInt, 0) {
			enableRandomWrite = true,
			dimension = TextureDimension.Tex3D,
			volumeDepth = 5
		};
		
		_currentBuffer.GetTemporaryRT(ShaderManager.COLOR_BUFFER, pixelWidth, pixelHeight, 0, FilterMode.Bilinear, RenderTextureFormat.Default, RenderTextureReadWrite.Default);
		_currentBuffer.GetTemporaryRT(ShaderManager.DEPTH_TEXTURE, pixelWidth, pixelHeight, 32, FilterMode.Bilinear, RenderTextureFormat.Depth, RenderTextureReadWrite.Linear);
		_currentBuffer.GetTemporaryRT(ShaderManager.OPAQUE_DEPTH_TEXTURE, pixelWidth, pixelHeight, 24, FilterMode.Bilinear, RenderTextureFormat.Depth, RenderTextureReadWrite.Linear, 1, true);
		_currentBuffer.GetTemporaryRT(ShaderManager.OPAQUE_NORMAL_TEXTURE, pixelWidth, pixelHeight, 0, FilterMode.Point, GraphicsFormat.R16G16B16A16_SFloat, 1);
		// _currentBuffer.GetTemporaryRT(ShaderManager.SUNLIGHT_SHADOWMAP, sunlightShadowmapDescriptor, FilterMode.Bilinear);
		_currentBuffer.GetTemporaryRT(ShaderManager.DEPTH_MASK_TEXTURE, tileWidth, tileHeight, 0, FilterMode.Point, GraphicsFormat.R32_UInt, 1, true);
		_currentBuffer.GetTemporaryRT(ShaderManager.DEPTH_BOUND_TEXTURE, tileWidth, tileHeight, 0, FilterMode.Point, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear, 1, true);
		_currentBuffer.GetTemporaryRT(ShaderManager.DEPTH_FRUSTUM_TEXTURE, depthFrustumDescriptor, FilterMode.Point);
		_currentBuffer.GetTemporaryRT(ShaderManager.CULLED_POINT_LIGHT_TEXTURE, culledPointLightDescriptor, FilterMode.Point);
		_currentBuffer.GetTemporaryRT(ShaderManager.CULLED_SPOT_LIGHT_TEXTURE, culledSpotLightDescriptor, FilterMode.Point);
	}

	protected override void Dispose(bool disposing) {
		base.Dispose(disposing);
		if (!disposing) return;
		DisposeComputeBuffers();
	}

	private void GenerateComputeBuffers() {
		_pointLightBuffer = new ComputeBuffer(50, sizeof(PointLight));
		_spotLightBuffer = new ComputeBuffer(50, sizeof(SpotLight));
		_pointLightInverseVPBuffer = new ComputeBuffer(3, sizeof(Matrix4x4));
		_spotLightInverseVPBuffer = new ComputeBuffer(3, sizeof(Matrix4x4));
	}

	private void DisposeComputeBuffers() {
		_pointLightBuffer.Dispose();
		_spotLightBuffer.Dispose();
		_pointLightInverseVPBuffer.Dispose();
		_spotLightInverseVPBuffer.Dispose();
	}

	private void ReleaseRTs() {
		_currentBuffer.ReleaseTemporaryRT(ShaderManager.COLOR_BUFFER);
		_currentBuffer.ReleaseTemporaryRT(ShaderManager.DEPTH_TEXTURE);
		_currentBuffer.ReleaseTemporaryRT(ShaderManager.OPAQUE_DEPTH_TEXTURE);
		_currentBuffer.ReleaseTemporaryRT(ShaderManager.OPAQUE_NORMAL_TEXTURE);
		_currentBuffer.ReleaseTemporaryRT(ShaderManager.DEPTH_BOUND_TEXTURE);
		_currentBuffer.ReleaseTemporaryRT(ShaderManager.DEPTH_MASK_TEXTURE);
		_currentBuffer.ReleaseTemporaryRT(ShaderManager.DEPTH_FRUSTUM_TEXTURE);
		_currentBuffer.ReleaseTemporaryRT(ShaderManager.CULLED_POINT_LIGHT_TEXTURE);
		_currentBuffer.ReleaseTemporaryRT(ShaderManager.CULLED_SPOT_LIGHT_TEXTURE);
		_currentBuffer.ReleaseTemporaryRT(ShaderManager.SUNLIGHT_SHADOWMAP);
		_currentBuffer.ReleaseTemporaryRT(ShaderManager.SUNLIGHT_SHADOWMAP_ARRAY);
		_currentBuffer.ReleaseTemporaryRT(ShaderManager.POINT_LIGHT_SHADOWMAP);
		_currentBuffer.ReleaseTemporaryRT(ShaderManager.POINT_LIGHT_SHADOWMAP_ARRAY);
		_currentBuffer.ReleaseTemporaryRT(ShaderManager.SPOT_LIGHT_SHADOWMAP_ARRAY);
	}
}