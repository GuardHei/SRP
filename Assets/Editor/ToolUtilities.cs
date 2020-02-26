using System;
using Unity.Mathematics;
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

	public static void Compute3DGaussianKernelWeightsFake() {
		float sig = .15f;
		float3 origin = new float3(0, 0, 0);
		float3[] offsets = {
			new float3(1, 1, 1), new float3(1, -1, 1), new float3(-1, -1, 1), new float3(-1, 1, 1),
			new float3(1, 1, -1), new float3(1, -1, -1), new float3(-1, -1, -1), new float3(-1, 1, -1),
			new float3(1, 1, 0), new float3(1, -1, 0), new float3(-1, -1, 0), new float3(-1, 1, 0),
			new float3(1, 0, 1), new float3(-1, 0, 1), new float3(1, 0, -1), new float3(-1, 0, -1),
			new float3(0, 1, 1), new float3(0, -1, 1), new float3(0, -1, -1), new float3(0, 1, -1)
		};
		
		float[] weights = new float[offsets.Length];
		float total = 0;
		for (int i = 0; i < offsets.Length; i++) {
			float g = Compute3DGaussianKernel(sig, offsets[i]);
			weights[i] = g;
			total += g;
		}

		string display = "";

		for (int i = 0; i < offsets.Length; i++) {
			weights[i] /= total;
			display += weights[i] + ", ";
			if ((i + 1) % 4 == 0) display += "\n";
		}
		
		Debug.Log(display);
	}
	
	[MenuItem("Tools/Compute 3D Gaussian Kernel Weight")]
	public static void Compute3DGaussianKernelWeights() {
		float sig = .15f;
		float3 origin = new float3(0, 0, 0);
		float3[] offsets = {
			new float3(1, 0, 0), new float3(-1, 0, 0), new float3(0, 0, 1), new float3(0, 0, -1), new float3(1, 0, 1), new float3(1, 0, -1), new float3(-1, 0, 1), new float3(-1, 0, -1),
			new float3(0, 1, 0), new float3(1, 1, 0), new float3(-1, 1, 0), new float3(0, 1, 1), new float3(0, 1, -1), new float3(1, 1, 1), new float3(1, 1, -1), new float3(-1, 1, 1), new float3(-1, 1, -1),
			new float3(0, -1, 0), new float3(1, -1, 0), new float3(-1, -1, 0), new float3(0, -1, 1), new float3(0, -1, -1), new float3(1, -1, 1), new float3(1, -1, -1), new float3(-1, -1, 1), new float3(-1, -1, -1)
		};
		
		float[] weights = new float[offsets.Length];
		float total = 0;
		for (int i = 0; i < offsets.Length; i++) {
			float g = Compute3DGaussianKernel(sig, offsets[i]);
			weights[i] = g;
			total += g;
		}

		string display = "";
		float sum = 0;

		for (int i = 0; i < offsets.Length; i++) {
			weights[i] /= total;
			display += weights[i] + ", ";
			sum += weights[i];
			if ((i + 1) % 9 == 0) display += "\n";
		}
		
		Debug.Log(display);
		Debug.Log(sum);
	}

	public static float Compute3DGaussianKernel(float sig, float3 pos) {
		float e = 2.71828182845904523536028747135266249775724709369995f;
		float result = 1f / (Mathf.Pow(2f * Mathf.PI, 1.5f) * sig * sig * sig);
		result *= Mathf.Pow(e, -(pos.x * pos.x + pos.y * pos.y + pos.z * pos.z) / (2f * sig * sig));
		return result;
	}
}