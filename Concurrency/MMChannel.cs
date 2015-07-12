/********************************************************************************************************************************************************
 * The goal of the AlphaSystematics Project is create an open-source system for forward-testing systematic strategies with live market data and trade feeds.
 * It enables strategies developed in Excel to be connected to trading venues via industry standard FIX messaging.
 * 
 * Copyright (C) 2009  Antonio Tapper. www.alphasystematics.org

 * This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or (at your option) any later version.

 * This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more details.

 * You should have received a copy of the GNU General Public License along with this program.  If not, see <http://www.gnu.org/licenses/>.
********************************************************************************************************************************************************/

using System;
using System.Collections.Generic;
using System.Threading;
using System.Text;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;

namespace com.alphaSystematics.concurrency
{
    public unsafe struct ControlData
    {
        public bool debug;
        public bool test;
        public int ds_type;
        public int queueAddPosition;
        public int queueTakePosition;
        public int initialCount;
        public int stackAddTakePosition;
        public long totalItemsEnqueued;
        public long totalItemsDequeued;
        public bool isInitialized;
        public bool areResultsLogged;
        public int startTimeLength;
        public fixed char startTime[30];
        public long startTimeTicks;
        public int endTimeLength;
        public fixed char endTime[30];
        public long endTimeTicks;
        public long ticksPerItem;
        public int throughput;
        public long microseconds;
        public long nanoseconds;
        public long testPutSum;
        public long testTakeSum;
        public bool shutdownFlag;
        public int reservations;
    }


    #region Class description

    // Windows Data Alignment on IPF X86 and X64 by Kang Su Gatlin. referred to on page 492 of Concurrent Programming on Windows

    // MemoryMappedQueue implements a fixed-length memory mapped file based queue with blocking put and take methods 
    // controlled by a pair of counting semaphores.
    // The ConsumerSemaphore represents the number of items that can be removed from the queue and is 
    // initially set to zero.
    // ProducerSemaphore represents the number of items that can be inserted into the queue and is 
    // initially set to the capacity of the queue.

    // A 'take' operation first requires that a permit be obtained from ConsumerSemaphore.
    // This succeeds immediately if the queue is non-empty or blocks until the queue becomes non-empty
    // Once the permit is obtained then the data structure is locked with a mutex then an 
    // element is removed from the head of the list and  a permit is released to the Producersemaphore.

    // The 'put' operation works conversely
    // On exit from either 'put' or 'take' the sum of the counts of both semaphores always equals the capacity of the buffer

    // The semaphores do not have 'thread affinity' (or coherence) so the Producer Semaphore can be released by the Consumer 
    // thread and vice-versa but the mutex does have and so must be released by the theead that acquires it.

    // The element could be guaranteed to be an object 
    //  We might declare the data structure to accept only objects and box any primitive if, for example, we wanted to be able 
    // to call Dispose on them. We would then have to box any primitive values used as elements
    // See Joe Duffy's Concurrent Programming in Windows, chapter 10, Memory Models and Lock Freedom page 527
    // public class MMQueueDEV<E> where E : class | public class MMQueueDEV<E> where E : struct
    #endregion Class description


    public class MMChannel : IDisposable
    {
        // See Concurrent Programming on Windows, J.Duffy, Chap 5 Windows Kernel Synchronization, pg 225 for Queue algorithm
        #region constructor

        private static MMChannel channel;
        private readonly static object lockConstructor = new object();

        public static MMChannel GetInstance(string ipcName, int fileSize, int viewSize, int capacity, 
            bool debug = false, bool test = false, DataStructureType dsType = DataStructureType.Queue) 
        {
            // The mutex is used to ensure atomic
            // creation and initialization of the IPC artefacts. If another process has already acquired the mutex then the method 
            // will return the artefacts created by the first and only process to create them

            // Ensure that even if more than one thread in the same process attempts to create a channel - referencing the system-wide IPC artefacts
            // only one instance will be created in a process. Not really necessary as we have a system-wide mutex and semaphores so it doesn't
            // matter if this class is instantiated more than once but seems cleaner as we also don't need more than one instance

            // Why I didn't use static lazy initialization. See Note 1. Another possible initialization method
            lock (lockConstructor)
            {
                if (channel == null) channel = new MMChannel(ipcName, fileSize, viewSize, capacity, debug, test, dsType);
            }

            return channel; 
        }

        private MMChannel(string ipcName, int fileSize, int viewSize, int capacity, 
            bool debug = false, bool test = false, DataStructureType dsType = DataStructureType.Queue) 
        {
            // We received _ipcFileName, fileSize, viewSize, collection type, timeout and capacity in the constructor
            _fileSize = fileSize; _viewSize = viewSize; _capacity = capacity; _ipcName = ipcName;
            _dsType = dsType; _debug = debug; _test = test; 

            if (ipcName.Length == 0 || _fileSize <= 0 || _viewSize <= 0 || _capacity <= 0)
            {
                string msg = string.Format("Invalid arguments (ipcName {0}, FileSize {1}, ViewSize {2}, Capacity {3}", 
                    ipcName, _fileSize, viewSize, _capacity);
                throw new Exception(msg);
            }
            // The capacity is the number of views, effectively elements in a queue, to be created in the file
            // so the number of elements times the size of each element must not be greater than the size of the file
            int cap_times_view = _capacity * _viewSize;
            if (_capacity * _viewSize > _fileSize)
            {
                string msg = "Invalid arguments (Capacity * ViewSize " + cap_times_view +
                                " > FileSize " + _fileSize + ") passed to Memory Mapped File constructor";
                throw new Exception(msg);
            }
            
            // Create the IPC artefact names by adding the pre-defined names to the user requested queue name
            _consumerSemaphoreName = IPCName + _consumerSemaphoreNameAppend; 
            _producerSemaphoreName = IPCName + _producerSemaphoreNameAppend; 
            _mutexLockChannelName = IPCName + _mutexLockChannelNameAppend;
            _memoryMappedDataFileName = IPCName + _memoryMappeDataFileNameAppend;
            _memoryMappedControlFileName = IPCName + _memoryMappedControlFileNameAppend;

            Start();
        }
        #endregion constructor

        #region variable declarations

        // space in a memory mapped file for the control variables
        const int CONTROL_DATA_FILE_SIZE = 1000; const int ZERO = 0;
        const int DEFAULT_TIMEOUT = System.Threading.Timeout.Infinite;

        private int _fileSize; private int _viewSize; private int _capacity; private bool _debug; private bool _test;

