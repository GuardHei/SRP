using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;
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
	public int depthTileResolution = 16;
	public Material opaqueDepthNormalMaterial;
	public Material transparentDepthMinMaterial;
	public Material transparentDepthMaxMaterial;
	public Material testMaterial;
	public ComputeShader tbrComputeShader;
	public bool testMaterialOn;
	public bool depthBoundOn;
	public bool gizmosOn;
}

// 用sealed关键字消除虚函数的开销
public sealed unsafe class SRPipeline : RenderPipeline {

	public const int MAX_LIGHT_PER_TILE = 64;
	
	private static readonly Color DepthTextureClearColor = new Color(0f, 1f, 0f);

	public static SRPipeline current {
		get;
		private set;
	}

	public SRPipelineParams @params;

	private RenderTargetIdentifier _transparentMinDepthId;
	private RenderTargetIdentifier _transparentMaxDepthId;
	private RenderTargetIdentifier _opaqueDepthId;
	private RenderTargetIdentifier _opaqueNormalId;
	private RenderTargetIdentifier _depthBoundId;
	private RenderTargetIdentifier _depthFrustumId;
	private RenderTargetIdentifier _culledPointLightId;
	private RenderTargetIdentifier _culledSpotLightId;

	private ComputeBuffer _pointLightBuffer;
	private ComputeBuffer _spotLightBuffer;
	
	private readonly CommandBuffer _currentBuffer = new CommandBuffer { name = "Render Camera" };
	
	private DrawingSettings _drawSettings;
	
	public SRPipeline() {
		GraphicsSettings.lightsUseLinearIntensity = true;
		current = this;
		Init();
	}

	private void Init() {
		foreach (var camera in Camera.allCameras) camera.forceIntoRenderTexture = true;
		
		_transparentMinDepthId = new RenderTargetIdentifier(ShaderManager.TRANSPARENT_MIN_DEPTH_TEXTURE);
		_transparentMaxDepthId = new RenderTargetIdentifier(ShaderManager.TRANSPARENT_MAX_DEPTH_TEXTURE);
		_opaqueDepthId = new RenderTargetIdentifier(ShaderManager.OPAQUE_DEPTH_TEXTURE);
		_opaqueNormalId = new RenderTargetIdentifier(ShaderManager.OPAQUE_NORMAL_TEXTURE);
		_depthBoundId = new RenderTargetIdentifier(ShaderManager.DEPTH_BOUND_TEXTURE);
		_depthFrustumId = new RenderTargetIdentifier(ShaderManager.DEPTH_FRUSTUM_TEXTURE);
		_culledPointLightId = new RenderTargetIdentifier(ShaderManager.CULLED_POINT_LIGHT_TEXTURE);
		_culledSpotLightId = new RenderTargetIdentifier(ShaderManager.CULLED_SPOT_LIGHT_TEXTURE);
		
		GenerateComputeBuffers();
	}
	
	protected override void Render(ScriptableRenderContext context, Camera[] cameras) {
		foreach (var camera in cameras) Render(context, camera);
	}

