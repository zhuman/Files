using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vanara.PInvoke;

namespace Files.App.Helpers
{
	public static partial class Win32Helper
    {
		public class STAThreadPool
		{
			private ConcurrentBag<DispatcherQueueController> dispatcherQueues = new();
			private ConcurrentBag<DispatcherQueueController> availableQueues = new();

			public STAThreadPool(int initialSize)
			{
				for (int i = 0; i < initialSize; i++)
				{
					var controller = DispatcherQueueController.CreateOnDedicatedThread();
					dispatcherQueues.Add(controller);
					availableQueues.Add(controller);
				}
			}

			public Task<T?> EnqueueTask<T>(Func<Task<T>> func)
			{
				if (!availableQueues.TryTake(out var controller))
				{
					controller = DispatcherQueueController.CreateOnDedicatedThread();
					dispatcherQueues.Add(controller);
				}
				var taskCompletionSource = new TaskCompletionSource<T?>();
				controller.DispatcherQueue.TryEnqueue(async () =>
				{
					try
					{
						var result = await func();
						taskCompletionSource.SetResult(result);
					}
					catch (Exception ex)
					{
						taskCompletionSource.SetResult(default);
						App.Logger.LogWarning(ex, ex.Message);
					}
					finally
					{
						availableQueues.Add(controller);
					}
				});
				return taskCompletionSource.Task;
			}

			public Task EnqueueTask(Func<Task> func)
			{
				if (!availableQueues.TryTake(out var controller))
				{
					controller = DispatcherQueueController.CreateOnDedicatedThread();
					dispatcherQueues.Add(controller);
				}
				var taskCompletionSource = new TaskCompletionSource();
				controller.DispatcherQueue.TryEnqueue(async () =>
				{
					try
					{
						await func();
						taskCompletionSource.SetResult();
					}
					catch (Exception ex)
					{
						taskCompletionSource.SetResult();
						App.Logger.LogWarning(ex, ex.Message);
					}
					finally
					{
						availableQueues.Add(controller);
					}
				});
				return taskCompletionSource.Task;
			}

			public Task EnqueueTask(Action action)
			{
				return EnqueueTask(async () => action);
			}

			public void TrimExcess()
			{
				while (dispatcherQueues.Count > Environment.ProcessorCount && availableQueues.TryTake(out var controller))
				{
					// ConcurrentBag doesn't have a TryRemove method, so we just discard one random item
					dispatcherQueues.TryTake(out _);
					controller.ShutdownQueueAsync().AsTask().Wait();
				}
			}
		}

		private static STAThreadPool staThreadPool = new(Environment.ProcessorCount);

		public static Task StartSTATask(Func<Task> func)
		{
			return staThreadPool.EnqueueTask(func);
		}

		public static Task StartSTATask(Action action)
		{
			return staThreadPool.EnqueueTask(action);
		}

		public static Task<T?> StartSTATask<T>(Func<T> func)
		{
			return staThreadPool.EnqueueTask(async () => func());
		}

		public static Task<T?> StartSTATask<T>(Func<Task<T>> func)
		{
			return staThreadPool.EnqueueTask(func);
		}


	}
}
