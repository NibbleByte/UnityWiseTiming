using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

namespace DevLocker.GFrame.Timing
{
	/// <summary>
	/// Simple component to update the <see cref="WiseTiming"/> coroutines with Unity deltaTime,
	/// or you can call <see cref="UpdateTime(float)"/> manually.
	///
	/// Use <see cref="TimeSpeedMultiplier"/> to scale the time progressing speed (make it slow-mo or faster).
	/// Can have child components that will be automatically updated by the parent with relative scaled deltaTime.
	/// </summary>
	public class WiseTimingComponent : MonoBehaviour
	{
		public WiseTiming Timing = new WiseTiming();

		[Tooltip("Scale the time progressing speed (make it slow-mo or faster).")]
		public float TimeSpeedMultiplier = 1f;

		[Tooltip("Should it automatically progress time using Unity Time.deltaTime in the well-known Update() method.\n" +
			"Turn off if you want to call it yourself with custom deltaTime.\n" +
			"This behaviour is overriden when the component has a parent that controls it.")]
		public bool AutomaticUpdateWithUnityTime = true;

		/// <summary>
		/// Child components controlled by this one.
		/// <see cref="UpdateTime(float)"/> will update all child components with relative scaled deltaTime.
		/// </summary>
		public IReadOnlyList<WiseTimingComponent> ChildComponents => m_ChildComponents.AsReadOnly();
		private List<WiseTimingComponent> m_ChildComponents = new List<WiseTimingComponent>();

		[SerializeField]
		[Tooltip("Parent component that controls it's children update with scaled time, achieving parent-to-child relative time scale.")]
		private WiseTimingComponent m_Parent;

		/// <summary>
		/// Parent component that controls it's children update with scaled time, achieving parent-to-child relative time scale.
		/// </summary>
		public WiseTimingComponent Parent {
			get => m_Parent;
			set {
				if (m_Parent) {
					m_Parent.m_ChildComponents.Remove(this);
				}

				m_Parent = value;

				if (m_Parent) {
					m_Parent.m_ChildComponents.Add(this);
				}
			}
		}

		private void Reset()
		{
			if (transform.parent) {
				m_Parent = transform.parent.GetComponentInParent<WiseTimingComponent>();

#if UNITY_EDITOR
				if (m_Parent) {
					UnityEditor.EditorUtility.SetDirty(this);
				}
#endif
			}
		}

		void Awake()
		{
			// Assigned in the inspector.
			if (m_Parent && !m_Parent.m_ChildComponents.Contains(this)) {
				m_Parent.m_ChildComponents.Add(this);
			}
		}

		void OnDestroy()
		{
			// Unregister from parent.
			if (Parent) {
				Parent = null;
			}

			while(m_ChildComponents.Count > 0) {
				m_ChildComponents[0].Parent = null;
			}

			Timing = null;
		}

		void Update()
		{
			if (AutomaticUpdateWithUnityTime && Parent == null) {
				UpdateTime(Time.deltaTime);
			}
		}


		public void UpdateTime(float deltaTime)
		{
			Timing.UpdateCoroutines(deltaTime * TimeSpeedMultiplier);

			foreach (WiseTimingComponent child in m_ChildComponents.ToList()) {

				// I may get destroyed during execution?
				if (this == null)
					return;

				// May get destroyed during execution.
				if (child == null)
					continue;

				if (child.isActiveAndEnabled) {
					child.UpdateTime(deltaTime * TimeSpeedMultiplier);
				}

			}
		}

		#region Wrapper Methods

		/// <summary>
		/// Start a coroutine. It will be updated on <see cref="UpdateCoroutines(float)"/>.
		/// </summary>
		/// <param name="source">Source object is used for tracking, debugging and also automatically stops the coroutine if the source dies(if it is <see cref = "UnityEngine.Object" />).</param>
		public WiseCoroutine StartCoroutine(IEnumerator routine, object source, WiseTiming.ExceptionHandlingDelegate exceptionHandler = null)
			=> Timing.StartCoroutine(routine, source, exceptionHandler);

		/// <summary>
		/// Check if coroutine is still running.
		/// </summary>
		public bool IsCoroutineAlive(WiseCoroutine coroutine) => Timing.IsCoroutineAlive(coroutine);

		/// <summary>
		/// Stop started coroutine.
		/// Returns true if coroutine was succesfully stopped.
		/// </summary>
		public bool StopCoroutine(WiseCoroutine coroutine) => Timing.StopCoroutine(coroutine);

		/// <summary>
		/// Stop all coroutines starte from provided source object.
		/// </summary>
		public void StopCoroutineBySource(object source) => Timing.StopCoroutineBySource(source);

		/// <summary>
		/// Stop all coroutines.
		/// </summary>
		public new void StopAllCoroutines() => Timing.StopAllCoroutines();

		#endregion
	}
}