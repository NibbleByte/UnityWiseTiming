using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;

namespace DevLocker.GFrame.Timing.Tests
{
	public class WiseTiming_Tests
	{
		private WiseTiming m_Timing;

		private int m_Progress;

		[SetUp]
		public void Setup()
		{
			m_Timing = new WiseTiming();
			m_Progress = 0;
		}

		#region EmptyCoroutine

		[Test]
		public void EmptyCoroutine()
		{
			Assert.AreEqual(0, m_Timing.CoroutinesCount);
			Assert.AreEqual(0, m_Timing.UpdatesCount);

			var coroutine = m_Timing.StartCoroutine(EmptyCoroutineCrt(), this);

			Assert.IsNotNull(coroutine);
			Assert.AreEqual(this, coroutine.Source);

			Assert.That(() => WiseTiming.CurrentTiming, Throws.TypeOf<InvalidOperationException>());
			Assert.AreEqual(0, m_Timing.TimeElapsed);
			Assert.AreEqual(1, m_Timing.CoroutinesCount);

			// Coroutine is alive until actual update happens, since we're not in an actual update.
			Assert.AreEqual(0, m_Progress);
			Assert.IsTrue(m_Timing.IsCoroutineAlive(coroutine));

			//
			// Update
			//
			m_Timing.UpdateCoroutines(1f);

			Assert.That(() => WiseTiming.CurrentTiming, Throws.TypeOf<InvalidOperationException>());
			Assert.AreEqual(1f, m_Timing.TimeElapsed);
			Assert.AreEqual(1, m_Timing.UpdatesCount);
			Assert.AreEqual(0, m_Timing.CoroutinesCount);

			// Now it should have finished, officially.
			Assert.AreEqual(1, m_Progress);
			Assert.IsFalse(m_Timing.IsCoroutineAlive(coroutine));

			//
			// Update
			//
			m_Timing.UpdateCoroutines(1f);

			Assert.That(() => WiseTiming.CurrentTiming, Throws.TypeOf<InvalidOperationException>());
			Assert.AreEqual(2f, m_Timing.TimeElapsed);
			Assert.AreEqual(2, m_Timing.UpdatesCount);
			Assert.AreEqual(0, m_Timing.CoroutinesCount);

			Assert.AreEqual(1, m_Progress);
			Assert.IsFalse(m_Timing.IsCoroutineAlive(coroutine));
		}

		private IEnumerator EmptyCoroutineCrt()
		{
			Assert.AreEqual(0, m_Timing.UpdatesCount);
			Assert.AreEqual(1f, m_Timing.TimeElapsed);
			Assert.AreEqual(1f, WiseTiming.DeltaTime);

			m_Progress++;

			yield break;
		}

		#endregion

		#region SkipFrameCoroutine

		[Test]
		public void SkipFrameCoroutine()
		{
			var coroutine = m_Timing.StartCoroutine(SkipFrameCoroutineCrt(), this);

			//
			// Update
			//
			m_Timing.UpdateCoroutines(1f);

			Assert.That(() => WiseTiming.CurrentTiming, Throws.TypeOf<InvalidOperationException>());
			Assert.AreEqual(1f, m_Timing.TimeElapsed);
			Assert.AreEqual(1, m_Timing.UpdatesCount);
			Assert.AreEqual(1, m_Timing.CoroutinesCount);

			Assert.AreEqual(1, m_Progress);
			Assert.IsTrue(m_Timing.IsCoroutineAlive(coroutine));

			//
			// Update
			//
			m_Timing.UpdateCoroutines(1f);

			Assert.That(() => WiseTiming.CurrentTiming, Throws.TypeOf<InvalidOperationException>());
			Assert.AreEqual(2f, m_Timing.TimeElapsed);
			Assert.AreEqual(2, m_Timing.UpdatesCount);
			Assert.AreEqual(0, m_Timing.CoroutinesCount);

			Assert.AreEqual(2, m_Progress);
			Assert.IsFalse(m_Timing.IsCoroutineAlive(coroutine));
		}

