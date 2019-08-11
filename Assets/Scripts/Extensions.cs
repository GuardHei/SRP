using UnityEngine;

public static class Extensions {
	public static void Resize(ref ComputeBuffer buffer, int newCapacity) {
		if (newCapacity <= buffer.count) return;
		newCapacity = Mathf.Max(newCapacity, (int) (buffer.count * 1.2f));
		var stride = buffer.stride;
		buffer.Dispose();
		buffer = new ComputeBuffer(newCapacity, stride);
	}
}