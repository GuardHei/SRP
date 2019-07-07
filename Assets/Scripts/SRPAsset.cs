using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using RenderPipeline = UnityEngine.Rendering.RenderPipeline;

[CreateAssetMenu(menuName = "Rendering/SRPAsset")]
public sealed class SRPAsset : RenderPipelineAsset {

	public bool enableDynamicBatching = true;
	public bool enableInstancing = true;
	public Material opaqueDepthMaterial;
	public Material transparentDepthMaterial;

	protected override RenderPipeline CreatePipeline() => new SRPipeline {
		enableDynamicBatching = enableDynamicBatching,
		enableInstancing = enableInstancing,
		opaqueDepthMaterial = opaqueDepthMaterial,
		transparentDepthMaterial = transparentDepthMaterial
	};
}

// 用sealed关键字消除虚函数的开销
public sealed class SRPipeline : RenderPipeline {
	
	private static readonly Color DepthTextureClearColor = new Color(0f, 1f, 0f);

	public static SRPipeline current {
		get;
		private set;
	}

	public bool enableDynamicBatching = true;
	public bool enableInstancing = true;
	public Material opaqueDepthMaterial;
	public Material transparentDepthMaterial;

	private RenderTexture _opaqueDepthTexture;
	private RenderTexture _transparentMinDepthTexture;
	private RenderTexture _transparentMaxDepthTexture;
	private RenderTexture _depthBoundTexture;
	
	private readonly CommandBuffer _currentBuffer = new CommandBuffer { name = "Render Camera" };

	public SRPipeline() {
		GraphicsSettings.lightsUseLinearIntensity = true;
		current = this;
		Init();
	}

	private void Init() {
		
	}
	
	protected override void Render(ScriptableRenderContext context, Camera[] cameras) {
		foreach (var camera in cameras) Render(context, camera);
	}

	private void Render(ScriptableRenderContext context, Camera camera) {
		// 初始化相机渲染属性
		context.SetupCameraProperties(camera);
		
		// 清除渲染目标
		var clearFlags = camera.clearFlags;
		_currentBuffer.ClearRenderTarget((clearFlags & CameraClearFlags.Depth) != 0, (clearFlags & CameraClearFlags.Color) != 0, camera.backgroundColor);
		
		_currentBuffer.BeginSample("Render Camera");
		
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
		var sortingSettings = new SortingSettings(camera) { criteria = SortingCriteria.CommonOpaque };
		var drawSettings = new DrawingSettings {
			enableDynamicBatching = enableDynamicBatching,
			enableInstancing = enableInstancing,
			sortingSettings = sortingSettings
		};

		var filterSettings = FilteringSettings.defaultValue;
		filterSettings.layerMask = camera.cullingMask;
		filterSettings.sortingLayerRange = SortingLayerRange.all;
		filterSettings.renderQueueRange = RenderQueueRange.opaque;
		
		_currentBuffer.BeginSample("Render Opaque Depth");
		
		_depthBoundTexture = RenderTexture.GetTemporary(camera.pixelWidth, camera.pixelHeight, 0, RenderTextureFormat.RG16, RenderTextureReadWrite.Default);
		
		// 创建不透明物体的深度图，绑定为渲染目标，初始化为最大值
		// 写入不透明物体的最小深度值（最近的片元）
		_opaqueDepthTexture = RenderTexture.GetTemporary(camera.pixelWidth, camera.pixelHeight, 16, RenderTextureFormat.Depth, RenderTextureReadWrite.Default);
		_currentBuffer.SetRenderTarget(_depthBoundTexture, _opaqueDepthTexture);
		_currentBuffer.ClearRenderTarget(true, true, DepthTextureClearColor, 1);
		
		context.ExecuteCommandBuffer(_currentBuffer);
		_currentBuffer.Clear();
		
		// 渲染不透明物体的深度图
		drawSettings.overrideMaterial = opaqueDepthMaterial;
		context.DrawRenderers(cull, ref drawSettings, ref filterSettings);
		
		_currentBuffer.EndSample("Render Opaque Depth");
		
		_currentBuffer.BeginSample("Render Transparent Max Depth");
		
		// 创建半透明物体的深度图，绑定为渲染目标，初始化为最大值
		// 写入半透明物体的最小深度值（最近的片元）
		_transparentMaxDepthTexture = RenderTexture.GetTemporary(camera.pixelWidth, camera.pixelHeight, 16, RenderTextureFormat.Depth, RenderTextureReadWrite.Default);
		_currentBuffer.SetRenderTarget(_depthBoundTexture, _transparentMinDepthTexture);
		_currentBuffer.ClearRenderTarget(true, true, DepthTextureClearColor, 1);
		
		context.ExecuteCommandBuffer(_currentBuffer);
		_currentBuffer.Clear();
		
		// 渲染半透明物体的深度最小值图，过程定义在着色器的第一个Pass里
		drawSettings.overrideMaterial = transparentDepthMaterial;
		drawSettings.overrideMaterialPassIndex = 0;
		context.DrawRenderers(cull, ref drawSettings, ref filterSettings);
		
		_currentBuffer.EndSample("Render Transparent Max Depth");
		
		_currentBuffer.BeginSample("Render Transparent Min Depth");
		
		// 创建半透明物体的深度图，绑定为渲染目标，初始化为最小值
		// 写入半透明物体的最d大深度值（最近的片元）
		_transparentMinDepthTexture = RenderTexture.GetTemporary(camera.pixelWidth, camera.pixelHeight, 16, RenderTextureFormat.Depth, RenderTextureReadWrite.Default);
		_currentBuffer.SetRenderTarget(_depthBoundTexture, _transparentMaxDepthTexture);
		_currentBuffer.ClearRenderTarget(true, true, DepthTextureClearColor, 0);
		
		context.ExecuteCommandBuffer(_currentBuffer);
		_currentBuffer.Clear();
		
		// 渲染半透明物体的深度最大值图，过程定义在着色器的第二个Pass里
		drawSettings.overrideMaterialPassIndex = 1;
		context.DrawRenderers(cull, ref drawSettings, ref filterSettings);
		
		_currentBuffer.EndSample("Render Transparent Min Depth");
		
		// 绑定渲染目标为相机
		context.SetupCameraProperties(camera);
		
		// 渲染不透明物体
		// 【暂时】使用无光照着色器
		sortingSettings.criteria = SortingCriteria.CommonOpaque;
		drawSettings.overrideMaterial = null;
		drawSettings.SetShaderPassName(0, ShaderTagManager.SRPDefaultUnlit);
		context.DrawRenderers(cull, ref drawSettings, ref filterSettings);
		
		// 渲染天空盒
		if ((camera.clearFlags & CameraClearFlags.Skybox) != 0) context.DrawSkybox(camera);

		// 渲染半透明物体
		sortingSettings.criteria = SortingCriteria.CommonTransparent;
		drawSettings.sortingSettings = sortingSettings;
		filterSettings.renderQueueRange = RenderQueueRange.transparent;
		context.DrawRenderers(cull, ref drawSettings, ref filterSettings);
		
		// 释放深度贴图
		RenderTexture.ReleaseTemporary(_opaqueDepthTexture);
		RenderTexture.ReleaseTemporary(_transparentMaxDepthTexture);
		RenderTexture.ReleaseTemporary(_transparentMinDepthTexture);
		
		_currentBuffer.EndSample("Render Camera");
		
		context.ExecuteCommandBuffer(_currentBuffer);
		_currentBuffer.Clear();
		
		context.Submit();
	}
}