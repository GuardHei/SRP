using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class TestBehaviour : MonoBehaviour {

	public Shader shader;

	private void OnValidate() {
		if (shader == null) return;
		Debug.Log(shader.passCount);
	}
}