        // All the Inter Process artefacts need names so they can be looked up
        // Inter-process throttle on the number of items that can be enqueued. 
        protected Semaphore _consumerSemaphore; private String _consumerSemaphoreName;
        protected Semaphore _producerSemaphore; private String _producerSemaphoreName;

        // guarded by the mutex _mutexLockChannel
        protected string _ipcName = "";

        // The name of the queue is passed into the constructor and either creates a new one or looks up an exisiting one
        // It is pre-pended to the semaphore and mutex names to create names that can be looked up inter process
        // These names are hidden from client programs to try to avoid accidental (or malicious) name collisions
        // Inter-process lock to guard the mutable shared state - the queue or stack
        private Mutex _mutexLockChannel; private String _mutexLockChannelName;
 
        // Currently the data structure can be instantiated as a queue or a stack
        protected DataStructureType _dsType;

        // Append to IPCName to form names for semaphores and mutexes
        private static string strGUID = "_{5C00361E-3C88-48A7-BB0A-F6ADF376C5A1}"; // Guid.NewGuid().ToString("N");
        private string _consumerSemaphoreNameAppend = "_consumer_" + strGUID;
        private string _producerSemaphoreNameAppend = "_producer_" + strGUID;
        private string _mutexLockChannelNameAppend = "_channel_mutex_" +strGUID;

        // Memory mapped file for IPC
        private MemoryMappedFile _memoryMappedDataFile; private String _memoryMappedDataFileName;
        private MemoryMappedFile _memoryMappedControlFile; private String _memoryMappedControlFileName;

        // Random Access views of the memory mapped data file.
        private MemoryMappedViewAccessor[] _viewAccessor;

        // Random Access views of the memory mapped control file - channel control section
        private MemoryMappedViewAccessor _controlDataAccessor;

        // Append to _ipcName to form name 
        private static string _memoryMappeDataFileNameAppend = "_memoryMappedDataFileNameAppend_" + strGUID;
        private static string _memoryMappedControlFileNameAppend = "_memoryMappedControlFileNameAppend_" + strGUID;

        private bool _didThisThreadCreateTheMutex;

        #endregion variable declarations

        private void Start()
        {
            // Return values from creating the IPC artefacts
            bool IsChannelMutexOwned = false; bool IsProducerSemaphoreNew = false; bool IsConsumerSemaphoreNew = false;

            // Add the event handler for handling UI thread exceptions to the event. 
            // Application.ThreadException += new
            //    ThreadExceptionEventHandler(ErrorHandlerForm.Form1_UIThreadException);
            // Set the unhandled exception mode to force all Windows Forms  
            // errors to go through our handler. 
            // Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException); 

            // Add the event handler for handling non-UI thread exceptions to the event.  
            AppDomain.CurrentDomain.UnhandledException +=
                new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException); 

            #region Atomic IPC artefact creation
            // Trying to make this atomic so we just create the inter-process artefacts of the Channel once only in one thread 
            // Plan is get the mutex first then use it to lock other threads and processes out of the critical section that creates the 
            // semaphores, memory mapped files and anything else that may be added in the future
            // If the mutex creation returns a value of "Existing" then we skip the critical section as the semaphores etc
            // should already have been created by the first thread that created the mutex as new

            // Don't initially acquire the mutex unless you're sure that you can lock out other threads 
            // Cos if you do it increments the acquition count and subsequently you can acquire it and the release in a 
            // finally block all day but it'll never get reset to zero so only the main thread can ever own it
            // This is why the tests that directly executed Put and Take methods from the main thread worked fine
            // but any test executed in a background thread blocked indefinately

            // Bigger problem is even when I did initially acquire the mutex using the safe idiom in 
            // Concurrent Programming on Windows, Joe Duffy, Chap 5 Windows Kernel Synchronization page 214
            // as follows:
            //
            //      bool IsMutexOwned;
            //      _mutexLockQueue = new Mutex(true, _mutexLockQueueName, out IsMutexOwned);
            //      if (!IsMutexOwned) _mutexLockQueue.WaitOne();
            //      ... critical region, release etc....
            //
            // This doesn't work. I followed it with a block of code to create new semaphores, memory mapped files etc
            // and another 'else' block to open existing ones. Every run produced errors either trying to create files
            // that already exist or trying to open files that don't
            // It works as I've done it now where I don't acquire the mutex on creation, wait for it and then create new 
            // semaphores which returns a handle to an existing one if there is one. I also use the CreateOrOpen method to 
            // open or get a handle to the memory mapped files
            //
            // Could understand this not working if this MMChannel were shared between threads because the IsMutexOwned field is
            // not guarded and so could be modified by another thread before the if statement is executed but this class is 
            // supposed to be effectively Thread Local and I checked to see that one object is instantiated for each thread
            // 
            // Needs more investigation!!!
            #endregion Atomic IPC artefact creation

