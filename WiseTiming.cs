using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

#if USE_UNITY
using UnityEngine;
#endif

namespace DevLocker.GFrame.Timing
{
	/// <summary>
	/// Coroutine handle that can be used for checks and stopping.
	/// </summary>
	public interface WiseCoroutine
	{
		/// <summary>
		/// Object that started the coroutine.
		/// </summary>
		object Source { get; }

		/// <summary>
		/// What should happen to currently active coroutine if the source is <see cref="Behaviour"/> and
		/// it is inactive (<see cref="Behaviour.isActiveAndEnabled"/>).
		/// </summary>
		WiseTiming.SourceInactiveBehaviour InactiveBehaviour { get; set; }

		/// <summary>
		/// Next scheduled time in seconds for update.
		/// </summary>
		float NextUpdateTime { get; }

		/// <summary>
		/// Next scheduled time in milliseconds for update.
		/// </summary>
		long NextUpdateTimeInMilliseconds { get; }

		/// <summary>
		/// Use this field to pause a coroutine (prevent it from updating).
		/// </summary>
		bool IsPaused { get; set; }

		/// <summary>
		/// Since you can't return values with coroutines,
		/// this is a place to put your results in. Use it in any way you want.
		///
		/// Example:
		///		WiseTiming.CurrentCoroutine.ResultData = someData;
		/// </summary>
		object ResultData { get; set; }

		/// <summary>
		/// Provide delegate to resolve coroutine exceptions.
		/// </summary>
		WiseTiming.ExceptionHandlingDelegate ExceptionHandling { set; }

		/// <summary>
		/// Debug info about the Coroutine.
		/// Check out <see cref="WiseTiming.DebugInfo_RecordCallstack"/>.
		/// </summary>
		WiseTiming.DebugInfo DebugInfo { get; }

		/// <summary>
		/// Returns the entry names of the current iterators stack.
		/// This can help you trace down the iterator calls back to the initial one.
		/// Use together with the <see cref="DebugInfo"/> and <see cref="WiseTiming.DebugInfo_RecordCallstack"/>.
		/// </summary>
		List<string> GetDebugStackNames();
	}

	/// <summary>
	/// Suspends the coroutine execution for the given amount of milliseconds.
	/// </summary>
	public sealed class WaitForMilliseconds
	{
		public readonly long Milliseconds;

		public WaitForMilliseconds(long miliseconds)
		{
			Milliseconds = miliseconds;
		}
	}

	/// <summary>
	/// DIY coroutines that users can update manually with their own deltaTime.
	/// Mostly compatible with Unity yield instructions and workflow. Just start the coroutines with this instance <see cref="StartCoroutine(IEnumerator, object)"/>, instead of Unity <see cref="MonoBehaviour.StartCoroutine(IEnumerator)"/>
	///
	/// Source object is used for tracking, debugging and also automatically stops the coroutine if the source dies (if it is <see cref="UnityEngine.Object"/>).
	/// </summary>
	[DebuggerNonUserCode]
	public class WiseTiming
	{
		// NOTE: to debug this class, remove the "[DebuggerNonUserCode]" attribute above, or disable "Just My Code" feature of Visual Studio.

		#region Helper Type Declarations

		/// <summary>
		/// What should happen to currently active coroutine if the source is <see cref="Behaviour"/> and
		/// it is inactive (<see cref="Behaviour.isActiveAndEnabled"/>).
		/// </summary>
		public enum SourceInactiveBehaviour
		{
			StopCoroutine,
			SkipAndResumeWhenActive,
			KeepExecuting,
		}

		/// <summary>
		/// What should happen on coroutine exception.
		/// </summary>
		public enum ExceptionHandlingAction
		{
			/// <summary>
			/// Let the exception propagate up the call chain and interrupt the update.
			/// Coroutine will be stopped to prevent other exceptions.
			/// This is the default behaviour.
			/// </summary>
			PropagateException,

			/// <summary>
			/// Catch the exception and stop the coroutine.
			/// </summary>
			CatchAndStopCoroutine,

			/// <summary>
			/// Catch the exception and resume coroutine with the caller of the current iterator (pop the stack and resume).
			/// WARNING: this may cause more exceptions in the following user code.
			/// </summary>
			CatchPopAndResumeCoroutine,
		}

