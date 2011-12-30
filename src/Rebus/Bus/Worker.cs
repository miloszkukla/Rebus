using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;
using System.Transactions;
using Rebus.Logging;
using Rebus.Messages;

namespace Rebus.Bus
{
    /// <summary>
    /// Internal worker thread that continually attempts to receive messages and dispatch to handlers.
    /// </summary>
    public class Worker : IDisposable
    {
        static readonly ILog Log = RebusLoggerFactory.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Keeps count of worker thread IDs.
        /// </summary>
        static int workerThreadCounter;

        /// <summary>
        /// Caching of dispatcher methods
        /// </summary>
        readonly ConcurrentDictionary<Type, MethodInfo> dispatchMethodCache = new ConcurrentDictionary<Type, MethodInfo>();

        readonly Thread workerThread;
        readonly Dispatcher dispatcher;
        readonly ErrorTracker errorTracker;
        readonly IReceiveMessages receiveMessages;
        readonly ISerializeMessages serializeMessages;

        volatile bool shouldExit;
        volatile bool shouldWork;

        public Worker(ErrorTracker errorTracker,
            IReceiveMessages receiveMessages,
            IActivateHandlers activateHandlers,
            IStoreSubscriptions storeSubscriptions,
            ISerializeMessages serializeMessages,
            IStoreSagaData storeSagaData,
            IInspectHandlerPipeline inspectHandlerPipeline)
        {
            this.receiveMessages = receiveMessages;
            this.serializeMessages = serializeMessages;
            this.errorTracker = errorTracker;
            dispatcher = new Dispatcher(storeSagaData, activateHandlers, storeSubscriptions, inspectHandlerPipeline);

            workerThread = new Thread(MainLoop) { Name = GenerateNewWorkerThreadName() };
            workerThread.Start();

            Log.Info("Worker {0} created and inner thread started", WorkerThreadName);
        }

        /// <summary>
        /// Event that will be raised whenever dispatching a given message has failed MAX number of times
        /// (usually 5 or something like that).
        /// </summary>
        public event Action<ReceivedTransportMessage, string> MessageFailedMaxNumberOfTimes = delegate { };

        /// <summary>
        /// Event that will be raised each time message delivery fails.
        /// </summary>
        public event Action<Worker, Exception> UserException = delegate { };

        /// <summary>
        /// Event that will be raised if an exception occurs outside of user code.
        /// </summary>
        public event Action<Worker, Exception> SystemException = delegate { };

        public void Start()
        {
            Log.Info("Starting worker thread {0}", WorkerThreadName);
            shouldWork = true;
        }

        public void Pause()
        {
            Log.Info("Pausing worker thread {0}", WorkerThreadName);
            shouldWork = false;
        }

        public void Stop()
        {
            Log.Info("Stopping worker thread {0}", WorkerThreadName);
            shouldWork = false;
            shouldExit = true;
        }

        public void Dispose()
        {
            Log.Info("Disposing worker thread {0}", WorkerThreadName);

            if (shouldWork)
            {
                Log.Info("Worker thread {0} is currently working", WorkerThreadName);
                Stop();
            }

            if (!workerThread.Join(TimeSpan.FromSeconds(30)))
            {
                Log.Info("Worker thread {0} did not exit within 30 seconds - aborting!", WorkerThreadName);
                workerThread.Abort();
            }
        }

        public string WorkerThreadName
        {
            get { return workerThread.Name; }
        }

        void MainLoop()
        {
            while (!shouldExit)
            {
                if (!shouldWork)
                {
                    Thread.Sleep(20);
                    continue;
                }

                try
                {
                    TryProcessIncomingMessage();
                }
                catch (Exception e)
                {
                    // if there's two levels of TargetInvocationExceptions, it's user code that threw...
                    if (e is TargetInvocationException && e.InnerException is TargetInvocationException)
                    {
                        UserException(this, e.InnerException.InnerException);
                    }
                    else
                    {
                        SystemException(this, e);
                    }
                }
            }
        }

        void TryProcessIncomingMessage()
        {
            using (var transactionScope = new TransactionScope())
            {
                var transportMessage = receiveMessages.ReceiveMessage();

                if (transportMessage == null)
                {
                    Thread.Sleep(20);
                    return;
                }

                var id = transportMessage.Id;
                var label = transportMessage.Label;

                if (errorTracker.MessageHasFailedMaximumNumberOfTimes(id))
                {
                    Log.Error("Handling message {0} has failed the maximum number of times", id);
                    MessageFailedMaxNumberOfTimes(transportMessage, errorTracker.GetErrorText(id));
                    errorTracker.StopTracking(id);
                }
                else
                {
                    try
                    {
                        var message = serializeMessages.Deserialize(transportMessage);
                        var returnAddress = message.GetHeader(Headers.ReturnAddress);

                        using (MessageContext.Enter(returnAddress))
                        {
                            foreach (var logicalMessage in message.Messages)
                            {
                                var typeToDispatch = logicalMessage.GetType();

                                Log.Debug("Dispatching message {0}: {1}", id, typeToDispatch);

                                GetDispatchMethod(typeToDispatch).Invoke(this, new[] { logicalMessage });
                            }
                        }
                    }
                    catch (Exception exception)
                    {
                        Log.Error(exception, "Handling message {0} ({1}) has failed", label, id);
                        errorTracker.TrackDeliveryFail(id, exception);
                        throw;
                    }
                }

                transactionScope.Complete();
                errorTracker.StopTracking(id);
            }
        }

        MethodInfo GetDispatchMethod(Type typeToDispatch)
        {
            MethodInfo method;
            if (dispatchMethodCache.TryGetValue(typeToDispatch, out method))
            {
                return method;
            }

            var newMethod = GetType()
                .GetMethod("DispatchGeneric", BindingFlags.Instance | BindingFlags.NonPublic)
                .MakeGenericMethod(typeToDispatch);

            dispatchMethodCache.TryAdd(typeToDispatch, newMethod);

            return newMethod;
        }

        /// <summary>
        /// Private strongly typed dispatcher method. Will be invoked through reflection to allow
        /// for some strongly typed interaction from this point and on....
        /// </summary>
        internal void DispatchGeneric<T>(T message)
        {
            dispatcher.Dispatch(message);
        }

        string GenerateNewWorkerThreadName()
        {
            return string.Format("Rebus worker #{0}", Interlocked.Increment(ref workerThreadCounter));
        }
    }
}