            _mutexLockChannel = new Mutex(false, _mutexLockChannelName, out IsChannelMutexOwned);
            // This critical section should be used to create ALL inter-process artefacts used in the system
            _mutexLockChannel.WaitOne();
            try
            {
                // Save the state of whether this thread originally owned the mutex or not for use in the Close() method
                _didThisThreadCreateTheMutex = IsChannelMutexOwned;

                _consumerSemaphore = new Semaphore(0, _capacity, _consumerSemaphoreName, out IsConsumerSemaphoreNew);
                _producerSemaphore = new Semaphore(_capacity, _capacity, _producerSemaphoreName, out IsProducerSemaphoreNew);
                _memoryMappedDataFile = MemoryMappedFile.CreateOrOpen(_memoryMappedDataFileName, _fileSize);
                _memoryMappedControlFile = MemoryMappedFile.CreateOrOpen(_memoryMappedControlFileName, CONTROL_DATA_FILE_SIZE); 

                if (_didThisThreadCreateTheMutex)
                {
                    string msg = string.Format("Start {0} Name = {1}, \n IsConsumerSemaphoreNew = {2}, IsProducerSemaphoreNew = {3}, IsMutexNew = {4}, Channel Type = {5}",
                        DateTime.Now, _memoryMappedDataFileName, IsConsumerSemaphoreNew, IsProducerSemaphoreNew, IsChannelMutexOwned, _dsType);
                    Console.WriteLine(msg);
                }

                // Create an array of views to access the data file
                _viewAccessor = (MemoryMappedViewAccessor[])new MemoryMappedViewAccessor[_capacity];

                // Populate the array of views from the memory mapped file. 
                // Each view starts at an offset calculated as the index times the size of the view and the size is specified as _viewSize
                for (int i = 0; i < _capacity; i++)
                {
                    _viewAccessor[i] = _memoryMappedDataFile.CreateViewAccessor(i * _viewSize, _viewSize);
                }

                // Create a view to access the control file - queue control section
                _controlDataAccessor = _memoryMappedControlFile.CreateViewAccessor(0, CONTROL_DATA_FILE_SIZE);

                ControlData data = default(ControlData);

                // Read the control data from the file. If this thread is the first to try to create the file then it will
                // not have been initialized
                _controlDataAccessor.Read(ZERO, out data);

                // Just need one thread to log results for the lifetime of the channel. 
                if (!data.isInitialized)
                {
                    data.queueAddPosition = 0;
                    data.queueTakePosition = 0;
                    data.ds_type = (int) _dsType;
                    data.initialCount = 0;
                    data.stackAddTakePosition = 0;
                    data.isInitialized = false;
                    data.areResultsLogged = false;
                    data.totalItemsDequeued = 0;
                    data.totalItemsEnqueued = 0;
                    data.startTimeLength = 0;
                    data.startTimeTicks = 0;
                    data.endTimeLength = 0;
                    data.endTimeTicks = 0;
                    data.ticksPerItem = 0;
                    data.throughput = 0;
                    data.microseconds = 0;
                    data.nanoseconds = 0;
                    data.testPutSum = 0;
                    data.testTakeSum = 0;
                    // Save the constructor parameters 
                    data.debug = _debug;
                    data.test = _test;

                    DateTime dtNow = DateTime.Now;
                    if (!data.debug) { data.startTimeTicks = dtNow.Ticks; }

                    string sTime = Convert.ToString(dtNow);
                    char[] cStart = sTime.ToCharArray();

                    data.startTimeLength = cStart.Length;
                    for (int k = 0; k < data.startTimeLength; k++) { unsafe { data.startTime[k] = cStart[k]; } }

                    data.startTimeTicks = DateTime.Now.Ticks;

                    data.shutdownFlag = false;
                    data.reservations = 0;

                    data.isInitialized = true;

                    // Save the isInitialized = true flag to the memory mapped file so we don't execute this code again
                    _controlDataAccessor.Write(ZERO, ref data);
                }
            }
            catch (Exception e) { Console.WriteLine(e); throw; }
            finally { MLockChannel.ReleaseMutex(); }
        }

        #region Properties (getter/setter methods)
        private Mutex MLockChannel { get { return _mutexLockChannel; } set { _mutexLockChannel = value; } }
        protected DataStructureType DSType { get { return _dsType; } set { _dsType = value; } }

        public string IPCName { get { return _ipcName; } }
        // public int FileSize { get { return _fileSize; } }
        // public int ViewSize { get { return _viewSize; } }
        // public int Capacity { get { return _capacity; } }

        #region Control Data properties

        public unsafe ControlData MMFControlData
        {
            // External - must be guarded by the mutex
            get 
            {
                ControlData data = default(ControlData);
                MLockChannel.WaitOne();
                try
                {
                    _controlDataAccessor.Read(ZERO, out data); 
                    return data;
                } 
                catch (Exception e) { Console.WriteLine(e); throw; }
                finally { MLockChannel.ReleaseMutex(); }
            }
            set
            {
                ControlData data = default(ControlData);

                MLockChannel.WaitOne();
                try
                {
                    data = value; _controlDataAccessor.Write(ZERO, ref data);
                }
                catch (Exception e) { Console.WriteLine(e); throw; }
                finally { MLockChannel.ReleaseMutex(); }
            }
        }

        public unsafe void ControlDataToString(ControlData data, String label = "")
        {
            MLockChannel.WaitOne();
            try
            {
                if (label != null && label.Trim().Length > 0) { Console.WriteLine(label); }
                Console.WriteLine("Debug = {0}", data.debug);
                Console.WriteLine("Test = {0}", data.test);
                Console.WriteLine("Data structure type = {0}", (DataStructureType)_dsType);
                Console.WriteLine("QueueAddPosition = {0}", data.queueAddPosition);
                Console.WriteLine("QueueTakePosition = {0}", data.queueTakePosition);
                Console.WriteLine("InitialCount = {0}", data.initialCount);
                Console.WriteLine("StackAddTakePosition = {0}", data.stackAddTakePosition);
                Console.WriteLine("TotalItemsEnqueued = {0}", data.totalItemsEnqueued);
                Console.WriteLine("TotalItemsDequeued = {0}", data.totalItemsDequeued);
                Console.WriteLine("IsInitialized = {0}", data.isInitialized);
                Console.WriteLine("AreResultsLogged = {0}", data.areResultsLogged);

                // Copy the fixed byte array to an object byte array then convert the object byte array to a string
                char[] bStart = new char[data.startTimeLength];
                for (int i = 0; i < data.startTimeLength; i++) { bStart[i] = data.startTime[i]; }
                Console.WriteLine("StartTime = {0}", new string(bStart));
                //Console.WriteLine("StartTimeTicks = {0}", data.startTimeTicks);
                char[] bEnd = new char[data.endTimeLength];
                for (int k = 0; k < data.endTimeLength; k++) { bEnd[k] = data.endTime[k]; }
                Console.WriteLine("EndTime = {0}", new string (bEnd));
                Console.WriteLine("EndTimeTicks = {0}", data.endTimeTicks);
                Console.WriteLine("TicksPerItem = {0}", data.ticksPerItem);
                Console.WriteLine("Throughput = {0} items/second", data.throughput);
                Console.WriteLine("Microseconds = {0} per item", data.microseconds);
                Console.WriteLine("Nanoseconds = {0} per item", data.nanoseconds);
                Console.WriteLine("Test Put Sum = {0} per item", data.testPutSum);
                Console.WriteLine("Test Take Sum = {0} per item", data.testTakeSum);

                Console.WriteLine("\n");

                Console.WriteLine("Channel Shutdown = {0}", data.shutdownFlag);
                Console.WriteLine("Count of reservations = {0}", data.reservations);

                Console.WriteLine("\n");

            }
            catch (Exception e) { Console.WriteLine(e); throw; }
            finally { MLockChannel.ReleaseMutex(); }
        }

        //public unsafe void ShutdownDataToString(ShutdownData data, String label = "")
        //{
        //    MLockShutdown.WaitOne();
        //    try
        //    {
        //        if (label != null && label.Trim().Length > 0) { Console.WriteLine(label); }
        //        Console.WriteLine("ShutdownFlag = {0}", data.shutdownFlag);
        //        Console.WriteLine("Reservations = {0}", data.reservations);

        //        Console.WriteLine("\n");
        //    }
        //    catch (Exception e) { Console.WriteLine(e); throw; }
        //    finally { MLockShutdown.ReleaseMutex(); }
        //}

 
        #endregion Control Data properties

