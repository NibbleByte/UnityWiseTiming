// MIT License Copyright(c) 2024 Filip Slavov, https://github.com/NibbleByte/UnityWiseTiming

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

		/// <summary>
		/// In case you're not using Unity, but still want the end of frame support,
		/// use this class instead.
		/// </summary>
		public sealed class WaitForEndOfFrame
		{
			// Keep empty. Used as marker.
		}

		/// <summary>
		/// In case you're not using Unity, but still want the fixed update support,
		/// use this class instead.
		/// </summary>
		public sealed class WaitForFixedUpdate
		{
			// Keep empty. Used as marker.
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
		public IEnumerable<WiseCoroutine> Coroutines => m_NextFrameCoroutines
			.Concat(m_FixedUpdateCoroutines)
			.Concat(m_TimedCoroutines)
			.Concat(m_EndOfFrameCoroutines)
			;

		/// <summary>
		/// Alive coroutines count, that will be processed the next update.
		/// </summary>
		public int CoroutinesCount => m_NextFrameCoroutines.Count + m_FixedUpdateCoroutines.Count + m_TimedCoroutines.Count + m_EndOfFrameCoroutines.Count;

		/// <summary>
		/// Time in seconds passed with updates since this instance was created.
		/// </summary>
		public float TimeElapsed => TimeElapsedInMilliseconds / 1000f;

		/// <summary>
		/// Time in milliseconds passed with updates since this instance was created.
		/// </summary>
		public long TimeElapsedInMilliseconds { get; private set; }

		/// <summary>
		/// Time in seconds passed with fixed updates since this instance was created.
		/// </summary>
		public float FixedTimeElapsed => m_FixedTiming?.TimeElapsed ?? 0f;

		/// <summary>
		/// Time in milliseconds passed with fixed updates since this instance was created.
		/// </summary>
		public long FixedTimeElapsedInMilliseconds => m_FixedTiming?.TimeElapsedInMilliseconds ?? 0;

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
		/// How many times fixed update executed.
		/// </summary>
		public int FixedUpdatesCount => m_FixedTiming?.UpdatesCount ?? 0;

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

		/// <summary>
		/// Fixed update is about to happen. Can get update delta time with <see cref="CurrentTiming"/>
		/// </summary>
		public event UpdateEventHandler PreFixedUpdate;

		/// <summary>
		/// Fixed update just finished. Can get update delta time with <see cref="CurrentTiming"/>
		/// </summary>
		public event UpdateEventHandler PostFixedUpdate;



		#region Implementation Details

		private enum ScheduleAction
		{
			NextFrame,
			FixedUpdate,
			Timed,
			EndOfFrame,

			Finished,
		}

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

		private List<WiseCoroutineImpl> m_NextFrameCoroutines = new List<WiseCoroutineImpl>();
		private List<WiseCoroutineImpl> m_FixedUpdateCoroutines = new List<WiseCoroutineImpl>();
		private List<WiseCoroutineImpl> m_TimedCoroutines = new List<WiseCoroutineImpl>();
		private List<WiseCoroutineImpl> m_EndOfFrameCoroutines = new List<WiseCoroutineImpl>();

		// Cache to avoid allocating garbage.
		private List<WiseCoroutineImpl> m_UpdatedCoroutinesCache = new List<WiseCoroutineImpl>();
		private List<WiseCoroutineImpl> m_TimedCoroutinesCache = new List<WiseCoroutineImpl>();

		private long m_CurrentDeltaTime;
		private WiseCoroutineImpl m_CurrentCoroutine;

		private static WiseTiming m_CurrentTiming;

		// Used for fixed updates. This way we can save up on some code.
		private WiseTiming m_FixedTiming = null;

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

				ScheduleAction nextSchedule = UpdateCoroutine(coroutine, ScheduleAction.NextFrame);

				if (nextSchedule != ScheduleAction.Finished) {
					ScheduleCoroutine(coroutine, nextSchedule);
					CoroutineStarted?.Invoke(coroutine);
				}

			} else {
				m_NextFrameCoroutines.Add(coroutine);

				CoroutineStarted?.Invoke(coroutine);
			}


			return coroutine;
		}

		/// <summary>
		/// Check if coroutine is still running.
		/// </summary>
		public bool IsCoroutineAlive(WiseCoroutine coroutine) =>
			m_NextFrameCoroutines.Contains(coroutine) ||
			m_FixedUpdateCoroutines.Contains(coroutine) ||
			m_TimedCoroutines.Contains(coroutine) ||
			m_EndOfFrameCoroutines.Contains(coroutine)
			;

		/// <summary>
		/// Stop started coroutine.
		/// Returns true if coroutine was succesfully stopped.
		/// </summary>
		public bool StopCoroutine(WiseCoroutine coroutine)
		{
			// NOTE: to debug this class, remove the "[DebuggerNonUserCode]" attribute above, or disable "Just My Code" feature of Visual Studio.

			bool success = m_NextFrameCoroutines.Remove((WiseCoroutineImpl)coroutine)
				|| m_FixedUpdateCoroutines.Remove((WiseCoroutineImpl)coroutine)
				|| m_TimedCoroutines.Remove((WiseCoroutineImpl)coroutine)
				|| m_EndOfFrameCoroutines.Remove((WiseCoroutineImpl)coroutine)
				;

			if (success) {
				CoroutineStopped?.Invoke(coroutine);
			}

			return success;
		}

		/// <summary>
		/// Stop all coroutines starte from provided source object.
		/// </summary>
		public void StopCoroutineBySource(object source)
		{
			// NOTE: to debug this class, remove the "[DebuggerNonUserCode]" attribute above, or disable "Just My Code" feature of Visual Studio.

			RemoveCoroutinesWhere(m_NextFrameCoroutines, coroutine => coroutine.Source == source);
			RemoveCoroutinesWhere(m_FixedUpdateCoroutines, coroutine => coroutine.Source == source);
			RemoveCoroutinesWhere(m_TimedCoroutines, coroutine => coroutine.Source == source);
			RemoveCoroutinesWhere(m_EndOfFrameCoroutines, coroutine => coroutine.Source == source);
		}

		/// <summary>
		/// Stop all coroutines.
		/// </summary>
		public void StopAllCoroutines()
		{
			// NOTE: to debug this class, remove the "[DebuggerNonUserCode]" attribute above, or disable "Just My Code" feature of Visual Studio.

			RemoveCoroutinesWhere(m_NextFrameCoroutines, coroutine => true);
			RemoveCoroutinesWhere(m_FixedUpdateCoroutines, coroutine => true);
			RemoveCoroutinesWhere(m_TimedCoroutines, coroutine => true);
			RemoveCoroutinesWhere(m_EndOfFrameCoroutines, coroutine => true);
		}


		/// <summary>
		/// Use this to change the order of the coroutines execution, for example, by source type, etc.
		/// This excludes coroutines that are currently scheduled for specific time.
		/// </summary>
		public void SortCoroutines(Comparison<WiseCoroutine> comparison)
		{
			m_NextFrameCoroutines.Sort(comparison);
			m_FixedUpdateCoroutines.Sort(comparison);
			// m_TimedCoroutines.Sort(comparison); // This is always sorted by time.
			m_EndOfFrameCoroutines.Sort(comparison);
		}

		/// <summary>
		/// Call in FixedUpdate()
		/// Advance all coroutines scheduled for fixed update with given delta time in seconds.
		/// All coroutines should use <see cref="DeltaTime"/> instead of Unity's version.
		/// </summary>
		public void FixedUpdateCoroutines(float deltaTime)
		{
			FixedUpdateCoroutines((long) (deltaTime * 1000));
		}

		/// <summary>
		/// Call in FixedUpdate()
		/// Advance all coroutines scheduled for fixed update with given delta time in milliseconds.
		/// All coroutines should use <see cref="DeltaTime"/> instead of Unity's version.
		/// </summary>
		public void FixedUpdateCoroutines(long deltaTime)
		{
			// NOTE: to debug this class, remove the "[DebuggerNonUserCode]" attribute above, or disable "Just My Code" feature of Visual Studio.

			if (m_CurrentTiming != null)
				throw new InvalidOperationException($"Calling {nameof(WiseTiming)} fixed update while another one is currently running is not allowed.");

			if (m_FixedTiming == null) {
				m_FixedTiming = new WiseTiming();

				// Refer to the same lists so scheduling & public properties work automatically.
				m_FixedTiming.m_NextFrameCoroutines = m_NextFrameCoroutines;
				m_FixedTiming.m_FixedUpdateCoroutines = m_FixedUpdateCoroutines;
				m_FixedTiming.m_TimedCoroutines = m_TimedCoroutines;
				m_FixedTiming.m_EndOfFrameCoroutines = m_EndOfFrameCoroutines;
			}

			try {

				// Refer to the internal timing, as it stores it's own times.
				// User code will get delta time from it.
				m_CurrentTiming = m_FixedTiming;

				m_FixedTiming.LastOrCurrentDeltaTimeInMilliseconds = m_FixedTiming.m_CurrentDeltaTime = deltaTime;

				PreFixedUpdate?.Invoke();

				m_FixedTiming.TimeElapsedInMilliseconds += deltaTime;

				// Keep in mind that the fixed timing refers to this one coroutines lists - scheduling back and forth should work.
				m_FixedTiming.UpdateAndScheduleList(m_FixedTiming.m_FixedUpdateCoroutines, ScheduleAction.FixedUpdate);

				m_FixedTiming.m_UpdatedCoroutinesCache.Clear();

				PostFixedUpdate?.Invoke();

			} finally {

				m_FixedTiming.UpdatesCount++;

				m_CurrentTiming = null;
			}
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

				// Use cache for timed entries, so newly scheduled ones resume on the next update.
				m_TimedCoroutinesCache.Clear();
				m_TimedCoroutinesCache.AddRange(m_TimedCoroutines);


				UpdateAndScheduleList(m_NextFrameCoroutines, ScheduleAction.NextFrame);


				#region TimedCoroutines

				// Only check the coroutines that time ran out from the sorted list.
				while (m_TimedCoroutinesCache.Count > 0 && m_TimedCoroutinesCache[0].NextUpdateTimeInMilliseconds <= TimeElapsedInMilliseconds) {
					var coroutine = m_TimedCoroutinesCache[0];
					m_TimedCoroutinesCache.RemoveAt(0);
					m_TimedCoroutines.Remove(coroutine);	// Might not be first element anymore?

					ScheduleAction nextSchedule = UpdateCoroutine(coroutine, ScheduleAction.Timed);
					ScheduleCoroutine(coroutine, nextSchedule);
				}

				m_TimedCoroutinesCache.Clear();

				#endregion


				UpdateAndScheduleList(m_EndOfFrameCoroutines, ScheduleAction.EndOfFrame);

				m_UpdatedCoroutinesCache.Clear();

				PostUpdate?.Invoke();

			} finally {

				UpdatesCount++;

				m_CurrentTiming = null;
			}
		}

		private void UpdateAndScheduleList(List<WiseCoroutineImpl> coroutines, ScheduleAction schedule)
		{
			// Cache coroutines as the list may change during execution.
			m_UpdatedCoroutinesCache.Clear();
			m_UpdatedCoroutinesCache.AddRange(coroutines);

			foreach (WiseCoroutineImpl coroutine in m_UpdatedCoroutinesCache) {

				ScheduleAction nextSchedule = UpdateCoroutine(coroutine, schedule);
				if (nextSchedule == schedule)
					continue;

				coroutines.Remove(coroutine);

				ScheduleCoroutine(coroutine, nextSchedule);
			}
		}

		private void ScheduleCoroutine(WiseCoroutineImpl coroutine, ScheduleAction nextSchedule)
		{
			switch (nextSchedule) {
				case ScheduleAction.NextFrame:
					m_NextFrameCoroutines.Add(coroutine);
					break;

				case ScheduleAction.FixedUpdate:
					m_FixedUpdateCoroutines.Add(coroutine);
					break;

				case ScheduleAction.EndOfFrame:
					m_EndOfFrameCoroutines.Add(coroutine);
					break;

				case ScheduleAction.Timed:
					// Insert sorted in ascending order. Work with the first elements later on.
					int index;
					for (index = 0; index < m_TimedCoroutines.Count; index++) {
						if (coroutine.NextUpdateTimeInMilliseconds < m_TimedCoroutines[index].NextUpdateTimeInMilliseconds)
							break;
					}

					// Works with index == Count.
					m_TimedCoroutines.Insert(index, coroutine);

					break;

				case ScheduleAction.Finished:
					CoroutineStopped?.Invoke(coroutine);
					break;
			}
		}

		private ScheduleAction UpdateCoroutine(WiseCoroutineImpl coroutine, ScheduleAction prevScheduleAction)
		{
			// NOTE: to debug this class, remove the "[DebuggerNonUserCode]" attribute above, or disable "Just My Code" feature of Visual Studio.

			// Check if source was destroyed and kill the coroutine if true.
			if (coroutine.Source is UnityEngine.Object unitySource && unitySource == null) {
				return ScheduleAction.Finished;
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
							return ScheduleAction.Finished;

						case SourceInactiveBehaviour.SkipAndResumeWhenActive:
							return prevScheduleAction;

						default: throw new NotSupportedException($"Not supported behaviour {coroutine.InactiveBehaviour}");
					}
				}
			}