		private IEnumerator SkipFrameCoroutineCrt()
		{
			Assert.AreEqual(0, m_Timing.UpdatesCount);
			Assert.AreEqual(1f, m_Timing.TimeElapsed);
			Assert.AreEqual(1f, WiseTiming.DeltaTime);

			m_Progress++;

			yield return null;

			Assert.AreEqual(1, m_Timing.UpdatesCount);
			Assert.AreEqual(2f, m_Timing.TimeElapsed);
			Assert.AreEqual(1f, WiseTiming.DeltaTime);

			m_Progress++;
		}

		#endregion

		#region SkipFrameParallelCoroutine

		[Test]
		public void SkipFrameParallelCoroutine()
		{
			var coroutine = m_Timing.StartCoroutine(SkipFrameCoroutineCrt(), this);
			var coroutine2 = m_Timing.StartCoroutine(SkipFrameCoroutineCrt(), this);
			var coroutine3 = m_Timing.StartCoroutine(SkipFrameCoroutineCrt(), this);

			//
			// Update
			//
			m_Timing.UpdateCoroutines(1f);

			Assert.That(() => WiseTiming.CurrentTiming, Throws.TypeOf<InvalidOperationException>());
			Assert.AreEqual(1f, m_Timing.TimeElapsed);
			Assert.AreEqual(1, m_Timing.UpdatesCount);
			Assert.AreEqual(3, m_Timing.CoroutinesCount);

			Assert.AreEqual(3, m_Progress);
			Assert.IsTrue(m_Timing.IsCoroutineAlive(coroutine));
			Assert.IsTrue(m_Timing.IsCoroutineAlive(coroutine2));
			Assert.IsTrue(m_Timing.IsCoroutineAlive(coroutine3));

			//
			// Update
			//
			m_Timing.UpdateCoroutines(1f);

			Assert.That(() => WiseTiming.CurrentTiming, Throws.TypeOf<InvalidOperationException>());
			Assert.AreEqual(2f, m_Timing.TimeElapsed);
			Assert.AreEqual(2, m_Timing.UpdatesCount);
			Assert.AreEqual(0, m_Timing.CoroutinesCount);

			Assert.AreEqual(6, m_Progress);
			Assert.IsFalse(m_Timing.IsCoroutineAlive(coroutine));
			Assert.IsFalse(m_Timing.IsCoroutineAlive(coroutine2));
			Assert.IsFalse(m_Timing.IsCoroutineAlive(coroutine3));
		}

		#endregion

		#region WaitForSecondsCoroutine

		[Test]
		public void WaitForSecondsCoroutine()
		{
			var coroutine = m_Timing.StartCoroutine(WaitForSecondsCoroutineCrt(), this);

			//
			// Update
			//
			m_Timing.UpdateCoroutines(1f);

			Assert.That(() => WiseTiming.CurrentTiming, Throws.TypeOf<InvalidOperationException>());
			Assert.AreEqual(1f, m_Timing.TimeElapsed);
			Assert.AreEqual(1, m_Timing.UpdatesCount);
			Assert.AreEqual(1, m_Timing.CoroutinesCount);

			Assert.AreEqual(1, m_Progress);
			Assert.IsTrue(m_Timing.IsCoroutineAlive(coroutine));

			//
			// Update
			//
			m_Timing.UpdateCoroutines(1f);

			Assert.That(() => WiseTiming.CurrentTiming, Throws.TypeOf<InvalidOperationException>());
			Assert.AreEqual(2f, m_Timing.TimeElapsed);
			Assert.AreEqual(2, m_Timing.UpdatesCount);
			Assert.AreEqual(1, m_Timing.CoroutinesCount);

			Assert.AreEqual(2, m_Progress);
			Assert.IsTrue(m_Timing.IsCoroutineAlive(coroutine));

			//
			// Update
			//
			m_Timing.UpdateCoroutines(1f);

			Assert.That(() => WiseTiming.CurrentTiming, Throws.TypeOf<InvalidOperationException>());
			Assert.AreEqual(3f, m_Timing.TimeElapsed);
			Assert.AreEqual(3, m_Timing.UpdatesCount);
			Assert.AreEqual(1, m_Timing.CoroutinesCount);

			Assert.AreEqual(2, m_Progress);
			Assert.IsTrue(m_Timing.IsCoroutineAlive(coroutine));

			//
			// Update
			//
			m_Timing.UpdateCoroutines(1f);

			Assert.That(() => WiseTiming.CurrentTiming, Throws.TypeOf<InvalidOperationException>());
			Assert.AreEqual(4f, m_Timing.TimeElapsed);
			Assert.AreEqual(m_Timing.UpdatesCount, 4);
			Assert.AreEqual(0, m_Timing.CoroutinesCount);

			Assert.AreEqual(3, m_Progress);
			Assert.IsFalse(m_Timing.IsCoroutineAlive(coroutine));
		}