        // Decodes byte array to unicode string.
        public static string ByteArrayToString(byte[] data)
        {
            Encoding utf16 = Encoding.Unicode;
            return utf16.GetString(data);
        }

        // Encodes, unicode, string to byte array.
        public static byte[] StringToByteArray(string data)
        {
            Encoding utf16 = Encoding.Unicode;
            return utf16.GetBytes(data);
        }

        #endregion Properties (getter/setter methods)

        #region Add/take elements 

        #region Put a Scalar

        public void Put<T>(T data, int timeoutMillis = DEFAULT_TIMEOUT ) where T : struct
        {
            ControlData controlData = default(ControlData);
    
            _producerSemaphore.WaitOne(timeoutMillis);

            try
            {
                MLockChannel.WaitOne(timeoutMillis);

                _controlDataAccessor.Read(ZERO, out controlData);

                if (controlData.shutdownFlag) { throw new Exception("Channel is shutdown - cannot enqueue any more items"); }

                // Increment the number of items in the queue waiting to be dequeued
                controlData.reservations++;

                int addPosition = controlData.queueAddPosition; 
                int originalAddPosition = addPosition;

                _viewAccessor[addPosition].Write(0, ref data);

                if (controlData.ds_type == (int)DataStructureType.Queue)
                {
                    controlData.queueAddPosition = (++addPosition == _capacity) ? 0 : addPosition;
                }
                else
                {
                    // Assuming the type defaults to Queue and the only alternative is a Stack
                    controlData.queueTakePosition = addPosition;
                    controlData.queueAddPosition = ++addPosition;
                }

                controlData.totalItemsEnqueued++;

                #region DEBUG
                // Attempt to catch ArrayIndexOutOfBoundsExceptions or data corruption due to cursors getting out of wack
                if (controlData.debug)
                {
                    int diff = Math.Abs(controlData.queueAddPosition - originalAddPosition);
                    if (!(diff == 1 || controlData.queueAddPosition == 0)) throw new Exception(string.Format
                        ("New Add Position = {0} Originally = {1}", controlData.queueAddPosition, originalAddPosition));
                }
                #endregion DEBUG

                // Currently in test mode ONLY integers can be processed
                if (controlData.test) { controlData.testPutSum += Convert.ToInt64(data); }
            }
            catch (Exception e) { Console.WriteLine(e); throw; }
            finally { _controlDataAccessor.Write(ZERO, ref controlData); MLockChannel.ReleaseMutex(); _consumerSemaphore.Release(); }

        }
        #endregion Put a Scalar

        #region Put an Array

        public void Put<T>(T[] data, int timeoutMillis = DEFAULT_TIMEOUT) where T : struct
        {
            // TODO Timeout not yet implemented as I haven't figured out what to do in that case - block forever
            ControlData controlData = default(ControlData);

            _producerSemaphore.WaitOne(timeoutMillis);

            try
            {
                MLockChannel.WaitOne(timeoutMillis);

                _controlDataAccessor.Read(ZERO, out controlData);

                if (controlData.shutdownFlag) { throw new Exception("Channel is shutdown - cannot enqueue any more items"); }

                // Increment the number of items in the queue waiting to be dequeued
                controlData.reservations++;

                int addPosition = controlData.queueAddPosition; 
                int originalAddPosition = addPosition;

                _viewAccessor[addPosition].Write(0, data.Length);
                _viewAccessor[addPosition].WriteArray(4, data, 0, data.Length);

                if (controlData.ds_type == (int)DataStructureType.Queue)
                {
                    controlData.queueAddPosition = (++addPosition == _capacity) ? 0 : addPosition;
                }
                else
                {
                    // Assuming the type defaults to Queue and the only alternative is a Stack
                    controlData.queueTakePosition = addPosition;
                    controlData.queueAddPosition = ++addPosition;
                }
                controlData.totalItemsEnqueued++;

                #region DEBUG
                // Attempt to catch ArrayIndexOutOfBoundsExceptions or data corruption due to cursors getting out of wack
                if (controlData.debug)
                {
                    int diff = Math.Abs(controlData.queueAddPosition - originalAddPosition);
                    if (!(diff == 1 || controlData.queueAddPosition == 0)) throw new Exception(string.Format
                        ("New Add Position = {0} Originally = {1}", controlData.queueAddPosition, originalAddPosition));
                }
                #endregion DEBUG
            }
            catch (Exception e) { Console.WriteLine(e); throw; }
            finally { _controlDataAccessor.Write(ZERO, ref controlData); MLockChannel.ReleaseMutex(); _consumerSemaphore.Release(); }
        }

        #endregion Put an Array

        #region Take a Scalar

