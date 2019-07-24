using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UI;

public class TestBehaviour : MonoBehaviour {

	public Text text;
	
	private StringBuilder _stringBuilder = new StringBuilder("100");

	private void Awake() {
		_stringBuilder.Clear();

		_stringBuilder.AppendLine("Graphic Device is " + SystemInfo.graphicsDeviceName);
		_stringBuilder.AppendLine("Graphic API is " + SystemInfo.graphicsDeviceType);
		_stringBuilder.AppendLine("Shader Level is " + SystemInfo.graphicsShaderLevel);
		_stringBuilder.AppendLine("Graphic Memory is " + SystemInfo.graphicsMemorySize + "MB");

		_stringBuilder.AppendLine("Reversed ZBuffer is " + (SystemInfo.usesReversedZBuffer ? "On" : "Off"));
		_stringBuilder.AppendLine("Async Compute is " + (SystemInfo.supportsAsyncCompute ? "On" : "Off"));
		_stringBuilder.AppendLine("Async GPU Readback is " + (SystemInfo.supportsAsyncGPUReadback ? "On" : "Off"));
		_stringBuilder.AppendLine("Graphics Fence is " + (SystemInfo.supportsGraphicsFence ? "On" : "Off"));
		_stringBuilder.AppendLine("Sparse Texture is " + (SystemInfo.supportsSparseTextures ? "On" : "Off"));
		_stringBuilder.AppendLine("32 Bits Index Buffer is " + (SystemInfo.supports32bitsIndexBuffer ? "On" : "Off"));
		_stringBuilder.AppendLine("2D Array Texture is " + (SystemInfo.supports2DArrayTextures ? "On" : "Off"));
		_stringBuilder.AppendLine("Cubemap Array Texture is " + (SystemInfo.supportsCubemapArrayTextures ? "On" : "Off"));
		_stringBuilder.AppendLine("Constant Buffer is " + (SystemInfo.supportsSetConstantBuffer ? "On" : "Off"));
		_stringBuilder.AppendLine("Hardware Quad Topology is " + (SystemInfo.supportsHardwareQuadTopology ? "On" : "Off"));
		_stringBuilder.AppendLine("Raw Shadow Depth Sampling is " + (SystemInfo.supportsRawShadowDepthSampling ? "On" : "Off"));
		_stringBuilder.AppendLine("Top UV is " + (SystemInfo.graphicsUVStartsAtTop ? "On" : "Off"));

		foreach (var format in Enum.GetValues(typeof(TextureFormat))) {
			var f = (TextureFormat) format;
			if (IsObsolete(f)) continue;
			_stringBuilder.AppendLine("Texture Format [" + f + "] is " + (SystemInfo.SupportsTextureFormat(f) ? "On" : "Off"));
		}
		
		foreach (var format in Enum.GetValues(typeof(RenderTextureFormat))) {
			var f = (RenderTextureFormat) format;
			if (IsObsolete(f)) continue;
			_stringBuilder.AppendLine("Render Texture Format [" + f + "] is " + (SystemInfo.SupportsRenderTextureFormat(f) ? "On" : "Off"));
		}

		foreach (var format in Enum.GetValues(typeof(GraphicsFormat))) {
        	var f = (GraphicsFormat) format;
        	if (IsObsolete(f)) continue;
            _stringBuilder.AppendLine("Render Texture Format [" + f + "] Sample is " + (SystemInfo.IsFormatSupported(f, FormatUsage.Sample) ? "On" : "Off"));
        	_stringBuilder.AppendLine("Render Texture Format [" + f + "] Render is " + (SystemInfo.IsFormatSupported(f, FormatUsage.Render) ? "On" : "Off"));
        }

		text.text = "Finished";
		
		File.WriteAllText(Application.dataPath + "/Collected Data.txt", _stringBuilder.ToString());
	}
	
	public static bool IsObsolete(Enum value) {
		var fi = value.GetType().GetField(value.ToString());
		var attributes = (ObsoleteAttribute[]) fi.GetCustomAttributes(typeof(ObsoleteAttribute), false);
		return attributes.Length > 0;
	}
}