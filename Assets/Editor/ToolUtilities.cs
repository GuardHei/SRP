using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

public class ToolUtilities {

	[MenuItem("Tools/Supported Render Texture Formats")]
	public static void SupportedRenderTextureFormats() {
		foreach (var format in Enum.GetValues(typeof(RenderTextureFormat))) CheckRenderTextureFormatSupport((RenderTextureFormat) format);
	}

	public static void CheckRenderTextureFormatSupport(RenderTextureFormat format) {
		Debug.Log(format + " is " + (SystemInfo.SupportsRenderTextureFormat(format) ? "supported" : "not supported"));
	}

	[MenuItem("Tools/Check ZBuffer State")]
	public static void CheckZBufferState() {
		Debug.Log("Reversed ZBuffer is " + (SystemInfo.usesReversedZBuffer ? "on" : "off"));
	}
}