using UnityEngine;
using UnityEngine.InputSystem;

public class MouseMovement : MonoBehaviour
{
	public float senx;
	public float seny;
	public Transform orientation;
	public Transform playerBody;

	float xRotation;
	float yRotation;

	private void Start()
	{
		Cursor.lockState = CursorLockMode.Locked;
		Cursor.visible = false;
	}

	private void Update()
	{
		float mouseX = Mouse.current.delta.x.ReadValue() * senx * Time.deltaTime;
		float mouseY = Mouse.current.delta.y.ReadValue() * seny * Time.deltaTime;

		yRotation += mouseX;
		xRotation -= mouseY;
		xRotation = Mathf.Clamp(xRotation, -90f, 60f);

		transform.rotation = Quaternion.Euler(xRotation, yRotation, 0);
		orientation.rotation = Quaternion.Euler(0, yRotation, 0);
	}
}
