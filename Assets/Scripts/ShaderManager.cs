using UnityEngine;

public class ShaderManager {

	public static readonly int OPAQUE_DEPTH_TEXTURE = Shader.PropertyToID("_OpaqueDepthTexture");
	public static readonly int OPAQUE_NORMAL_TEXTURE = Shader.PropertyToID("_OpaqueNormalTexture");
	public static readonly int TRANSPARENT_MIN_DEPTH_TEXTURE = Shader.PropertyToID("_TransparentMinDepthTexture");
	public static readonly int TRANSPARENT_MAX_DEPTH_TEXTURE = Shader.PropertyToID("_TransparentMaxDepthTexture");
	public static readonly int DEPTH_BOUND_TEXTURE = Shader.PropertyToID("_DepthBoundTexture");
}