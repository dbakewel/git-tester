using UnityEngine;
using System.Collections;

//Allows pointing a x,z plane. Attach to controller.

public class PlanePointer : MonoBehaviour {
	[Tooltip("File Pointer if selected, else Direcotry Pointer.")]
	public bool FilePointer = true; //true then we are pointing at files and dir. false then we are only pointng at dirs.
	public GameObject plane;
	public GameController gameController;
	public Material hitMaterial;
	public Material missMaterial;

	private float pointerThickness = 0.002f;
	private float pointerLength = 100f;
	private float pointerXRotation = 60;

	private GameObject pointerHolder;
	private GameObject pointer;

	void OnEnable()
	{
		pointerHolder = new GameObject("PlanePointerHolder");
		pointerHolder.transform.parent = transform;
		pointerHolder.transform.localPosition = Vector3.zero;

		pointer = GameObject.CreatePrimitive(PrimitiveType.Cube);
		pointer.transform.name = string.Format("PlanePointer");
		pointer.transform.parent = pointerHolder.transform;
		pointer.GetComponent<BoxCollider>().isTrigger = true;
		pointer.AddComponent<Rigidbody>().isKinematic = true;

		pointerHolder.transform.localRotation = Quaternion.Euler (pointerXRotation, 0, 0);

		SetPointerTransform(pointerLength, pointerThickness);
	}

	void OnDisable()
	{
		if (pointerHolder != null)
		{
			Destroy(pointerHolder);
		}
	}

	void Update()
	{
		//note, plane is assumed to only be rotated around y and nothing else;
		Plane p = new Plane(plane.transform.position,plane.transform.position + new Vector3(1,0,0),plane.transform.position + new Vector3(0,0,1));

		Ray r = new Ray(pointerHolder.transform.position, pointerHolder.transform.rotation * Vector3.forward);
		float rayDistance;
		if (p.Raycast (r, out rayDistance)) {
			SetPointerTransform(rayDistance, pointerThickness);

			Vector3 hitPoint = plane.transform.InverseTransformPoint (r.GetPoint (rayDistance));
			float hitX = hitPoint.x;
			float hitZ = hitPoint.z;
			float hitAngle = Mathf.Atan2 (hitPoint.z,hitPoint.x) * Mathf.Rad2Deg;
			//The code below is not needed because we make everything only from -180 to 180 degrees.
			//if (hitAngle < 0)
			//	hitAngle = 360 + hitAngle;
			float hitRadius = hitPoint.magnitude;
			setHit(gameController.pointedAt (hitX, hitZ, hitAngle, hitRadius, FilePointer));
		} else {
			setHit(false);
			SetPointerTransform(pointerLength, pointerThickness);
		}
	}

	public void setHit(bool hit) {
		if(hit)
			pointer.GetComponent<Renderer> ().material = hitMaterial;
		else
			pointer.GetComponent<Renderer> ().material = missMaterial;
	}

	private void SetPointerTransform(float setLength, float setThicknes)
	{
		//if the additional decimal isn't added then the beam position glitches
		var beamPosition = setLength / (2 + 0.00001f);

		pointer.transform.localScale = new Vector3(setThicknes, setThicknes, setLength);
		pointer.transform.localPosition = new Vector3(0f, 0f, beamPosition);
	}
}