        public T Take<T>(int timeoutMillis = DEFAULT_TIMEOUT) where T : struct
        {
            // TODO Timeout not yet implemented as I haven't figured out what to do in that case - block forever

            T data = default(T);
            ControlData controlData = default(ControlData);
            
            _consumerSemaphore.WaitOne(timeoutMillis);

            try
            {
                MLockChannel.WaitOne(timeoutMillis);

                _controlDataAccessor.Read(ZERO, out controlData);

                if (controlData.shutdownFlag && controlData.reservations == 0)
                    { throw new Exception("Channel is shutdown and empty - now disposing all resources"); }

                int takePosition = controlData.queueTakePosition; 
                int originalTakePosition = takePosition;

                _viewAccessor[takePosition].Read<T>(0, out data);

                if (controlData.ds_type == (int)DataStructureType.Queue)
                {
                    controlData.queueTakePosition = (++takePosition == _capacity) ? 0 : takePosition;
                }
                else
                {
                    // Assuming the type defaults to Queue and the only alternative is a Stack
                    controlData.queueAddPosition = takePosition;
                    controlData.queueTakePosition = --takePosition;
                }

                controlData.totalItemsDequeued++;
                controlData.reservations--;

                #region DEBUG
                // Attempt to catch ArrayIndexOutOfBoundsExceptions or data corruption due to cursors getting out of wack
                if (controlData.debug)
                {
                    int diff = Math.Abs(controlData.queueTakePosition - originalTakePosition);
                    if (!(diff == 1 || controlData.queueTakePosition == 0)) throw new Exception(string.Format
                        ("New Take Position = {0} Originally = {1}", controlData.queueTakePosition, originalTakePosition));
                }
                #endregion DEBUG

                // Currently in test mode ONLY integers can be processed
                if (controlData.test) { controlData.testTakeSum += Convert.ToInt64(data); }
            }
            catch (Exception e) { Console.WriteLine(e); throw; }
            finally { _controlDataAccessor.Write(ZERO, ref controlData); MLockChannel.ReleaseMutex(); _producerSemaphore.Release(); }

            return data;
            
            #region Type Parameters and Conversions
            // The most common scenario is when you want to perform a reference conversion:
            // StringBuilder Foo<T> (T arg)
            // {
            //     if (arg is StringBuilder)
            //     return (StringBuilder) arg; // Will not compile
            // }
            // Without knowledge of T’s actual type, the compiler is concerned that you might
            // have intended this to be a custom conversion. The simplest solution is to instead use
            // the as operator, which is unambiguous because it cannot perform custom conversions:
            // StringBuilder Foo<T> (T arg)
            // {
            //     StringBuilder sb = arg as StringBuilder;
            //     if (sb != null) return sb;
            // }
            // A more general solution is to first cast to object. 
            // This works because conversions to/from object are assumed not to be custom conversions, but reference or boxing/
            // unboxing conversions. In this case, StringBuilder is a reference type, so it has to be a reference conversion:
            // return (StringBuilder) (object) arg;
            #endregion Type Parameters and Conversions
            #region default generic types and explicitly nulling a reference
            // INFO This is one of the few cases where explicitly setting to null is necessary because the element wouldn't otherwise go out of scope
            // TODO when replaced by memory mapped file the 'view' will just be overwritten
            // NOTE: This does not release the memory!! Garbage collection is still necessary. All it does is let the GC know that
            // the object is dead when the object is checked during a collection
            // itegms[i] = default(E); // null;

            // E enull = default(E);
            // this needs to write the default value to the viewSize buffer
            // _accessor[i].Write(0, ref enull);

            // Could use items[i] = null if the element was guaranteed to be an object (see class declaration above)
            // We might declare the data structure to accept only objects and box any primitive if, for example, we wanted to be able 
            // to call Dispose on them. 
            // See Joe Duffy's Concurrent Programming in Windows, chapter 10, Memory Models and Lock Freedom page 527
            #endregion default generic types and explicitly nulling a reference
        }

        #endregion Take a Scalar

        #region Take an Array

        public int Take<T>(out T[] data, int timeoutMillis = DEFAULT_TIMEOUT) where T : struct
        {
            // TODO Timeout not yet implemented as I haven't figured out what to do in that case - block forever

            int numItems = 0;
            data = default(T[]);
            ControlData controlData = default(ControlData);

            _consumerSemaphore.WaitOne(timeoutMillis);

            try
            {
                MLockChannel.WaitOne(timeoutMillis);

                _controlDataAccessor.Read(ZERO, out controlData);

                if (controlData.shutdownFlag && controlData.reservations == 0) 
                    { throw new Exception("Channel is shutdown and empty - now disposing all resources"); }

                int takePosition = controlData.queueTakePosition; 
                int originalTakePosition = takePosition;

                #region Array size issue
                // Read an array of data items from the view and assign it to the output parameter - type T[]
                // The length of the array was written to the view by the Put method as an Int in 4 bytes starting at position 0
                // Seems unlikely that we would have an array of data bigger than 2 billion - odd but be careful if you change the 
                // array size to a long and the ReadInt32 to ReadInt64. Did that accidently without changing the return value of this
                // method. NUnit reported an arithmetic overflow exception but on the line "int numItems = 0";
                // Took a long time to find the real cause of the problem i.e. changing to ReadInt64
                #endregion Array size issue
                data = new T[_viewAccessor[takePosition].ReadInt32(0)];
                numItems = _viewAccessor[takePosition].ReadArray(4, data, 0, data.Length);

                if (controlData.ds_type == (int)DataStructureType.Queue)
                {
                    controlData.queueTakePosition = (++takePosition == _capacity) ? 0 : takePosition;
                }
                else
                {
                    // Assuming the type defaults to Queue and the only alternative is a Stack
                    controlData.queueAddPosition = takePosition;
                    controlData.queueTakePosition = --takePosition;
                }

                controlData.totalItemsDequeued++;
                controlData.reservations--;

                #region DEBUG
                // Attempt to catch ArrayIndexOutOfBoundsExceptions or data corruption due to cursors getting out of wack
                if (controlData.debug)
                {
                    int diff = Math.Abs(controlData.queueTakePosition - originalTakePosition);
                    if (!(diff == 1 || controlData.queueTakePosition == 0)) throw new Exception(string.Format
                        ("New Take Position = {0} Originally = {1}", controlData.queueTakePosition, originalTakePosition));
                }
                #endregion DEBUG
            }
            catch (Exception e) { Console.WriteLine(e); throw; }
            finally { _controlDataAccessor.Write(ZERO, ref controlData); MLockChannel.ReleaseMutex(); _producerSemaphore.Release(); }

            return numItems;

            #region Type Parameters and Conversions
            // The most common scenario is when you want to perform a reference conversion:
            // StringBuilder Foo<T> (T arg)
            // {
            //     if (arg is StringBuilder)
            //     return (StringBuilder) arg; // Will not compile
            // }
            // Without knowledge of T’s actual type, the compiler is concerned that you might
            // have intended this to be a custom conversion. The simplest solution is to instead use
            // the as operator, which is unambiguous because it cannot perform custom conversions:
            // StringBuilder Foo<T> (T arg)
            // {
            //     StringBuilder sb = arg as StringBuilder;
            //     if (sb != null) return sb;
            // }
            // A more general solution is to first cast to object. 
            // This works because conversions to/from object are assumed not to be custom conversions, but reference or boxing/
            // unboxing conversions. In this case, StringBuilder is a reference type, so it has to be a reference conversion:
            // return (StringBuilder) (object) arg;
            #endregion Type Parameters and Conversions
            #region default generic types and explicitly nulling a reference
            // See Joe Duffy's Concurrent Programming in Windows, chapter 10, Memory Models and Lock Freedom page 527
            #endregion default generic types and explicitly nulling a reference
        }

        #endregion Take an Array

        #endregion Add/take elements


        public bool Debug
        {
            // External - must be guarded by the mutex
            get
            {
                ControlData data = default(ControlData);
                MLockChannel.WaitOne();
                try
                {
                    _controlDataAccessor.Read(ZERO, out data);
                    return data.debug;
                }
                catch (Exception e) { Console.WriteLine(e); throw; }
                finally { MLockChannel.ReleaseMutex(); }
            }
            set
            {
                ControlData data = default(ControlData);

                MLockChannel.WaitOne();
                try
                {
                    data.debug = value; _controlDataAccessor.Write(ZERO, ref data);
                }
                catch (Exception e) { Console.WriteLine(e); throw; }
                finally { MLockChannel.ReleaseMutex(); }
            }
        }

