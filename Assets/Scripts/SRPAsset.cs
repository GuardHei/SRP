using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/SRPAsset")]
public sealed class SRPAsset : RenderPipelineAsset {

	public bool enableDynamicBatching = true;
	public bool enableInstancing = true;

	protected override RenderPipeline CreatePipeline() => new SRPipeline { enableDynamicBatching = enableDynamicBatching, enableInstancing = enableInstancing };
}

public sealed class SRPipeline : RenderPipeline {

	public static SRPipeline current {
		get;
		private set;
	}

	public bool enableDynamicBatching = true;
	public bool enableInstancing = true;
	
	private readonly CommandBuffer _cameraBuffer = new CommandBuffer { name = "Render Camera" };

	public SRPipeline() {
		GraphicsSettings.lightsUseLinearIntensity = true;
		current = this; 
	}
	
	protected override void Render(ScriptableRenderContext context, Camera[] cameras) {
		foreach (var camera in cameras) Render(context, camera);
	}

	private void Render(ScriptableRenderContext context, Camera camera) {
		context.SetupCameraProperties(camera);
		
		var clearFlags = camera.clearFlags;
		_cameraBuffer.ClearRenderTarget((clearFlags & CameraClearFlags.Depth) != 0, (clearFlags & CameraClearFlags.Color) != 0, camera.backgroundColor);
		
		_cameraBuffer.BeginSample("Render Camera");
		
		context.ExecuteCommandBuffer(_cameraBuffer);
		_cameraBuffer.Clear();

		if (!camera.TryGetCullingParameters(out var cullingParameters)) return;

#if UNITY_EDITOR
		if (camera.cameraType == CameraType.SceneView) ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
#endif
		
		var cull = context.Cull(ref cullingParameters);
		var sortingSettings = new SortingSettings(camera) { criteria = SortingCriteria.CommonOpaque };
		var drawSettings = new DrawingSettings {
			enableDynamicBatching = enableDynamicBatching,
			enableInstancing = enableInstancing,
			sortingSettings = sortingSettings,
			overrideMaterial = null
		};

		var filterSettings = FilteringSettings.defaultValue;
		filterSettings.layerMask = camera.cullingMask;
		filterSettings.sortingLayerRange = SortingLayerRange.all;
		filterSettings.renderQueueRange = RenderQueueRange.opaque;
		
		// 渲染不透明物体
		sortingSettings.criteria = SortingCriteria.CommonOpaque;
		// 【暂时】使用无光照着色器
		drawSettings.SetShaderPassName(0, ShaderTagManager.SRPDefaultUnlit);
		context.DrawRenderers(cull, ref drawSettings, ref filterSettings);
		context.DrawSkybox(camera);

		// 渲染半透明物体
		sortingSettings.criteria = SortingCriteria.CommonTransparent;
		drawSettings.sortingSettings = sortingSettings;
		filterSettings.renderQueueRange = RenderQueueRange.transparent;
		context.DrawRenderers(cull, ref drawSettings, ref filterSettings);
		
		_cameraBuffer.EndSample("Render Camera");
		
		context.ExecuteCommandBuffer(_cameraBuffer);
		_cameraBuffer.Clear();
		
		context.Submit();
	}
}