using System;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class ToolUtilities {

	[MenuItem("Tools/Processor Info")]
	public static void ProcessorInfo() {
		Debug.Log("Processor Count : " + SystemInfo.processorCount);
		Debug.Log("Processor Type : " + SystemInfo.processorType);
		Debug.Log("Processor Frequency : " + SystemInfo.processorFrequency + " MHz");
	}

	[MenuItem("Tools/Supported Render Texture Formats")]
	public static void SupportedRenderTextureFormats() {
		foreach (var format in Enum.GetValues(typeof(RenderTextureFormat))) CheckRenderTextureFormatSupport((RenderTextureFormat) format);
		Debug.Log("Cubemap Array Texture is " + (SystemInfo.supportsCubemapArrayTextures ? "supported" : "not supported"));
		Debug.Log("Async GPU Readback is " + (SystemInfo.supportsAsyncGPUReadback ? "supported" : "not supported"));
		Debug.Log("Async Compute is " + (SystemInfo.supportsAsyncCompute ? "supported" : "not supported"));
	}

	public static void CheckRenderTextureFormatSupport(RenderTextureFormat format) {
		Debug.Log(format + " is " + (SystemInfo.SupportsRenderTextureFormat(format) ? "supported" : "not supported"));
	}

	[MenuItem("Tools/Check ZBuffer State")]
	public static void CheckZBufferState() => Debug.Log("Reversed ZBuffer is " + (SystemInfo.usesReversedZBuffer ? "on" : "off"));
}