        public bool Test
        {
            // External - must be guarded by the mutex
            get
            {
                ControlData data = default(ControlData);
                MLockChannel.WaitOne();
                try
                {
                    _controlDataAccessor.Read(ZERO, out data);
                    return data.test;
                }
                catch (Exception e) { Console.WriteLine(e); throw; }
                finally { MLockChannel.ReleaseMutex(); }
            }
            set
            {
                ControlData data = default(ControlData);

                MLockChannel.WaitOne();
                try
                {
                    data.test = value; _controlDataAccessor.Write(ZERO, ref data);
                }
                catch (Exception e) { Console.WriteLine(e); throw; }
                finally { MLockChannel.ReleaseMutex(); }
            }
        }


        public void clearTestData()
        {
            // External - must be guarded by the mutex

                ControlData data = default(ControlData);
                MLockChannel.WaitOne();
                try
                {
                    _controlDataAccessor.Read(ZERO, out data);

                    data.totalItemsEnqueued = 0;
                    data.ticksPerItem = 0;
                    data.nanoseconds = 0;
                    data.microseconds = 0;
                    data.throughput = 0;
                }
                catch (Exception e) { Console.WriteLine(e); throw; }
                finally { _controlDataAccessor.Write(ZERO, ref data); MLockChannel.ReleaseMutex(); }
         }


        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            // Set up uncaught exception handler in case some dodgy code throws a RunTimeException 
            // This won't work if the exception is passed to some even more dodgy 3rd psrty code that swallows
            // the exception. Does work in the case of dodgy 3rd party rogue code like ActiveMQ which kindly throws
            // some kind of runtime exception if you don't have a 'geronimo' jar in your classpath when you try to
            // instantiate a connectionFactory or ActiveMQConnectionFactory
            // Java version looks like this - ASExceptionHandler UEH = new ASExceptionHandler();
            //                                Thread.setDefaultUncaughtExceptionHandler(UEH);
            // Java also has per-thread scheduler handlers set up using the same class
            Console.Write(e.ExceptionObject.ToString());

        }

        #region Dispose of IPC artefacts

        // TODO Implement IDisposable 
        public void Report()
        {
            ControlData data = default(ControlData);

            MLockChannel.WaitOne();
            try
            {
                _controlDataAccessor.Read(ZERO, out data);

                // Just need one thread to log results from the lifetime of the channel. 
                if (!data.areResultsLogged)
                {
                    data.areResultsLogged = true;

                    DateTime dtNow = DateTime.Now;

                    string eTime = Convert.ToString(dtNow);
                    char[] cEnd = eTime.ToCharArray();

                    data.endTimeLength = cEnd.Length;
                    for (int k = 0; k < data.endTimeLength; k++) { unsafe { data.endTime[k] = cEnd[k]; } }
                    data.endTimeTicks = DateTime.Now.Ticks;

                    long elapsedTime = data.endTimeTicks - data.startTimeTicks;

                    // calculate throughput if any data was actually processed
                    if (data.totalItemsEnqueued > 0)
                    {
                        data.ticksPerItem = (int)(elapsedTime / data.totalItemsEnqueued);
                        TimeSpan elapsedSpan = new TimeSpan(data.ticksPerItem);
                        double milliSeconds = elapsedSpan.TotalMilliseconds;
                        data.nanoseconds = data.ticksPerItem * 100;
                        data.microseconds = data.nanoseconds / 1000;
                        data.throughput = (int)(1000000000 / data.nanoseconds);
                    }

                    // Print out the results
                    ControlDataToString(data, "MMChannel");

                    // Save the areResultsLogged = true flag to the memory mapped file so we don't execute this code again
                    _controlDataAccessor.Write(ZERO, ref data);
                }
            }
            catch (Exception e) { Console.WriteLine(e); throw; }
            finally
            {
                MLockChannel.ReleaseMutex();
            }
        }

        public void Dispose() // NOT virtual
        {
            Dispose(true);
            GC.SuppressFinalize(this); // Prevent finalizer from running.
        }

        public void shutdown()
        {
            // Goal is to shutdown gracefully so if there are still items in the queue then allow the consumer(s) to drain them
            // Once we have the mutex then the producers cannot enqueue any more items and once we release the mutex in here
            // the checks in the 'Put()' methods will prevent them doing so in the future
            // The consumers(s) will continue to drain the queue until it is empty 
            ControlData data = default(ControlData);

            MLockChannel.WaitOne();
            try
            {
                _controlDataAccessor.Read(ZERO, out data);

                data.shutdownFlag = true;
                // Dispose();
            }
            catch (Exception e) { Console.WriteLine(e); throw; }
            finally { _controlDataAccessor.Write(ZERO, ref data); MLockChannel.ReleaseMutex(); }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Call Dispose() on other objects owned by this instance.
                // You can reference other finalizable objects here.
                Report();
            }

            // Release unmanaged resources owned by (just) this object.
            _consumerSemaphore.Dispose();
            _producerSemaphore.Dispose();

            for (int i = 0; i < _capacity; i++) { _viewAccessor[i].Dispose(); }

            _controlDataAccessor.Dispose();

            #region Garbage Collection and Finalizers
            // I forgot to dispose of the memory mapped files - oops!
            // This bug survived literally hundreds of tests runs because I was running them in groups of three, each 
            // creating a memory mapped file with a different name.
            // It wasn't until I tried running the same test re[eatedly that it failed - throwing an Exception that 
            // the file already exists (I was using the CreateNew() method to create them)
            // The mm file has a built-in finalizer which gets rid of it when the GC collector runs so I gues that by the time
            // you've recycled round to the first test the GC has disposed of the file it created in its previous incarnation
            // Once I started repeating the same test it only took two or three goes before the test tried to create a file
            // with the same name as one left over from the previous run
            // Moral of the story is you can't depend on finalizers being run in a timely fashion or in fact ever which 
            // any fule know of course but the first time I've seen it in action
            #endregion Garbage Collection and Finalizers

            _memoryMappedDataFile.Dispose();
            _memoryMappedControlFile.Dispose();

            _mutexLockChannel.Dispose();
        }
        ~MMChannel()
        {
            Dispose(false);
        }

        // Dispose is overloaded to accept a bool disposing flag. The parameterless version is not declared as virtual 
        // and simply calls the enhanced version with true.
        // The enhanced version contains the actual disposal logic and is protected and virtual; this provides a safe 
        // point for subclasses to add their own disposal logic.
        // The disposing flag means it’s being called “properly” from the Dispose method rather than in “last-resort mode” 
        // from the finalizer. The idea is that when called with disposing set to false, this method should not, in general, 
        // reference other objects with finalizers (because such objects may themselves have been finalized and
        // so be in an unpredictable state). This rules out quite a lot! Here are a couple of tasks it can still perform in 
        // last-resort mode, when disposing is false:
        // • Releasing any direct references to operating system resources (obtained, perhaps, via a P/Invoke call to the Win32 API)
        // • Deleting a temporary file created on construction
        // To make this robust, any code capable of throwing an exception should be wrapped n a try/catch block, and the exception, 
        // ideally, logged. Any logging should be as simple and robust as possible.
        // Notice that we call GC.SuppressFinalize in the parameterless Dispose method—this prevents the finalizer from running when 
        // the GC later catches up with it. Technically, this is unnecessary, as Dispose methods must tolerate repeated calls. However,
        // doing so improves performance because it allows the object (and its referenced objects) to be garbage-collected in a single cycle.


        #endregion Dispose of IPC artefacts
    }



}