	private void Render(ScriptableRenderContext context, Camera camera) {
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
		
		_currentBuffer.SetGlobalVector(ShaderManager.Z_BUFFER_PARAMS, zBufferParams);
		
		context.ExecuteCommandBuffer(_currentBuffer);
		_currentBuffer.Clear();

// 只有在编辑器的模式下才需要构建UI的网格
#if UNITY_EDITOR
		if (camera.cameraType == CameraType.SceneView) ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
#endif
		
		// 场景剔除
		if (!camera.TryGetCullingParameters(out var cullingParameters)) return;
		var cull = context.Cull(ref cullingParameters);
		
		// 渲染深度图
		_drawSettings.enableDynamicBatching = @params.enableDynamicBatching;
		_drawSettings.enableInstancing = @params.enableInstancing;
		
		var sortingSettings = new SortingSettings(camera) { criteria = SortingCriteria.CommonOpaque };

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
		_drawSettings.SetShaderPassName(0, ShaderTagManager.SRPDefaultUnlit);
		_drawSettings.overrideMaterial = @params.opaqueDepthNormalMaterial;
		filterSettings.renderQueueRange = RenderQueueRange.opaque;
		context.DrawRenderers(cull, ref _drawSettings, ref filterSettings);

		var depthBoundTextureWidth = pixelWidth / @params.depthTileResolution;
		var depthBoundTextureHeight = pixelHeight / @params.depthTileResolution;
		var threadGroupsX = depthBoundTextureWidth / 16;
		var threadGroupsY = depthBoundTextureHeight / 9;

		var depthBoundKernel = @params.tbrComputeShader.FindKernel("GenerateDepthBound");
		_currentBuffer.SetComputeFloatParams(@params.tbrComputeShader, ShaderManager.TILE_NUMBER, (float) depthBoundTextureWidth, (float) depthBoundTextureHeight);
		_currentBuffer.SetComputeTextureParam(@params.tbrComputeShader, depthBoundKernel, ShaderManager.OPAQUE_DEPTH_TEXTURE, _opaqueDepthId);
		_currentBuffer.SetComputeTextureParam(@params.tbrComputeShader, depthBoundKernel, ShaderManager.DEPTH_BOUND_TEXTURE, _depthBoundId);
		_currentBuffer.DispatchCompute(@params.tbrComputeShader, depthBoundKernel, threadGroupsX, threadGroupsY, 1);

		ExecuteCurrentBuffer(context);
		
		var allLights = cull.visibleLights;

		var pointLightCount = 0;
		var spotLightCount = 0;

		for (int i = 0, l = allLights.Length; i < l; i++) {
			var lightType = allLights[i].lightType;
			switch (lightType) {
				case LightType.Point:
					pointLightCount++;
					break;
				case LightType.Spot:
					spotLightCount++;
					break;
			}
		}
		
		var pointLights = new NativeArray<PointLight>(pointLightCount, Allocator.Temp);
		var spotLights = new NativeArray<SpotLight>(spotLightCount, Allocator.Temp);

		var pointLightIndex = 0;
		var spotLightIndex = 0;
		
		var sunlightColor = new Vector4(0, 0, 0);
		var sunlightDirection = new Vector4(0, 0, 0);

		for (int i = 0, l = allLights.Length; i < l; i++) {
			var visibleLight = allLights[i];
			var lightType = allLights[i].lightType;
			switch (lightType) {
				case LightType.Point:
					var pointLightColor = visibleLight.finalColor;
					var pointLight = new PointLight {
						color = new float3(pointLightColor.r, pointLightColor.g, pointLightColor.b),
						sphere = new float4(visibleLight.light.transform.position, visibleLight.range)
					};

					pointLights[pointLightIndex] = pointLight;
					pointLightIndex++;
					break;
				case LightType.Spot:
					var originalSpotLight = visibleLight.light;
					var spotLightColor = visibleLight.finalColor;
					var spotLightDirection = visibleLight.localToWorldMatrix.GetColumn(2);
					var spotLightAngle = Mathf.Deg2Rad * visibleLight.spotAngle * .5f;
					var spotLight = new SpotLight {
						color = new float3(spotLightColor.r, spotLightColor.g, spotLightColor.b),
						cone = new Cone(visibleLight.localToWorldMatrix.GetPositionFromLocalTransform(), spotLightAngle, visibleLight.range, new float3(spotLightDirection.x, spotLightDirection.y, spotLightDirection.z)),
						matrixVP = originalSpotLight.shadowMatrixOverride * visibleLight.localToWorldMatrix.inverse,
						innerAngle = Mathf.Deg2Rad * originalSpotLight.innerSpotAngle * .5f,
						nearClip = originalSpotLight.shadowNearPlane
					};

					spotLights[spotLightIndex] = spotLight;
					spotLightIndex++;
					break;
				case LightType.Directional:
					sunlightDirection = visibleLight.localToWorldMatrix.GetDirectionFromLocalTransform();
					sunlightColor = visibleLight.finalColor;
					break;
			}
		}
		
		_currentBuffer.SetGlobalVector(ShaderManager.SUNLIGHT_COLOR, sunlightColor);
		_currentBuffer.SetGlobalVector(ShaderManager.SUNLIGHT_DIRECTION, sunlightDirection);
		
		Extensions.Resize(ref _pointLightBuffer, pointLightCount);
		Extensions.Resize(ref _spotLightBuffer, spotLightCount);
		
		_pointLightBuffer.SetData(pointLights);
		_spotLightBuffer.SetData(spotLights);

		var lightKernel = @params.tbrComputeShader.FindKernel("CullLight");
		var cameraTransform = camera.transform;
		var cameraForward = cameraTransform.forward;
		var cameraPosition = cameraTransform.position;
		_currentBuffer.SetComputeIntParam(@params.tbrComputeShader, ShaderManager.POINT_LIGHT_COUNT, pointLightCount);
		_currentBuffer.SetComputeIntParam(@params.tbrComputeShader, ShaderManager.SPOT_LIGHT_COUNT, spotLightCount);
		_currentBuffer.SetComputeFloatParams(@params.tbrComputeShader, ShaderManager.CAMERA_FORWARD, cameraForward.x, cameraForward.y, cameraForward.z);
		_currentBuffer.SetComputeFloatParams(@params.tbrComputeShader, ShaderManager.CAMERA_POSITION, cameraPosition.x, cameraPosition.y, cameraPosition.z);
		_currentBuffer.SetComputeVectorParam(@params.tbrComputeShader, ShaderManager.Z_BUFFER_PARAMS, zBufferParams);
		_currentBuffer.SetComputeMatrixParam(@params.tbrComputeShader, ShaderManager.INVERSE_VP, (camera.projectionMatrix * camera.worldToCameraMatrix).inverse);
		_currentBuffer.SetComputeBufferParam(@params.tbrComputeShader, lightKernel, ShaderManager.POINT_LIGHT_BUFFER, _pointLightBuffer);
		_currentBuffer.SetComputeBufferParam(@params.tbrComputeShader, lightKernel, ShaderManager.SPOT_LIGHT_BUFFER, _spotLightBuffer);
		_currentBuffer.SetComputeTextureParam(@params.tbrComputeShader, lightKernel, ShaderManager.DEPTH_BOUND_TEXTURE, _depthBoundId);
		_currentBuffer.SetComputeTextureParam(@params.tbrComputeShader, lightKernel, ShaderManager.CULLED_POINT_LIGHT_TEXTURE, _culledPointLightId);
		_currentBuffer.SetComputeTextureParam(@params.tbrComputeShader, lightKernel, ShaderManager.CULLED_SPOT_LIGHT_TEXTURE, _culledSpotLightId);
		_currentBuffer.DispatchCompute(@params.tbrComputeShader, lightKernel, threadGroupsX, threadGroupsY, 1);
		
		ExecuteCurrentBuffer(context);
		
		_currentBuffer.SetGlobalTexture(ShaderManager.CULLED_POINT_LIGHT_TEXTURE, _culledPointLightId);
		_currentBuffer.SetGlobalTexture(ShaderManager.CULLED_SPOT_LIGHT_TEXTURE, _culledSpotLightId);
		_currentBuffer.SetGlobalBuffer(ShaderManager.POINT_LIGHT_BUFFER, _pointLightBuffer);
		_currentBuffer.SetGlobalBuffer(ShaderManager.SPOT_LIGHT_BUFFER, _spotLightBuffer);
		
		ExecuteCurrentBuffer(context);

		// 绑定渲染目标为相机
		context.SetupCameraProperties(camera);
		
		// 渲染不透明物体
		// 【暂时】使用无光照着色器
		sortingSettings.criteria = SortingCriteria.OptimizeStateChanges;
		_drawSettings.overrideMaterial = null;
		_drawSettings.SetShaderPassName(0, ShaderTagManager.SRPDefaultUnlit);
		context.DrawRenderers(cull, ref _drawSettings, ref filterSettings);
		
		// 渲染天空盒
		if ((camera.clearFlags & CameraClearFlags.Skybox) != 0) context.DrawSkybox(camera);

		// 渲染半透明物体
		/*
		sortingSettings.criteria = SortingCriteria.CommonTransparent;
		_drawSettings.sortingSettings = sortingSettings;
		filterSettings.renderQueueRange = RenderQueueRange.transparent;
		context.DrawRenderers(cull, ref _drawSettings, ref filterSettings);
		*/

#if UNITY_EDITOR
		if (@params.testMaterialOn) {
			_currentBuffer.Blit(BuiltinRenderTextureType.CurrentActive, BuiltinRenderTextureType.CurrentActive, @params.testMaterial);
			if (@params.depthBoundOn) _currentBuffer.Blit(_depthBoundId, BuiltinRenderTextureType.CurrentActive);
		}
#endif
		
		ExecuteCurrentBuffer(context);
		
		// 释放临时申请的贴图
		ReleaseRTs();
		
		// 释放非托管对象
		pointLights.Dispose();
		spotLights.Dispose();

#if UNITY_EDITOR
		if (@params.gizmosOn) context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
#endif	

		context.Submit();
	}