#endif

			if (coroutine.IsPaused)
				return prevScheduleAction;


			while (coroutine.Iterators.Count > 0) {

				try {

					m_CurrentCoroutine = coroutine;

					//
					// Waiting
					//

					if (coroutine.WiseYieldInstruction != null) {
						if (coroutine.WiseYieldInstruction.keepWaiting)
							return ScheduleAction.NextFrame;

						coroutine.WiseYieldInstruction = null;
					}

#if USE_UNITY
					if (coroutine.CustomYieldInstruction != null) {
						if (coroutine.CustomYieldInstruction.keepWaiting)
							return ScheduleAction.NextFrame;

						coroutine.CustomYieldInstruction = null;
					}

					if (coroutine.WebRequest != null) {
						if (!coroutine.WebRequest.isDone)
							return ScheduleAction.NextFrame;

						coroutine.WebRequest = null;
					}

					if (coroutine.AsyncOperation != null) {
						if (!coroutine.AsyncOperation.isDone)
							return ScheduleAction.NextFrame;

						coroutine.AsyncOperation = null;
					}
#endif

					if (coroutine.Task != null) {
						if (!coroutine.Task.IsCompleted)
							return ScheduleAction.NextFrame;

						coroutine.Task = null;
					}

					if (coroutine.WaitedOnCoroutine != null) {

						// NOTE: This works only with coroutines managed by this timing.
						if (IsCoroutineAlive(coroutine))
							return ScheduleAction.NextFrame;

						coroutine.WaitedOnCoroutine = null;
					}

					if (coroutine.NextUpdateTimeInMilliseconds > TimeElapsedInMilliseconds)
						return ScheduleAction.Timed;


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
						return ScheduleAction.NextFrame;
					}

					if (current is IEnumerator nestedIterator) {
						coroutine.Iterators.Push(nestedIterator);
						continue;
					}

					if (current is WiseYieldInstruction wiseYieldInstruction) {
						if (!wiseYieldInstruction.keepWaiting)
							continue;

						coroutine.WiseYieldInstruction = wiseYieldInstruction;
						return ScheduleAction.NextFrame;
					}

					if (current is WaitForMilliseconds waitForMilliseconds) {
						coroutine.NextUpdateTimeInMilliseconds = TimeElapsedInMilliseconds + waitForMilliseconds.Milliseconds;
						return ScheduleAction.Timed;
					}

					// When Unity support is not available.
					if (current is WiseTiming.WaitForEndOfFrame) {
						return ScheduleAction.EndOfFrame;
					}

					// When Unity support is not available.
					if (current is WiseTiming.WaitForFixedUpdate) {
						return ScheduleAction.FixedUpdate;
					}

#if USE_UNITY
					if (current is WaitForSeconds waitForSeconds) {
						var seconds = (float)m_WaitForSeconds_Seconds_FieldInfo.GetValue(waitForSeconds);
						coroutine.NextUpdateTimeInMilliseconds = TimeElapsedInMilliseconds + (long)(seconds * 1000);
						return ScheduleAction.Timed;
					}

					if (current is UnityEngine.WaitForFixedUpdate) {
						return ScheduleAction.FixedUpdate;
					}

					if (current is UnityEngine.WaitForEndOfFrame) {
						return ScheduleAction.EndOfFrame;
					}



					if (current is CustomYieldInstruction customYieldInstruction) {
						if (!customYieldInstruction.keepWaiting)
							continue;

						coroutine.CustomYieldInstruction = customYieldInstruction;
						return ScheduleAction.NextFrame;
					}

					if (current is UnityEngine.Networking.UnityWebRequest webRequest) {
						if (webRequest.isDone)
							continue;

						coroutine.WebRequest = webRequest;
						return ScheduleAction.NextFrame;
					}

					if (current is AsyncOperation asyncOperation) {
						if (asyncOperation.isDone)
							continue;

						coroutine.AsyncOperation = asyncOperation;
						return ScheduleAction.NextFrame;
					}
#endif

					if (current is Task task) {
						if (task.IsCompleted)
							continue;

						coroutine.Task = task;
						return ScheduleAction.NextFrame;
					}

					if (current is WiseCoroutine anotherCoroutine) {

						// NOTE: This works only with coroutines managed by this timing.
						if (!IsCoroutineAlive(anotherCoroutine))
							continue;

						coroutine.WaitedOnCoroutine = anotherCoroutine;
						return ScheduleAction.NextFrame;
					}

					throw new NotSupportedException($"Not supported yield instruction: {current}");

				} catch(Exception ex) {

					ExceptionEvent?.Invoke(ex, coroutine);

					ExceptionHandlingAction action = coroutine.ExceptionHandling != null
						? coroutine.ExceptionHandling(ex)
						: ExceptionHandlingAction.PropagateException
						;

					switch(action) {
						case ExceptionHandlingAction.PropagateException:
							m_NextFrameCoroutines.Remove(coroutine);
							m_FixedUpdateCoroutines.Remove(coroutine);
							m_TimedCoroutines.Remove(coroutine);
							m_EndOfFrameCoroutines.Remove(coroutine);
							CoroutineStopped?.Invoke(coroutine);
							throw;
						case ExceptionHandlingAction.CatchAndStopCoroutine: return ScheduleAction.Finished;
						case ExceptionHandlingAction.CatchPopAndResumeCoroutine: coroutine.Iterators.Pop(); continue;
						default: throw new NotSupportedException($"Not supported type {action}");
					}

				} finally {

					m_CurrentCoroutine = null;

				}
			}


			return ScheduleAction.Finished;
		}

		private void RemoveCoroutinesWhere(List<WiseCoroutineImpl> coroutines, Predicate<WiseCoroutineImpl> predicate)
		{
			for (int i = coroutines.Count - 1; i >= 0; i--) {
				WiseCoroutineImpl coroutine = coroutines[i];

				if (predicate(coroutine)) {
					coroutines.RemoveAt(i);

					CoroutineStopped?.Invoke(coroutine);
				}
			}
		}
	}
}