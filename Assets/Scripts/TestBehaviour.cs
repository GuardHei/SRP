using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class TestBehaviour : MonoBehaviour {

	public float intensity;

	private void OnValidate() {
		GetComponent<Light>().intensity = intensity;
	}
}