	private void ExecuteCurrentBuffer(ScriptableRenderContext context) {
		context.ExecuteCommandBuffer(_currentBuffer);
		_currentBuffer.Clear();
	}

	private void ResetRenderTarget(RenderTargetIdentifier colorBuffer, RenderTargetIdentifier depthBuffer, bool clearDepth, bool clearColor, float depth, Color color) {
		_currentBuffer.SetRenderTarget(colorBuffer, depthBuffer);
		_currentBuffer.ClearRenderTarget(clearDepth, clearColor, color, depth);
	}

	private void GenerateRTs(int pixelWidth, int pixelHeight) {
		var tileWidth = pixelWidth / @params.depthTileResolution;
		var tileHeight = pixelHeight / @params.depthTileResolution;
		var depthFrustumDescriptor = new RenderTextureDescriptor(tileWidth, tileHeight, GraphicsFormat.R16G16B16A16_SFloat, 0) {
			enableRandomWrite = true,
			dimension = TextureDimension.Tex3D,
			volumeDepth = 65
		};

		var culledLightDescriptor = new RenderTextureDescriptor(tileWidth, tileHeight, GraphicsFormat.R16_UInt, 0) {
			enableRandomWrite = true,
			dimension = TextureDimension.Tex3D,
			volumeDepth = 65
		};

		// _currentBuffer.GetTemporaryRT(ShaderManager.TRANSPARENT_MIN_DEPTH_TEXTURE, pixelWidth, pixelHeight, 16, FilterMode.Point, RenderTextureFormat.Depth);
		_currentBuffer.GetTemporaryRT(ShaderManager.OPAQUE_DEPTH_TEXTURE, pixelWidth, pixelHeight, 32, FilterMode.Point, RenderTextureFormat.Depth);
		_currentBuffer.GetTemporaryRT(ShaderManager.OPAQUE_NORMAL_TEXTURE, pixelWidth, pixelHeight, 0, FilterMode.Point, GraphicsFormat.R16G16B16A16_SFloat);
		_currentBuffer.GetTemporaryRT(ShaderManager.DEPTH_BOUND_TEXTURE, tileWidth, tileHeight, 0, FilterMode.Point, RenderTextureFormat.RGHalf, RenderTextureReadWrite.Linear, 1, true);
		// _currentBuffer.GetTemporaryRT(ShaderManager.DEPTH_FRUSTUM_TEXTURE, depthFrustumDescriptor, FilterMode.Point);
		_currentBuffer.GetTemporaryRT(ShaderManager.CULLED_POINT_LIGHT_TEXTURE, culledLightDescriptor, FilterMode.Point);
		_currentBuffer.GetTemporaryRT(ShaderManager.CULLED_SPOT_LIGHT_TEXTURE, culledLightDescriptor, FilterMode.Point);
	}

