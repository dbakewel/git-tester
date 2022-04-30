using UnityEngine;
using System.Collections;

public class ControllerEventListener : MonoBehaviour {

	public GameObject fileSystemRoot;

	public bool triggerDown = false;
	public bool gridDown = false;
	public bool menuDown = false;
	public Vector3 gripVector; //Vector from controller to center of filesystemroot;
	public float gripAngle;
	public Quaternion fileSystemRootRotation;

	private void Start()
	{
		if (GetComponent<VRTK.VRTK_ControllerEvents>() == null)
		{
			Debug.LogError("VRTK_ControllerEvents_ListenerExample is required to be attached to a Controller that has the VRTK_ControllerEvents script attached to it");
			return;
		}

		//Setup controller event listeners
		GetComponent<VRTK.VRTK_ControllerEvents>().TriggerPressed += new VRTK.ControllerInteractionEventHandler(DoTriggerPressed);
		GetComponent<VRTK.VRTK_ControllerEvents>().TriggerReleased += new VRTK.ControllerInteractionEventHandler(DoTriggerReleased);

		GetComponent<VRTK.VRTK_ControllerEvents>().TriggerTouchStart += new VRTK.ControllerInteractionEventHandler(DoTriggerTouchStart);
		GetComponent<VRTK.VRTK_ControllerEvents>().TriggerTouchEnd += new VRTK.ControllerInteractionEventHandler(DoTriggerTouchEnd);

		GetComponent<VRTK.VRTK_ControllerEvents>().TriggerHairlineStart += new VRTK.ControllerInteractionEventHandler(DoTriggerHairlineStart);
		GetComponent<VRTK.VRTK_ControllerEvents>().TriggerHairlineEnd += new VRTK.ControllerInteractionEventHandler(DoTriggerHairlineEnd);

		GetComponent<VRTK.VRTK_ControllerEvents>().TriggerClicked += new VRTK.ControllerInteractionEventHandler(DoTriggerClicked);
		GetComponent<VRTK.VRTK_ControllerEvents>().TriggerUnclicked += new VRTK.ControllerInteractionEventHandler(DoTriggerUnclicked);

		GetComponent<VRTK.VRTK_ControllerEvents>().TriggerAxisChanged += new VRTK.ControllerInteractionEventHandler(DoTriggerAxisChanged);

		GetComponent<VRTK.VRTK_ControllerEvents>().ApplicationMenuPressed += new VRTK.ControllerInteractionEventHandler(DoApplicationMenuPressed);
		GetComponent<VRTK.VRTK_ControllerEvents>().ApplicationMenuReleased += new VRTK.ControllerInteractionEventHandler(DoApplicationMenuReleased);

		GetComponent<VRTK.VRTK_ControllerEvents>().GripPressed += new VRTK.ControllerInteractionEventHandler(DoGripPressed);
		GetComponent<VRTK.VRTK_ControllerEvents>().GripReleased += new VRTK.ControllerInteractionEventHandler(DoGripReleased);

		GetComponent<VRTK.VRTK_ControllerEvents>().TouchpadPressed += new VRTK.ControllerInteractionEventHandler(DoTouchpadPressed);
		GetComponent<VRTK.VRTK_ControllerEvents>().TouchpadReleased += new VRTK.ControllerInteractionEventHandler(DoTouchpadReleased);

		GetComponent<VRTK.VRTK_ControllerEvents>().TouchpadTouchStart += new VRTK.ControllerInteractionEventHandler(DoTouchpadTouchStart);
		GetComponent<VRTK.VRTK_ControllerEvents>().TouchpadTouchEnd += new VRTK.ControllerInteractionEventHandler(DoTouchpadTouchEnd);

		GetComponent<VRTK.VRTK_ControllerEvents>().TouchpadAxisChanged += new VRTK.ControllerInteractionEventHandler(DoTouchpadAxisChanged);

		GetComponent<VRTK.VRTK_ControllerEvents>().ControllerEnabled += new VRTK.ControllerInteractionEventHandler(DoControllerEnabled);
		GetComponent<VRTK.VRTK_ControllerEvents>().ControllerDisabled += new VRTK.ControllerInteractionEventHandler(DoControllerDisabled);
	}

	private void DebugLogger(uint index, string button, string action, VRTK.ControllerInteractionEventArgs e)
	{
		//Debug.Log("Controller on index '" + index + "' " + button + " has been " + action
		//	+ " with a pressure of " + e.buttonPressure + " / trackpad axis at: " + e.touchpadAxis + " (" + e.touchpadAngle + " degrees)");
	}

