using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ImagePlaneStickyDisableEnableRigidOnPickup : MonoBehaviour {

	public ImagePlane_StickyHand hand;
	// Use this for initialization
	void Start () {
		hand.selectedObjectEvent.AddListener(setRigidKinematic);
		hand.droppedObject.AddListener(setRigidNotKinematic);
	}
	


	void setRigidKinematic() {
		if(hand.selectedObject == this.gameObject) {
			this.GetComponent<Rigidbody>().isKinematic = true;
		}
		
	}

	void setRigidNotKinematic() {
		if(hand.selectedObject == this.gameObject) {
			this.GetComponent<Rigidbody>().isKinematic = false;
		}
	}
}
