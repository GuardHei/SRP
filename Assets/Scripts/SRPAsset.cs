using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;
using Object = UnityEngine.Object;
using RenderPipeline = UnityEngine.Rendering.RenderPipeline;

[CreateAssetMenu(menuName = "Rendering/SRPAsset")]
public sealed class SRPAsset : RenderPipelineAsset {

	public SRPipelineParams @params;

	protected override RenderPipeline CreatePipeline() => new SRPipeline {
		@params = @params
	};
}

[Serializable]
public class SRPipelineParams {
	public bool enableDynamicBatching = true;
	public bool enableInstancing = true;
	public float resolutionRatio = 1f;
	public int depthTileResolution = 16;
	public float alphaTestDepthCutoff = .001f;
	public ComputeShader tbrComputeShader;
	public Material depthFlipMaterial;
	public SunlightParams sunlightParams;
	public PointLightParams pointLightParams;
	public SpotLightParams spotLightParams;
	public bool testMaterialOn;
	public Material testMaterial;
	public bool depthBoundOn;
	public bool gizmosOn;
}

[Serializable]
public class SunlightParams {
	public bool shadowOn = true;
	public int shadowResolution = 1024;
	public int shadowCascades = 4;
	public Vector3 shadowCascadeSplits = new Vector3(.067f, .2f, .467f);
	public float shadowDistance = 100;
}

[Serializable]
public class PointLightParams {
	public bool shadowOn = true;
	public bool softShadow = true;
	public int shadowResolution = 1024;
	public int maxShadowCount = 10;
}

[Serializable]
public class SpotLightParams {
	public bool shadowOn = true;
	public bool softShadow = true;
	public int shadowResolution = 1024;
	public int maxShadowCount = 10;
}

// 用sealed关键字消除虚函数的开销
public sealed unsafe class SRPipeline : RenderPipeline {

	public const int MAX_LIGHT_PER_TILE = 16;
	public const int MAX_DECAL_PER_TILE = 4;
	
	private static readonly Color DepthTextureClearColor = new Color(0f, 1f, 0f);

	public static SRPipeline current {
		get;
		private set;
	}

	public SRPipelineParams @params;
	public Light sunlight;

	private RenderTargetIdentifier _colorBufferId;
	private RenderTargetIdentifier _opaqueDepthId;
	private RenderTargetIdentifier _opaqueNormalId;
	private RenderTargetIdentifier _sunlightShadowmapId;
	private RenderTargetIdentifier _sunlightShadowmapArrayId;
	private RenderTargetIdentifier _pointLightShadowmapArrayId;
	private RenderTargetIdentifier _spotLightShadowmapArrayId;
	private RenderTargetIdentifier _depthBoundId;
	private RenderTargetIdentifier _depthFrustumId;
	private RenderTargetIdentifier _culledPointLightId;
	private RenderTargetIdentifier _culledSpotLightId;

	private ComputeBuffer _pointLightBuffer;
	private ComputeBuffer _spotLightBuffer;
	private ComputeBuffer _pointLightInverseVPBuffer;
	private ComputeBuffer _spotLightInverseVPBuffer;
	
	private readonly CommandBuffer _currentBuffer = new CommandBuffer { name = "Render Camera" };
	private readonly bool _reversedZBuffer;

	public SRPipeline() {
		GraphicsSettings.lightsUseLinearIntensity = true;
		_reversedZBuffer = SystemInfo.usesReversedZBuffer;
		current = this;
		Init();
	}

	private void Init() {
		foreach (var camera in Camera.allCameras) camera.forceIntoRenderTexture = true;

		foreach (var light in Object.FindObjectsOfType<Light>()) {
			if (light.type != LightType.Directional) continue;
			sunlight = light;
			break;
		}
		
		_colorBufferId = new RenderTargetIdentifier(ShaderManager.COLOR_BUFFER);
		_opaqueDepthId = new RenderTargetIdentifier(ShaderManager.OPAQUE_DEPTH_TEXTURE);
		_opaqueNormalId = new RenderTargetIdentifier(ShaderManager.OPAQUE_NORMAL_TEXTURE);
		_sunlightShadowmapId = new RenderTargetIdentifier(ShaderManager.SUNLIGHT_SHADOWMAP);
		_sunlightShadowmapArrayId = new RenderTargetIdentifier(ShaderManager.SUNLIGHT_SHADOWMAP_ARRAY);
		_depthBoundId = new RenderTargetIdentifier(ShaderManager.DEPTH_BOUND_TEXTURE);
		_depthFrustumId = new RenderTargetIdentifier(ShaderManager.DEPTH_FRUSTUM_TEXTURE);
		_culledPointLightId = new RenderTargetIdentifier(ShaderManager.CULLED_POINT_LIGHT_TEXTURE);
		_culledSpotLightId = new RenderTargetIdentifier(ShaderManager.CULLED_SPOT_LIGHT_TEXTURE);
		_pointLightShadowmapArrayId = new RenderTargetIdentifier(ShaderManager.POINT_LIGHT_SHADOWMAP_ARRAY);
		_spotLightShadowmapArrayId = new RenderTargetIdentifier(ShaderManager.SPOT_LIGHT_SHADOWMAP_ARRAY);
		
		GenerateComputeBuffers();
	}
	