	private void DoTriggerPressed(object sender, VRTK.ControllerInteractionEventArgs e)
	{
		DebugLogger(e.controllerIndex, "TRIGGER", "pressed", e);
	}

	private void DoTriggerReleased(object sender, VRTK.ControllerInteractionEventArgs e)
	{
		DebugLogger(e.controllerIndex, "TRIGGER", "released", e);
	}

	private void DoTriggerTouchStart(object sender, VRTK.ControllerInteractionEventArgs e)
	{
		DebugLogger(e.controllerIndex, "TRIGGER", "touched", e);
	}

	private void DoTriggerTouchEnd(object sender, VRTK.ControllerInteractionEventArgs e)
	{
		DebugLogger(e.controllerIndex, "TRIGGER", "untouched", e);
	}

	private void DoTriggerHairlineStart(object sender, VRTK.ControllerInteractionEventArgs e)
	{
		DebugLogger(e.controllerIndex, "TRIGGER", "hairline start", e);
	}

	private void DoTriggerHairlineEnd(object sender, VRTK.ControllerInteractionEventArgs e)
	{
		DebugLogger(e.controllerIndex, "TRIGGER", "hairline end", e);
	}

	private void DoTriggerClicked(object sender, VRTK.ControllerInteractionEventArgs e)
	{
		triggerDown = true;
		DebugLogger(e.controllerIndex, "TRIGGER", "clicked", e);
	}

	private void DoTriggerUnclicked(object sender, VRTK.ControllerInteractionEventArgs e)
	{
		triggerDown = false;
		DebugLogger(e.controllerIndex, "TRIGGER", "unclicked", e);
	}

	private void DoTriggerAxisChanged(object sender, VRTK.ControllerInteractionEventArgs e)
	{
		DebugLogger(e.controllerIndex, "TRIGGER", "axis changed", e);
	}

	private void DoApplicationMenuPressed(object sender, VRTK.ControllerInteractionEventArgs e)
	{
		menuDown = true;
		DebugLogger(e.controllerIndex, "APPLICATION MENU", "pressed down", e);
	}

	private void DoApplicationMenuReleased(object sender, VRTK.ControllerInteractionEventArgs e)
	{
		menuDown = false;
		DebugLogger(e.controllerIndex, "APPLICATION MENU", "released", e);
	}

	public void setControllerValues() {
		gripVector = this.transform.position - fileSystemRoot.transform.position;
		gripAngle = Mathf.Atan2 (
				this.transform.position.z - fileSystemRoot.transform.position.z, 
				this.transform.position.x - fileSystemRoot.transform.position.x
		) * Mathf.Rad2Deg;
		fileSystemRootRotation = fileSystemRoot.transform.rotation;
	}

	private void DoGripPressed(object sender, VRTK.ControllerInteractionEventArgs e)
	{
		gridDown = true;
		setControllerValues ();
		DebugLogger(e.controllerIndex, "GRIP", "pressed down", e);
	}

	private void DoGripReleased(object sender, VRTK.ControllerInteractionEventArgs e)
	{
		gridDown = false;
		DebugLogger(e.controllerIndex, "GRIP", "released", e);
	}

	private void DoTouchpadPressed(object sender, VRTK.ControllerInteractionEventArgs e)
	{
		DebugLogger(e.controllerIndex, "TOUCHPAD", "pressed down", e);
	}

	private void DoTouchpadReleased(object sender, VRTK.ControllerInteractionEventArgs e)
	{
		DebugLogger(e.controllerIndex, "TOUCHPAD", "released", e);
	}

	private void DoTouchpadTouchStart(object sender, VRTK.ControllerInteractionEventArgs e)
	{
		DebugLogger(e.controllerIndex, "TOUCHPAD", "touched", e);
	}

	private void DoTouchpadTouchEnd(object sender, VRTK.ControllerInteractionEventArgs e)
	{
		DebugLogger(e.controllerIndex, "TOUCHPAD", "untouched", e);
	}

	private void DoTouchpadAxisChanged(object sender, VRTK.ControllerInteractionEventArgs e)
	{
		DebugLogger(e.controllerIndex, "TOUCHPAD", "axis changed", e);
	}

	private void DoControllerEnabled(object sender, VRTK.ControllerInteractionEventArgs e)
	{
		DebugLogger(e.controllerIndex, "CONTROLLER STATE", "ENABLED", e);
	}

	private void DoControllerDisabled(object sender, VRTK.ControllerInteractionEventArgs e)
	{
		DebugLogger(e.controllerIndex, "CONTROLLER STATE", "DISABLED", e);
	}
}
