using UnityEngine;

public class FramerateCounter : MonoBehaviour {

	public int frameCount;
	public float dt;
	public float fps;
	public float updateRate;

	private void OnGUI() {
		GUI.Label(new Rect(50, 50, 200, 100), fps.ToString());
	}

	private void Update() {
		frameCount++;
		dt += Time.deltaTime;
		var rate = 1f / updateRate;
		if (dt > rate) {
			fps = frameCount / dt;
			frameCount = 0;
			dt -= rate;
		}
	}
}