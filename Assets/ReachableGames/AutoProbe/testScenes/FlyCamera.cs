//-------------------
// Copyright 2019
// Reachable Games, LLC
//-------------------

using UnityEngine;

namespace ReachableGames
{
	namespace AutoProbe
	{
		public class FlyCamera : MonoBehaviour
		{
			public float Speed = 1.0f;
			public float MouseSensitivity = 1.0f;
			public bool InvertMouse = false;

			// Start off in fly mode
			private void Start()
			{
				Cursor.visible = false;
				Cursor.lockState = CursorLockMode.Locked;
			}

			void Update()
			{
				if (Cursor.visible)
				{
					if (Input.GetKeyDown(KeyCode.BackQuote))
					{
						Cursor.visible = false;
						Cursor.lockState = CursorLockMode.Locked;
					}
				}
				else
				{
					Vector3 movement = Vector3.zero;
					if (Input.GetKey(KeyCode.W))
						movement.z += 1.0f;
					if (Input.GetKey(KeyCode.S))
						movement.z += -1.0f;
					if (Input.GetKey(KeyCode.A))
						movement.x += -1.0f;
					if (Input.GetKey(KeyCode.D))
						movement.x += 1.0f;
					transform.localPosition += transform.TransformVector(movement * Speed * Time.deltaTime);

					float pitch = Input.GetAxis("Mouse Y") * MouseSensitivity * (InvertMouse ? -1.0f : 1.0f);
					float yaw = Input.GetAxis("Mouse X") * MouseSensitivity;

					Vector3 angles = transform.localEulerAngles;
					angles.x = Mathf.Clamp(MakeRelative(angles.x) + pitch, -89.9999f, 89.9999f);
					angles.y = MakeRelative(angles.y) + yaw;
					angles.z = 0.0f;
					transform.localEulerAngles = angles;

					if (Input.GetKeyDown(KeyCode.BackQuote))
					{
						Cursor.visible = true;
						Cursor.lockState = CursorLockMode.None;
					}
				}
			}

			float MakeRelative(float euler)
			{
				float normalized = euler - Mathf.Floor(euler / 360.0f) * 360.0f;  // 0..360 range
				float relative = euler - (normalized > 180.0f ? 360.0f : 0.0f);  // -180..180 range
				return relative;
			}
		}
	}
}