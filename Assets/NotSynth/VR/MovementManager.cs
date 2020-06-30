using System;
using UnityEngine;
using Valve.VR;

namespace NotSynth.VR {
	public class MovementManager : MonoBehaviour {


		public Transform lController;

		public Transform rController;

		public Transform hmdTrans;


		public Transform _vrPlayerHolder;

		[SerializeField] private float scaleSpeed = 1f;
		[SerializeField] private float gripStrength = 0.35f;
		


		private bool isScaling = false;
		private bool isDragging = false;
		private bool leftGripping = false;
		private bool rightGripping = false;
		private bool leftDragging = false;
		private bool rightDragging = false;

		//Scaling variables
		private float startDistance;
		private Vector3 startScale;
		private Vector3 startPos;
		private float startYRot;


		private Vector3 startDragPos;
		private Vector3 startDragControllerPos;
		private Transform draggingController;

		private void Update() {


			//leftGripping = SteamVR_Actions._default.Squeeze.GetAxis(SteamVR_Input_Sources.LeftHand) > gripStrength;

			//rightGripping = SteamVR_Actions._default.Squeeze.GetAxis(SteamVR_Input_Sources.RightHand) > gripStrength;

			
			HandleScaling();
			
			HandleDragMovement();
			
		}




		private void HandleScaling() {
			if (!isScaling && leftGripping && rightGripping) {
				EndDrag();
				BeginScaleChange();
			}

			if (isScaling && !leftGripping || !rightGripping) {
				isScaling = false;
			}

			if (isScaling) {
				float curDistance = Vector3.Distance(lController.localPosition, rController.localPosition);
				float changed = (curDistance - startDistance) * scaleSpeed * -1f;
				
				_vrPlayerHolder.localScale = new Vector3(startScale.x + changed, startScale.y + changed, startScale.z + changed);
				
				float rotChanged = ((lController.localEulerAngles.y + rController.localEulerAngles.y) / 2f) - startYRot;


				//Vector3 before = hmdTrans.position;
				//_vrPlayerHolder.localEulerAngles = new Vector3(0, rotChanged * scaleSpeed, 0);

				//Vector3 offset = hmdTrans.position - before;

				//_vrPlayerHolder.transform.position += offset;

			}
		}


		private void HandleDragMovement() {

			if (!isDragging && leftGripping && !rightGripping) {
				StartDrag(lController);
				leftDragging = true;
			}

			else if (!isDragging && !leftGripping && rightGripping) {
				StartDrag(rController);
				rightDragging = true;
			}

			if (leftDragging && !leftGripping) EndDrag();
			else if (rightDragging && !rightGripping) EndDrag();
	

			if (isDragging) {

				Vector3 offset = draggingController.transform.localPosition - startDragControllerPos;

				_vrPlayerHolder.transform.position = startDragPos - offset * _vrPlayerHolder.localScale.x;

			}
			
			
		}

		private void StartDrag(Transform controller) {
			startDragControllerPos = controller.transform.localPosition;
			startDragPos = _vrPlayerHolder.transform.position;
			draggingController = controller;
			isDragging = true;
		}

		private void EndDrag() {
			leftDragging = false;
			rightDragging = false;
			isDragging = false;
		}
		

		public void ResetScale() {
			_vrPlayerHolder.localScale = Vector3.one;
		}


		public void BeginScaleChange() {

			startDistance = Vector3.Distance(lController.localPosition, rController.localPosition);
			startScale = _vrPlayerHolder.localScale;
			startPos = hmdTrans.position;

			startYRot = (lController.localEulerAngles.y + rController.localEulerAngles.y) / 2f;

			isScaling = true;



		}


	}
}