		private IEnumerator WaitForSecondsCoroutineCrt()
		{
			Assert.AreEqual(0, m_Timing.UpdatesCount);
			Assert.AreEqual(1f, m_Timing.TimeElapsed);
			Assert.AreEqual(1f, WiseTiming.DeltaTime);

			m_Progress++;

			yield return new WaitForSeconds(0.5f);

			Assert.AreEqual(1, m_Timing.UpdatesCount);
			Assert.AreEqual(2f, m_Timing.TimeElapsed);
			Assert.AreEqual(1f, WiseTiming.DeltaTime);

			m_Progress++;

			yield return new WaitForSeconds(1.5f);

			Assert.AreEqual(3, m_Timing.UpdatesCount);
			Assert.AreEqual(4f, m_Timing.TimeElapsed);
			Assert.AreEqual(1f, WiseTiming.DeltaTime);

			m_Progress++;
		}

		#endregion

		#region WaitForZeroSecondsCoroutine

		[Test]
		public void WaitForZeroSecondsCoroutine()
		{
			var coroutine = m_Timing.StartCoroutine(WaitForZeroSecondsCoroutineCrt(), this);

			//
			// Update
			//
			m_Timing.UpdateCoroutines(1f);

			Assert.That(() => WiseTiming.CurrentTiming, Throws.TypeOf<InvalidOperationException>());
			Assert.AreEqual(1f, m_Timing.TimeElapsed);
			Assert.AreEqual(1, m_Timing.UpdatesCount);
			Assert.AreEqual(1, m_Timing.CoroutinesCount);

			Assert.AreEqual(1, m_Progress);
			Assert.IsTrue(m_Timing.IsCoroutineAlive(coroutine));

			//
			// Update
			//
			m_Timing.UpdateCoroutines(1f);

			Assert.That(() => WiseTiming.CurrentTiming, Throws.TypeOf<InvalidOperationException>());
			Assert.AreEqual(2f, m_Timing.TimeElapsed);
			Assert.AreEqual(2, m_Timing.UpdatesCount);
			Assert.AreEqual(1, m_Timing.CoroutinesCount);

			Assert.AreEqual(2, m_Progress);
			Assert.IsTrue(m_Timing.IsCoroutineAlive(coroutine));

			//
			// Update
			//
			m_Timing.UpdateCoroutines(1f);

			Assert.That(() => WiseTiming.CurrentTiming, Throws.TypeOf<InvalidOperationException>());
			Assert.AreEqual(3f, m_Timing.TimeElapsed);
			Assert.AreEqual(3, m_Timing.UpdatesCount);
			Assert.AreEqual(0, m_Timing.CoroutinesCount);

			Assert.AreEqual(3, m_Progress);
			Assert.IsFalse(m_Timing.IsCoroutineAlive(coroutine));
		}

