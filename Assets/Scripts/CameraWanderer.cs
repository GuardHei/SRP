using UnityEngine;

public class CameraWanderer : MonoBehaviour {

    public float walkSpeed = 1f;
    public float runSpeed = 2f;
    public float sensitivity = 1f;
    [HideInInspector]
    public Vector3 lastPos;

    private void Awake() => lastPos = transform.position;

    private void Update() {
        var speed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;
        if (Input.GetKey(KeyCode.W)) transform.Translate(Vector3.forward * speed * Time.deltaTime);
        if (Input.GetKey(KeyCode.S)) transform.Translate(Vector3.back * speed * Time.deltaTime);
        if (Input.GetKey(KeyCode.A)) transform.Translate(Vector3.left * speed * Time.deltaTime);
        if (Input.GetKey(KeyCode.D)) transform.Translate(Vector3.right * speed * Time.deltaTime);

        if (Input.GetKey(KeyCode.Mouse0)) {
            var dPos = Input.mousePosition - lastPos;
            transform.Rotate(new Vector3(-dPos.y * sensitivity, dPos.x * sensitivity, 0));
            var x = transform.rotation.eulerAngles.x;
            var y = transform.rotation.eulerAngles.y;
            transform.rotation = Quaternion.Euler(x, y, 0);
        }

        lastPos = Input.mousePosition;
    }
}