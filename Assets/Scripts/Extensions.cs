using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

public static class Extensions {
	
	public static void Resize(ref ComputeBuffer buffer, int newCapacity) {
		if (newCapacity <= buffer.count) return;
		newCapacity = Mathf.Max(newCapacity, (int) (buffer.count * 1.2f));
		var stride = buffer.stride;
		buffer.Dispose();
		buffer = new ComputeBuffer(newCapacity, stride);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Vector4 GetDirectionFromLocalTransform(this Matrix4x4 transform) {
		var direction = transform.GetColumn(2);
		direction.x = -direction.x;
		direction.y = -direction.y;
		direction.z = -direction.z;
		return direction;
	}

	public static Vector3 GetPositionFromLocalTransform(this Matrix4x4 transform) {
		var position = transform.GetColumn(3);
		return new Vector3(position.x, position.y, position.z);
	}

	public static float3 ToFloat3(this Color color) => new float3(color.r, color.g, color.b);

	public static float4 ToFloat4(this Color color) => new float4(color.a, color.r, color.g, color.b);
	
	public static float3 ToFloat3(this Vector4 v) => new float3(v.x, v.y, v.z);

	public static bool Exists(this Light light) => light != null && light.isActiveAndEnabled;
}