		private IEnumerator WaitForZeroSecondsCoroutineCrt()
		{
			Assert.AreEqual(0, m_Timing.UpdatesCount);
			Assert.AreEqual(1f, m_Timing.TimeElapsed);
			Assert.AreEqual(1f, WiseTiming.DeltaTime);

			m_Progress++;

			yield return new WaitForSeconds(0.0f);

			Assert.AreEqual(1, m_Timing.UpdatesCount);
			Assert.AreEqual(2f, m_Timing.TimeElapsed);
			Assert.AreEqual(1f, WiseTiming.DeltaTime);

			m_Progress++;

			yield return new WaitForSeconds(0.0f);

			Assert.AreEqual(2, m_Timing.UpdatesCount);
			Assert.AreEqual(3f, m_Timing.TimeElapsed);
			Assert.AreEqual(1f, WiseTiming.DeltaTime);

			m_Progress++;
		}

		#endregion

		#region WaitForEndOfFrameCoroutine

		[Test]
		public void WaitForEndOfFrameCoroutine()
		{
			var coroutine = m_Timing.StartCoroutine(WaitForEndOfFrameCoroutineCrt(), this);

			//
			// Update
			//
			m_Timing.UpdateCoroutines(1f);

			Assert.That(() => WiseTiming.CurrentTiming, Throws.TypeOf<InvalidOperationException>());
			Assert.AreEqual(1f, m_Timing.TimeElapsed);
			Assert.AreEqual(1, m_Timing.UpdatesCount);
			Assert.AreEqual(1, m_Timing.CoroutinesCount);

			Assert.AreEqual(2, m_Progress);
			Assert.IsTrue(m_Timing.IsCoroutineAlive(coroutine));

			//
			// Update
			//
			m_Timing.UpdateCoroutines(1f);

			Assert.That(() => WiseTiming.CurrentTiming, Throws.TypeOf<InvalidOperationException>());
			Assert.AreEqual(2f, m_Timing.TimeElapsed);
			Assert.AreEqual(2, m_Timing.UpdatesCount);
			Assert.AreEqual(0, m_Timing.CoroutinesCount);

			Assert.AreEqual(3, m_Progress);
			Assert.IsFalse(m_Timing.IsCoroutineAlive(coroutine));
		}

		private IEnumerator WaitForEndOfFrameCoroutineCrt()
		{
			Assert.AreEqual(0, m_Timing.UpdatesCount);
			Assert.AreEqual(1f, m_Timing.TimeElapsed);
			Assert.AreEqual(1f, WiseTiming.DeltaTime);

			m_Progress++;

			yield return new WaitForEndOfFrame();

			Assert.AreEqual(0, m_Timing.UpdatesCount);
			Assert.AreEqual(1f, m_Timing.TimeElapsed);
			Assert.AreEqual(1f, WiseTiming.DeltaTime);

			m_Progress++;

			yield return new WaitForEndOfFrame();

			Assert.AreEqual(1, m_Timing.UpdatesCount);
			Assert.AreEqual(2f, m_Timing.TimeElapsed);
			Assert.AreEqual(1f, WiseTiming.DeltaTime);

			m_Progress++;
		}

		#endregion

		#region PausedCoroutine