		public delegate ExceptionHandlingAction ExceptionHandlingDelegate(Exception ex);
		public delegate void ExceptionEventHandler(Exception ex, WiseCoroutine coroutine);
		public delegate void CoroutineEventHandler(WiseCoroutine coroutine);
		public delegate void UpdateEventHandler();

		[DebuggerNonUserCode]
		public struct DebugInfo
		{
			public long CreatedTimeInMilliseconds;
			public float CreatedUnityTime;
			public int CreatedUnityFrame;

			/// <summary>
			/// Initial callstack of the coroutine when it was created.
			/// Check out <see cref="DebugInfo_RecordCallstack"/>.
			/// </summary>
			public StackTrace StackTrace;
		}

		/// <summary>
		/// Same as <see cref="UnityEngine.CustomYieldInstruction"/>, but can be used without UnityEngine dependencies.
		/// </summary>
		[DebuggerNonUserCode]
		public abstract class WiseYieldInstruction : IEnumerator
		{
			/// <summary>
			/// Indicates if coroutine should be kept suspended.
			/// </summary>
			public abstract bool keepWaiting { get; }

			public object Current => null;

			public bool MoveNext()
			{
				return keepWaiting;
			}

			public virtual void Reset()
			{
			}
		}

		#endregion

		/// <summary>
		/// Check if currently there is a timing being updated.
		/// Use this to avoid exceptions with <see cref="CurrentTiming"/> or <see cref="DeltaTime"/>
		/// </summary>
		public static bool CurrentTimingAvailable => m_CurrentTiming != null;

		/// <summary>
		/// Current <see cref="WiseTiming"/> being updated. Use this in the coroutine code.
		/// </summary>
		public static WiseTiming CurrentTiming {
			get {
				if (m_CurrentTiming == null)
					throw new InvalidOperationException($"Accessing {nameof(CurrentTiming)} outside of {nameof(WiseTiming)} coroutine update is not allowed.");

				return m_CurrentTiming;
			}
		}

		/// <summary>
		/// Current delta time seconds. Use this in the coroutine code, instead of Unity <see cref="Time.deltaTime"/>.
		/// </summary>
		public static float DeltaTime {
			get {
				if (m_CurrentTiming == null)
					throw new InvalidOperationException($"Accessing {nameof(DeltaTime)} outside of {nameof(WiseTiming)} coroutine update is not allowed.");

				return m_CurrentTiming.m_CurrentDeltaTime / 1000f;
			}
		}

		/// <summary>
		/// Current delta time in milliseconds. Use this in the coroutine code, instead of Unity <see cref="Time.deltaTime"/>.
		/// </summary>
		public static long DeltaTimeInMilliseconds {
			get {
				if (m_CurrentTiming == null)
					throw new InvalidOperationException($"Accessing {nameof(DeltaTime)} outside of {nameof(WiseTiming)} coroutine update is not allowed.");

				return m_CurrentTiming.m_CurrentDeltaTime;
			}
		}

		/// <summary>
		/// Current time in seconds passed with updates since this timing instance was created. Use this in the coroutine code, instead of Unity <see cref="Time.time"/>.
		/// </summary>
		public static float Time {
			get {
				if (m_CurrentTiming == null)
					throw new InvalidOperationException($"Accessing {nameof(Time)} outside of {nameof(WiseTiming)} coroutine update is not allowed.");

				return m_CurrentTiming.TimeElapsed;
			}
		}

		/// <summary>
		/// Current time in milliseconds passed with updates since this timing instance was created. Use this in the coroutine code, instead of Unity <see cref="Time.time"/>.
		/// </summary>
		public static long TimeInMilliseconds {
			get {
				if (m_CurrentTiming == null)
					throw new InvalidOperationException($"Accessing {nameof(Time)} outside of {nameof(WiseTiming)} coroutine update is not allowed.");

				return m_CurrentTiming.TimeElapsedInMilliseconds;
			}
		}

		/// <summary>
		/// Currently executed coroutine.
		/// </summary>
		public static WiseCoroutine CurrentCoroutine
		{
			get {
				if (m_CurrentTiming == null)
					throw new InvalidOperationException($"Accessing {nameof(CurrentCoroutine)} outside of {nameof(WiseTiming)} coroutine update is not allowed.");

				return m_CurrentTiming.m_CurrentCoroutine;
			}
		}


