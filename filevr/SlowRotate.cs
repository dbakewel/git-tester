using UnityEngine;
using System.Collections;

public class SlowRotate : MonoBehaviour {
	public float speed = 1;
	public enum Axis {X, Y, Z};
	public Axis axis = Axis.Y;

	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {
		switch (axis) {
		case Axis.X:
			transform.Rotate (Vector3.right * Time.deltaTime * speed, Space.World);
			break;
		case Axis.Y:
			transform.Rotate (Vector3.up * Time.deltaTime * speed, Space.World);
			break;
		case Axis.Z:
			transform.Rotate (Vector3.forward * Time.deltaTime * speed, Space.World);
			break;
		}
	}
}