#region Note 1. Another possible initialization method
// Doesn't seem feasible though because either the variables should be readonly or guarder with a lock
// Not possible to set the values of readonly variables except in a static constructor or variable initializer and if we need to
// guard with a lock then no point trying to use lazy static initialization

// private static readonly string ipcName;
// private static readonly int fileSize;
// private static readonly int viewSize;
// private static readonly int capacity;
// private static readonly bool debug;
// private static readonly bool test;
// private static readonly DataStructureType dsType;

// public static void init(string aIpcName, int aFileSize, int aViewSize, int aCapacity,
// bool aDebug = false, bool aTest = false, DataStructureType aDsType = DataStructureType.Queue)
// {
//     ipcName = aIpcName;
//     fileSize = aFileSize;
//     viewSize = aViewSize;
//     capacity = aCapacity;
//     debug = aDebug;
//     test = aTest;
//     dsType = aDsType;
// }

//     private class LazyResourceHolder {

// Problem. How do we get the parameters to pass to the static initializer? 
// Store them somewhere externally before calling the getResource() method?
//         private static MMChannel channel; // new MMChannel(ipcName, fileSize, viewSize, capacity, debug, test, dsType);

//         public static MMChannel getResource(string ipcName, int fileSize, int viewSize, int capacity,
//                                         bool debug = false, bool test = false, DataStructureType dsType = DataStructureType.Queue) {

//         return LazyResourceHolder.channel; 
//         }
//     }

// Using static lazy initialization. The static LazyResourceHolder inner class only exists to create the resource the first time it 
// is referenced by calling getResource()
// return LazyResourceHolder.getResource(ipcName, fileSize, viewSize, capacity, debug, test, dsType);

#endregion another possible initialization method

//public void shutdown() 
//{
//    // Goal is to shutdown gracefully so if there are still items in the queue then allow the consumer(s) to drain them
//    // Once we have the mutex then the producers cannot enqueue any more items and once we release the mutex in here
//    // the checks in the 'Put()' methods will prevent them doing so in the future
//    // The consumers(s) will continue to drain the queue until it is empty 
//    ShutdownData shutdownData = default(ShutdownData);

//    MLockShutdown.WaitOne();
//    try
//    {
//        _shutdownDataAccessor.Read(ZERO, out shutdownData); 

//        shutdownData.shutdownFlag = true;
//    }
//    catch (Exception e) { Console.WriteLine(e); throw; }
//    finally { _shutdownDataAccessor.Write(ZERO, ref shutdownData); MLockShutdown.ReleaseMutex(); }
//}


/***********************************
Memory Pressure
The runtime decides when to initiate collections based on a number of factors, including
the total memory load on the machine. If your program allocates unmanaged
memory (Chapter 25), the runtime will get an unrealistically optimistic perception
of its memory usage, because the CLR knows only about managed memory. You
can mitigate this by telling the CLR to assume a specified quantity of unmanaged
memory has been allocated, by calling GC.AddMemoryPressure. To undo this (when
the unmanaged memory is released) call GC.RemoveMemoryPressure.
**********************************/