		/// <summary>
		/// Currently active coroutines.
		/// </summary>
		public IReadOnlyList<WiseCoroutine> Coroutines => m_Coroutines.AsReadOnly();

		/// <summary>
		/// Alive coroutines count, that will be processed the next update.
		/// </summary>
		public int CoroutinesCount => m_Coroutines.Count;

		/// <summary>
		/// Time in seconds passed with updates since this instance was created.
		/// </summary>
		public float TimeElapsed => TimeElapsedInMilliseconds / 1000f;

		/// <summary>
		/// Time in milliseconds passed with updates since this instance was created.
		/// </summary>
		public long TimeElapsedInMilliseconds { get; private set; }

		/// <summary>
		/// DeltaTime in seconds from the last <see cref="UpdateCoroutines(float)"/> or the currently happening one.
		/// </summary>
		public float LastOrCurrentDeltaTime => LastOrCurrentDeltaTimeInMilliseconds / 1000f;

		/// <summary>
		/// DeltaTime in milliseconds from the last <see cref="UpdateCoroutines(long)"/> or the currently happening one.
		/// </summary>
		public long LastOrCurrentDeltaTimeInMilliseconds { get; private set; }

		/// <summary>
		/// How many times update executed.
		/// </summary>
		public int UpdatesCount { get; private set; }

		/// <summary>
		/// If set to true, coroutines will record the initial callstack they were created from.
		/// NOTE: this is a slow operation so avoid using it in release.
		/// </summary>
		public bool DebugInfo_RecordCallstack { get; set; }

		/// <summary>
		/// Get notified if exception happens while any coroutine is executing.
		/// To control the outcome of the exception, use <see cref="WiseCoroutine.ExceptionHandling"/>
		/// </summary>
		public event ExceptionEventHandler ExceptionEvent;

		/// <summary>
		/// Coroutine starts.
		/// </summary>
		public event CoroutineEventHandler CoroutineStarted;

		/// <summary>
		/// Coroutine stopped for any reason.
		/// </summary>
		public event CoroutineEventHandler CoroutineStopped;

		/// <summary>
		/// Update is about to happen. Can get update delta time with <see cref="CurrentTiming"/>
		/// </summary>
		public event UpdateEventHandler PreUpdate;

		/// <summary>
		/// Update just finished. Can get update delta time with <see cref="CurrentTiming"/>
		/// </summary>
		public event UpdateEventHandler PostUpdate;



		#region Implementation Details

		[DebuggerNonUserCode]
		private class WiseCoroutineImpl : WiseCoroutine
		{
			public object Source { get; set; }

			public SourceInactiveBehaviour InactiveBehaviour { get; set; } = SourceInactiveBehaviour.StopCoroutine;

			public readonly Stack<IEnumerator> Iterators = new Stack<IEnumerator>();

			public float NextUpdateTime => NextUpdateTimeInMilliseconds / 1000f;

			public long NextUpdateTimeInMilliseconds { get; set; } = 0;

			public bool IsPaused { get; set; } = false;

			public object ResultData { get; set; }

			public bool WaitForEndOfFrame = false;

#if USE_UNITY
			public CustomYieldInstruction CustomYieldInstruction;
			public UnityEngine.Networking.UnityWebRequest WebRequest;
			public AsyncOperation AsyncOperation;
#endif

			public Task Task;
			public WiseCoroutine WaitedOnCoroutine;
			public WiseYieldInstruction WiseYieldInstruction;

			public ExceptionHandlingDelegate ExceptionHandling { get; set; }

			public DebugInfo DebugInfo { get; set; }

			public List<string> GetDebugStackNames() => Iterators.Select(it => it.ToString()).ToList();
		}

		private List<WiseCoroutineImpl> m_Coroutines = new List<WiseCoroutineImpl>();

		// Cache to avoid allocating garbage.
		private List<WiseCoroutineImpl> m_UpdatedCoroutinesCache = new List<WiseCoroutineImpl>();
		private List<WiseCoroutineImpl> m_RemovedCoroutinesCache = new List<WiseCoroutineImpl>();

		private long m_CurrentDeltaTime;
		private WiseCoroutineImpl m_CurrentCoroutine;

