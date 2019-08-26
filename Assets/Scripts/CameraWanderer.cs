using UnityEngine;

public class CameraWanderer : MonoBehaviour {

    public float speed = 1f;
    public float sensitivity = 1f;
    public Vector3 lastPos;

    private void Awake() => lastPos = transform.position;

    private void Update() {
        if (Input.GetKey(KeyCode.W)) transform.Translate(Vector3.forward * speed * Time.deltaTime);
        if (Input.GetKey(KeyCode.S)) transform.Translate(Vector3.back * speed * Time.deltaTime);
        if (Input.GetKey(KeyCode.A)) transform.Translate(Vector3.left * speed * Time.deltaTime);
        if (Input.GetKey(KeyCode.D)) transform.Translate(Vector3.right * speed * Time.deltaTime);

        if (Input.GetKey(KeyCode.Mouse0)) {
            var dPos = Input.mousePosition - lastPos;
            transform.Rotate(new Vector3(-dPos.y * sensitivity, dPos.x * sensitivity, 0));
            float x = transform.rotation.eulerAngles.x;
            float y = transform.rotation.eulerAngles.y;
            transform.rotation = Quaternion.Euler(x, y, 0);
        }

        lastPos = Input.mousePosition;
    }
}