// Mutex
// ------
// aka Mutant is a kernel object meant solely for synchronization purposes by facilitating building mutually 
// exclusive critical regions (Chap 2 Concurrent programming in Windows)
// Accomplished by the mutex object transitioning between signaled and non-signaled states atomically
// Signaled = available for acquisition, there is no current owner
// A subsequent wait will atomically transfer the mutex into a non-signaled state. Atomic because the Windows kernel handles
// cases in which multiple threads wait on the same mutex simultaneously. Only one will be permitted to initiate
// the transition - all the others will see the mutex as non-signaled
// Non-signaled = there is a single thread that currently owns the mutex
// See page 211 for more detail about the CLR's use of the Thread.BeginThreadAffinity and EndThreadAffinity APIs to
// notify hosts when affinity has been acquired and released (and therefore the mutex)
//
// CHECK THE RETURN VALUES OF EACH API CALL
// 
// In Win32 API use CreateMutex (or CreateMutexEx from Vista onwards - see page 213)
// With CreateMutex if bInitialOwner is true then the calling thread will be the owner of the mutex and it will be in
// a non-signaled state so no other thread can locate the mutex (e.g. by a name lookup) before the caller is able to acquire it
// There is also an optional security descriptor to control subsequent access to the mutex WHICH IT IS RECOMMENDED TO USE
// The name can be null if you don't need to share the mutex between processes or look it up by name later on
// Because any program on the machine can create a mutex with the same name you should carefully name them and ensure they
// are protected by ACLs
// In CLR - m_mutex = new Mutex()  - Returns a new mutex in the signaled state i.e. caller must wait on it to acquire it
//          m_mutex = new Mutex(bool initiallyOwned true);
//                                 - If true, returns a new mutex in the non-signaled state i.e. caller is the owner of the mutex
// More complicated now we are using named mutexes
//          m_mutex = new Mutex(bool initiallyOwned true, string name);
//          m_mutex = new Mutex(bool initiallyOwned true, string name, out bool createdNew);
//          m_mutex = new Mutex(bool initiallyOwned true, string name, out bool createdNew, MutexSecurity mutexSecurity);
//          If a mutex with the same name already exists then the new mutex will reference that object
//          Otherwise a new one is created. CRUCIAL TO CHECK AND IF NECCESSARY AQUIRE THE MUTEX BEFORE PROCEEDING
//          e.g. if ( !CreatedNew ) mutex.WaitOne();
// The handle returned from CreateMutex MUST eventually be closed with the CloseHandle API
// When the last handle to the mutex is closed the kernel object manager will destroy the object and reclaim its resources
// Mutex implements IDisposable. Calling Close or Dispose will eagerly release the sole handle which is also 
// protected by a finalizer that will close it if you forget but good practice to close it explicitly
// (Finalizers add GC work and are unreliable)
// 
// If you want to access an existing mutex and the program would be in an invalid state if it could not be found
// then you can use the underlying WIN32 API check GetLastError etc... or use the CLRs static APIs
// public static Mutex OpenExisting(string name);
// public static Mutex OpenExisting(string name, MutexRights rights);
// These throw WaithandleCannotBeOpenedException if the mutex cannot be found
// 
// Acquiring and Releasing
// =======================
// A mutex is acquired by waiting on it i.e. Waitone, WaitAll, WaitAny
// When the API returns successfully the the mutex is acquired by the calling thread and is in a non-signaled state
// Release it with public void ReleaseMutex();
// If the thread does not own the mutex then Applicationexception will be thrown
// Once released the mutex becomes signaled and any thread may acquire it
// Only one thread is awoken, usually in FIFO order. and is guaranteed to acquire the mutex. There is no barging allowed
// This can increase the rate of lock convoys see chapter 11 concurrency hazards
// Many locks in Windows are unfair and allow barging to improve scalability and reduce convoys but mutex isn't one of them
//
// Mutex supports recursive acquires
// If the owning thread waits on the mutex the wait is satisfied immediately even though the object is non-signaled
// An internal recursion counter is maintained, starts at zero and is incremented with each acquistion and decremented
// with each release. When the count drops to zero the mutex becomes signaled and available to other threads
// Recursion produces brittle designs and reliability problems - see chapter 11 concurrency hazards
// 
// Abandoned Mutexes
// If the mutex is not correctly released before its owning thread terminates it becomes abandoned
// Perhaps the finally block didn't get executed (see chapter 4 Advanced Thread for details on this)
// A waiting thread will be awakened as though the mutex had been released and will acquire it but also be told that it was
// abandoned via the AbandonedmutexException being thrown
// For WaitAll and WaitAny the index of the first abandoned mutex from the array passed to the API is captured in the 
// Exception's MutexIndex property and the mutex object is in the Mutex property
// 
// The receiving thread MUST release the mutex
//
// Abandoned mutexes indicate that state corruption could have occurred
// Machine-wide mutexes - any state that they guard may have been corrupted
// May be able to fix it but if not the could require the machine to be re-booted
// Don't persist state e.g. writing to a file or database with mutexes because that would be really difficult to recover
//
// SEMAPHORES
// ==========
// Do not have thread affinity. Used to throttle access to a resource
// When the count is non-zero the semaphore is signaled and available for a thread to acquire a permit
// When the count reaches zero the semaphore is non-signaled and no thread can acquire a permit
// 
// API is similar to Mutex
// public Semaphore(int initialiCount, int maximumCount)
// public Semaphore(int initialiCount, int maximumCount, string name)
// public Semaphore(int initialiCount, int maximumCount, string name, out bool createdNew)
// public Semaphore(int initialiCount, int maximumCount, string name, out bool createdNew, SemaphoreSecurity semaphoreSecurity)
//
// public static OpenExisting(string name)
// public static OpenExisting(string name, SemaphoreRights rights)
//
// If you try to create a named Semaphore and it already exusts its not so serious as with mutexes because the calling thread
// doesn't "own" it but stiil indicates a problem because the specified counts will have been ignored
//
// Take a Semaphores by waiting on it i.e. Waitone, WaitAll, WaitAny
// In .NET can only acquire one permit at a time unlike Java
// because there is no thread affinity there is no concept of and "abandened semaphore" or of recursion
// 
// Release allows more than one permit to be returned
// public int Release();
// public int Release(int releaseCount);
// If the release would have caused the Semaphore's count to exceed its maximum value then SemaphoreFullException is thrown
// 




//public bool WaitOneWithCancellation(WaitHandle handle, int millisecondsTimeout = -1)
//{
//    #region Summary:
//    //     Blocks the current thread until the current System.Threading.WaitHandle receives
//    //     a signal, using a 32-bit signed integer to measure the time interval.
//    // Parameters:
//    //   millisecondsTimeout:
//    //     The number of milliseconds to wait, or System.Threading.Timeout.Infinite
//    //     (-1) to wait indefinitely - couldn't get thi to work!
//    // Returns:      true if the current instance receives a signal; otherwise, false.
//    // Exceptions:
//    //   System.ObjectDisposedException:            The current instance has already been disposed.
//    //   System.ArgumentOutOfRangeException:        millisecondsTimeout is a negative number other than -1, which represents
//    //                                              an infinite time-out.
//    //   System.Threading.AbandonedMutexException:  The wait completed because a thread exited without releasing a mutex. 
//    //   System.InvalidOperationException:          The current instance is a transparent proxy for a 
//    //                                              System.Threading.WaitHandle
//    //                                              in another application domain.
//    #endregion Summary:

//    bool result = false;

//    Console.WriteLine("WaitOneWithCancellation millisecondsTimeout = {0}", millisecondsTimeout);
//    try
//    {
//        // If the calling code specified a timeout then implement it
//        // We cant pass -1 for indefinite as per the spec is causing a System.ArgumentOutOfRangeException
//        //if (millisecondsTimeout > 0) { return handle.WaitOne(millisecondsTimeout); }
//        //else 
//            result = handle.WaitOne();

//        //// If -1 or no timeout was specified then we will wait for ever but in a loop allowing us to atomically check for cancellation
//        //while (!result)
//        //{
//        //    // if not signalled in the wait period then check if the service has been shutdown. 
//        //    // If yes then throw an exception that 
//        //    result = handle.WaitOne(CHECK_FOR_CANCEL_TIMEOUT_MILLIS);
//        //    if (!result)
//        //    {
//        //        if (ShutDown)
//        //        {
//        //            throw new Exception("Message Channel has shutdown");
//        //        }
//        //    }
//        //}
//    }
//    catch (Exception e)
//    {
//        Console.WriteLine(e);
//        throw;
//    }

//    return result;
//}


// string retval = (string)(object)MMChannel.ByteArrayToString(bytSTime);
// You can also directly access the underlying unmanaged memory via a pointer. Following
// on from the previous example:
// unsafe
// {
//     byte* pointer = null;
//     accessor.SafeMemoryMappedViewHandle.AcquirePointer (ref pointer);
//     int* intPointer = (int*) pointer;
//     Console.WriteLine (*intPointer); // 123
// }
// Pointers can be advantageous when working with large structures: they let you work
// directly with the raw data rather than using Read/Write to copy data between managed
// and unmanaged memory.