		[Test]
		public void PausedCoroutine()
		{
			var coroutine = m_Timing.StartCoroutine(PausedCoroutineCrt(), this);

			coroutine.IsPaused = true;

			//
			// Update
			//
			m_Timing.UpdateCoroutines(1f);

			Assert.That(() => WiseTiming.CurrentTiming, Throws.TypeOf<InvalidOperationException>());
			Assert.AreEqual(1f, m_Timing.TimeElapsed);
			Assert.AreEqual(1, m_Timing.UpdatesCount);
			Assert.AreEqual(1, m_Timing.CoroutinesCount);

			Assert.AreEqual(0, m_Progress);
			Assert.IsTrue(m_Timing.IsCoroutineAlive(coroutine));

			coroutine.IsPaused = false;

			//
			// Update
			//
			m_Timing.UpdateCoroutines(1f);

			Assert.That(() => WiseTiming.CurrentTiming, Throws.TypeOf<InvalidOperationException>());
			Assert.AreEqual(2f, m_Timing.TimeElapsed);
			Assert.AreEqual(2, m_Timing.UpdatesCount);
			Assert.AreEqual(1, m_Timing.CoroutinesCount);

			Assert.AreEqual(1, m_Progress);
			Assert.IsTrue(m_Timing.IsCoroutineAlive(coroutine));

			//
			// Update
			//
			m_Timing.UpdateCoroutines(1f);

			Assert.That(() => WiseTiming.CurrentTiming, Throws.TypeOf<InvalidOperationException>());
			Assert.AreEqual(3f, m_Timing.TimeElapsed);
			Assert.AreEqual(3, m_Timing.UpdatesCount);
			Assert.AreEqual(0, m_Timing.CoroutinesCount);

			Assert.AreEqual(2, m_Progress);
			Assert.IsFalse(m_Timing.IsCoroutineAlive(coroutine));
		}

		private IEnumerator PausedCoroutineCrt()
		{
			Assert.AreEqual(1, m_Timing.UpdatesCount);
			Assert.AreEqual(2f, m_Timing.TimeElapsed);
			Assert.AreEqual(1f, WiseTiming.DeltaTime);

			m_Progress++;

			yield return null;

			Assert.AreEqual(2, m_Timing.UpdatesCount);
			Assert.AreEqual(3f, m_Timing.TimeElapsed);
			Assert.AreEqual(1f, WiseTiming.DeltaTime);

			m_Progress++;
		}

		#endregion

		#region NestedCoroutine

		[Test]
		public void NestedCoroutine()
		{
			var coroutine = m_Timing.StartCoroutine(NestedCoroutineCrt(), this);

			//
			// Update
			//
			m_Timing.UpdateCoroutines(1f);

			Assert.That(() => WiseTiming.CurrentTiming, Throws.TypeOf<InvalidOperationException>());
			Assert.AreEqual(1f, m_Timing.TimeElapsed);
			Assert.AreEqual(1, m_Timing.UpdatesCount);
			Assert.AreEqual(1, m_Timing.CoroutinesCount);

			Assert.AreEqual(3, m_Progress);
			Assert.IsTrue(m_Timing.IsCoroutineAlive(coroutine));

			//
			// Update
			//
			m_Timing.UpdateCoroutines(1f);

			Assert.That(() => WiseTiming.CurrentTiming, Throws.TypeOf<InvalidOperationException>());
			Assert.AreEqual(2f, m_Timing.TimeElapsed);
			Assert.AreEqual(2, m_Timing.UpdatesCount);
			Assert.AreEqual(0, m_Timing.CoroutinesCount);

			Assert.AreEqual(5, m_Progress);
			Assert.IsFalse(m_Timing.IsCoroutineAlive(coroutine));
		}

		private IEnumerator NestedCoroutineCrt()
		{
			Assert.AreEqual(0, m_Timing.UpdatesCount);
			Assert.AreEqual(1f, m_Timing.TimeElapsed);
			Assert.AreEqual(1f, WiseTiming.DeltaTime);

			m_Progress++;

			yield return EmptyCoroutineCrt();

			yield return SkipFrameCoroutineCrt();

			Assert.AreEqual(1, m_Timing.UpdatesCount);
			Assert.AreEqual(2f, m_Timing.TimeElapsed);
			Assert.AreEqual(1f, WiseTiming.DeltaTime);

			m_Progress++;
		}

		#endregion

		#region NestedCoroutine2

