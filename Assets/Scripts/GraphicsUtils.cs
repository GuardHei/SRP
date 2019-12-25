using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public static class GraphicsUtils {

	public static Mesh FullScreenTriangle {
		get {
			if (fullScreenTriangle != null) return fullScreenTriangle;
			
			fullScreenTriangle = new Mesh { name = "Full Screen Triangle" };
			fullScreenTriangle.SetVertices(new List<Vector3> { new Vector3(-1, -1, 0), new Vector3(-1, 3, 0), new Vector3(3, -1, 0) });
			fullScreenTriangle.SetIndices(new [] { 0, 1, 2 }, MeshTopology.Triangles, 0, false);
			fullScreenTriangle.UploadMeshData(false);
			
			return fullScreenTriangle;
		}
	}

	public static Material CopyMaterial {
		get {
			if (copyMaterial != null) return copyMaterial;
			
			copyMaterial = new Material(Shader.Find("SRP/StandardBlit")) { name = "Copy", hideFlags = HideFlags.HideAndDontSave };
			
			return copyMaterial;
		}
	}

	private static Mesh fullScreenTriangle;
	private static Material copyMaterial;

	public static void BlitWithDepth(this CommandBuffer buffer, RenderTargetIdentifier source, RenderTargetIdentifier destination, RenderTargetIdentifier depth) => buffer.BlitWithDepth(source, destination, depth, CopyMaterial);

	public static void BlitWithDepth(this CommandBuffer buffer, RenderTargetIdentifier source, RenderTargetIdentifier destination, RenderTargetIdentifier depth, Material material, int pass = 0) {
		buffer.SetGlobalTexture(ShaderManager.MAIN_TEXTURE, source);
		buffer.SetRenderTarget(destination, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, depth, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
		buffer.DrawMesh(FullScreenTriangle, Matrix4x4.identity, material, 0, pass);
	}
}
