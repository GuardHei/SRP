using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;

public class ShaderManager {

	// Properties
	public static readonly int COLOR_BUFFER = Shader.PropertyToID("_ColorBuffer");
	public static readonly int TEMPORARY_TEXTURE_1 = Shader.PropertyToID("_TemporaryTexture1");
	public static readonly int TEMPORARY_TEXTURE_2 = Shader.PropertyToID("_TemporaryTexture2");
	public static readonly int BLIT_TEMPORARY_TEXTURE_1 = Shader.PropertyToID("_BlitTemporaryTexture1");
	public static readonly int MAIN_TEXTURE = Shader.PropertyToID("_MainTex");
	public static readonly int DEPTH_TEXTURE = Shader.PropertyToID("_DepthTexture");
	public static readonly int OPAQUE_DEPTH_TEXTURE = Shader.PropertyToID("_OpaqueDepthTexture");
	public static readonly int OPAQUE_NORMAL_TEXTURE = Shader.PropertyToID("_OpaqueNormalTexture");
	public static readonly int DEPTH_MASK_TEXTURE = Shader.PropertyToID("_DepthMaskTexture");
	public static readonly int DEPTH_BOUND_TEXTURE = Shader.PropertyToID("_DepthBoundTexture");
	public static readonly int DEPTH_FRUSTUM_TEXTURE = Shader.PropertyToID("_DepthFrustumTexture");
	public static readonly int CULLED_POINT_LIGHT_TEXTURE = Shader.PropertyToID("_CulledPointLightTexture");
	public static readonly int CULLED_SPOT_LIGHT_TEXTURE = Shader.PropertyToID("_CulledSpotLightTexture");
	public static readonly int TILE_NUMBER = Shader.PropertyToID("_TileNumber");
	public static readonly int CAMERA_FORWARD = Shader.PropertyToID("_CameraForward");
	public static readonly int CAMERA_POSITION = Shader.PropertyToID("_CameraPosition");
	public static readonly int STENCIL_REF = Shader.PropertyToID("_StencilRef");
	public static readonly int Z_BUFFER_PARAMS = Shader.PropertyToID("_ZBufferParams");
	public static readonly int ALPHA_TEST_DEPTH_CUTOFF = Shader.PropertyToID("_AlphaTestDepthCutoff");
	public static readonly int BLUR_RADIUS = Shader.PropertyToID("_BlurRadius");
	public static readonly int SHADOW_BIAS = Shader.PropertyToID("_ShadowBias");
	public static readonly int SHADOW_NORMAL_BIAS = Shader.PropertyToID("_ShadowNormalBias");
	public static readonly int LIGHT_POS = Shader.PropertyToID("_LightPos");
	public static readonly int SUNLIGHT_COLOR = Shader.PropertyToID("_SunlightColor");
	public static readonly int SUNLIGHT_DIRECTION = Shader.PropertyToID("_SunlightDirection");
	public static readonly int SUNLIGHT_SHADOW_DISTANCE = Shader.PropertyToID("_SunlightShadowDistance");
	public static readonly int SUNLIGHT_SHADOW_STRENGTH = Shader.PropertyToID("_SunlightShadowStrength");
	public static readonly int SUNLIGHT_SHADOWMAP = Shader.PropertyToID("_SunlightShadowmap");
	public static readonly int SUNLIGHT_SHADOWMAP_SIZE = Shader.PropertyToID("_SunlightShadowmapSize");
	public static readonly int SUNLIGHT_SHADOWMAP_ARRAY = Shader.PropertyToID("_SunlightShadowmapArray");
	public static readonly int SUNLIGHT_INVERSE_VP = Shader.PropertyToID("sunlight_InverseVP");
	public static readonly int SUNLIGHT_INVERSE_VP_ARRAY = Shader.PropertyToID("sunlight_InverseVPArray");
	public static readonly int SUNLIGHT_SHADOW_SPLIT_BOUND_ARRAY = Shader.PropertyToID("_SunlightShadowSplitBoundArray");
	public static readonly int UNITY_MATRIX_V = Shader.PropertyToID("unity_MatrixV");
	public static readonly int UNITY_INVERSE_P = Shader.PropertyToID("unity_InverseP");
	public static readonly int UNITY_INVERSE_VP = Shader.PropertyToID("unity_InverseVP");
	public static readonly int POINT_LIGHT_COUNT = Shader.PropertyToID("_PointLightCount");
	public static readonly int SPOT_LIGHT_COUNT = Shader.PropertyToID("_SpotLightCount");
	public static readonly int POINT_LIGHT_BUFFER = Shader.PropertyToID("_PointLightBuffer");
	public static readonly int SPOT_LIGHT_BUFFER = Shader.PropertyToID("_SpotLightBuffer");
	public static readonly int POINT_LIGHT_SHADOWMAP_SIZE = Shader.PropertyToID("_PointLightShadowmapSize");
	public static readonly int POINT_LIGHT_SHADOWMAP = Shader.PropertyToID("_PointLightShadowmap");
	public static readonly int POINT_LIGHT_SHADOWMAP_ARRAY = Shader.PropertyToID("_PointLightShadowmapArray");
	public static readonly int SPOT_LIGHT_SHADOWMAP_SIZE = Shader.PropertyToID("_SpotLightShadowmapSize");
	public static readonly int SPOT_LIGHT_SHADOWMAP_ARRAY = Shader.PropertyToID("_SpotLightShadowmapArray");
	public static readonly int SPOT_LIGHT_INVERSE_VP_BUFFER = Shader.PropertyToID("spotLight_InverseVPBuffer");

	// Keywords
	public const string SUNLIGHT_SHADOWS = "_SUNLIGHT_SHADOWS";
	public const string SUNLIGHT_SOFT_SHADOWS = "_SUNLIGHT_SOFT_SHADOWS";
	public const string POINT_LIGHT = "_POINT_LIGHT";
	public const string POINT_LIGHT_SHADOWS = "_POINT_LIGHT_SHADOWS";
	public const string POINT_LIGHT_SOFT_SHADOWS = "_POINT_LIGHT_SOFT_SHADOWS";
	public const string SPOT_LIGHT = "_SPOT_LIGHT";
	public const string SPOT_LIGHT_SHADOWS = "_SPOT_LIGHT_SHADOWS";
	public const string SPOT_LIGHT_SOFT_SHADOWS = "_SPOT_LIGHT_SOFT_SHADOWS";
	
	// Custom Render Queues
	public const int OPAQUE_RENDER_QUEUE = 2000;
	public const int ALPHA_TEST_RENDER_QUEUE = 2450;
	public const int DITHER_TRANSPARENT_RENDER_QUEUE = 2700;
	public const int TRANSPARENT_QUEUE = 3000;
	
	// Render Queue Ranges
	public static readonly RenderQueueRange OPAQUE_RENDER_QUEUE_RANGE = new RenderQueueRange(OPAQUE_RENDER_QUEUE, ALPHA_TEST_RENDER_QUEUE - 1);
	public static readonly RenderQueueRange ALPHA_TEST_QUEUE_RANGE = new RenderQueueRange(ALPHA_TEST_RENDER_QUEUE, TRANSPARENT_QUEUE - 1);
	public static readonly RenderQueueRange DITHER_TRANSPARENT_QUEUE_RANGE = new RenderQueueRange(DITHER_TRANSPARENT_RENDER_QUEUE, TRANSPARENT_QUEUE - 1);
	public static readonly RenderQueueRange NON_TRANSPARENT_RENDER_QUEUE = new RenderQueueRange(OPAQUE_RENDER_QUEUE, TRANSPARENT_QUEUE - 1);
}