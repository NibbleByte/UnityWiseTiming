#if USE_UNITY && UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DevLocker.GFrame.Timing
{
	/// <summary>
	/// Sample behaviour showing how to use <see cref="WiseTimingComponent"/>.
	/// Check out the scene in the same folder.
	/// </summary>
	internal class WiseTiming_SampleTester : MonoBehaviour
	{
		public const float Speed = 5f;

		public WiseTimingComponent TimingComponent;

		void Start ()
		{
			// NOTE: both WiseTimingComponent in the scene have different speed multiplier so one cube will be slower.
			//		 Also check the root timing component - changing it's multiplier will affect the child timings as well.
			//
			//		 The WiseTimingComponent is just wrapper of the WiseTiming class - you don't have to use the component.
			TimingComponent.StartCoroutine(DoSampleMovement(), this);
		}

		IEnumerator DoSampleMovement()
		{
			yield return null;						// Skip a frame

			yield return Rotate180();				// Wait for this method to finish.

			yield return new WaitForSeconds(1f);    // Suspens...

			var targetPos = transform.position + new Vector3(10f, 0f, 0f);

			// Move towards the target.
			while (Vector3.Distance(transform.position, targetPos) > 0.001f) {
				yield return null;  // yield till next frame.

				var step = Speed * WiseTiming.DeltaTime;  // DeltaTime is available ONLY inside WiseTiming update.

				transform.position = Vector3.MoveTowards(transform.position, targetPos, step);
			}
		}

		IEnumerator Rotate180()
		{
			float rotateAngle = 0f;
			while (rotateAngle < 180) {

				rotateAngle += 50 * Speed * WiseTiming.DeltaTime;  // DeltaTime is available ONLY inside WiseTiming update.
				transform.localEulerAngles = new Vector3(0f, rotateAngle, 0f);

				yield return null;
			}
		}
	}

}

#endif