		[Test]
		public void NestedCoroutine2()
		{
			var coroutine = m_Timing.StartCoroutine(NestedCoroutine2Crt(), this);

			//
			// Update
			//
			m_Timing.UpdateCoroutines(1f);

			Assert.That(() => WiseTiming.CurrentTiming, Throws.TypeOf<InvalidOperationException>());
			Assert.AreEqual(1f, m_Timing.TimeElapsed);
			Assert.AreEqual(1, m_Timing.UpdatesCount);
			Assert.AreEqual(1, m_Timing.CoroutinesCount);

			Assert.AreEqual(5, m_Progress);
			Assert.IsTrue(m_Timing.IsCoroutineAlive(coroutine));

			//
			// Update
			//
			m_Timing.UpdateCoroutines(1f);

			Assert.That(() => WiseTiming.CurrentTiming, Throws.TypeOf<InvalidOperationException>());
			Assert.AreEqual(2f, m_Timing.TimeElapsed);
			Assert.AreEqual(2, m_Timing.UpdatesCount);
			Assert.AreEqual(0, m_Timing.CoroutinesCount);

			Assert.AreEqual(8, m_Progress);
			Assert.IsFalse(m_Timing.IsCoroutineAlive(coroutine));
		}

		private IEnumerator NestedCoroutine2Crt()
		{
			Assert.AreEqual(0, m_Timing.UpdatesCount);
			Assert.AreEqual(1f, m_Timing.TimeElapsed);
			Assert.AreEqual(1f, WiseTiming.DeltaTime);

			m_Progress++;

			yield return EmptyCoroutineCrt();

			yield return NestedCoroutineCrt();

			Assert.AreEqual(1, m_Timing.UpdatesCount);
			Assert.AreEqual(2f, m_Timing.TimeElapsed);
			Assert.AreEqual(1f, WiseTiming.DeltaTime);

			m_Progress++;
		}

		#endregion

		#region StopCoroutine

		[Test]
		public void StopCoroutine()
		{
			var coroutine = m_Timing.StartCoroutine(SkipFrameCoroutineCrt(), this);

			//
			// Update
			//
			m_Timing.UpdateCoroutines(1f);

			Assert.That(() => WiseTiming.CurrentTiming, Throws.TypeOf<InvalidOperationException>());
			Assert.AreEqual(1f, m_Timing.TimeElapsed);
			Assert.AreEqual(1, m_Timing.UpdatesCount);
			Assert.AreEqual(1, m_Timing.CoroutinesCount);

			Assert.AreEqual(1, m_Progress);
			Assert.IsTrue(m_Timing.IsCoroutineAlive(coroutine));

			//
			// Update
			//
			m_Timing.StopCoroutine(coroutine);

			Assert.That(() => WiseTiming.CurrentTiming, Throws.TypeOf<InvalidOperationException>());
			Assert.AreEqual(1f, m_Timing.TimeElapsed);
			Assert.AreEqual(1, m_Timing.UpdatesCount);
			Assert.AreEqual(0, m_Timing.CoroutinesCount);

			Assert.AreEqual(1, m_Progress);
			Assert.IsFalse(m_Timing.IsCoroutineAlive(coroutine));
		}

		#endregion

		#region StopCoroutineBySource

		[Test]
		public void StopCoroutineBySource()
		{
			var coroutine = m_Timing.StartCoroutine(SkipFrameCoroutineCrt(), this);

			//
			// Update
			//
			m_Timing.UpdateCoroutines(1f);

			Assert.That(() => WiseTiming.CurrentTiming, Throws.TypeOf<InvalidOperationException>());
			Assert.AreEqual(1f, m_Timing.TimeElapsed);
			Assert.AreEqual(1, m_Timing.UpdatesCount);
			Assert.AreEqual(1, m_Timing.CoroutinesCount);

			Assert.AreEqual(1, m_Progress);
			Assert.IsTrue(m_Timing.IsCoroutineAlive(coroutine));

			//
			// Update
			//
			m_Timing.StopCoroutineBySource(this);

			Assert.That(() => WiseTiming.CurrentTiming, Throws.TypeOf<InvalidOperationException>());
			Assert.AreEqual(1f, m_Timing.TimeElapsed);
			Assert.AreEqual(1, m_Timing.UpdatesCount);
			Assert.AreEqual(0, m_Timing.CoroutinesCount);

			Assert.AreEqual(1, m_Progress);
			Assert.IsFalse(m_Timing.IsCoroutineAlive(coroutine));
		}

