using UnityEngine.Rendering;

public static class ShaderTagManager {

	public static readonly ShaderTagId None = ShaderTagId.none;
	public static readonly ShaderTagId SRPDefaultUnlit = new ShaderTagId("SRPDefaultUnlit");
	public static readonly ShaderTagId DepthMin = new ShaderTagId("DepthMin");
	public static readonly ShaderTagId DepthMax = new ShaderTagId("DepthMax");
	public static readonly ShaderTagId DepthAndNormal = new ShaderTagId("DepthAndNormal");
}