	protected override void Dispose(bool disposing) {
		base.Dispose(disposing);
		if (!disposing) return;
		_pointLightBuffer.Dispose();
		_spotLightBuffer.Dispose();
	}

	private void GenerateComputeBuffers() {
		_pointLightBuffer = new ComputeBuffer(50, sizeof(PointLight));
		_spotLightBuffer = new ComputeBuffer(50, sizeof(SpotLight));
	}

	private void ReleaseComputeBuffers() {
		_pointLightBuffer.Release();
		_spotLightBuffer.Release();
	}

	private void ReleaseRTs() {
		// _currentBuffer.ReleaseTemporaryRT(ShaderManager.TRANSPARENT_MIN_DEPTH_TEXTURE);
		_currentBuffer.ReleaseTemporaryRT(ShaderManager.OPAQUE_DEPTH_TEXTURE);
		_currentBuffer.ReleaseTemporaryRT(ShaderManager.OPAQUE_NORMAL_TEXTURE);
		_currentBuffer.ReleaseTemporaryRT(ShaderManager.DEPTH_BOUND_TEXTURE);
		// _currentBuffer.ReleaseTemporaryRT(ShaderManager.DEPTH_FRUSTUM_TEXTURE);
		_currentBuffer.ReleaseTemporaryRT(ShaderManager.CULLED_POINT_LIGHT_TEXTURE);
		_currentBuffer.ReleaseTemporaryRT(ShaderManager.CULLED_SPOT_LIGHT_TEXTURE);
	}
}

[Serializable]
public struct Point {
	public float3 position;
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
	public float4 sphere;
}

[Serializable]
public struct SpotLight {
	public float3 color;
	public Cone cone;
	public float4x4 matrixVP;
	public float innerAngle;
	public float nearClip;
}