using UnityEngine;

public class ShaderManager {

	public static readonly int OPAQUE_DEPTH_TEXTURE = Shader.PropertyToID("_OpaqueDepthTexture");
	public static readonly int OPAQUE_NORMAL_TEXTURE = Shader.PropertyToID("_OpaqueNormalTexture");
	public static readonly int TRANSPARENT_MIN_DEPTH_TEXTURE = Shader.PropertyToID("_TransparentMinDepthTexture");
	public static readonly int TRANSPARENT_MAX_DEPTH_TEXTURE = Shader.PropertyToID("_TransparentMaxDepthTexture");
	public static readonly int DEPTH_BOUND_TEXTURE = Shader.PropertyToID("_DepthBoundTexture");
	public static readonly int DEPTH_FRUSTUM_TEXTURE = Shader.PropertyToID("_DepthFrustumTexture");
	public static readonly int CULLED_POINT_LIGHT_TEXTURE = Shader.PropertyToID("_CulledPointLightTexture");
	public static readonly int CULLED_SPOT_LIGHT_TEXTURE = Shader.PropertyToID("_CulledSpotLightTexture");
	public static readonly int TILE_NUMBER = Shader.PropertyToID("_TileNumber");
	public static readonly int CAMERA_FORWARD = Shader.PropertyToID("_CameraForward");
	public static readonly int CAMERA_POSITION = Shader.PropertyToID("_CameraPosition");
	public static readonly int Z_BUFFER_PARAMS = Shader.PropertyToID("_ZBufferParams");
	public static readonly int ALPHA_TEST_DEPTH_CUTOFF = Shader.PropertyToID("_AlphaTestDepthCutoff");
	public static readonly int SUNLIGHT_COLOR = Shader.PropertyToID("_SunlightColor");
	public static readonly int SUNLIGHT_DIRECTION = Shader.PropertyToID("_SunlightDirection");
	public static readonly int SUNLIGHT_SHADOW_BIAS = Shader.PropertyToID("_SunlightShadowBias");
	public static readonly int SUNLIGHT_SHADOW_DISTANCE = Shader.PropertyToID("_SunlightShadowDistance");
	public static readonly int SUNLIGHT_SHADOW_STRENGTH = Shader.PropertyToID("_SunlightShadowStrength");
	public static readonly int SUNLIGHT_SHADOWMAP = Shader.PropertyToID("_SunlightShadowmap");
	public static readonly int SUNLIGHT_SHADOWMAP_ARRAY = Shader.PropertyToID("_SunlightShadowmapArray");
	public static readonly int SUNLIGHT_INVERSE_VP = Shader.PropertyToID("sunlight_InverseVP");
	public static readonly int SUNLIGHT_INVERSE_VP_ARRAY = Shader.PropertyToID("sunlight_InverseVPArray");
	public static readonly int SUNLIGHT_SHADOW_SPLIT_BOUND_ARRAY = Shader.PropertyToID("_SunlightShadowSplitBoundArray");
	public static readonly int INVERSE_VP = Shader.PropertyToID("_InverseVP");
	public static readonly int POINT_LIGHT_COUNT = Shader.PropertyToID("_PointLightCount");
	public static readonly int SPOT_LIGHT_COUNT = Shader.PropertyToID("_SpotLightCount");
	public static readonly int POINT_LIGHT_BUFFER = Shader.PropertyToID("_PointLightBuffer");
	public static readonly int SPOT_LIGHT_BUFFER = Shader.PropertyToID("_SpotLightBuffer");
}