		#endregion

		#region Exceptions_NotSupportedIteratorException

		[Test]
		public void Exceptions_NotSupportedIteratorException()
		{
			var coroutine = m_Timing.StartCoroutine(Exceptions_NotSupportedIteratorExceptionCrt(), this);

			//
			// Update
			//
			Assert.That(() => m_Timing.UpdateCoroutines(1f), Throws.TypeOf<NotSupportedException>());

			Assert.That(() => WiseTiming.CurrentTiming, Throws.TypeOf<InvalidOperationException>());
			Assert.AreEqual(1f, m_Timing.TimeElapsed);
			Assert.AreEqual(1, m_Timing.UpdatesCount);
			Assert.AreEqual(0, m_Timing.CoroutinesCount);

			Assert.AreEqual(1, m_Progress);
			Assert.IsFalse(m_Timing.IsCoroutineAlive(coroutine));

			//
			// Update
			//
			m_Timing.UpdateCoroutines(1f);

			Assert.That(() => WiseTiming.CurrentTiming, Throws.TypeOf<InvalidOperationException>());
			Assert.AreEqual(2f, m_Timing.TimeElapsed);
			Assert.AreEqual(2, m_Timing.UpdatesCount);
			Assert.AreEqual(0, m_Timing.CoroutinesCount);

			Assert.AreEqual(1, m_Progress);
			Assert.IsFalse(m_Timing.IsCoroutineAlive(coroutine));
		}

		private IEnumerator Exceptions_NotSupportedIteratorExceptionCrt()
		{
			Assert.AreEqual(0, m_Timing.UpdatesCount);
			Assert.AreEqual(1f, m_Timing.TimeElapsed);
			Assert.AreEqual(1f, WiseTiming.DeltaTime);

			m_Progress++;

			yield return new int[2];

			m_Progress++;

			throw new InvalidOperationException("Not supported iterator passed.");
		}

		#endregion

		#region Exceptions_PropagateExceptionHandler

		[Test]
		public void Exceptions_PropagateException()
		{
			var coroutine = m_Timing.StartCoroutine(Exceptions_SharedTestCrt(), this, WiseTiming.SourceInactiveBehaviour.StopCoroutine, (ex) => {
				Assert.AreEqual("Test", ex.Message);
				return WiseTiming.ExceptionHandlingAction.PropagateException;
			});

			//
			// Update
			//
			Assert.That(() => m_Timing.UpdateCoroutines(1f), Throws.TypeOf<InvalidOperationException>());

			Assert.That(() => WiseTiming.CurrentTiming, Throws.TypeOf<InvalidOperationException>());
			Assert.AreEqual(1f, m_Timing.TimeElapsed);
			Assert.AreEqual(1, m_Timing.UpdatesCount);
			Assert.AreEqual(0, m_Timing.CoroutinesCount);

			Assert.AreEqual(1, m_Progress);
			Assert.IsFalse(m_Timing.IsCoroutineAlive(coroutine));

			//
			// Update
			//
			m_Timing.UpdateCoroutines(1f);

			Assert.That(() => WiseTiming.CurrentTiming, Throws.TypeOf<InvalidOperationException>());
			Assert.AreEqual(2f, m_Timing.TimeElapsed);
			Assert.AreEqual(2, m_Timing.UpdatesCount);
			Assert.AreEqual(0, m_Timing.CoroutinesCount);

			Assert.AreEqual(1, m_Progress);
			Assert.IsFalse(m_Timing.IsCoroutineAlive(coroutine));
		}