	protected override void Render(ScriptableRenderContext context, Camera[] cameras) {
		foreach (var camera in cameras) RenderScene(context, camera);
	}

	private void RenderDirectionalShadow(ScriptableRenderContext context, CullingResults cull, int sunlightIndex, Light sunlight) {
		ResetRenderTarget(_sunlightShadowmapId, true, false, 1, Color.black);
		
		var shadowSettings = new ShadowDrawingSettings(cull, sunlightIndex);
		if (!cull.ComputeDirectionalShadowMatricesAndCullingPrimitives(sunlightIndex, 0, 1, new Vector3(1, 0, 0), @params.sunlightParams.shadowResolution, sunlight.shadowNearPlane, out var viewMatrix, out var projectionMatrix, out var splitData)) {
			ExecuteCurrentBuffer(context);
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
		scaleOffset.m00 = scaleOffset.m11 = scaleOffset.m22 = 0.5f;
		scaleOffset.m03 = scaleOffset.m13 = scaleOffset.m23 = 0.5f;
		
		var sunlightInverseVP = scaleOffset * (projectionMatrix * viewMatrix);
		
		_currentBuffer.SetGlobalMatrix(ShaderManager.SUNLIGHT_INVERSE_VP, sunlightInverseVP);
		
		ExecuteCurrentBuffer(context);
		
		context.DrawShadows(ref shadowSettings);
	}

	private void RenderCascadedDirectionalShadow(ScriptableRenderContext context, CullingResults cull, int sunlightIndex, Light sunlight, float shadowDistance) {
		_currentBuffer.GetTemporaryRTArray(ShaderManager.SUNLIGHT_SHADOWMAP_ARRAY, @params.sunlightParams.shadowResolution, @params.sunlightParams.shadowResolution, @params.sunlightParams.shadowCascades, 16, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
		
		var shadowCascades = @params.sunlightParams.shadowCascades;
		
		var shadowSettings = new ShadowDrawingSettings(cull, sunlightIndex);
		var sunlightInverseVPArray = new Matrix4x4[shadowCascades];
		var sunlightShadowSplitBoundArray = new Vector4[shadowCascades];
		var scaleOffset = Matrix4x4.identity;
		scaleOffset.m00 = scaleOffset.m11 = scaleOffset.m22 = 0.5f;
		scaleOffset.m03 = scaleOffset.m13 = scaleOffset.m23 = 0.5f;
		
		_currentBuffer.SetGlobalFloat(ShaderManager.SHADOW_BIAS, sunlight.shadowBias);
		_currentBuffer.SetGlobalFloat(ShaderManager.SHADOW_NORMAL_BIAS, sunlight.shadowNormalBias);

		for (var i = 0; i < shadowCascades; i++) {
			ResetRenderTarget(_sunlightShadowmapArrayId, CubemapFace.Unknown, i, true, false, 1, Color.black);

			if (!cull.ComputeDirectionalShadowMatricesAndCullingPrimitives(sunlightIndex, i, shadowCascades, @params.sunlightParams.shadowCascadeSplits, @params.sunlightParams.shadowResolution, sunlight.shadowNearPlane, out var viewMatrix, out var projectionMatrix, out var splitData)) {
				ExecuteCurrentBuffer(context);
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
			
			ExecuteCurrentBuffer(context);
			
			context.DrawShadows(ref shadowSettings);
		}

		var inverseShadowmapSize = 1f / @params.sunlightParams.shadowResolution;
		
		_currentBuffer.SetGlobalFloat(ShaderManager.SUNLIGHT_SHADOW_STRENGTH, sunlight.shadowStrength);
		_currentBuffer.SetGlobalFloat(ShaderManager.SUNLIGHT_SHADOW_DISTANCE, shadowDistance);
		_currentBuffer.SetGlobalVector(ShaderManager.SUNLIGHT_SHADOWMAP_SIZE, new Vector4(inverseShadowmapSize, inverseShadowmapSize, @params.sunlightParams.shadowResolution, @params.sunlightParams.shadowResolution));
		_currentBuffer.SetGlobalMatrixArray(ShaderManager.SUNLIGHT_INVERSE_VP_ARRAY, sunlightInverseVPArray);
		_currentBuffer.SetGlobalVectorArray(ShaderManager.SUNLIGHT_SHADOW_SPLIT_BOUND_ARRAY, sunlightShadowSplitBoundArray);
		
		if (sunlight.shadows == LightShadows.Soft) _currentBuffer.EnableShaderKeyword(ShaderManager.SUNLIGHT_SOFT_SHADOWS);
		else _currentBuffer.DisableShaderKeyword(ShaderManager.SUNLIGHT_SOFT_SHADOWS);
		
		ExecuteCurrentBuffer(context);
	}

	private void RenderPointLightShadow(ScriptableRenderContext context, CullingResults cull, int shadowLightCount, Light[] shadowLights, int[] shadowLightIndices) {
		var shadowSlices = shadowLightCount * 6;
		// _currentBuffer.GetTemporaryRT(ShaderManager.POINT_LIGHT_SHADOWMAP_ARRAY, @params.pointLightParams.shadowResolution, @params.pointLightParams.shadowResolution, 16, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
		_currentBuffer.GetTemporaryRTArray(ShaderManager.POINT_LIGHT_SHADOWMAP_ARRAY, @params.pointLightParams.shadowResolution, @params.pointLightParams.shadowResolution, shadowSlices, 16, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
		// var shadowLightInverseVPArray = new Matrix4x4[shadowSlices];
		/*
		var scaleOffset = Matrix4x4.identity;
		scaleOffset.m00 = scaleOffset.m11 = scaleOffset.m22 = 0.5f;
		scaleOffset.m03 = scaleOffset.m13 = scaleOffset.m23 = 0.5f;
		*/
		
		for (var i = 0; i < shadowLightCount; i++) {
			var light = shadowLights[i];
			_currentBuffer.SetGlobalFloat(ShaderManager.SHADOW_BIAS, light.shadowBias);
			_currentBuffer.SetGlobalFloat(ShaderManager.SHADOW_NORMAL_BIAS, light.shadowNormalBias);
			
			var shadowSettings = new ShadowDrawingSettings(cull, shadowLightIndices[i]);

			for (var j = 0; j < 6; j++) {
				var shadowSlice = i * 6 + j;
				ResetRenderTarget(_pointLightShadowmapArrayId, CubemapFace.Unknown, shadowSlice, true, true, 1, Color.black);
				if (!cull.ComputePointShadowMatricesAndCullingPrimitives(shadowLightIndices[i], (CubemapFace) j, 0, out var viewMatrix, out var projectionMatrix, out var splitData)) {
					ExecuteCurrentBuffer(context);
					continue;
				}

				shadowSettings.splitData = splitData;
				
				_currentBuffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
				/*
				if (_reversedZBuffer) {
					projectionMatrix.m20 = -projectionMatrix.m20;
					projectionMatrix.m21 = -projectionMatrix.m21;
					projectionMatrix.m22 = -projectionMatrix.m22;
					projectionMatrix.m23 = -projectionMatrix.m23;
				}
				
				shadowLightInverseVPArray[shadowSlice] = scaleOffset * (projectionMatrix * viewMatrix);
				*/
				ExecuteCurrentBuffer(context);
				
				context.DrawShadows(ref shadowSettings);
			}
		}
		
		// Extensions.Resize(ref _pointLightInverseVPBuffer, shadowSlices);
		// _pointLightInverseVPBuffer.SetData(shadowLightInverseVPArray);
		
		var inverseShadowmapSize = 1f / @params.pointLightParams.shadowResolution;
		
		// _currentBuffer.SetGlobalBuffer(ShaderManager.POINT_LIGHT_INVERSE_VP_BUFFER, _pointLightInverseVPBuffer);
		_currentBuffer.SetGlobalVector(ShaderManager.POINT_LIGHT_SHADOWMAP_SIZE, new Vector4(inverseShadowmapSize, inverseShadowmapSize, @params.pointLightParams.shadowResolution, @params.pointLightParams.shadowResolution));
		// _currentBuffer.SetGlobalTexture(ShaderManager.POINT_LIGHT_SHADOWMAP_ARRAY, _pointLightShadowmapArrayId);
		
		if (@params.pointLightParams.softShadow) _currentBuffer.EnableShaderKeyword(ShaderManager.POINT_LIGHT_SOFT_SHADOWS);
		else _currentBuffer.DisableShaderKeyword(ShaderManager.POINT_LIGHT_SOFT_SHADOWS);
		
		ExecuteCurrentBuffer(context);
	}

	private void RenderSpotLightShadow(ScriptableRenderContext context, CullingResults cull, int shadowLightCount, Light[] shadowLights, int[] shadowLightIndices) {
		_currentBuffer.GetTemporaryRTArray(ShaderManager.SPOT_LIGHT_SHADOWMAP_ARRAY, @params.spotLightParams.shadowResolution, @params.spotLightParams.shadowResolution, shadowLightCount, 16, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
		var shadowLightInverseVPArray = new Matrix4x4[shadowLightCount];
		var scaleOffset = Matrix4x4.identity;
		scaleOffset.m00 = scaleOffset.m11 = scaleOffset.m22 = 0.5f;
		scaleOffset.m03 = scaleOffset.m13 = scaleOffset.m23 = 0.5f;
		
		for (var i = 0; i < shadowLightCount; i++) {
			var light = shadowLights[i];
			
			_currentBuffer.SetGlobalFloat(ShaderManager.SHADOW_BIAS, light.shadowBias);
			_currentBuffer.SetGlobalFloat(ShaderManager.SHADOW_NORMAL_BIAS, light.shadowNormalBias);
			
			var shadowSettings = new ShadowDrawingSettings(cull, shadowLightIndices[i]);
			ResetRenderTarget(_spotLightShadowmapArrayId, CubemapFace.Unknown, i, true, false, 1, Color.black);

			if (!cull.ComputeSpotShadowMatricesAndCullingPrimitives(shadowLightIndices[i], out var viewMatrix, out var projectionMatrix, out var splitData)) {
				ExecuteCurrentBuffer(context);
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
			
			ExecuteCurrentBuffer(context);
			
			context.DrawShadows(ref shadowSettings);
		}
		
		Extensions.Resize(ref _spotLightInverseVPBuffer, shadowLightCount);
		_spotLightInverseVPBuffer.SetData(shadowLightInverseVPArray);
		
		var inverseShadowmapSize = 1f / @params.spotLightParams.shadowResolution;
		
		_currentBuffer.SetGlobalBuffer(ShaderManager.SPOT_LIGHT_INVERSE_VP_BUFFER, _spotLightInverseVPBuffer);
		_currentBuffer.SetGlobalVector(ShaderManager.SPOT_LIGHT_SHADOWMAP_SIZE, new Vector4(inverseShadowmapSize, inverseShadowmapSize, @params.spotLightParams.shadowResolution, @params.spotLightParams.shadowResolution));
		
		if (@params.spotLightParams.softShadow) _currentBuffer.EnableShaderKeyword(ShaderManager.SPOT_LIGHT_SOFT_SHADOWS);
		else _currentBuffer.DisableShaderKeyword(ShaderManager.SPOT_LIGHT_SOFT_SHADOWS);
		
		ExecuteCurrentBuffer(context);
	}

	private void RenderScene(ScriptableRenderContext context, Camera camera) {
		// 清除渲染目标
		var clearFlags = camera.clearFlags;
		
		_currentBuffer.ClearRenderTarget((clearFlags & CameraClearFlags.Depth) != 0, (clearFlags & CameraClearFlags.Color) != 0, camera.backgroundColor);
		
		// 设置视口、观察矩阵和投影矩阵
		// context.SetupCameraProperties(camera);

		var viewMatrix = camera.worldToCameraMatrix;
		
		_currentBuffer.SetViewport(camera.pixelRect);
		_currentBuffer.SetViewProjectionMatrices(viewMatrix, camera.projectionMatrix);

		var farClipPlane = camera.farClipPlane;
		var nearClipPlane = camera.nearClipPlane;
		var clipDistance = farClipPlane - nearClipPlane;
		
		var zBufferParams = new Vector4(clipDistance / nearClipPlane, 1, clipDistance / (farClipPlane * nearClipPlane), 1 / farClipPlane);
		
		_currentBuffer.SetGlobalFloat(ShaderManager.ALPHA_TEST_DEPTH_CUTOFF, @params.alphaTestDepthCutoff);
		_currentBuffer.SetGlobalVector(ShaderManager.Z_BUFFER_PARAMS, zBufferParams);
		
		context.ExecuteCommandBuffer(_currentBuffer);
		_currentBuffer.Clear();

// 只有在编辑器的模式下才需要构建UI的网格
#if UNITY_EDITOR
		if (camera.cameraType == CameraType.SceneView) ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
#endif
		
		// 场景剔除
		if (!camera.TryGetCullingParameters(out var cullingParameters)) return;
		cullingParameters.shadowDistance = Mathf.Min(@params.sunlightParams.shadowDistance, farClipPlane);
		var cull = context.Cull(ref cullingParameters);
		
		var sortingSettings = new SortingSettings(camera) { criteria = SortingCriteria.QuantizedFrontToBack | SortingCriteria.OptimizeStateChanges };

		// 渲染深度图
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
		
		// 绑定不透明物体的深度图为渲染目标，初始化为最大值
		// 法线图是Color Buffer，和深度图一起被绘制
		// 写入不透明物体的最小深度值（最近的片元）和归一化后的法线
		ResetRenderTarget(_opaqueNormalId, _opaqueDepthId, true, true, 1, Color.black);
		
		ExecuteCurrentBuffer(context);
		
		// 渲染不透明物体的法线图和深度图
		var depthNormalDrawSettings = new DrawingSettings(ShaderTagManager.DEPTH_NORMAL, sortingSettings) {
			enableDynamicBatching = @params.enableDynamicBatching,
			enableInstancing = @params.enableInstancing
		};

		filterSettings.renderQueueRange = ShaderManager.OPAQUE_RENDER_QUEUE_RANGE;
		context.DrawRenderers(cull, ref depthNormalDrawSettings, ref filterSettings);

		sortingSettings.criteria = SortingCriteria.CommonTransparent;
		depthNormalDrawSettings.sortingSettings = sortingSettings;
		filterSettings.renderQueueRange = ShaderManager.ALPHA_TEST_QUEUE_RANGE;
		context.DrawRenderers(cull, ref depthNormalDrawSettings, ref filterSettings);
		
		//todo 渲染Dither Transparent的Stencil
		// filterSettings.renderQueueRange = ShaderManager.DITHER_TRANSPARENT_RENDER_QUEUE;

		var depthBoundTextureWidth = pixelWidth / @params.depthTileResolution;
		var depthBoundTextureHeight = pixelHeight / @params.depthTileResolution;
		if (pixelWidth % @params.depthTileResolution != 0) depthBoundTextureWidth++;
		if (pixelHeight % @params.depthTileResolution != 0) depthBoundTextureHeight++;
		var threadGroupsX = depthBoundTextureWidth / 16;
		var threadGroupsY = depthBoundTextureHeight / 9;
		if (depthBoundTextureWidth % 16 != 0) threadGroupsX++;
		if (depthBoundTextureHeight % 9 != 0) threadGroupsY++;
		
		// Debug.Log(pixelWidth + ", " + pixelHeight);

		var depthBoundKernel = @params.tbrComputeShader.FindKernel("GenerateDepthBound");
		_currentBuffer.SetComputeFloatParams(@params.tbrComputeShader, ShaderManager.TILE_NUMBER, (float) depthBoundTextureWidth, (float) depthBoundTextureHeight);
		_currentBuffer.SetComputeTextureParam(@params.tbrComputeShader, depthBoundKernel, ShaderManager.OPAQUE_DEPTH_TEXTURE, _opaqueDepthId);
		_currentBuffer.SetComputeTextureParam(@params.tbrComputeShader, depthBoundKernel, ShaderManager.DEPTH_BOUND_TEXTURE, _depthBoundId);
		_currentBuffer.DispatchCompute(@params.tbrComputeShader, depthBoundKernel, threadGroupsX, threadGroupsY, 1);

		ExecuteCurrentBuffer(context);

		var depthFrustumKernel = @params.tbrComputeShader.FindKernel("GenerateDepthFrustum");
		var cameraTransform = camera.transform;
		var cameraForward = cameraTransform.forward;
		var cameraPosition = cameraTransform.position;
		_currentBuffer.SetComputeFloatParams(@params.tbrComputeShader, ShaderManager.CAMERA_FORWARD, cameraForward.x, cameraForward.y, cameraForward.z);
		_currentBuffer.SetComputeFloatParams(@params.tbrComputeShader, ShaderManager.CAMERA_POSITION, cameraPosition.x, cameraPosition.y, cameraPosition.z);
		_currentBuffer.SetComputeVectorParam(@params.tbrComputeShader, ShaderManager.Z_BUFFER_PARAMS, zBufferParams);
		_currentBuffer.SetComputeMatrixParam(@params.tbrComputeShader, ShaderManager.INVERSE_VP, (camera.projectionMatrix * camera.worldToCameraMatrix).inverse);
		_currentBuffer.SetComputeTextureParam(@params.tbrComputeShader, depthFrustumKernel, ShaderManager.DEPTH_BOUND_TEXTURE, _depthBoundId);
		_currentBuffer.SetComputeTextureParam(@params.tbrComputeShader, depthFrustumKernel, ShaderManager.DEPTH_FRUSTUM_TEXTURE, _depthFrustumId);
		_currentBuffer.DispatchCompute(@params.tbrComputeShader, depthFrustumKernel, threadGroupsX, threadGroupsY, 1);
		
		ExecuteCurrentBuffer(context);
		
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

		var sunlightIndex = 0;

		var pointLightCount = 0;
		var spotLightCount = 0;

		var pointLightShadowCount = 0;
		var spotLightShadowCount = 0;

		for (int i = 0, l = allLights.Length; i < l; i++) {
			var lightType = allLights[i].lightType;
			switch (lightType) {
				case LightType.Point:
					pointLightCount++;
					if (allLights[i].light.shadows != LightShadows.None) pointLightShadowCount++;
					break;
				case LightType.Spot:
					spotLightCount++;
					if (allLights[i].light.shadows != LightShadows.None) spotLightShadowCount++;
					break;
				case LightType.Directional:
					if (allLights[i].light == sunlight) sunlightIndex = lightIndexMap[i];
					break;
			}
		}

		if (@params.sunlightParams.shadowOn && sunlight.Exists() && sunlight.shadows != LightShadows.None) {
			_currentBuffer.EnableShaderKeyword(ShaderManager.SUNLIGHT_SHADOWS);
			_currentBuffer.EnableShaderKeyword(ShaderManager.SPOT_LIGHT_SOFT_SHADOWS);
			RenderCascadedDirectionalShadow(context, cull, sunlightIndex, sunlight, cullingParameters.shadowDistance);
		} else _currentBuffer.DisableShaderKeyword(ShaderManager.SUNLIGHT_SHADOWS);

		pointLightShadowCount = Mathf.Min(pointLightShadowCount, @params.pointLightParams.maxShadowCount);
		spotLightShadowCount = Mathf.Min(spotLightShadowCount, @params.spotLightParams.maxShadowCount);
		
		var pointLights = new NativeArray<PointLight>(pointLightCount, Allocator.Temp);
		var spotLights = new NativeArray<SpotLight>(spotLightCount, Allocator.Temp);

		var pointLightIndices = new int[pointLightShadowCount];
		var spotLightIndices = new int[spotLightShadowCount];

		var shadowPointLights = new Light[pointLightShadowCount];
		var shadowSpotLights = new Light[spotLightShadowCount];

		var pointLightIndex = 0;
		var spotLightIndex = 0;

		var pointLightShadowIndex = 0u;
		var spotLightShadowIndex = 0u;

		for (int i = 0, l = allLights.Length; i < l; i++) {
			var visibleLight = allLights[i];
			var lightType = allLights[i].lightType;
			switch (lightType) {
				case LightType.Point:
					var originalPointLight = visibleLight.light;
					var pointLightColor = visibleLight.finalColor;
					var pointLight = new PointLight {
						color = new float3(pointLightColor.r, pointLightColor.g, pointLightColor.b),
						sphere = new float4(visibleLight.light.transform.position, visibleLight.range)
					};

					if (originalPointLight.shadows != LightShadows.None) {
						pointLight.shadowStrength = originalPointLight.shadowStrength;
						pointLight.shadowIndex = pointLightShadowIndex + 1;
						pointLightIndices[pointLightShadowIndex] = lightIndexMap[i];
						shadowPointLights[pointLightShadowIndex] = originalPointLight;
						pointLightShadowIndex++;
					} else pointLight.shadowIndex = 0;

					pointLights[pointLightIndex] = pointLight;
					pointLightIndex++;
					break;
				case LightType.Spot:
					var originalSpotLight = visibleLight.light;
					var spotLightColor = visibleLight.finalColor;
					var spotLightDirection = visibleLight.localToWorldMatrix.GetDirectionFromLocalTransform();
					var spotLightAngle = Mathf.Deg2Rad * visibleLight.spotAngle * .5f;
					var spotLight = new SpotLight {
						color = new float3(spotLightColor.r, spotLightColor.g, spotLightColor.b),
						cone = new Cone(visibleLight.localToWorldMatrix.GetPositionFromLocalTransform(), spotLightAngle, visibleLight.range, new float3(spotLightDirection.x, spotLightDirection.y, spotLightDirection.z)),
						// matrixVP = originalSpotLight.shadowMatrixOverride * visibleLight.localToWorldMatrix.inverse,
						innerAngle = Mathf.Deg2Rad * originalSpotLight.innerSpotAngle * .5f,
						nearClip = originalSpotLight.shadowNearPlane
					};

					if (originalSpotLight.shadows != LightShadows.None) {
						spotLight.shadowStrength = originalSpotLight.shadowStrength;
						spotLight.shadowIndex = spotLightShadowIndex + 1;
						spotLightIndices[spotLightShadowIndex] = lightIndexMap[i];
						shadowSpotLights[spotLightShadowIndex] = originalSpotLight;
						spotLightShadowIndex++;
					} else spotLight.shadowIndex = 0;

					spotLights[spotLightIndex] = spotLight;
					spotLightIndex++;
					break;
			}
		}

		if (@params.pointLightParams.shadowOn) {
			_currentBuffer.EnableShaderKeyword(ShaderManager.POINT_LIGHT_SHADOWS);
			RenderPointLightShadow(context, cull, pointLightShadowCount, shadowPointLights, pointLightIndices);
		} else _currentBuffer.DisableShaderKeyword(ShaderManager.POINT_LIGHT_SHADOWS);

		if (@params.spotLightParams.shadowOn) {
			_currentBuffer.EnableShaderKeyword(ShaderManager.SPOT_LIGHT_SHADOWS);
			RenderSpotLightShadow(context, cull, spotLightShadowCount, shadowSpotLights, spotLightIndices);
		} else _currentBuffer.DisableShaderKeyword(ShaderManager.SPOT_LIGHT_SHADOWS);
		
		Extensions.Resize(ref _pointLightBuffer, pointLightCount);
		Extensions.Resize(ref _spotLightBuffer, spotLightCount);
		
		_pointLightBuffer.SetData(pointLights);
		_spotLightBuffer.SetData(spotLights);

		var pointLightKernel = @params.tbrComputeShader.FindKernel("CullPointLight");
		_currentBuffer.SetComputeIntParam(@params.tbrComputeShader, ShaderManager.POINT_LIGHT_COUNT, pointLightCount);
		_currentBuffer.SetComputeBufferParam(@params.tbrComputeShader, pointLightKernel, ShaderManager.POINT_LIGHT_BUFFER, _pointLightBuffer);
		_currentBuffer.SetComputeTextureParam(@params.tbrComputeShader, pointLightKernel, ShaderManager.DEPTH_FRUSTUM_TEXTURE, _depthFrustumId);
		_currentBuffer.SetComputeTextureParam(@params.tbrComputeShader, pointLightKernel, ShaderManager.CULLED_POINT_LIGHT_TEXTURE, _culledPointLightId);
		_currentBuffer.DispatchCompute(@params.tbrComputeShader, pointLightKernel, threadGroupsX, threadGroupsY, 1);
		
		var spotLightKernel = @params.tbrComputeShader.FindKernel("CullSpotLight");
		_currentBuffer.SetComputeIntParam(@params.tbrComputeShader, ShaderManager.SPOT_LIGHT_COUNT, spotLightCount);
		_currentBuffer.SetComputeBufferParam(@params.tbrComputeShader, spotLightKernel, ShaderManager.SPOT_LIGHT_BUFFER, _spotLightBuffer);
		_currentBuffer.SetComputeTextureParam(@params.tbrComputeShader, spotLightKernel, ShaderManager.DEPTH_FRUSTUM_TEXTURE, _depthFrustumId);
		_currentBuffer.SetComputeTextureParam(@params.tbrComputeShader, spotLightKernel, ShaderManager.CULLED_SPOT_LIGHT_TEXTURE, _culledSpotLightId);
		_currentBuffer.DispatchCompute(@params.tbrComputeShader, spotLightKernel, threadGroupsX, threadGroupsY, 1);
		
		ExecuteCurrentBuffer(context);
		
		_currentBuffer.SetGlobalTexture(ShaderManager.CULLED_POINT_LIGHT_TEXTURE, _culledPointLightId);
		_currentBuffer.SetGlobalTexture(ShaderManager.CULLED_SPOT_LIGHT_TEXTURE, _culledSpotLightId);
		_currentBuffer.SetGlobalBuffer(ShaderManager.POINT_LIGHT_BUFFER, _pointLightBuffer);
		_currentBuffer.SetGlobalBuffer(ShaderManager.SPOT_LIGHT_BUFFER, _spotLightBuffer);
		
		ExecuteCurrentBuffer(context);

		context.SetupCameraProperties(camera);
		
		ResetRenderTarget(_colorBufferId, _opaqueDepthId, false, true, 0, Color.black);
		
		ExecuteCurrentBuffer(context);
		
		// 渲染不透明物体
		sortingSettings.criteria = SortingCriteria.OptimizeStateChanges;
		drawSettings.overrideMaterial = null;
		filterSettings.renderQueueRange = ShaderManager.OPAQUE_RENDER_QUEUE_RANGE;
		context.DrawRenderers(cull, ref drawSettings, ref filterSettings);
		
		// 渲染天空盒
		if ((camera.clearFlags & CameraClearFlags.Skybox) != 0) context.DrawSkybox(camera);

		// todo 渲染Dither Transparent
		sortingSettings.criteria = SortingCriteria.CommonTransparent;
		filterSettings.renderQueueRange = ShaderManager.ALPHA_TEST_QUEUE_RANGE;
		context.DrawRenderers(cull, ref drawSettings, ref filterSettings);
		
		// 将颜色缓冲输出到当前照相机渲染目标，一般是屏幕
		_currentBuffer.Blit(_colorBufferId, BuiltinRenderTextureType.CameraTarget);

#if UNITY_EDITOR
		if (@params.testMaterialOn) {
			 _currentBuffer.Blit(BuiltinRenderTextureType.CurrentActive, BuiltinRenderTextureType.CurrentActive, @params.testMaterial);
			if (@params.depthBoundOn) _currentBuffer.Blit(_depthBoundId, BuiltinRenderTextureType.CurrentActive);
		}
#endif
		
		ExecuteCurrentBuffer(context);

#if UNITY_EDITOR
		if (@params.gizmosOn) context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
#endif
		
		// 释放临时申请的贴图
		ReleaseRTs();
		
		// 释放非托管对象
		// DisposeComputeBuffers();
		
		ExecuteCurrentBuffer(context);

		context.Submit();
	}

	private void ExecuteCurrentBuffer(ScriptableRenderContext context) {
		context.ExecuteCommandBuffer(_currentBuffer);
		_currentBuffer.Clear();
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

		var culledLightDescriptor = new RenderTextureDescriptor(tileWidth, tileHeight, GraphicsFormat.R16_UInt, 0) {
			enableRandomWrite = true,
			dimension = TextureDimension.Tex3D,
			volumeDepth = 17
		};

		_currentBuffer.GetTemporaryRT(ShaderManager.COLOR_BUFFER, pixelWidth, pixelHeight, 0, FilterMode.Bilinear, RenderTextureFormat.Default);
		_currentBuffer.GetTemporaryRT(ShaderManager.OPAQUE_DEPTH_TEXTURE, pixelWidth, pixelHeight, 16, FilterMode.Point, RenderTextureFormat.Depth, RenderTextureReadWrite.Linear);
		_currentBuffer.GetTemporaryRT(ShaderManager.OPAQUE_NORMAL_TEXTURE, pixelWidth, pixelHeight, 0, FilterMode.Point, GraphicsFormat.R16G16B16A16_SFloat);
		// _currentBuffer.GetTemporaryRT(ShaderManager.SUNLIGHT_SHADOWMAP, sunlightShadowmapDescriptor, FilterMode.Bilinear);
		_currentBuffer.GetTemporaryRT(ShaderManager.DEPTH_BOUND_TEXTURE, tileWidth, tileHeight, 0, FilterMode.Point, RenderTextureFormat.RGHalf, RenderTextureReadWrite.Linear, 1, true);
		_currentBuffer.GetTemporaryRT(ShaderManager.DEPTH_FRUSTUM_TEXTURE, depthFrustumDescriptor, FilterMode.Point);
		_currentBuffer.GetTemporaryRT(ShaderManager.CULLED_POINT_LIGHT_TEXTURE, culledLightDescriptor, FilterMode.Point);
		_currentBuffer.GetTemporaryRT(ShaderManager.CULLED_SPOT_LIGHT_TEXTURE, culledLightDescriptor, FilterMode.Point);
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
		_currentBuffer.ReleaseTemporaryRT(ShaderManager.OPAQUE_DEPTH_TEXTURE);
		_currentBuffer.ReleaseTemporaryRT(ShaderManager.OPAQUE_NORMAL_TEXTURE);
		_currentBuffer.ReleaseTemporaryRT(ShaderManager.DEPTH_BOUND_TEXTURE);
		_currentBuffer.ReleaseTemporaryRT(ShaderManager.DEPTH_FRUSTUM_TEXTURE);
		_currentBuffer.ReleaseTemporaryRT(ShaderManager.CULLED_POINT_LIGHT_TEXTURE);
		_currentBuffer.ReleaseTemporaryRT(ShaderManager.CULLED_SPOT_LIGHT_TEXTURE);
		_currentBuffer.ReleaseTemporaryRT(ShaderManager.SUNLIGHT_SHADOWMAP);
		_currentBuffer.ReleaseTemporaryRT(ShaderManager.SUNLIGHT_SHADOWMAP_ARRAY);
		_currentBuffer.ReleaseTemporaryRT(ShaderManager.POINT_LIGHT_SHADOWMAP_ARRAY);
		_currentBuffer.ReleaseTemporaryRT(ShaderManager.SPOT_LIGHT_SHADOWMAP_ARRAY);
	}
}

[Serializable]
public struct Cone {
	public float3 vertex;
	public float angle;
	public float height;
	public float3 direction;
	public float radius;

	public Cone(float3 vertex, float angle, float height, float3 direction) {
		this.vertex = vertex;
		this.angle = angle;
		this.height = height;
		this.direction = direction;
		radius = Mathf.Tan(angle) * height;
	}
}

[Serializable]
public struct PointLight {
	public float3 color;
	public float shadowStrength;
	public float4 sphere;
	public uint shadowIndex;
}

[Serializable]
public struct SpotLight {
	public float3 color;
	public float shadowStrength;
	public Cone cone;
	// public float4x4 matrixVP;
	public float innerAngle;
	public float nearClip;
	public uint shadowIndex;
}