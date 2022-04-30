using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ClearTextTimer : MonoBehaviour {
	bool counting = false;
	float countDown;
	
	// Update is called once per frame
	void Update () {
		if (counting) {
			countDown -= Time.deltaTime;
			if (countDown < 0) {
				counting = false;
				this.GetComponent<Text> ().text = "";
			}
		}
	}

	public void clearInSec(float s) {
		counting = true;
		countDown = s;
	}
}
