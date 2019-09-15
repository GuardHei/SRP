using UnityEngine.Rendering;

public static class ShaderTagManager {

	public static readonly ShaderTagId NONE = ShaderTagId.none;
	public static readonly ShaderTagId SRP_DEFAULT_UNLIT = new ShaderTagId("SRPDefaultUnlit");
	public static readonly ShaderTagId DEPTH_NORMAL = new ShaderTagId("DepthNormal");
}