		private static WiseTiming m_CurrentTiming;

#if USE_UNITY
		private System.Reflection.FieldInfo m_WaitForSeconds_Seconds_FieldInfo = typeof(WaitForSeconds).GetField("m_Seconds", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
#endif

		#endregion


		/// <summary>
		/// Start a coroutine. It will be updated on <see cref="UpdateCoroutines(long)"/>.
		/// </summary>
		/// <param name="source">Source object is used for tracking, debugging and also automatically stops the coroutine if the source dies(if it is <see cref = "UnityEngine.Object" />).</param>
		public WiseCoroutine StartCoroutine(IEnumerator routine, object source, SourceInactiveBehaviour inactiveBehaviour = SourceInactiveBehaviour.StopCoroutine, ExceptionHandlingDelegate exceptionHandler = null)
		{
			// NOTE: to debug this class, remove the "[DebuggerNonUserCode]" attribute above, or disable "Just My Code" feature of Visual Studio.

			if (routine == null)
				throw new NullReferenceException("routine is null");

			var coroutine = new WiseCoroutineImpl() {
				Source = source,
				InactiveBehaviour = inactiveBehaviour,
				ExceptionHandling = exceptionHandler,
			};

			coroutine.DebugInfo = new DebugInfo {
				CreatedTimeInMilliseconds = TimeElapsedInMilliseconds,
				CreatedUnityTime = UnityEngine.Time.time,
				CreatedUnityFrame = UnityEngine.Time.frameCount,
				StackTrace = DebugInfo_RecordCallstack ? new StackTrace() : null,
			};

			coroutine.Iterators.Push(routine);

			if (m_CurrentTiming == this) {

				if (UpdateCoroutine(coroutine)) {
					m_Coroutines.Add(coroutine);

					CoroutineStarted?.Invoke(coroutine);
				}

			} else {
				m_Coroutines.Add(coroutine);

				CoroutineStarted?.Invoke(coroutine);
			}


			return coroutine;
		}

		/// <summary>
		/// Check if coroutine is still running.
		/// </summary>
		public bool IsCoroutineAlive(WiseCoroutine coroutine) => m_Coroutines.Contains(coroutine);

		/// <summary>
		/// Stop started coroutine.
		/// Returns true if coroutine was succesfully stopped.
		/// </summary>
		public bool StopCoroutine(WiseCoroutine coroutine)
		{
			// NOTE: to debug this class, remove the "[DebuggerNonUserCode]" attribute above, or disable "Just My Code" feature of Visual Studio.

			bool success = m_Coroutines.Remove((WiseCoroutineImpl)coroutine);

			CoroutineStopped?.Invoke(coroutine);

			return success;
		}

		/// <summary>
		/// Stop all coroutines starte from provided source object.
		/// </summary>
		public void StopCoroutineBySource(object source)
		{
			// NOTE: to debug this class, remove the "[DebuggerNonUserCode]" attribute above, or disable "Just My Code" feature of Visual Studio.

			for (int i = 0; i < m_Coroutines.Count; i++) {
				WiseCoroutine coroutine = m_Coroutines[i];

				if (coroutine.Source == source) {
					m_Coroutines.RemoveAt(i);
					i--;

					CoroutineStopped?.Invoke(coroutine);
				}
			}
		}

		/// <summary>
		/// Stop all coroutines.
		/// </summary>
		public void StopAllCoroutines()
		{
			// NOTE: to debug this class, remove the "[DebuggerNonUserCode]" attribute above, or disable "Just My Code" feature of Visual Studio.

			while (m_Coroutines.Count > 0) {
				WiseCoroutine coroutine = m_Coroutines[0];

				// Keep the order. Just in case.
				m_Coroutines.RemoveAt(0);

				CoroutineStopped?.Invoke(coroutine);
			}
		}


		/// <summary>
		/// Use this to change the order of the coroutines execution, for example, by source type, etc.
		/// </summary>
		public void SortCoroutines(Comparison<WiseCoroutine> comparison)
		{
			m_Coroutines.Sort(comparison);
		}

		/// <summary>
		/// Advance all coroutines with given delta time in seconds.
		/// All coroutines should use <see cref="DeltaTime"/> instead of Unity's version.
		/// </summary>
		public void UpdateCoroutines(float deltaTime)
		{
			UpdateCoroutines((long) (deltaTime * 1000));
		}

		/// <summary>
		/// Advance all coroutines with given delta time in milliseconds.
		/// All coroutines should use <see cref="DeltaTime"/> instead of Unity's version.
		/// </summary>
		public void UpdateCoroutines(long deltaTime)
		{
			// NOTE: to debug this class, remove the "[DebuggerNonUserCode]" attribute above, or disable "Just My Code" feature of Visual Studio.

			if (m_CurrentTiming != null)
				throw new InvalidOperationException($"Calling {nameof(WiseTiming)} update while another one is currently running is not allowed.");

			try {

				m_CurrentTiming = this;

				LastOrCurrentDeltaTimeInMilliseconds = m_CurrentDeltaTime = deltaTime;

				PreUpdate?.Invoke();

				TimeElapsedInMilliseconds += deltaTime;

				m_UpdatedCoroutinesCache.Clear();
				m_UpdatedCoroutinesCache.AddRange(m_Coroutines);
				m_RemovedCoroutinesCache.Clear();

				// Cache coroutines as the list may change during execution.
				foreach (WiseCoroutineImpl coroutine in m_UpdatedCoroutinesCache) {

					if (!UpdateCoroutine(coroutine)) {
						m_RemovedCoroutinesCache.Add(coroutine);
					}
				}

				foreach (WiseCoroutineImpl coroutine in m_RemovedCoroutinesCache) {
					m_Coroutines.Remove(coroutine);

					CoroutineStopped?.Invoke(coroutine);
				}


				//
				// WaitForEndOfFrame
				//
				m_UpdatedCoroutinesCache.Clear();
				m_UpdatedCoroutinesCache.AddRange(m_Coroutines);
				m_RemovedCoroutinesCache.Clear();

				// Cache coroutines as the list may change during execution.
				foreach (WiseCoroutineImpl coroutine in m_UpdatedCoroutinesCache) {

					if (!coroutine.WaitForEndOfFrame)
						continue;

					coroutine.WaitForEndOfFrame = false;

					if (!UpdateCoroutine(coroutine)) {
						m_RemovedCoroutinesCache.Add(coroutine);
					}
				}

				foreach (WiseCoroutineImpl coroutine in m_RemovedCoroutinesCache) {
					m_Coroutines.Remove(coroutine);

					CoroutineStopped?.Invoke(coroutine);
				}

				m_UpdatedCoroutinesCache.Clear();
				m_RemovedCoroutinesCache.Clear();

				PostUpdate?.Invoke();

			} finally {

				UpdatesCount++;

				m_CurrentTiming = null;
			}
		}

		private bool UpdateCoroutine(WiseCoroutineImpl coroutine)
		{
			// NOTE: to debug this class, remove the "[DebuggerNonUserCode]" attribute above, or disable "Just My Code" feature of Visual Studio.

			// Check if source was destroyed and kill the coroutine if true.
			if (coroutine.Source is UnityEngine.Object unitySource && unitySource == null) {
				return false;
			}

#if USE_UNITY
			if (coroutine.InactiveBehaviour != SourceInactiveBehaviour.KeepExecuting) {
				bool sourceIsInactive =
					(coroutine.Source is Behaviour behaviourSource && !behaviourSource.isActiveAndEnabled) ||
					(coroutine.Source is GameObject gameObjectSource && !gameObjectSource.activeInHierarchy)
				;

				if (sourceIsInactive) {

					switch (coroutine.InactiveBehaviour) {
						case SourceInactiveBehaviour.StopCoroutine:
							return false;

						case SourceInactiveBehaviour.SkipAndResumeWhenActive:
							return true;

						default: throw new NotSupportedException($"Not supported behaviour {coroutine.InactiveBehaviour}");
					}
				}
			}
#endif

			if (coroutine.IsPaused)
				return true;


			while (coroutine.Iterators.Count > 0) {

				try {

					m_CurrentCoroutine = coroutine;

					//
					// Waiting
					//

					if (coroutine.WiseYieldInstruction != null) {
						if (coroutine.WiseYieldInstruction.keepWaiting)
							return true;

						coroutine.WiseYieldInstruction = null;
					}

#if USE_UNITY
					if (coroutine.CustomYieldInstruction != null) {
						if (coroutine.CustomYieldInstruction.keepWaiting)
							return true;

						coroutine.CustomYieldInstruction = null;
					}

					if (coroutine.WebRequest != null) {
						if (!coroutine.WebRequest.isDone)
							return true;

						coroutine.WebRequest = null;
					}

					if (coroutine.AsyncOperation != null) {
						if (!coroutine.AsyncOperation.isDone)
							return true;

						coroutine.AsyncOperation = null;
					}
#endif

					if (coroutine.Task != null) {
						if (!coroutine.Task.IsCompleted)
							return true;

						coroutine.Task = null;
					}

					if (coroutine.WaitedOnCoroutine != null) {

						// NOTE: This works only with coroutines managed by this timing.
						if (IsCoroutineAlive(coroutine))
							return true;

						coroutine.WaitedOnCoroutine = null;
					}

					if (coroutine.NextUpdateTimeInMilliseconds > TimeElapsedInMilliseconds)
						return true;


					//
					// Moving
					//

					IEnumerator iterator = coroutine.Iterators.Peek();

					if (!iterator.MoveNext()) {
						coroutine.Iterators.Pop();
						continue;
					}

					//
					// Processing
					//

					object current = iterator.Current;

					if (current == null) {
						return true;
					}

					if (current is IEnumerator nestedIterator) {
						coroutine.Iterators.Push(nestedIterator);
						continue;
					}

					if (current is WiseYieldInstruction wiseYieldInstruction) {
						if (!wiseYieldInstruction.keepWaiting)
							continue;

						coroutine.WiseYieldInstruction = wiseYieldInstruction;
						return true;
					}

					if (current is WaitForMilliseconds waitForMilliseconds) {
						coroutine.NextUpdateTimeInMilliseconds = TimeElapsedInMilliseconds + waitForMilliseconds.Milliseconds;
						return true;
					}

#if USE_UNITY
					if (current is WaitForSeconds waitForSeconds) {
						var seconds = (float)m_WaitForSeconds_Seconds_FieldInfo.GetValue(waitForSeconds);
						coroutine.NextUpdateTimeInMilliseconds = TimeElapsedInMilliseconds + (long)(seconds * 1000);
						return true;
					}

					if (current is WaitForEndOfFrame) {
						coroutine.WaitForEndOfFrame = true;
						return true;
					}



					if (current is CustomYieldInstruction customYieldInstruction) {
						if (!customYieldInstruction.keepWaiting)
							continue;

						coroutine.CustomYieldInstruction = customYieldInstruction;
						return true;
					}

					if (current is UnityEngine.Networking.UnityWebRequest webRequest) {
						if (webRequest.isDone)
							continue;

						coroutine.WebRequest = webRequest;
						return true;
					}

					if (current is AsyncOperation asyncOperation) {
						if (asyncOperation.isDone)
							continue;

						coroutine.AsyncOperation = asyncOperation;
						return true;
					}
#endif

					if (current is Task task) {
						if (task.IsCompleted)
							continue;

						coroutine.Task = task;
						return true;
					}

					if (current is WiseCoroutine anotherCoroutine) {

						// NOTE: This works only with coroutines managed by this timing.
						if (!IsCoroutineAlive(anotherCoroutine))
							continue;

						coroutine.WaitedOnCoroutine = anotherCoroutine;
						return true;
					}

					throw new NotSupportedException($"Not supported yield instruction: {current}");

				} catch(Exception ex) {

					ExceptionEvent?.Invoke(ex, coroutine);

					ExceptionHandlingAction action = coroutine.ExceptionHandling != null
						? coroutine.ExceptionHandling(ex)
						: ExceptionHandlingAction.PropagateException
						;

					switch(action) {
						case ExceptionHandlingAction.PropagateException: m_Coroutines.Remove(coroutine); CoroutineStopped?.Invoke(coroutine); throw;
						case ExceptionHandlingAction.CatchAndStopCoroutine: return false;
						case ExceptionHandlingAction.CatchPopAndResumeCoroutine: coroutine.Iterators.Pop(); continue;
						default: throw new NotSupportedException($"Not supported type {action}");
					}

				} finally {

					m_CurrentCoroutine = null;

				}
			}


			return false;
		}
	}
}