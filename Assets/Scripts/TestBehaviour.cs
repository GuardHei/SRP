using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class TestBehaviour : MonoBehaviour {

	public Transform money;
	public RectTransform ui;
	public Camera camera;
	public float duration;

	private bool _flying;
	private float _startTime;
	private Vector3 _startPosition;

	private void Awake() {
		Fly();
	}

	private void Update() {
		if (!_flying) return;

		float percentage = (Time.time - _startTime) / duration;
		Vector3 targetPosition = camera.ScreenToWorldPoint(new Vector3(ui.position.x, ui.position.y, camera.nearClipPlane));
		if (percentage >= 1) {
			money.position = targetPosition;
			_flying = false;
			return;
		}
		
		money.position = Vector3.LerpUnclamped(_startPosition, targetPosition, percentage);
	}

	public void Fly() {
		_flying = true;
		_startTime = Time.time;
		_startPosition = money.position;
	}
}