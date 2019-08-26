using System.Runtime.CompilerServices;
using UnityEngine;

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
}