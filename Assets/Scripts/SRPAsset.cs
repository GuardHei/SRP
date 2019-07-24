using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;
using RenderPipeline = UnityEngine.Rendering.RenderPipeline;

[CreateAssetMenu(menuName = "Rendering/SRPAsset")]
public sealed class SRPAsset : RenderPipelineAsset {

	public bool enableDynamicBatching = true;
	public bool enableInstancing = true;
	public Material opaqueDepthMaterial;
	public Material transparentDepthMinMaterial;
	public Material transparentDepthMaxMaterial;
	public Material testMaterial;

	protected override RenderPipeline CreatePipeline() => new SRPipeline {
		enableDynamicBatching = enableDynamicBatching,
		enableInstancing = enableInstancing,
		opaqueDepthMaterial = opaqueDepthMaterial,
		transparentDepthMinMaterial = transparentDepthMinMaterial,
		transparentDepthMaxMaterial = transparentDepthMaxMaterial,
		testMaterial = testMaterial
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
	public Material transparentDepthMinMaterial;
	public Material transparentDepthMaxMaterial;
	public Material testMaterial;

	private RenderTargetIdentifier _transparentMinDepthId;
	private RenderTargetIdentifier _transparentMaxDepthId;
	private RenderTargetIdentifier _opaqueDepthId;
	private RenderTargetIdentifier _opaqueNormalId;
	private RenderTargetIdentifier _depthBoundId;
	
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
	}
	
	protected override void Render(ScriptableRenderContext context, Camera[] cameras) {
		foreach (var camera in cameras) Render(context, camera);
	}

	private void Render(ScriptableRenderContext context, Camera camera) {
		// 清除渲染目标
		var clearFlags = camera.clearFlags;
		
		_currentBuffer.ClearRenderTarget((clearFlags & CameraClearFlags.Depth) != 0, (clearFlags & CameraClearFlags.Color) != 0, camera.backgroundColor);
		
		// 设置观察矩阵和投影矩阵
		context.SetupCameraProperties(camera);
		
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
		_drawSettings.enableDynamicBatching = enableDynamicBatching;
		_drawSettings.enableInstancing = enableInstancing;
		
		var sortingSettings = new SortingSettings(camera) { criteria = SortingCriteria.CommonOpaque };

		var filterSettings = FilteringSettings.defaultValue;
		filterSettings.layerMask = camera.cullingMask;
		filterSettings.renderQueueRange = RenderQueueRange.transparent;
		
		var pixelWidth = camera.pixelWidth;
		var pixelHeight = camera.pixelHeight;
		
		// 创建半透明物体的最大/最小深度图
		// 创建不透明物体的深度/法线图
		GenerateRTs(pixelWidth, pixelHeight);
		
		// 绑定半透明物体的深度最小值图为渲染目标，初始化为最大值
		// 写入半透明物体的最小深度值（最近的片元）
		ResetRenderTarget(_opaqueNormalId, _transparentMinDepthId, true, false, 1, DepthTextureClearColor);
		
		ExecuteCurrentBuffer(context);
		
		// 渲染半透明物体的深度最小值图
		_drawSettings.overrideMaterial = transparentDepthMinMaterial;
		_drawSettings.overrideMaterialPassIndex = 0;
		context.DrawRenderers(cull, ref _drawSettings, ref filterSettings);
		
		// 绑定半透明物体的深度最大值图为渲染目标，初始化为最小值
		// 写入半透明物体的最大深度值（最远的片元）
		ResetRenderTarget(_opaqueNormalId, _transparentMaxDepthId, true, false, 0, DepthTextureClearColor);
		
		ExecuteCurrentBuffer(context);
		
		// 渲染半透明物体的深度最大值图
		_drawSettings.SetShaderPassName(0, ShaderTagManager.SRPDefaultUnlit);
		_drawSettings.overrideMaterial = transparentDepthMaxMaterial;
		context.DrawRenderers(cull, ref _drawSettings, ref filterSettings);
		
		// 绑定不透明物体的深度图为渲染目标，初始化为最大值
		// 法线图是Color Buffer，和深度图一起被绘制
		// 写入不透明物体的最小深度值（最近的片元）和归一化后的法线
		ResetRenderTarget(_opaqueNormalId, _opaqueDepthId, true, true, 1, Color.black);
		
		ExecuteCurrentBuffer(context);
		
		// 渲染不透明物体的法线图和深度图
		_drawSettings.SetShaderPassName(0, ShaderTagManager.SRPDefaultUnlit);
		_drawSettings.overrideMaterial = opaqueDepthMaterial;
		filterSettings.renderQueueRange = RenderQueueRange.opaque;
		context.DrawRenderers(cull, ref _drawSettings, ref filterSettings);
		
		// 绑定渲染目标为相机
		context.SetupCameraProperties(camera);
		
		// 渲染不透明物体
		// 【暂时】使用无光照着色器
		sortingSettings.criteria = SortingCriteria.CommonOpaque;
		_drawSettings.overrideMaterial = null;
		_drawSettings.SetShaderPassName(0, ShaderTagManager.SRPDefaultUnlit);
		context.DrawRenderers(cull, ref _drawSettings, ref filterSettings);
		
		// 渲染天空盒
		if ((camera.clearFlags & CameraClearFlags.Skybox) != 0) context.DrawSkybox(camera);

		// 渲染半透明物体
		sortingSettings.criteria = SortingCriteria.CommonTransparent;
		_drawSettings.sortingSettings = sortingSettings;
		filterSettings.renderQueueRange = RenderQueueRange.transparent;
		context.DrawRenderers(cull, ref _drawSettings, ref filterSettings);
		
		_currentBuffer.Blit(ShaderManager.OPAQUE_DEPTH_TEXTURE, BuiltinRenderTextureType.CurrentActive, testMaterial);
		
		ExecuteCurrentBuffer(context);
		
		// 释放临时申请的贴图
		ReleaseRTs();
		
/*		
#if UNITY_EDITOR
		context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
#endif
*/		

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
		_currentBuffer.GetTemporaryRT(ShaderManager.TRANSPARENT_MIN_DEPTH_TEXTURE, pixelWidth, pixelHeight, 16, FilterMode.Point, RenderTextureFormat.Depth);
		_currentBuffer.GetTemporaryRT(ShaderManager.TRANSPARENT_MAX_DEPTH_TEXTURE, pixelWidth, pixelHeight, 16, FilterMode.Point, RenderTextureFormat.Depth);
		_currentBuffer.GetTemporaryRT(ShaderManager.OPAQUE_DEPTH_TEXTURE, pixelWidth, pixelHeight, 16, FilterMode.Point, RenderTextureFormat.Depth);
		_currentBuffer.GetTemporaryRT(ShaderManager.OPAQUE_NORMAL_TEXTURE, pixelWidth, pixelHeight, 0, FilterMode.Point, GraphicsFormat.R8G8B8A8_SNorm);
	}

	private void ReleaseRTs() {
		_currentBuffer.ReleaseTemporaryRT(ShaderManager.TRANSPARENT_MIN_DEPTH_TEXTURE);
		_currentBuffer.ReleaseTemporaryRT(ShaderManager.TRANSPARENT_MAX_DEPTH_TEXTURE);
		_currentBuffer.ReleaseTemporaryRT(ShaderManager.OPAQUE_DEPTH_TEXTURE);
		_currentBuffer.ReleaseTemporaryRT(ShaderManager.OPAQUE_NORMAL_TEXTURE);
		_currentBuffer.ReleaseTemporaryRT(ShaderManager.DEPTH_BOUND_TEXTURE);
	}
}