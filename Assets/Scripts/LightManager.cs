using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.SocialPlatforms;
using UnityEngine.UI;

[ExecuteInEditMode]
public class LightManager : MonoBehaviour {

	public Color color;
	public float intensity;
	public float range;
	public bool draw;
	public Light[] lights;

	private void Awake() {
		lights = GetComponentsInChildren<Light>();
	}

	private void OnTransformChildrenChanged() {
		lights = GetComponentsInChildren<Light>();
	}

	private void OnValidate() {
		foreach (var light in lights) {
			light.color = color;
			light.intensity = intensity;
			light.range = range;
		}
	}

	private void OnDrawGizmosSelected() {
		if (lights == null && !draw) return;
		Gizmos.color = color;
		foreach (var light in lights) {
			Gizmos.DrawWireSphere(light.transform.position, light.range);
		}
	}
}