		private IEnumerator Exceptions_SharedTestCrt()
		{
			m_Progress++;

			if (m_Progress > 0) {
				throw new InvalidOperationException("Test");
			}

			m_Progress++;

			// Need this or it will execute instantly like normal function.
			yield break;
		}

		#endregion

		#region Exceptions_CatchAndStopCoroutineHandler

		[Test]
		public void Exceptions_CatchAndStopCoroutineHandler()
		{
			var coroutine = m_Timing.StartCoroutine(Exceptions_SharedTestCrt(), this, WiseTiming.SourceInactiveBehaviour.StopCoroutine, (ex) => {
				Assert.AreEqual("Test", ex.Message);
				return WiseTiming.ExceptionHandlingAction.CatchAndStopCoroutine;
			});

			//
			// Update
			//
			m_Timing.UpdateCoroutines(1f);

			Assert.That(() => WiseTiming.CurrentTiming, Throws.TypeOf<InvalidOperationException>());
			Assert.AreEqual(1f, m_Timing.TimeElapsed);
			Assert.AreEqual(1, m_Timing.UpdatesCount);
			Assert.AreEqual(0, m_Timing.CoroutinesCount);

			Assert.AreEqual(1, m_Progress);
			Assert.IsFalse(m_Timing.IsCoroutineAlive(coroutine));

			//
			// Update
			//
			m_Timing.UpdateCoroutines(1f);

			Assert.That(() => WiseTiming.CurrentTiming, Throws.TypeOf<InvalidOperationException>());
			Assert.AreEqual(2f, m_Timing.TimeElapsed);
			Assert.AreEqual(2, m_Timing.UpdatesCount);
			Assert.AreEqual(0, m_Timing.CoroutinesCount);

			Assert.AreEqual(1, m_Progress);
			Assert.IsFalse(m_Timing.IsCoroutineAlive(coroutine));
		}

		#endregion

		#region Exceptions_CatchPopAndResumeCoroutine

		[Test]
		public void Exceptions_CatchPopAndResumeCoroutine()
		{
			var coroutine = m_Timing.StartCoroutine(Exceptions_CatchPopAndResumeCoroutineCrt(), this, WiseTiming.SourceInactiveBehaviour.StopCoroutine, (ex) => {
				Assert.AreEqual("Test", ex.Message);
				return WiseTiming.ExceptionHandlingAction.CatchPopAndResumeCoroutine;
			});

			//
			// Update
			//
			m_Timing.UpdateCoroutines(1f);

			Assert.That(() => WiseTiming.CurrentTiming, Throws.TypeOf<InvalidOperationException>());
			Assert.AreEqual(1f, m_Timing.TimeElapsed);
			Assert.AreEqual(1, m_Timing.UpdatesCount);
			Assert.AreEqual(0, m_Timing.CoroutinesCount);

			Assert.AreEqual(3, m_Progress);
			Assert.IsFalse(m_Timing.IsCoroutineAlive(coroutine));

			//
			// Update
			//
			m_Timing.UpdateCoroutines(1f);

			Assert.That(() => WiseTiming.CurrentTiming, Throws.TypeOf<InvalidOperationException>());
			Assert.AreEqual(2f, m_Timing.TimeElapsed);
			Assert.AreEqual(2, m_Timing.UpdatesCount);
			Assert.AreEqual(0, m_Timing.CoroutinesCount);

			Assert.AreEqual(3, m_Progress);
			Assert.IsFalse(m_Timing.IsCoroutineAlive(coroutine));

			// Coroutine will be stuck trying to get past the exception.
		}

		private IEnumerator Exceptions_CatchPopAndResumeCoroutineCrt()
		{
			m_Progress++;

			yield return Exceptions_SharedTestCrt();

			m_Progress++;
		}

		#endregion
	}
}