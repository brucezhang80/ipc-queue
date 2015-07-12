using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace com.alphaSystematics.concurrency
{

    // The hardest part of writing tests is that when they fail you don't know if it is the test or the application 
    // thats broken unless you have confidence that the tests themselves have been tested thoroughly
    // in this case we are lucky in that we are trying to mimic the functionality of an existing library class but extend
    // it to use inter process.
    // We can drop in the library class here in order to test the test because we have confidence that the library class
    // works so if the tests fail when using library class then the tests are broken

    [TestFixture]
    public class MMChannel_NUnit_Tests
    {
        static DataStructureType initTestDataStructureType = default(DataStructureType);
        const long AVERAGE_THROUGHPUT_THRESHOLD_TICKS = 10000;
        int initNoOfTrials = 0; int initTestRunNumber = 0; int initTestSuiteNumber = 0; 
        const int maxLongRandomSeed = 1000000; 
        const int maxIntRandomSeed = 1000;
        const bool DEBUG = true; bool TEST = true;

        public void main(String [] args)
        {
            // new MMChannel_NUnit_Tests().go();
        }


        [SetUp]
        public void Init() 
        { 
            // Configure all tests to be run on a stack type channel
            // TestDataStructureType = DataStructureType.Stack; 

            // Configure all tests to be run on a queue type channel
            initTestDataStructureType = DataStructureType.Stack;
            // initTestDataStructureType = DataStructureType.Queue;
            initNoOfTrials = 1000000;
        }

        public void StartWindowsService()
        {
            int capacity = 500, fileSize = 1000000, viewSize = 1000;
            string QueueName = "_07_testPutTakeString"; 
            // MMChannel mmq = new MMChannel(QueueName, fileSize, viewSize, capacity, TestDataStructureType);

             Console.WriteLine(
                 "Launched MMChannel windows service with nane {0}, capacity {1}, fileSize {2}, viewSize {3}, type {4}",
                 QueueName, capacity, fileSize, viewSize, initTestDataStructureType);

             Console.WriteLine("Press ENTER to shutdown");
             Console.ReadLine();
        }







        [Test]
        public void _01_testEmptyWhenConstructed() 
        {
            // BlockingCollection<int> mmq = null;
            MMChannel mmMain = null;
            
            try
            {
                int initialCount = 0;
                string QueueName = "_01_testEmptyWhenConstructed";
                int viewSize = 1000;
                int fileSize = 1000000;
                int capacity = 500;

                // mmq = new BlockingCollection<string>(new ConcurrentQueue<string>(), capacity);
                // mmq = new MMQueueArrayType(QueueName, new MMFileValueType(QueueName, fileSize, viewSize, capacity));
                mmMain = MMChannel.GetInstance(QueueName, fileSize, viewSize, capacity, DEBUG, TEST);

                ControlData controlData = mmMain.MMFControlData;

                Assert.True(controlData.totalItemsEnqueued == initialCount);
                Console.WriteLine("Result of _01_testEmptyWhenConstructed() = (Count {1} == initialCount {2}) = {0}",
                    controlData.totalItemsEnqueued == initialCount, controlData.totalItemsEnqueued, initialCount);
            }
            finally
            {
                // mmq.Cleanup();
                // mmq.CompleteAdding(); mmq.Dispose();

                mmMain.Report();
                mmMain.Dispose();
            }
        }




        [Test]   // String values
        public void _02_testIsFullAfterPutsAndEmptyAfterTakes()
        {
            // BlockingCollection<string> mmq = null;
            MMChannel mmMain = null;
            try
            {
                int initialCount = 0;
                string QueueName = "_02_testIsFullAfterPutsAndEmptyAfterTakes";
                int viewSize = 1000;
                int fileSize = 1000000;
                int capacity = 500;

                // mmq = new BlockingCollection<string>(new ConcurrentQueue<string>(), capacity);
                // mmq = new MMQueueArrayType(QueueName, new MMFileValueType(QueueName, fileSize, viewSize, capacity));
                mmMain = MMChannel.GetInstance(QueueName, fileSize, viewSize, capacity, DEBUG, TEST);

                // Fill the queue
                // for (int i = 0; i < capacity; i++) { mmq.Add(i.ToString()); }
                for (int i = 0; i < capacity; i++) { mmMain.Put((char)i); }

                // Verify that the queue is full
                ControlData controlData = mmMain.MMFControlData;

                Assert.True(controlData.totalItemsEnqueued == capacity);
                Console.WriteLine("_02_testIsFullAfterPutsAndEmptyAfterTakes count = {0} capacity = {1}", controlData.totalItemsEnqueued, capacity);

                // Empty the queue
                for (int i = 0; i < capacity; i++) { mmMain.Take<char>(); }

                // Verify that the queue is empty
                controlData = mmMain.MMFControlData;

                Console.WriteLine("Result of _02_testIsFullAfterPutsAndEmptyAfterTakes() = (reservations {1} == initialCount {2}) = {0} after enqueuing and dequeueing {3} items",
                   controlData.totalItemsEnqueued - controlData.totalItemsDequeued == initialCount, controlData.totalItemsEnqueued, initialCount, capacity);
                Assert.True(controlData.totalItemsEnqueued - controlData.totalItemsDequeued == initialCount);
            }
            finally
            {
                // mmq.Cleanup();
                // mmq.CompleteAdding();  mmq.Dispose();

                mmMain.Report();
                mmMain.Dispose();
            }
	    }




        [Test]
        // This annotation should be correct and in fact a ThreadInterruptedException is thrown but using it causes NUnit to report
        // a test failure even if you don't catch the exception or even if you cath and rethrow it
        // The exception is thrown in a spawned thread so maybe this only works if thown by the main thread or something. 
        // [ExpectedException(typeof(ThreadInterruptedException))]
        public void _03_testTakeBlocksWhenEmpty()
        {
           int LOCKUP_DETECT_TIMEOUT_MILLIS = 1000;
           int initialCount = 0;
           int viewSize = 1000;
           int fileSize = 1000000;
           int capacity = 500;

           string QueueName = "_03_testTakeBlocksWhenEmpty";
           // BlockingCollection<int> mmq = new BlockingCollection<int>(new ConcurrentQueue<int>(), maxCount);
           MMChannel mmMain = null;

           // Create the Consumer thread with anonymous lambda expression
           Thread Consumer =
               new Thread(
                   new ThreadStart(
                   // Old way - replace lamda expression '() =>' with 'delegate'
                   () =>
                       {
                           try
                           {
                               char unused = mmMain.Take<char>();
                               Console.WriteLine("_03_testTakeBlocksWhenEmpty() = Fail - the test thread was not blocked in 'Take()'");
                               // Must put all the console output before this statement because the test stops at this point
                               Assert.Fail(); // Take should have blocked because the queue is empty so its an error
                           }
                           catch (ThreadInterruptedException success)
                           {
                               Console.WriteLine("_03_testTakeBlocksWhenEmpty() = Pass - ThreadInterruptedException was thrown");
                               Console.WriteLine(success);
                               // Must put all the console output before this statement because the test stops at this point
                               Assert.Pass();
                               throw;
                           }
                       }
                   )
               );

           // perform the test from the main thread
           try
           {
               mmMain = MMChannel.GetInstance(QueueName, fileSize, viewSize, capacity, DEBUG, TEST);

               Consumer.Start();
               Thread.Sleep(LOCKUP_DETECT_TIMEOUT_MILLIS);

               ControlData controlData = mmMain.MMFControlData;
               Assert.True(controlData.totalItemsEnqueued == initialCount);
                
               Consumer.Interrupt();
               Consumer.Join(LOCKUP_DETECT_TIMEOUT_MILLIS);
               Console.WriteLine("_03_testTakeBlocksWhenEmpty() = Join the main thread to the Consumer thread returned after {0} ms. Consumer thread alive = {1}",
                   LOCKUP_DETECT_TIMEOUT_MILLIS, Consumer.IsAlive);
               // Must put all the console output before this statement because the test stops at this point
               Assert.False(Consumer.IsAlive);
           }
           catch (Exception unexpected)
           {
               Console.WriteLine("_03_testTakeBlocksWhenEmpty() = An unexpected Exception was thrown");
               Console.WriteLine(unexpected);
               Assert.Fail();
           }
           finally
           {
               // mmq.Cleanup();
               // mmq.CompleteAdding(); mmq.Dispose();

               mmMain.Report();
               mmMain.Dispose();
           }
       }




        // This is the default layout that the compiler would use anyway 
        [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
        unsafe struct _04_MMData
        {
            public int Value;
            public char Letter;
            public int NumbersLength;
            public fixed float Numbers[10];
            public int TextLength;
            public fixed char Text[100];
        }
        unsafe struct _04_args
        {
            public MMChannel mQueue;
            public _04_MMData dData;
        }

        private void _04_EnqueueData(_04_args arg)
        {
            try
            {
                Console.WriteLine("_04_testTakeIsUnblockedWhenElementAdded() - Starting a producer to ad an item to the queue");

                // Local 'data' or its members cannot have their address taken and be used inside an anonymous 
                // method or lambda expression - Error when trying to enqueue a struct
                // mmq.Put(data);  // mmq.Add("Tony");
                arg.mQueue.Put(arg.dData);

                StringBuilder numbers = new StringBuilder();

                for (int i = 0; i < arg.dData.NumbersLength; i++)
                {
                    unsafe
                    {
                        numbers.Append(arg.dData.Numbers[i] + ", ");
                    }
                }

                char[] txt = new char[arg.dData.TextLength];
                for (int i = 0; i < arg.dData.TextLength; i++)
                {
                    unsafe { txt[i] = arg.dData.Text[i]; }
                }
                string text = new String(txt);

                Console.WriteLine("_04_testTakeIsUnblockedWhenElementAdded() - data items enqueued \n'{0}', \n'{1}', \n'{2}', \n'{3}'",
                    arg.dData.Value, arg.dData.Letter, numbers, text);
            }
            catch (ThreadInterruptedException failure)
            {
                Console.WriteLine("_04_testTakeIsUnblockedWhenElementAdded() = The thread enqueuing items was interrupted while blocked in 'Put()'");
                Console.WriteLine(failure);
                // Must put all the console output before this statement because the test stops at this point
                Assert.Fail();
            }
        }

        private void _04_DequeueData(_04_args arg)
        {
            try
            {
                Console.WriteLine("_04_testTakeIsUnblockedWhenElementAdded() - Starting a Consumer to take an item from the queue");

                _04_MMData data = arg.mQueue.Take<_04_MMData>();
                StringBuilder numbers = new StringBuilder();
                String text;
                for (int i = 0; i < data.NumbersLength; i++)
                {
                    unsafe
                    {
                        numbers.Append(data.Numbers[i] + ", ");
                    }
                }

                char[] txt = new char[data.TextLength];
                for (int i = 0; i < data.TextLength; i++)
                {
                    unsafe { txt[i] = data.Text[i]; }
                }
                text = new String(txt);

                Console.WriteLine("_04_testTakeIsUnblockedWhenElementAdded() = data items dequeued = \n'{0}' \n'{1}' \n'{2}' \n'{3}'",
                    data.Value, data.Letter, numbers, text);
            }
            catch (ThreadInterruptedException failure)
            {
                Console.WriteLine("_04_testTakeIsUnblockedWhenElementAdded() = The thread dequeuing items was interrupted while blocked in 'take()'");
                Console.WriteLine(failure);
                // Must put all the console output before this statement because the test stops at this point
                Assert.Fail();
            }
        }

        [Test]  
        public void _04_testTakeIsUnblockedWhenElementAdded()
        {
            int LOCKUP_DETECT_TIMEOUT_MILLIS = 1000;
            int initialCount = 0;
            int viewSize = 1000;
            int fileSize = 1000000;
            int capacity = 500;

            TEST = false;

            MMChannel mmMain = null;

            _04_MMData data; // A struct containing data to be enqueued and dequeued 
            _04_args arg;    // A struct containing the data struct and the Memory Mapped File View Accessor to be passed as a parameter
            // to a parameterized threadstart

            // CANNOT PASS THIS TO THE VIEW ACCESSOR USING AN ANONYMOUS DELEGATE OR LAMBDA EXPRESSION but parameterized threadstart
            // only accepts one argument. A lambda expression is the easiest way to pass multiple arguments to a delegate but will
            // have to use a class or struct containing all the arguments and use parameterized threadstart
            //        // Local 'data' or its members cannot have their address taken and be used inside an anonymous 
            //        // method or lambda expression
            //        // Error when trying to enqueue a struct

            string QueueName = "_04_testTakeIsUnblockedWhenElementAdded";
            // BlockingCollection<string> mmq = new BlockingCollection<string>(new ConcurrentQueue<string>(), maxCount);
            mmMain = MMChannel.GetInstance(QueueName, fileSize, viewSize, capacity, DEBUG, TEST);

            // Populate the struct of data to be enqueued and dequeued 
            data.Value = 1;
            data.Letter = 'A';
            // Because we have to use value type arrays fixed in unmanaged memory to pass to the ViewAccessor there will be any kind
            // of rubbish in the cells to which we have not explicitly written data. therefore we could initialize the 
            // view by writing initial values to every cell before writing or after reading
            // Alternatively, and probably quicker, we can store the length of the array subset that we actually wrote to
            // So each array field should have a corresponding length field
            data.NumbersLength = 5;
            // Because the array is fixed in unmanaged memory we have to access it as 'unsafe'
            for (int i = 0; i < data.NumbersLength; i++) { unsafe { data.Numbers[i] = i; } }

            // Test data string to enqueue and dequeue. Convert to a char array. This array is a reference type so cannot be directly
            // passed to the View Accessor
            string msg = "EUR/GBP USD/JPY AUD/USD";
            char[] txt = msg.ToCharArray();
            // Store the length of the array for dequeueing later
            data.TextLength = txt.Length;
            // Copy the data to unmanaged memory char by char
            for (int i = 0; i < data.TextLength; i++) { unsafe { data.Text[i] = txt[i]; } }

            // Assign the data and the view accessor to the struct that we will use for the parameterized threadstart
            arg.mQueue = mmMain;
            arg.dData = data;

            // Create the Producer threads with lamda expression that refers to a method rather than anonymous 
            Thread Producer_1 = new Thread(() => _04_EnqueueData(arg));
            Thread Producer_2 = new Thread(() => _04_EnqueueData(arg));

            // Create the Consumer threads with anonymous lamda expression
            Thread Consumer_1 = new Thread(() => _04_DequeueData(arg));
            Thread Consumer_2 = new Thread(() => _04_DequeueData(arg));

            // perform the test from the main thread
            try
            {
                // Read the control data into a local variable because although access is synchronized internally in the queue
                // it could change at any moment. Only getting a snapshot not a live view
                ControlData controlData = mmMain.MMFControlData;

                Console.WriteLine("Verify that the queue is empty");
                long itemsInQueue = controlData.totalItemsEnqueued - controlData.totalItemsDequeued;

                Console.WriteLine("_04_testTakeIsUnblockedWhenElementAdded() = (Count {1} == initialCount {2}) = {0}\n",
                     itemsInQueue == initialCount, itemsInQueue, initialCount);

                Assert.True((controlData.totalItemsEnqueued - controlData.totalItemsDequeued) == initialCount);

                // Start a thread to enqueue an element
                Producer_1.Start();

                // Wait for a period for the thread to die
                Thread.Sleep(LOCKUP_DETECT_TIMEOUT_MILLIS);
                Producer_1.Join(LOCKUP_DETECT_TIMEOUT_MILLIS);
                Console.WriteLine("Verify the Producer thread has died");
                Console.WriteLine("_04_testTakeIsUnblockedWhenElementAdded() = Joining the main thread to the Producer thread returned before {0} ms and Isalive = {1}\n",
                     LOCKUP_DETECT_TIMEOUT_MILLIS, Producer_1.IsAlive);
                Assert.False(Producer_1.IsAlive);

                Console.WriteLine("Verify the queue now contains one element");
                controlData = mmMain.MMFControlData;
                itemsInQueue = controlData.totalItemsEnqueued - controlData.totalItemsDequeued;
                Console.WriteLine("_04_testTakeIsUnblockedWhenElementAdded() = (Count {1} == initialCount {2}) = {0} after enqueuing {3} items\n",
                     itemsInQueue == initialCount, itemsInQueue, initialCount, controlData.totalItemsEnqueued);

                Assert.True(controlData.totalItemsEnqueued - controlData.totalItemsDequeued == 1);

                Console.WriteLine("Start a thread to dequeue an element");
                Consumer_1.Start();

                // Wait for a period for the thread to die        
                Thread.Sleep(LOCKUP_DETECT_TIMEOUT_MILLIS);
                Consumer_1.Join(LOCKUP_DETECT_TIMEOUT_MILLIS);
                Console.WriteLine("Verify the Consumer thread has died");
                Console.WriteLine("_04_testTakeIsUnblockedWhenElementAdded() = Joining the main thread to the Consumer thread returned before {0} ms and Isalive = {1}\n",
                     LOCKUP_DETECT_TIMEOUT_MILLIS, Consumer_1.IsAlive);
                Assert.False(Consumer_1.IsAlive);

                Console.WriteLine("verify that the queue is empty");
                controlData = mmMain.MMFControlData;

                itemsInQueue = controlData.totalItemsEnqueued - controlData.totalItemsDequeued;
                Console.WriteLine("_04_testTakeIsUnblockedWhenElementAdded() = (Count {1} == initialCount {2}) = {0} after dequeueing {3} items\n",
                     itemsInQueue == initialCount,
                     itemsInQueue, initialCount, controlData.totalItemsDequeued);

                Assert.True(controlData.totalItemsEnqueued - controlData.totalItemsDequeued == initialCount);

                Console.WriteLine("Start another thread to dequeue an element");
                Consumer_2.Start();
                // Wait for a period for the thread to die        
                Thread.Sleep(LOCKUP_DETECT_TIMEOUT_MILLIS);
                Consumer_2.Join(LOCKUP_DETECT_TIMEOUT_MILLIS);
                Console.WriteLine("Verify the Consumer thread has NOT died");
                Console.WriteLine("_04_testTakeIsUnblockedWhenElementAdded() = Joining the main thread to the Consumer thread returned before {0} ms and Isalive = {1}\n",
                     LOCKUP_DETECT_TIMEOUT_MILLIS, Consumer_2.IsAlive);
                Assert.True(Consumer_2.IsAlive);

                // Start a thread to enqueue an element
                Producer_2.Start();

                // Wait for a period for the thread to die
                Thread.Sleep(LOCKUP_DETECT_TIMEOUT_MILLIS);
                Producer_2.Join(LOCKUP_DETECT_TIMEOUT_MILLIS);
                Console.WriteLine("Verify the Producer thread has died");
                Console.WriteLine("_04_testTakeIsUnblockedWhenElementAdded() = Joining the main thread to the Producer thread returned before {0} ms and Isalive = {1}\n",
                     LOCKUP_DETECT_TIMEOUT_MILLIS, Producer_2.IsAlive);
                Assert.False(Producer_2.IsAlive);


                // Wait for a period for the thread to die        
                Thread.Sleep(LOCKUP_DETECT_TIMEOUT_MILLIS);
                Consumer_2.Join(LOCKUP_DETECT_TIMEOUT_MILLIS);
                Console.WriteLine("Verify the Consumer thread has died");
                Console.WriteLine("_04_testTakeIsUnblockedWhenElementAdded() = Joining the main thread to the Consumer thread returned before {0} ms and Isalive = {1}\n",
                     LOCKUP_DETECT_TIMEOUT_MILLIS, Consumer_1.IsAlive);
                Assert.False(Consumer_2.IsAlive);

                Console.WriteLine("verify that the queue is empty");
                controlData = mmMain.MMFControlData;

                itemsInQueue = controlData.totalItemsEnqueued - controlData.totalItemsDequeued;
                Console.WriteLine("_04_testTakeIsUnblockedWhenElementAdded() = (Count {1} == initialCount {2}) = {0} after dequeueing {3} items\n",
                     itemsInQueue == initialCount,
                     itemsInQueue, initialCount, controlData.totalItemsDequeued);

            }
            catch (Exception unexpected)
            {
                Console.WriteLine("_04_testTakeIsUnblockedWhenElementAdded() = An unexpected Exception was thrown");
                Console.WriteLine(unexpected);
                // Must put all the console output before this statement because the test stops at this point
                Assert.Fail();
            }
            finally
            {
                // mmq.Cleanup();
                // mmq.CompleteAdding(); mmq.Dispose();

                mmMain.Report();
                mmMain.Dispose();
            }
        }





        [Test]  // String values
        public void _05_testPutTakeInt()
        {
            // Test that the queue performs correctly under unpredictable concurrent access by using multiple threads to 
            // to to perform Put and Take operations over a period of time and that nothing went wrong
            TEST = true;

            MMChannel mmMain = null;

            Console.WriteLine("\nStart of Test Run No. {0} in Test Suite No. {1}\n", ++initTestRunNumber, initTestSuiteNumber);

            try
            {
                int capacity = 10, fileSize = 1000000, viewSize = 1000;
                string QueueName = "_05_testPutTakeInt";
                
                // If only performing a small number of trials then GC could impact the timing tests so try and request it beforehand
                // In the case of a small number of trials, hopefully GC won't be required again before the end of the test
                System.GC.Collect();

                // INFO Cannot use the Property (get/set) with an Interlocked - 
                // Store the value of the computed checksums here using Interlocked to ensure atomicty
                long putSum = 0, takeSum = 0;
                // Start and end times of the test run
                long timerStartTime = 0, timerEndTime = 0;

                // test parameters
                int nPairs = 10, nTrials = initNoOfTrials;

                Random rand = new Random();

                // Create the MMChannel which will instantiate the memory mapped files, mutexes, semaphores etc ... 
                mmMain = MMChannel.GetInstance(QueueName, fileSize, viewSize, capacity, DEBUG, TEST, initTestDataStructureType);

                #region Barrier and Barrier Action declaration
                // The barrier will wait for the test runner thread plus a producer and consumer each for the number of pairs
                // Waits for them all to be ready at the start line and again at the finish
                Barrier _barrier = new Barrier(nPairs * 2 + 1,
                    actionDelegate =>
                    {
                        // Check to see if the start time variable has been assigned or still = zero
                        // If false then this is the first execution of the barrier action (at the start). Otherwise it is the 
                        // second execution 9at the finish)
                        const long zeroFalse_1 = 0; // Not passed by ref so no need to be assignable
                        bool started = Interlocked.Equals(timerStartTime, zeroFalse_1);
                        started = !started;

                        // Store the start time or the end time depending on which execution this is
                        long t = DateTime.Now.Ticks;
                        if (!started)
                        {
                            Interlocked.Exchange(ref timerStartTime, t);
                        }
                        else
                        {
                            Interlocked.Exchange(ref timerEndTime, t);
                        }
                    }
                );
                #endregion Barrier and Barrier Action declaration

                // create pairs of threads to put and take items to/from the queue
                // Including the test runner thread the barriers will wait for nPairs * 2 + 1 there
                for (int i = 0; i < nPairs; i++)
                {
                    #region Producer Lamda declaration

                    new Thread(
                        new ThreadStart(
                        // Old way - replace lamda expression '() =>' with 'delegate'
                        () =>
                        {
                            try
                            {
                                // B.Goetz's Java version used "this.hashCode()" and this method was in a Runnable inner class
                                // Creating an inner (nested) class inside a method may be possible in C# but seems to me all we 
                                // need is an Object so we can get a hash code
                                // http://msdn.microsoft.com/en-us/library/system.datetime.ticks.aspx
                                // TimeSpan elapsedSpan = new TimeSpan(elapsedTicks);
                                // Java = seed = (this.hashCode() ^ (int) System.nanoTime());
                                DateTime centuryBegin = new DateTime(2001, 1, 1);
                                DateTime currentDate = DateTime.Now;

                                int elapsedTicks = (int)(currentDate.Ticks - centuryBegin.Ticks);

                                #region  WARNING - THIS TEST IS FOR INTEGERS ONLY!
                                // IT IS THE PROGRAMMER'S RESPONSIBILITY TO ENSURE THAT THE COMPUTED
                                // RESULT DOES NOT EXCEED THE MAX SIZE OF AN INTEGER
                                // The result depends on the product of number of trials and the max size of the random number generated
                                // In this case nTrials and maxIntRandomSeed respectively.
                                // Choose values that will not exceed the max size or you will get corrupted results
                                // An example is the Put sum is positive and the Take sum is negative because the result 
                                // overflowed the integer size and wrote a 1 to the sign bit or the Put sum is very large but
                                // the Take sum is orders of magnitude smaller because it overflowed but wrote a zero to the sign bit
                                #endregion WARNING - THIS TEST IS FOR INTEGERS ONLY!

                                int result = 0;

                                // Wait at the barrier (start line) until all test threads have been created and are ready to go
                                _barrier.SignalAndWait();

                                for (int j = nTrials; j > 0; --j)
                                {
                                    // enqueue the random value
                                    // If the RNG is sound then this proves that the data enqueued was dequeued
                                    int r = rand.Next(maxIntRandomSeed);
                                    mmMain.Put(r);
                                    result += r;
                                }
                                // Atomically store the computed checksum
                                Interlocked.Add(ref putSum, result);

                                // Wait at the barrier (finish line) until all test threads have been finished
                                _barrier.SignalAndWait();

                            }
                            catch (Exception unexpected)
                            {
                                Console.WriteLine("_05_testPutTake() = An unexpected Exception was thrown");
                                Console.WriteLine(unexpected);
                            }
                        }
                    )).Start();

                    #endregion Producer Lamda declaration

                    #region Consumer Lamda declaration

                    new Thread(
                        new ThreadStart(
                        // Old way - replace lamda expression '() =>' with 'delegate'
                        () =>
                        {
                            try
                            {
                                #region  WARNING - THIS TEST IS FOR INTEGERS ONLY!
                                // IT IS THE PROGRAMMER'S RESPONSIBILITY TO ENSURE THAT THE COMPUTED
                                // RESULT DOES NOT EXCEED THE MAX SIZE OF AN INTEGER
                                // The result depends on the product of number of trials and the max size of the random number generated
                                // In this case nTrials and maxIntRandomSeed respectively.
                                // Choose values that will not exceed the max size or you will get corrupted results
                                // An example is the Put sum is positive and the Take sum is negative because the result 
                                // overflowed the integer size and wrote a 1 to the sign bit or the Put sum is very large but
                                // the Take sum is orders of magnitude smaller because it overflowed but wrote a zero to the sign bit
                                #endregion WARNING - THIS TEST IS FOR INTEGERS ONLY!

                                int result = 0;

                                // Wait at the barrier (start line) until all test threads have been created and are ready to go
                                _barrier.SignalAndWait();

                                // The Producer's sum should equal the Consumer's sum at the end of the test
                                for (int k = nTrials; k > 0; --k)
                                {
                                    // Test 03 - dequeue the random value
                                    // result += Convert.ToInt32(mmMain.Take<int>());
                                    result += mmMain.Take<int>();
                                }

                                // Atomically store the computed checksum
                                // Comment out for Test 01 as we have already incremented it
                                Interlocked.Add(ref takeSum, result);

                                // Wait at the barrier (finish line) until all test threads have been finished
                                _barrier.SignalAndWait();

                            }
                            catch (Exception unexpected)
                            {
                                Console.WriteLine("_05_testPutTakeInt() Consumers = An unexpected Exception was thrown");
                                Console.WriteLine(unexpected);
                            }
                        }
                    )).Start();
                    #endregion Consumer Lamda declaration

                }

                _barrier.SignalAndWait();   // Wait for all the threads to be ready
                _barrier.SignalAndWait();   // Wait for all the threads to finish

                // calculate the number of ticks elapsed during the test run
                long elapsedTime = Interlocked.Read(ref timerEndTime) - Interlocked.Read(ref timerStartTime);
                Console.WriteLine("Intermediate Result of _05_testPutTakeInt() - elapsed time = {0} timer ticks for {1} producer/consumer pairs and {2} Messages",
                    elapsedTime, nPairs, nTrials);

                // Calculate the number of ticks per item enqueued and dequeued - the throughput of the queue
                // A single tick represents one hundred nanoseconds or one ten-millionth of a second. 
                // There are 10,000 ticks in a millisecond. 
                long ticksPerItem = elapsedTime / (nPairs * (long)nTrials);
                TimeSpan elapsedSpan = new TimeSpan(ticksPerItem);
                double milliSeconds = elapsedSpan.TotalMilliseconds;
                long nanoSeconds = ticksPerItem * 100;
                long throughput = 1000000000 / nanoSeconds;

                // Compares the checksum values computed to determine if the data enqueued was exactly the data dequeued
                Console.WriteLine("_05_testPutTakeInt() = (data enqueued {1} == data dequeued {2}) = {0} after {3} trials each by {4} pairs of producers/consumers",
                    Interlocked.Read(ref putSum) == Interlocked.Read(ref takeSum), Interlocked.Read(ref putSum), Interlocked.Read(ref takeSum), nTrials, nPairs);
                Assert.AreEqual(Interlocked.Read(ref putSum), Interlocked.Read(ref takeSum));

                Console.WriteLine("_05_testPutTakeInt() = (Average latency = {0} timer ticks <= Threshold value {1}) = {2}",
                    Interlocked.Read(ref ticksPerItem), 
                    AVERAGE_THROUGHPUT_THRESHOLD_TICKS, 
                    Interlocked.Read(ref ticksPerItem) <= AVERAGE_THROUGHPUT_THRESHOLD_TICKS);

                Console.WriteLine("_05_testPutTakeInt Throughput = {0} messages per second ", throughput);

                Console.WriteLine("_05_testPutTakeInt {0} timer ticks = {1} nanoseconds or {2} milliseconds",
                    ticksPerItem, nanoSeconds, milliSeconds);

                Assert.LessOrEqual(Interlocked.Read(ref ticksPerItem), AVERAGE_THROUGHPUT_THRESHOLD_TICKS);

                Console.WriteLine("\nEnd of Test Run No. {0} in Test Suite No. {1}\n", initTestRunNumber, initTestSuiteNumber);
            }
            catch (AssertionException assertionFailed)
            {
                Console.WriteLine("_05_testPutTakeInt() = An assertion failed");
                Console.WriteLine(assertionFailed);
                // Must put all the console output before this statement because the test stops at this point
                Assert.Fail();
            }
            catch (Exception unexpected)
            {
                Console.WriteLine("_05_testPutTakeInt() = An unexpected Exception was thrown");
                Console.WriteLine(unexpected);
                // Must put all the console output before this statement because the test stops at this point
                Assert.Fail();
            }
            finally
            {
                // Temporarily delay disposing the queue and its IPC artefacts to allow the consumers to finish draining the queue
                // This will be fixed by waiting in an interrupible loop for the mutex inside the queue and checking if shutdown
                Thread.Sleep(1000);

                mmMain.Report();
                mmMain.Dispose();

                Console.WriteLine("\n");
            }
        }


        [Test]  // String values
        public void _05_testPutTakeLong()
        {
            // Test that the queue performs correctly under unpredictable concurrent access by using multiple threads to 
            // to to perform Put and Take operations over a period of time and that nothing wnet wrong
            TEST = true;

            MMChannel mmMain = null;

            Console.WriteLine("\nStart of Test Run No. {0} in Test Suite No. {1}\n", ++initTestRunNumber, initTestSuiteNumber);

            try
            {
                int capacity = 10, fileSize = 1000000, viewSize = 1000;
                string QueueName = "_05_testPutTakeLong";

                // If only performing a small number of trials then GC could impact the timing tests so try and request it beforehand
                // In the case of a small number of trials, hopefully GC won't be required again before the end of the test
                System.GC.Collect();

                // INFO Cannot use the Property (get/set) with an Interlocked - 
                // Store the value of the computed checksums here using Interlocked to ensure atomicty
                long putSum = 0, takeSum = 0;
                // Start and end times of the test run
                long timerStartTime = 0, timerEndTime = 0;

                // test parameters
                int nPairs = 10, nTrials = initNoOfTrials;

                Random rand = new Random();

                // Create the MMChannel which will instantiate the memory mapped files, mutexes, semaphores etc ... 
                mmMain = MMChannel.GetInstance(QueueName, fileSize, viewSize, capacity, DEBUG, TEST, initTestDataStructureType);

                #region Barrier and Barrier Action declaration
                // The barrier will wait for the test runner thread plus a producer and consumer each for the number of pairs
                // Waits for them all to be ready at the start line and again at the finish
                Barrier _barrier = new Barrier(nPairs * 2 + 1,
                    actionDelegate =>
                    {
                        // Check to see if the start time variable has been assigned or still = zero
                        // If false then this is the first execution of the barrier action (at the start). Otherwise it is the 
                        // second execution 9at the finish)
                        const long zeroFalse_1 = 0; // Not passed by ref so no need to be assignable
                        bool started = Interlocked.Equals(timerStartTime, zeroFalse_1);
                        started = !started;

                        // Store the start time or the end time depending on which execution this is
                        long t = DateTime.Now.Ticks;
                        if (!started)
                        {
                            Interlocked.Exchange(ref timerStartTime, t);
                        }
                        else
                        {
                            Interlocked.Exchange(ref timerEndTime, t);
                        }
                    }
                );
                #endregion Barrier and Barrier Action declaration

                // create pairs of threads to put and take items to/from the queue
                // Including the test runner thread the barriers will wait for nPairs * 2 + 1 ther
                for (int i = 0; i < nPairs; i++)
                {
                    #region Producer Lamda declaration

                    new Thread(
                        new ThreadStart(
                        // Old way - replace lamda expression '() =>' with 'delegate'
                        () =>
                        {
                            try
                            {
                                // B.Goetz's Java version used "this.hashCode()" and this method was in a Runnable inner class
                                // Creating an inner (nested) class inside a method may be possible in C# but seems to me all we 
                                // need is an Object so we can get a hash code
                                // http://msdn.microsoft.com/en-us/library/system.datetime.ticks.aspx
                                // TimeSpan elapsedSpan = new TimeSpan(elapsedTicks);
                                // Java = seed = (this.hashCode() ^ (int) System.nanoTime());
                                DateTime centuryBegin = new DateTime(2001, 1, 1);
                                DateTime currentDate = DateTime.Now;

                                int elapsedTicks = (int)(currentDate.Ticks - centuryBegin.Ticks);

                                long result = 0;

                                // Wait at the barrier (start line) until all test threads have been created and are ready to go
                                _barrier.SignalAndWait();

                                for (int j = nTrials; j > 0; --j)
                                {
                                    // enqueue the random value
                                    // If the RNG is sound then this proves that the data enqueued was dequeued
                                    int r = rand.Next(maxLongRandomSeed);
                                    mmMain.Put(r);
                                    result += r;
                                }
                                // Atomically store the computed checksum
                                // Comment out for Test 01 as we have already incremented it
                                Interlocked.Add(ref putSum, result);

                                // Wait at the barrier (finish line) until all test threads have been finished
                                _barrier.SignalAndWait();

                            }
                            catch (Exception unexpected)
                            {
                                Console.WriteLine("_05_testPutTakeLong() = An unexpected Exception was thrown");
                                Console.WriteLine(unexpected);
                            }
                        }
                    )).Start();

                    #endregion Producer Lamda declaration

                    #region Consumer Lamda declaration

                    new Thread(
                        new ThreadStart(
                        // Old way - replace lamda expression '() =>' with 'delegate'
                        () =>
                        {
                            try
                            {
                                long result = 0;

                                // Wait at the barrier (start line) until all test threads have been created and are ready to go
                                _barrier.SignalAndWait();

                                // The Producer's sum should equal the Consumer's sum at the end of the test
                                for (int k = nTrials; k > 0; --k)
                                {
                                    // Test 03 - dequeue the random value
                                    // result += Convert.ToInt64(mmMain.Take<long>());
                                    result += mmMain.Take<long>();
                                }

                                // Atomically store the computed checksum
                                // Comment out for Test 01 as we have already incremented it
                                Interlocked.Add(ref takeSum, result);

                                // Wait at the barrier (finish line) until all test threads have been finished
                                _barrier.SignalAndWait();

                            }
                            catch (Exception unexpected)
                            {
                                Console.WriteLine("_05_testPutTakeLong() Consumers = An unexpected Exception was thrown");
                                Console.WriteLine(unexpected);
                            }
                        }
                    )).Start();
                    #endregion Consumer Lamda declaration

                }

                _barrier.SignalAndWait();   // Wait for all the threads to be ready
                _barrier.SignalAndWait();   // Wait for all the threads to finish

                // calculate the number of ticks elapsed during the test run
                long elapsedTime = Interlocked.Read(ref timerEndTime) - Interlocked.Read(ref timerStartTime);
                Console.WriteLine("Intermediate Result of _05_testPutTakeLong() - elapsed time = {0} timer ticks for {1} producer/consumer pairs and {2} Messages",
                    elapsedTime, nPairs, nTrials);

                // Calculate the number of ticks per item enqueued and dequeued - the throughput of the queue
                // A single tick represents one hundred nanoseconds or one ten-millionth of a second. 
                // There are 10,000 ticks in a millisecond. 
                long ticksPerItem = elapsedTime / (nPairs * (long)nTrials);
                TimeSpan elapsedSpan = new TimeSpan(ticksPerItem);
                double milliSeconds = elapsedSpan.TotalMilliseconds;
                long nanoSeconds = ticksPerItem * 100;
                long throughput = 1000000000 / nanoSeconds;

                // Compares the checksum values computed to determine if the data enqueued was exactly the data dequeued
                Console.WriteLine("_05_testPutTakeLong() = (data enqueued {1} == data dequeued {2}) = {0} after {3} trials each by {4} pairs of producers/consumers",
                    Interlocked.Read(ref putSum) == Interlocked.Read(ref takeSum), Interlocked.Read(ref putSum), Interlocked.Read(ref takeSum), nTrials, nPairs);
                Assert.AreEqual(Interlocked.Read(ref putSum), Interlocked.Read(ref takeSum));

                Console.WriteLine("_05_testPutTake() = (Average latency = {0} timer ticks <= Threshold value {1}) = {2}",
                    Interlocked.Read(ref ticksPerItem),
                    AVERAGE_THROUGHPUT_THRESHOLD_TICKS,
                    Interlocked.Read(ref ticksPerItem) <= AVERAGE_THROUGHPUT_THRESHOLD_TICKS);

                Console.WriteLine("_05_testPutTakeLong Throughput = {0} messages per second ", throughput);

                Console.WriteLine("_05_testPutTakeLong {0} timer ticks = {1} nanoseconds or {2} milliseconds",
                    ticksPerItem, nanoSeconds, milliSeconds);

                Assert.LessOrEqual(Interlocked.Read(ref ticksPerItem), AVERAGE_THROUGHPUT_THRESHOLD_TICKS);

                Console.WriteLine("\nEnd of Test Run No. {0} in Test Suite No. {1}\n", initTestRunNumber, initTestSuiteNumber);
            }
            catch (AssertionException assertionFailed)
            {
                Console.WriteLine("_05_testPutTakeLong() = An assertion failed");
                Console.WriteLine(assertionFailed);
                // Must put all the console output before this statement because the test stops at this point
                Assert.Fail();
            }
            catch (Exception unexpected)
            {
                Console.WriteLine("_05_testPutTakeLong() = An unexpected Exception was thrown");
                Console.WriteLine(unexpected);
                // Must put all the console output before this statement because the test stops at this point
                Assert.Fail();
            }
            finally
            {
                // Temporarily delay disposing the queue and its IPC artefacts to allow the consumers to finish draining the queue
                // This will be fixed by waiting in an interrupible loop for the mutex inside the queue and checking if shutdown
                Thread.Sleep(1000);

                mmMain.Report();
                mmMain.Dispose();

                Console.WriteLine("\n");
            }
        }

        struct _06_array_args
        {
            public MMChannel mQueue;
            public string dData;
        }

        private void _06_array_EnqueueData(_06_array_args arg)
        {
            try
            {
                // Local 'data' or its members cannot have their address taken and be used inside an anonymous 
                // method or lambda expression - Error when trying to enqueue a struct

                byte[] encodedData = MMChannel.StringToByteArray((string)(object)arg.dData);

                arg.mQueue.Put(encodedData);

                string retval = (string)(object)MMChannel.ByteArrayToString(encodedData);

                Console.WriteLine("_06_array_EnqueueData() = data item enqueued = '{0}' ", retval);

            }
            catch (ThreadInterruptedException failure)
            {
                Console.WriteLine("Result of _06_array_EnqueueData() = The thread enqueuing items was interrupted while blocked in 'Put()'");
                Console.WriteLine(failure);
                // Must put all the console output before this statement because the test stops at this point
                Assert.Fail();
            }
        }

        private void _06_array_DequeueData(_06_array_args arg, string messageEnqueued)
        {
            try
            {
                byte[] data;
                int numItems = arg.mQueue.Take<byte>(out data);

                string retval = (string)(object)MMChannel.ByteArrayToString(data);

                Console.WriteLine("_06_array_DequeueData() = data item dequeued = '{0}', original message = {1}", retval, messageEnqueued);

                Assert.True(retval == messageEnqueued);
            }
            catch (ThreadInterruptedException failure)
            {
                Console.WriteLine("Result of _06_value_DequeueData() = The thread dequeuing items was interrupted while blocked in 'take()'");
                Console.WriteLine(failure);
                // Must put all the console output before this statement because the test stops at this point
                Assert.Fail();
            }
        }

        // [Test]  MMData values
        public void _06_array_testTakeIsUnblockedWhenElementAdded()
        {
            int LOCKUP_DETECT_TIMEOUT_MILLIS = 1000;
            long initialCount = 0;
            int viewSize = 1000;
            int fileSize = 1000000;
            int capacity = 500;

            TEST = false;

            _06_array_args arg;    // A struct containing the data struct and the Memory Mapped File View Accessor to be passed as a parameter
            // to a parameterized threadstart

            // CANNOT PASS THIS TO THE VIEW ACCESSOR USING AN ANONYMOUS DELEGATE OR LAMBDA EXPRESSION but parameterized threadstart
            // only accepts one argument. A lambda expression is the easiest way to pass multiple arguments to a delegate but will
            // have to use a class or struct containing all the arguments and use parameterized threadstart

            string QueueName = "_6_array_testTakeIsUnblockedWhenElementAdded";
            // BlockingCollection<string> mmq = new BlockingCollection<string>(new ConcurrentQueue<string>(), maxCount);

            // Instantiate a memory mapped file based queue with the value type file created inline
            MMChannel mmMain = MMChannel.GetInstance(QueueName, fileSize, viewSize, capacity, DEBUG, TEST);

            // Test data string to enqueue and dequeue. Convert to a byte array. This array is a reference type so cannot be directly
            // passed to the View Accessor
            string msg = "EUR/GBP USD/JPY AUD/USD";
            arg.dData = msg; // new _6_value_Data { X = 123, Y = 456 };
            arg.mQueue = mmMain;


            // Create the Producer thread with lamda expression that refers to a method rather than anonymous 
            Thread Producer = new Thread(() => _06_array_EnqueueData(arg));

            // Create the Consumer thread with anonymous lamda expression
            Thread Consumer = new Thread(() => _06_array_DequeueData(arg, msg));

            // perform the test from the main thread
            try
            {
                // verify that the queue is empty
                ControlData controlData = mmMain.MMFControlData;

                Console.WriteLine("_6_array_testTakeIsUnblockedWhenElementAdded() = (Count {1} == initialCount {2}) = {0}",
                     controlData.totalItemsEnqueued - controlData.totalItemsDequeued == initialCount, 
                     controlData.totalItemsEnqueued - controlData.totalItemsDequeued, initialCount);
                Assert.True(controlData.totalItemsEnqueued - controlData.totalItemsDequeued == initialCount);

                // Start a thread to enqueue an element
                Producer.Start();

                // Wait for a period for the thread to die
                Thread.Sleep(LOCKUP_DETECT_TIMEOUT_MILLIS);
                Producer.Join(LOCKUP_DETECT_TIMEOUT_MILLIS);
                // Verify the thread has died
                Console.WriteLine("_6_array_testTakeIsUnblockedWhenElementAdded() = Joining the main thread to the Producer thread returned before (0)ms and Isalive = {1}",
                     LOCKUP_DETECT_TIMEOUT_MILLIS, Producer.IsAlive);
                Assert.False(Producer.IsAlive);

                // Verify the queue now contains one element
                controlData = mmMain.MMFControlData;

                Console.WriteLine("_06_array_testTakeIsUnblockedWhenElementAdded() = (Count {1} == initialCount {2}) = {0} after enqueuing {3} items",
                     controlData.totalItemsEnqueued - controlData.totalItemsDequeued == initialCount,
                     controlData.totalItemsEnqueued - controlData.totalItemsDequeued, initialCount, controlData.totalItemsEnqueued - controlData.totalItemsDequeued);
                Assert.True(controlData.totalItemsEnqueued - controlData.totalItemsDequeued == 1);

                // Start a thread to dequeue an element
                Consumer.Start();

                // Wait for a period for the thread to die        
                Thread.Sleep(LOCKUP_DETECT_TIMEOUT_MILLIS);
                Consumer.Join(LOCKUP_DETECT_TIMEOUT_MILLIS);
                // Verify the thread has died
                Console.WriteLine("_06_array_testTakeIsUnblockedWhenElementAdded() = Joining the main thread to the Consumer thread returned before (0)ms and Isalive = {1}",
                     LOCKUP_DETECT_TIMEOUT_MILLIS, Consumer.IsAlive);
                Assert.False(Consumer.IsAlive);

                // verify that the queue is empty
                controlData = mmMain.MMFControlData;

                Console.WriteLine("Result of _06_array_testTakeIsUnblockedWhenElementAdded() = (Count {1} == initialCount {2}) = {0} after dequeueing {3} items",
                     controlData.totalItemsEnqueued - controlData.totalItemsDequeued == initialCount,
                     controlData.totalItemsEnqueued - controlData.totalItemsDequeued, initialCount, 
                     controlData.totalItemsEnqueued - controlData.totalItemsDequeued);

                Assert.True((controlData.totalItemsEnqueued - controlData.totalItemsDequeued) == initialCount);
            }
            catch (Exception unexpected)
            {
                Console.WriteLine("_06_array_testTakeIsUnblockedWhenElementAdded() = An unexpected Exception was thrown");
                Console.WriteLine(unexpected);
                // Must put all the console output before this statement because the test stops at this point
                Assert.Fail();
            }
            finally
            {
                // mmq.Cleanup();
                // mmq.CompleteAdding(); mmq.Dispose();

                mmMain.Report();
                mmMain.Dispose();
            }
        }



        [Test]  // String values
        public void _07_testPutTakeString()
        {
            // Test that the queue performs correctly under unpredictable concurrent access by using multiple threads to 
            // to to perform Put and Take operations over a period of time and that nothing wnet wrong
            // MMChannel mmq = null;
            // BlockingCollection<string> mmq = null;

            TEST = false;

            Console.WriteLine("\nStart of Test Run No. {0} in Test Suite No. {1}\n", ++initTestRunNumber, initTestSuiteNumber);

            String applicationInstance = UTCutil.GetInstanceNameForProcessId(Process.GetCurrentProcess().Id);
            Dictionary<String, PerformanceCounter> map = UTCutil.ReadKeyMemoryAndHandlePerformanceCounters(applicationInstance);

            PerformanceCounter all_heaps_counter;
            map.TryGetValue(UTCutil.performanceCounter_bytes_in_all_heaps, out all_heaps_counter);
            String name = all_heaps_counter.CounterName.ToString();

            MMChannel mmMain = null;

            try
            {
                int capacity = 500, fileSize = 1000000, viewSize = 1000;
                string QueueName = "_07_testPutTakeString";

                // If only performing a small number of trials then GC could impact the timing tests so try and request it beforehand
                // In the case of a small number of trials, hopefully GC won't be required again before the end of the test
                System.GC.Collect();

                // mmq = new BlockingCollection<string>(new ConcurrentQueue<string>(), maxCount);
                // mmq = new MMChannel(QueueName, new MMFile(QueueName, fileSize, viewSize, capacity), TestDataStructureType);

                // INFO Cannot use the Property (get/set) with an Interlocked - 
                // Store the value of the computed checksums here using Interlocked to ensure atomicty
                long putSum = 0, takeSum = 0;
                // Start and end times of the test run
                long timerStartTime = 0, timerEndTime = 0;

                // test parameters
                // Performance nPairs = 10, capacity = 10, nTrials = 1,000,000 = BlockingCollection = 60s, MMQueue = 254s
                int nPairs = 10, nTrials = initNoOfTrials;

                Random rand = new Random();

                // Create the MMChannel which will instantiate the memory mapped files, mutexes, semaphores etc ... 
                mmMain = MMChannel.GetInstance(QueueName, fileSize, viewSize, capacity, DEBUG, TEST);

                #region Barrier and Barrier Action declaration
                // The barrier will wait for the test runner thread plus a producer and consumer each for the number of pairs
                // Waits for them all to be ready at the start line and again at the finish
                Barrier _barrier = new Barrier(nPairs * 2 + 1,
                    actionDelegate =>
                    {
                        // Check to see if the start time variable has been assigned or still = zero
                        // If false then this is the first execution of the barrier action (at the start). Otherwise it is the 
                        // second execution at the finish)
                        const long zeroFalse_1 = 0; // Not passed by ref so no need to be assignable
                        bool started = Interlocked.Equals(timerStartTime, zeroFalse_1);
                        started = !started;

                        // Store the start time or the end time depending on which execution this is
                        long t = DateTime.Now.Ticks;
                        if (!started)
                        {
                            Interlocked.Exchange(ref timerStartTime, t);
                        }
                        else
                        {
                            Interlocked.Exchange(ref timerEndTime, t);
                        }
                    }
                );
                #endregion Barrier and Barrier Action declaration

                // create pairs of threads to put and take items to/from the queue
                // Including the test runner thread the barriers will wait for nPairs * 2 + 1 there
                for (int i = 0; i < nPairs; i++)
                {
                    #region Producer Lamda declaration

                    new Thread(
                        new ThreadStart(
                        () =>
                        {
                            try
                            {
                                // B.Goetz's Java version used "this.hashCode()" and this method was in a Runnable inner class
                                // Creating an inner (nested) class inside a method may be possible in C# but seems to me all we 
                                // need is an Object so we can get a hash code
                                // http://msdn.microsoft.com/en-us/library/system.datetime.ticks.aspx
                                // TimeSpan elapsedSpan = new TimeSpan(elapsedTicks);
                                // Java = seed = (this.hashCode() ^ (int) System.nanoTime());
                                DateTime centuryBegin = new DateTime(2001, 1, 1);
                                DateTime currentDate = DateTime.Now;

                                int elapsedTicks = (int)(currentDate.Ticks - centuryBegin.Ticks);
                                // int seed = (computedHashCode ^ elapsedTicks);
                                // int seed = (int)(new Object().GetHashCode() ^ elapsedTicks);
                                // int seed = (int)(new Object().GetHashCode());
                                long result = 0;

                                // Wait at the barrier (start line) until all test threads have been created and are ready to go
                                _barrier.SignalAndWait();

                                // Put the data into the queue as Strings, generating a new random number each time
                                // The consumer will convert back to integers and sum them 
                                // The Producer's sum should equal the Consumenr's sum at the end of the test
                                for (int j = nTrials; j > 0; --j)
                                {
        
                                    // Test 03 - enqueue the random value
                                    // If the RNG is sound then this proves that the data enqueued was dequeued
                                    // mmq.Add(Convert.ToString(seed));
                                    int r = rand.Next(maxLongRandomSeed);
                                    // byte[] encodedData = MMChannel.StringToByteArray(Convert.ToString(r));
                                    char[] encodedData = Convert.ToString(r).ToCharArray();
                                    mmMain.Put(encodedData);
                                    result += r;

                                    // re-compute the random number
                                    // seed = MMQueue<string>.xorShift(seed);
                                }
                                // Atomically store the computed checksum
                                // Comment out for Test 01 as we have already incremented it
                                Interlocked.Add(ref putSum, result);

                                // Wait at the barrier (finish line) until all test threads have been finished
                                _barrier.SignalAndWait();

                            }
                            catch (Exception unexpected)
                            {
                                Console.WriteLine("Result of _07_testPutTakeString() = An unexpected Exception was thrown");
                                Console.WriteLine(unexpected);
                                // Must put all the console output before this statement because the test stops at this point
                                Assert.Fail();
                            }
                        }
                    )).Start();


                    // Start a thread to enqueue an element
                    // Producer.Start();

                    #endregion Producer Lamda declaration

                    #region Consumer Lamda declaration

                    new Thread(
                        new ThreadStart(
                        // Old way - replace lamda expression '() =>' with 'delegate'
                        () =>
                        {
                            try
                            {
                                long result = 0;

                                // Wait at the barrier (start line) until all test threads have been created and are ready to go
                                _barrier.SignalAndWait();

                                // take the data from the queue as Strings, convert back to ints and sum them 
                                // The Producer's sum should equal the Consumer's sum at the end of the test
                                for (int k = nTrials; k > 0; --k)
                                {
                                    char[] data;
                                    int numItems = mmMain.Take<char>(out data);
                                    string retval = new string(data);
                                    result += Convert.ToInt64(retval);
                                }

                                // Atomically store the computed checksum
                                // Comment out for Test 01 as we have already incremented it
                                Interlocked.Add(ref takeSum, result);

                                // Wait at the barrier (finish line) until all test threads have been finished
                                _barrier.SignalAndWait();

                            }
                            catch (Exception unexpected)
                            {
                                Console.WriteLine("Result of _07_testPutTakeString() = An unexpected Exception was thrown");
                                Console.WriteLine(unexpected);
                                // Must put all the console output before this statement because the test stops at this point
                                Assert.Fail();
                            }
                        }
                    )).Start();
                    #endregion Consumer Lamda declaration

                }

                int THRESHOLD = 1000; long diff;

                #region heap profiling testing notes
                // I got this off Java Concurrency in Practice, chap 12 Testing Concurrent Programs Page 258
                // It doesn't work as written though!
                // Generally, the heap size after testing was fraction of the size before the test
                // Obviously, processing these huge messages has triggered a GC during the test
                // Even then you would expect this to result in a false positive where the two snapshot were similar even if
                // your code was leaking memory so the most likely explanation seems to be that NUnit itself is creating objects
                // which have not yet been reclaimed before the test starts
                // Requesting a GC before the initial snapshot solves the problem but you have to accept that you cannot 
                // completely control managed memory allocation and at some point the GC will probably ignore your request
                // and the test will fail
                #endregion heap profiling testing notes

                System.GC.Collect();
                long heapSizeBeforeTest = Convert.ToInt64(UTCutil.GetCounterValue(all_heaps_counter));

                _barrier.SignalAndWait();   // Wait for all the threads to be ready
                _barrier.SignalAndWait();   // Wait for all the threads to finish

                System.GC.Collect();
                long heapSizeAfterTest = Convert.ToInt64(UTCutil.GetCounterValue(all_heaps_counter));
                diff = Math.Abs(heapSizeBeforeTest - heapSizeAfterTest);

                Console.WriteLine("Result of TestLeak() Heap size at end of run = {0}, Heap size at start of run = {1} Difference = {2}, Passed = {3}",
                    heapSizeAfterTest, heapSizeBeforeTest, diff, diff <= THRESHOLD);

                // calculate the number of ticks elapsed during the test run
                long elapsedTime = Interlocked.Read(ref timerEndTime) - Interlocked.Read(ref timerStartTime);
                Console.WriteLine("Intermediate Result of _07_testPutTakeString() - elapsed time = {0} timer ticks for {1} producer/consumer pairs and {2} Messages",
                    elapsedTime, nPairs, nTrials);

                // Calculate the number of ticks per item enqueued and dequeued - the throughput of the queue
                // A single tick represents one hundred nanoseconds or one ten-millionth of a second. 
                // There are 10,000 ticks in a millisecond. 
                long ticksPerItem = elapsedTime / (nPairs * (long)nTrials);
                TimeSpan elapsedSpan = new TimeSpan(ticksPerItem);
                double milliSeconds = elapsedSpan.TotalMilliseconds;
                long nanoSeconds = ticksPerItem * 100;
                long throughput = 1000000000 / nanoSeconds;

                // Compares the checksum values computed to determine if the data enqueued was exactly the data dequeued
                Console.WriteLine("1st Result of _07_testPutTakeString() = (data enqueued {1} == data dequeued {2}) = {0} after {3} trials each by {4} pairs of producers/consumers",
                    Interlocked.Read(ref putSum) == Interlocked.Read(ref takeSum), Interlocked.Read(ref putSum), Interlocked.Read(ref takeSum), nTrials, nPairs);
                Assert.AreEqual(Interlocked.Read(ref putSum), Interlocked.Read(ref takeSum));

                Console.WriteLine("2nd Result of _07_testPutTakeString() = (Average latency = {0} timer ticks <= Threshold value {1}) = {2}",
                    Interlocked.Read(ref ticksPerItem),
                    AVERAGE_THROUGHPUT_THRESHOLD_TICKS,
                    Interlocked.Read(ref ticksPerItem) <= AVERAGE_THROUGHPUT_THRESHOLD_TICKS);

                Console.WriteLine("_07_testPutTakeString Throughput = {0} messages per second ", throughput);

                Console.WriteLine("_07_testPutTakeString n.b. {0} timer ticks = {1} nanoseconds or {2} milliseconds",
                    ticksPerItem, nanoSeconds, milliSeconds);

                Assert.LessOrEqual(Interlocked.Read(ref ticksPerItem), AVERAGE_THROUGHPUT_THRESHOLD_TICKS);

                Console.WriteLine("\nEnd of Test Run No. {0} in Test Suite No. {1}\n", initTestRunNumber, initTestSuiteNumber);

            }
            catch (AssertionException assertionFailed)
            {
                Console.WriteLine("Result of _07_testPutTakeString() = An assertion failed");
                Console.WriteLine(assertionFailed);
                // Must put all the console output before this statement because the test stops at this point
                Assert.Fail();
            }
            catch (Exception unexpected)
            {
                Console.WriteLine("Result of _07_testPutTakeString() = An unexpected Exception was thrown");
                Console.WriteLine(unexpected);
                // Must put all the console output before this statement because the test stops at this point
                Assert.Fail();
            }
            finally
            {
                // Temporarily delay disposing the queue and its IPC artefacts to allow the consumers to finish draining the queue
                // This will be fixed by waiting in an interrupible loop for the mutex inside the queue and checking if shutdown
                Thread.Sleep(1000);

                mmMain.Report();
                mmMain.Dispose();
            }
        }



        // This is the default layout that the compiler would use anyway 
        [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
        unsafe struct _08_MMData
        {
            public int TextLength;
            public fixed char Text[100];
        }
        [Test]  // String values
        public void _08_testPutTake_fixed()
        {
            TEST = false;

            // Test that the queue performs correctly under unpredictable concurrent access by using multiple threads to 
            // to to perform Put and Take operations over a period of time and that nothing wnet wrong

            // BlockingCollection<string> mmq = null;

            Console.WriteLine("\nStart of Test Run No. {0} in Test Suite No. {1}\n", ++initTestRunNumber, initTestSuiteNumber);

            MMChannel mmMain = null;

            try
            {
                int capacity = 500, fileSize = 1000000, viewSize = 1000;
                string QueueName = "_08_testPutTake_fixed";

                // If only performing a small number of trials then GC could impact the timing tests so try and request it beforehand
                // In the case of a small number of trials, hopefully GC won't be required again before the end of the test
                System.GC.Collect();

                // INFO Cannot use the Property (get/set) with an Interlocked - 
                // Store the value of the computed checksums here using Interlocked to ensure atomicty
                long putSum = 0, takeSum = 0;
                // Start and end times of the test run
                long timerStartTime = 0, timerEndTime = 0;

                // test parameters
                // Performance nPairs = 10, capacity = 10, nTrials = 1,000,000 = BlockingCollection = 60s, MMQueue = 254s
                int nPairs = 10, nTrials = initNoOfTrials;

                Random rand = new Random();

                // Create the MMChannel which will instantiate the memory mapped files, mutexes, semaphores etc ... 
                mmMain = MMChannel.GetInstance(QueueName, fileSize, viewSize, capacity, DEBUG, TEST);

                #region Barrier and Barrier Action declaration
                // The barrier will wait for the test runner thread plus a producer and consumer each for the number of pairs
                // Waits for them all to be ready at the start line and again at the finish
                Barrier _barrier = new Barrier(nPairs * 2 + 1,
                    actionDelegate =>
                    {
                        // Check to see if the start time variable has been assigned or still = zero
                        // If false then this is the first execution of the barrier action (at the start). Otherwise it is the 
                        // second execution 9at the finish)
                        const long zeroFalse_1 = 0; // Not passed by ref so no need to be assignable
                        bool started = Interlocked.Equals(timerStartTime, zeroFalse_1);
                        started = !started;

                        // Store the start time or the end time depending on which execution this is
                        long t = DateTime.Now.Ticks;
                        if (!started)
                        {
                            Interlocked.Exchange(ref timerStartTime, t);
                        }
                        else
                        {
                            Interlocked.Exchange(ref timerEndTime, t);
                        }
                    }
                );
                #endregion Barrier and Barrier Action declaration

                // create pairs of threads to put and take items to/from the queue
                // Including the test runner thread the barriers will wait for nPairs * 2 + 1 ther
                for (int i = 0; i < nPairs; i++)
                {
                    #region Producer Lamda declaration

                    new Thread(
                        new ThreadStart(
                        // Old way - replace lamda expression '() =>' with 'delegate'
                        () =>
                        {
                            try
                            {
                                DateTime centuryBegin = new DateTime(2001, 1, 1);
                                DateTime currentDate = DateTime.Now;

                                int elapsedTicks = (int)(currentDate.Ticks - centuryBegin.Ticks);
                                long result = 0;

                                // Wait at the barrier (start line) until all test threads have been created and are ready to go
                                _barrier.SignalAndWait();

                                // Put the data into the queue as Strings, generating a new random number each time
                                // The consumer will convert back to integers and sum them 
                                // The Producer's sum should equal the Consumenr's sum at the end of the test
                                for (int j = nTrials; j > 0; --j)
                                {
                                    // If the RNG is sound then this proves that the data enqueued was dequeued
                                    int r = rand.Next(maxLongRandomSeed);

                                    _08_MMData data;

                                    // Test data string to enqueue and dequeue. Convert to a byte array. This array is a reference type so cannot be directly
                                    // passed to the View Accessor
                                    char[] encodedData = Convert.ToString(r).ToCharArray();
                                    // Store the length of the array for dequeueing later
                                    data.TextLength = encodedData.Length;
                                    // Copy the data to unmanaged memory char by char
                                    for (int k = 0; k < data.TextLength; k++) { unsafe { data.Text[k] = encodedData[k]; } }

                                    mmMain.Put(data);
                                    result += r;

                                }
                                // Atomically store the computed checksum
                                // Comment out for Test 01 as we have already incremented it
                                Interlocked.Add(ref putSum, result);

                                // Wait at the barrier (finish line) until all test threads have been finished
                                _barrier.SignalAndWait();

                            }
                            catch (Exception unexpected)
                            {
                                Console.WriteLine("Result of _08_testPutTake_fixed() = An unexpected Exception was thrown");
                                Console.WriteLine(unexpected);
                                // Must put all the console output before this statement because the test stops at this point
                                Assert.Fail();
                            }
                        }
                    )).Start();


                    // Start a thread to enqueue an element
                    // Producer.Start();

                    #endregion Producer Lamda declaration

                    #region Consumer Lamda declaration

                    new Thread(
                        new ThreadStart(
                        // Old way - replace lamda expression '() =>' with 'delegate'
                        () =>
                        {
                            try
                            {
                                long result = 0;

                                // Wait at the barrier (start line) until all test threads have been created and are ready to go
                                _barrier.SignalAndWait();

                                // take the data from the queue as Strings, convert back to ints and sum them 
                                // The Producer's sum should equal the Consumer's sum at the end of the test
                                for (int k = nTrials; k > 0; --k)
                                {
                                    _08_MMData data = mmMain.Take<_08_MMData>();

                                    String decodedData;

                                    char[] txt = new char[data.TextLength];
                                    for (int m = 0; m < data.TextLength; m++)
                                    {
                                        unsafe { txt[m] = data.Text[m]; }
                                    }
                                    decodedData = new string(txt);

                                    result += Convert.ToInt64(decodedData);
                                }

                                // Atomically store the computed checksum
                                // Comment out for Test 01 as we have already incremented it
                                Interlocked.Add(ref takeSum, result);

                                // Wait at the barrier (finish line) until all test threads have been finished
                                _barrier.SignalAndWait();

                            }
                            catch (Exception unexpected)
                            {
                                Console.WriteLine("Result of _08_testPutTake_fixed() = An unexpected Exception was thrown");
                                Console.WriteLine(unexpected);
                                // Must put all the console output before this statement because the test stops at this point
                                Assert.Fail();
                            }
                        }
                    )).Start();
                    #endregion Consumer Lamda declaration

                }

                _barrier.SignalAndWait();   // Wait for all the threads to be ready
                _barrier.SignalAndWait();   // Wait for all the threads to finish

                // calculate the number of ticks elapsed during the test run
                long elapsedTime = Interlocked.Read(ref timerEndTime) - Interlocked.Read(ref timerStartTime);
                Console.WriteLine("Intermediate Result of _08_testPutTake_fixed() - elapsed time = {0} timer ticks for {1} producer/consumer pairs and {2} Messages",
                    elapsedTime, nPairs, nTrials);

                // Calculate the number of ticks per item enqueued and dequeued - the throughput of the queue
                // A single tick represents one hundred nanoseconds or one ten-millionth of a second. 
                // There are 10,000 ticks in a millisecond. 
                long ticksPerItem = elapsedTime / (nPairs * (long)nTrials);
                TimeSpan elapsedSpan = new TimeSpan(ticksPerItem);
                double milliSeconds = elapsedSpan.TotalMilliseconds;
                long nanoSeconds = ticksPerItem * 100;
                long throughput = 1000000000 / nanoSeconds;

                // Compares the checksum values computed to determine if the data enqueued was exactly the data dequeued
                Console.WriteLine("1st Result of _08_testPutTake_fixed() = (data enqueued {1} == data dequeued {2}) = {0} after {3} trials each by {4} pairs of producers/consumers",
                    Interlocked.Read(ref putSum) == Interlocked.Read(ref takeSum), Interlocked.Read(ref putSum), Interlocked.Read(ref takeSum), nTrials, nPairs);
                Assert.AreEqual(Interlocked.Read(ref putSum), Interlocked.Read(ref takeSum));

                Console.WriteLine("2nd Result of _08_testPutTake_fixed() = (Average latency = {0} timer ticks <= Threshold value {1}) = {2}",
                    Interlocked.Read(ref ticksPerItem),
                    AVERAGE_THROUGHPUT_THRESHOLD_TICKS,
                    Interlocked.Read(ref ticksPerItem) <= AVERAGE_THROUGHPUT_THRESHOLD_TICKS);

                Console.WriteLine("_08_testPutTake_fixed Throughput = {0} messages per second ", throughput);

                Console.WriteLine("_08_testPutTake_fixed {0} timer ticks = {1} nanoseconds or {2} milliseconds",
                    ticksPerItem, nanoSeconds, milliSeconds);

                Assert.LessOrEqual(Interlocked.Read(ref ticksPerItem), AVERAGE_THROUGHPUT_THRESHOLD_TICKS);

                Console.WriteLine("\nEnd of Test Run No. {0} in Test Suite No. {1}\n", initTestRunNumber, initTestSuiteNumber);

            }
            catch (AssertionException assertionFailed)
            {
                Console.WriteLine("Result of _08_testPutTake_fixed() = An assertion failed");
                Console.WriteLine(assertionFailed);
                // Must put all the console output before this statement because the test stops at this point
                Assert.Fail();
            }
            catch (Exception unexpected)
            {
                Console.WriteLine("Result of _08_testPutTake_fixed() = An unexpected Exception was thrown");
                Console.WriteLine(unexpected);
                // Must put all the console output before this statement because the test stops at this point
                Assert.Fail();
            }
            finally
            {
                // Temporarily delay disposing the queue and its IPC artefacts to allow the consumers to finish draining the queue
                // This will be fixed by waiting in an interrupible loop for the mutex inside the queue and checking if shutdown
                Thread.Sleep(1000);
                mmMain.Report();
                mmMain.Dispose();

                Console.WriteLine("\n");
            }
        }



        #region TEST Groups

        [Test]
        public void test_group_00()
        {
            int numTestRuns = 100;

            Console.WriteLine("Start Test Suite No. {0} using Integers - {1} test runs\n", ++initTestSuiteNumber, numTestRuns);

            for (int i = 0; i < numTestRuns; i++)
            {
                _05_testPutTakeInt();
            }

            Console.WriteLine("End Test Suite No. {0}\n", initTestSuiteNumber);
        }

        [Test]
        public void test_group_01()
        {
            int numTestRuns = 100;

            Console.WriteLine("Start Test Suite No. {0} using Long Integers - {1} test runs\n", ++initTestSuiteNumber, numTestRuns);

            for (int i = 0; i < numTestRuns; i++)
            {
                _05_testPutTakeLong();
            }

            Console.WriteLine("End Test Suite No. {0}\n", initTestSuiteNumber);
        }

        [Test]
        public void test_group_02()
        {
            int numTestRuns = 100;

            Console.WriteLine("Start Test Suite No. {0} using Strings - {1} test runs\n", ++initTestSuiteNumber, numTestRuns);

            for (int i = 0; i < numTestRuns; i++)
            {
                _07_testPutTakeString();
            }

            Console.WriteLine("End Test Suite No. {0}\n", initTestSuiteNumber);
        }

        [Test]
        public void test_group_03()
        {
            int numTestRuns = 100;

            Console.WriteLine("Start Test Suite No. {0} using Structs inc. Fixed Arrays - {1} test runs\n", ++initTestSuiteNumber, numTestRuns);

            for (int i = 0; i < numTestRuns; i++)
            {
                _08_testPutTake_fixed();
            }

            Console.WriteLine("End Test Suite No. {0}\n", initTestSuiteNumber);
        }

        #endregion TEST groups




        // This is the default layout that the compiler would use anyway 
        [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
        unsafe struct _09_MMData
        {
            public int TextLength;
            public fixed char Text[100];
        }

                // This is the default layout that the compiler would use anyway 
        [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
        unsafe struct _09_args
        {
            public MMChannel mQueue;
            public Barrier barrier;
        }

        private void _09_ControllerThread(_09_args args)
        {
            try
            {
                // Wait at the barrier (start line) until all test threads have been created and are ready to go
                args.barrier.SignalAndWait();

                Console.WriteLine("_09_ControllerThread released from start line barrier");

                Thread.Sleep(10000);

                Console.WriteLine("_09_ControllerThread calling 'stop' on the queue");
                // args.mQueue.shutdown();

                // Wait at the barrier (finish line) until all test threads have been finished
                args.barrier.SignalAndWait();
                Console.WriteLine("_09_ControllerThread released from finish line barrier");

            }
            catch (Exception unexpected)
            {
                Console.WriteLine("Result of _09_testPutTake_fixed_Cancel() = An unexpected Exception was thrown");
                Console.WriteLine(unexpected);
                // Must put all the console output before this statement because the test stops at this point
            }
        }

        // [Test]  // String values
        [Ignore("Cancel works to a certain extent but test hangs after shutdown")]
        public void _09_testPutTake_fixed_Cancel()
        {
            // Test that the queue performs correctly under unpredictable concurrent access by using multiple threads to 
            // to to perform Put and Take operations over a period of time and that nothing wnet wrong

            MMChannel mmMain = null;
            try
            {
                int capacity = 500, fileSize = 1000000, viewSize = 1000;
                string QueueName = "_09_testPutTake_fixed_Cancel";

                // If only performing a small number of trials then GC could impact the timing tests so try and request it beforehand
                // In the case of a small number of trials, hopefully GC won't be required again before the end of the test
                System.GC.Collect();

                // INFO Cannot use the Property (get/set) with an Interlocked - 
                // Store the value of the computed checksums here using Interlocked to ensure atomicty
                long putSum = 0, takeSum = 0;
                // Start and end times of the test run
                long timerStartTime = 0, timerEndTime = 0;

                // test parameters
                // Performance nPairs = 10, capacity = 10, nTrials = 1,000,000 = BlockingCollection = 60s, MMQueue = 254s
                int nPairs = 10, nTrials = 100000;

                Random rand = new Random();

                // Create the MMChannel which will instantiate the memory mapped files, mutexes, semaphores etc ... 
                mmMain = MMChannel.GetInstance(QueueName, fileSize, viewSize, capacity, DEBUG, TEST);

                #region Barrier and Barrier Action declaration
                // The barrier will wait for the test runner thread plus a producer and consumer each for the number of pairs
                // Waits for them all to be ready at the start line and again at the finish
                Barrier _barrier = new Barrier(nPairs * 2 + 1 + 1,
                    actionDelegate =>
                    {
                        // Check to see if the start time variable has been assigned or still = zero
                        // If false then this is the first execution of the barrier action (at the start). Otherwise it is the 
                        // second execution 9at the finish)
                        const long zeroFalse_1 = 0; // Not passed by ref so no need to be assignable
                        bool started = Interlocked.Equals(timerStartTime, zeroFalse_1);
                        started = !started;

                        // Store the start time or the end time depending on which execution this is
                        long t = DateTime.Now.Ticks;
                        if (!started)
                        {
                            Interlocked.Exchange(ref timerStartTime, t);
                        }
                        else
                        {
                            Interlocked.Exchange(ref timerEndTime, t);
                        }
                    }
                );
                #endregion Barrier and Barrier Action declaration

                // create pairs of threads to put and take items to/from the queue
                // Including the test runner thread the barriers will wait for nPairs * 2 + 1 there

                int i = 0; bool shutdown = false;

                while (i < nPairs && !shutdown)
                // for (int i = 0; i < nPairs; i++)
                {
                    #region Producer Lamda declaration

                    new Thread(
                        new ThreadStart(
                        // Old way - replace lamda expression '() =>' with 'delegate'
                        () =>
                        {
                            MMChannel mmq = null;
                            try
                            {
                                mmq = MMChannel.GetInstance(QueueName, fileSize, viewSize, capacity, DEBUG, TEST);

                                DateTime centuryBegin = new DateTime(2001, 1, 1);
                                DateTime currentDate = DateTime.Now;

                                int elapsedTicks = (int)(currentDate.Ticks - centuryBegin.Ticks);
                                int result = 0;

                                // Wait at the barrier (start line) until all test threads have been created and are ready to go
                                _barrier.SignalAndWait();

                                // Put the data into the queue as Strings, generating a new random number each time
                                // The consumer will convert back to integers and sum them 
                                // The Producer's sum should equal the Consumenr's sum at the end of the test
                                for (int j = nTrials; j > 0; --j)
                                {
                                    // If the RNG is sound then this proves that the data enqueued was dequeued
                                    int r = rand.Next(maxLongRandomSeed);

                                    _09_MMData data;

                                    // Test data string to enqueue and dequeue. Convert to a byte array. This array is a reference type so cannot be directly
                                    // passed to the View Accessor
                                    char[] encodedData = Convert.ToString(r).ToCharArray();
                                    // Store the length of the array for dequeueing later
                                    data.TextLength = encodedData.Length;
                                    // Copy the data to unmanaged memory char by char
                                    for (int k = 0; k < data.TextLength; k++) { unsafe { data.Text[k] = encodedData[k]; } }

                                    mmq.Put(data);
                                    result += r;

                                }
                                // Atomically store the computed checksum
                                // Comment out for Test 01 as we have already incremented it
                                Interlocked.Add(ref putSum, result);

                                // Wait at the barrier (finish line) until all test threads have been finished
                                _barrier.SignalAndWait();

                            }
                            catch (Exception unexpected)
                            {
                                shutdown = true;

                                Console.WriteLine("Result of _09_testPutTake_fixed_Cancel() = An unexpected Exception was thrown");
                                Console.WriteLine(unexpected);
                                // Must put all the console output before this statement because the test stops at this point
                                Assert.Fail();
                                throw;
                            }
                        }
                    )).Start();

                    #endregion Producer Lamda declaration

                    #region Consumer Lamda declaration

                    new Thread(
                        new ThreadStart(
                        // Old way - replace lamda expression '() =>' with 'delegate'
                        () =>
                        {
                            try
                            {
                                long result = 0;

                                // Wait at the barrier (start line) until all test threads have been created and are ready to go
                                _barrier.SignalAndWait();

                                // take the data from the queue as Strings, convert back to ints and sum them 
                                // The Producer's sum should equal the Consumer's sum at the end of the test
                                for (int k = nTrials; k > 0; --k)
                                {

                                    _08_MMData data = mmMain.Take<_08_MMData>();

                                    String decodedData;

                                    char[] txt = new char[data.TextLength];
                                    for (int m = 0; m < data.TextLength; m++)
                                    {
                                        unsafe { txt[m] = data.Text[m]; }
                                    }
                                    decodedData = new string(txt);

                                    result += Convert.ToInt64(decodedData);
                                }

                                // Atomically store the computed checksum
                                // Comment out for Test 01 as we have already incremented it
                                Interlocked.Add(ref takeSum, result);

                                // Wait at the barrier (finish line) until all test threads have been finished
                                _barrier.SignalAndWait();

                            }
                            catch (Exception unexpected)
                            {
                                shutdown = true;

                                Console.WriteLine("Result of _09_testPutTake_fixed_Cancel() = An unexpected Exception was thrown");
                                Console.WriteLine(unexpected);
                                // Must put all the console output before this statement because the test stops at this point
                                Assert.Fail();
                                throw;
                            }
                        }
                    )).Start();
                    #endregion Consumer Lamda declaration

                    i++;
                }

                // The controller thread with lamda expression that refers to a method rather than anonymous 

                // Assign the data and the view accessor to the struct that we will use for the parameterized threadstart
                _09_args arg = default(_09_args);
                arg.barrier = _barrier;

                Thread controller = new Thread(() => _09_ControllerThread(arg));
                controller.Start();

                _barrier.SignalAndWait();   // Wait for all the threads to be ready
                _barrier.SignalAndWait();   // Wait for all the threads to finish

                // calculate the number of ticks elapsed during the test run
                long elapsedTime = Interlocked.Read(ref timerEndTime) - Interlocked.Read(ref timerStartTime);
                Console.WriteLine("Intermediate Result of _09_testPutTake_fixed_Cancel() - elapsed time = {0} timer ticks for {1} producer/consumer pairs and {2} Messages",
                    elapsedTime, nPairs, nTrials);

                // Calculate the number of ticks per item enqueued and dequeued - the throughput of the queue
                // A single tick represents one hundred nanoseconds or one ten-millionth of a second. 
                // There are 10,000 ticks in a millisecond. 
                long ticksPerItem = elapsedTime / (nPairs * (long)nTrials);
                TimeSpan elapsedSpan = new TimeSpan(ticksPerItem);
                double milliSeconds = elapsedSpan.TotalMilliseconds;
                long nanoSeconds = ticksPerItem * 100;
                long throughput = 1000000000 / nanoSeconds;

                // Compares the checksum values computed to determine if the data enqueued was exactly the data dequeued
                Console.WriteLine("1st Result of _09_testPutTake_fixed_Cancel() = (data enqueued {1} == data dequeued {2}) = {0} after {3} trials each by {4} pairs of producers/consumers",
                    Interlocked.Read(ref putSum) == Interlocked.Read(ref takeSum), Interlocked.Read(ref putSum), Interlocked.Read(ref takeSum), nTrials, nPairs);
                Assert.AreEqual(Interlocked.Read(ref putSum), Interlocked.Read(ref takeSum));

                Console.WriteLine("2nd Result of _09_testPutTake_fixed_Cancel() = (Average latency = {0} timer ticks <= Threshold value {1}) = {2}",
                    Interlocked.Read(ref ticksPerItem),
                    AVERAGE_THROUGHPUT_THRESHOLD_TICKS,
                    Interlocked.Read(ref ticksPerItem) <= AVERAGE_THROUGHPUT_THRESHOLD_TICKS);

                Console.WriteLine("_09_testPutTake_fixed_Cancel Throughput = {0} messages per second ", throughput);

                Console.WriteLine("_09_testPutTake_fixed_Cancel {0} timer ticks = {1} nanoseconds or {2} milliseconds",
                    ticksPerItem, nanoSeconds, milliSeconds);

                Assert.LessOrEqual(Interlocked.Read(ref ticksPerItem), AVERAGE_THROUGHPUT_THRESHOLD_TICKS);

            }
            catch (AssertionException assertionFailed)
            {
                Console.WriteLine("Result of _09_testPutTake_fixed_Cancel() = An assertion failed");
                Console.WriteLine(assertionFailed);
                // Must put all the console output before this statement because the test stops at this point
                Assert.Fail();
            }
            catch (Exception unexpected)
            {
                Console.WriteLine("Result of _09_testPutTake_fixed_Cancel() = An unexpected Exception was thrown");
                Console.WriteLine(unexpected);
                // Must put all the console output before this statement because the test stops at this point
                Assert.Fail();
            }
            finally
            {
                mmMain.Report();
                mmMain.Dispose();
            }
        }



 
    }
}   // End of Namespace








        /**************************************************
Working with View Accessors
Calling CreateViewAccessor on a MemoryMappedFile gives you a view accessor that
lets you read/write values at random positions.
The Read/Write methods accept numeric types, bool, and char, as well as arrays
and structs that contain value-type elements or fields. Reference types—and arrays
or structs that contain reference types—are prohibited because they cannot map
into unmanaged memory. So if you want to write a string, you must encode it into
an array of bytes:
byte[] data = Encoding.UTF8.GetBytes ("This is a test");
accessor.Write (0, data.Length);
accessor.WriteArray (4, data, 0, data.Length);
Notice that we wrote the length first. This means we know how many bytes to read
back later:
byte[] data = new byte [accessor.ReadInt32 (0)];
accessor.ReadArray (4, data, 0, data.Length);
Console.WriteLine (Encoding.UTF8.GetString (data)); // This is a test
Here’s an example of reading/writing a struct:
struct Data { public int X, Y; }
...
var data = new Data { X = 123, Y = 456 };
accessor.Write (0, ref data);
accessor.Read (0, out data);
Console.WriteLine (data.X + " " + data.Y); // 123 456
        **************************************************/



//  Basic Unit Tests
//  ==============
//  The most basic unit tests for the Queue are similar to those which would be used in a sequential context;
//  Create the Queue, call its methods and assert postconditions and invariants
//  Invariants:
//    - A newly created Queue should identify itself as enpty and also not full
//  Could also test safety by inserting N elements into an empty queue of capacity N (which should succeed without blocking) and test
//  that the queue recognizes that it is full (and not empty) 

//  These simple tests are entirely sequential. This is often helpful because they can disclose when a problem is NOT due to 
//  concurrency issues before you start looking for data races.
//  JUnit tests
//  ========


// Testing Blocking Operations
// =====================

// Tests of essential concurrency properties require introducing more than one thread.
// Most testing frameworks are not very concurrency-friendly, they rarely include facilities to create threads or monitor them to ensure 
// that they do not die unexpectedly.
// If a helper thread created by a test case discovers a failure the framework usually does not with which thread the test is associated
// so some work may be required to relay success or failure info back to the main test runner thread so it can be reported.

// JSR 166 Expert Group's base class for java.util.concurrent tests
// http://gee.cs.oswego.edu/cgi-bin/viewcvs.cgi/jsr166/src/test/tck/JSR166TestCase
// Relays and reports failures during TearDown - each test must wait until all the threads that it cretaed have terminated.

// If a method is supposed to block under certain conditions then the test must only succeed if the thread does NOT proceed
// Another complication is that if a method successfully blocks then you have to convince it somehow to unblock.
// This can be done with Interruption. Start the blocking activity in a separate thread, wait until that thread blocks, interrupt it and then 
// assert that the blocking operation completed.
// So your blocking methods must respond to interruption by throwing InterruptedException or returning early.

// Difficult decision to make is how long to wait for the thread to block

// JUnit Test
// ==========
// Creates a Consumer thread that attempts to take an element from an empty Queue
// if take succeeds it registers failure

// The test runner thread starts the Consumer thread, waits a long time and then interrupts it
// If the Consumer thread has correctly blocked in the take operation then it will throw InterruptedException and the catch block for the
// Exception will treat this as success and let the thread exit

// The main test runner thread then attempts to join with the Consumer thread and and verifies that the join returned successfully by calling 
// thread.isAlive(). if the Consumer thread responded to the Interrupt then the join should complete quickly.

// The timed join ensures that the test completes even if take gets stuck in some unexpected way..
// Tests several properties of take. Not only that it blocks but that when interrupted it throws InterruptedException
// This is one of the few cases when it is appropriate to subclass Thread directly instead of using a Runnable or Callable in a pool
// in order to test proper termination with join.
// Use the same approach to test that the Consumer thread unblocks after an element is placed in the queue by the main thread

// Tempting to use Thread.getState to verify that the thread is actually blocking on a condition wait but unreliable
// There is nothing that requires a thread to ever enter WAITING or TIMED_WAITING state when blocked. The JVM could choose
// to implement blocking by spin-waiting instead.

// Similarly, because spurious wakeups from Object.wait or Condition.wait are permitted (see chapter 14) a thread in WAITING or 
// TIMED_WAITING state could temporarily transition into RUNNABLE state even if the condition for which it is waiting is not yet true.

// Also, it could take time for the target thread to settle into a blocking state

// The result of Thread.getState should not be used for concurrency control and is of limited usefulness for testing
// Its prime use is as a source of debugging information



// TimeSpan elapsedSpan = new TimeSpan(elapsedTicks);
// Java = seed = (this.hashCode() ^ (int) System.nanoTime());
// DateTime centuryBegin = new DateTime(2001, 1, 1);
// DateTime currentDate = DateTime.Now;

// Object to get a hash code from
// int computedHashCode = new Object().GetHashCode();

// int elapsedTicks = (int)centuryBegin.Ticks; 
// int elapsedTicks = (int)currentDate.Ticks;
// int elapsedTicks = (computedHashCode ^ DateTime.Now.Ticks);
// int elapsedTicks = (int) (currentDate.Ticks - centuryBegin.Ticks);
// int seed = (computedHashCode ^ elapsedTicks);
// int seed = (int)(new Object().GetHashCode() ^ elapsedTicks);
// int seed = (int)(new Object().GetHashCode());

// re-compute the random number
// seed = MMQueue<string>.xorShift(seed);


/*************************************************************************************************************
[Test]
public void testPutTake_Original()
{
    // Test that the queue performs correctly under unpredictable concurrent access by using multiple threads to 
    // to to perform Put and Take operations over a period of time and that nothing wnet wrong
    MMQueue<string> mmq = null;
    // BlockingCollection<string> mmq = null;

    try
    {
        int initialCount = 0;
        int maxCount = 20;
        string QueueName = "TestQueue_05";

        // Instantiate random number generator using system-supplied value as seed.
        Random rand = new Random();

        mmq = new MMQueue<string>(initialCount, maxCount, QueueName);
        // mmq = new BlockingCollection<string>(new ConcurrentQueue<string>(), maxCount);

        // INFO Cannot use the Property (get/set) with an Interlocked - 
        // Store the value of the computed checksums here using Interlocked to ensure atomicty
        long putSum = 0, takeSum = 0; 
                     
        // test parameters
        // Performance nPairs = 10, capacity = 10, nTrials = 1,000,000 = BlockingCollection = 60s, MMQueue = 254s
        int nPairs = 1, capacity = maxCount, nTrials = 1000;

        // The barrier will wait for the test runner thread plus a producer and consumer each for the number of pairs
        // Waits for them all to be ready at the start line and again at the finish
        Barrier _barrier = new Barrier(nPairs * 2 + 1);

        // create pairs of threads to put and take items to/from the queue
        // Including the test runner thread the barriers will wait for nPairs * 2 + 1 ther
        for (int i = 0; i < nPairs; i++)
        {
            Task<long> producerTask = Task.Factory.StartNew<long> (() => testPutTakeProducer(_barrier, nTrials, rand, mmq));


            #region Consumer Lamda declaration
            // Thread consumer =
            new Thread(
                new ThreadStart(
                // Old way - replace lamda expression '() =>' with 'delegate'
                () =>
                {
                    try
                    {
                        int result = 0;

                        // Wait at the barrier (start line) until all test threads have been created and are ready to go
                        _barrier.SignalAndWait();

                        // take the data from the queue as Strings, convert back to ints and sum them 
                        // The Producer's sum should equal the Consumenr's sum at the end of the test
                        for (int k = nTrials; k > 0; --k)
                        {
                            // Test 01 - increment a counter
                            Interlocked.Increment(ref takeSum);

                            // :> Have to take something from the collection or the producers block forever - duh!
                            // Test 01 - dequeue the number 0
                            // Test 02 - dequeue the number 1
                            // Test 03 - dequeue the random value
                            result += Convert.ToInt64(mmq.Take());

                        }

                        // Atomically store the computed checksum
                        // Comment out for Test 01 as we have already incremented it
                        // Interlocked.Add(ref takeSum, result);

                        // Wait at the barrier (finish line) until all test threads have been finished
                        _barrier.SignalAndWait();

                    }
                    catch (Exception failure)
                    {
                        Console.WriteLine(failure);
                        Assert.Fail();
                    }
                }
            )).Start();
            #endregion Consumer Lamda declaration


            long producerResult = producerTask.Result;

            Interlocked.Add(ref putSum, producerResult);

        }

        _barrier.SignalAndWait();   // Wait for all the threads to be ready
        _barrier.SignalAndWait();   // Wait for all the threads to finish

        Assert.AreEqual(Interlocked.Read(ref putSum), Interlocked.Read(ref takeSum));

    }
    catch (Exception failure)
    {
        Console.WriteLine(failure);
        Assert.Fail();
    }
    finally
    {
        mmq.Cleanup();
        // mmq.CompleteAdding(); mmq.Dispose();
    }

    #region testPutTake strategy

    // Brian Goetz - The challenge is to identify easily checked properties that will with high probability fail
    // if something goes wrong while at the same time not letting the failure auditing code limit concurrency artificially
    // Best if the test property does not require any synchronization

    // Starts N producer threads that generate and enqueue elements and N consumer threads that dequeue them
    // Each thread updates the checksum of the elements as they go in or out using a per thread checksum that is
    // combined at the end of the test so as to add no more synchronization or contention than is required to test the queue

    // Creating or starting a thread could be a moderately heavyweight operation
    // If a thread is short-running and you start a number of them in a loop then they run sequentially rather than 
    // concurrently in the worst case. Even if just one or two get a head start there could be fewer interleavings
    // To prevent this we could use a CountDownLatch as a starting gate and another as a finish gate
    // Another way is to use a cyclic barrier initialized with the number of worker threads plus one and have the 
    // worker threads and the driver wait at the barrier at the beginning and at the end of their run
    // (Latches are for waiting for events and barriers ar for waiting for threads - either will do here
    // This potentially creates more interleavings. The scheduler could still run each thread to completion sequentially
    // but if the runs are long enough it reduces the extent to which scheduling affects the results
            
    // The test also uses a deterministic termination criterion so that no additional inter-thread co-ordination to
    // figure out when the test is finished. It starts exactly as many producers as consumers and each of them puts or
    // takes the same numebr of elements so the total number of items added and removed is the same

    // Tests should be run on multi-processor systems to increase the diversity of potential interleavings.
    // There should be more active threads than CPUs so that any given time some threads are running and some are 
    // switched out thus reducing the predictability of interactions between threads

    #endregion testPutTake strategy

}

#region Producer Lamda declaration

// void testPutTakeProducer(Barrier _barrier, int nTrials, Random rand, BlockingCollection<string> mmq, long putSum)
long testPutTakeProducer(Barrier _barrier, int nTrials, Random rand, MMQueue<string> mmq)
{
    long putS = 0;

    try
    {
        // B.Goetz's Java version used "this.hashCode()" and this method was in a Runnable inner class
        // Creating an inner (nested) class inside a method may be possible in C# but seems to me all we 
        // need is an Object so we can get a hash code
        // http://msdn.microsoft.com/en-us/library/system.datetime.ticks.aspx

        int result = 0;
                

        // Wait at the barrier (start line) until all test threads have been created and are ready to go
         _barrier.SignalAndWait();

        // Put the data into the queue as Strings, generating a new random number each time
        // The consumer will convert back to integers and sum them 
        // The Producer's sum should equal the Consumenr's sum at the end of the test
                                
        for (int j = nTrials; j > 0; --j)             
        {              
            // Test 01 - increment a counter.               
            // Proves that the same number of objects were enqueued as dequeued            
            Interlocked.Increment(ref putS);              
            // :> Have to put something in the collection or the consumers block forever - duh!           
            // mmq.Add(Convert.ToString(0));      
            mmq.Put(Convert.ToString(0));

            // Test 02 - enqueue the number 1
            // Gives a little confidence that the data enqueued was dequeued but the compiler can easily
            // guess this and pre-compute the sum so this is just a stepping stone to the random numbers
            // mmq.Add(Convert.ToString(1));
            // mmq.Put(Convert.ToString(1));
            // result += 1;

            // Test 03 - enqueue the random value
            // If the RNG is sound then this proves that the data enqueued was dequeued
            // mmq.Add(Convert.ToString(seed));
            // int r = rand.Next(100001);
            // mmq.Add(Convert.ToString(r));
            // mmq.Put(Convert.ToString(r));
            // result += r;                     
        }
        // Atomically store the computed checksum
        // Comment out for Test 01 as we have already incremented it
        // Interlocked.Add(ref putS, result);

                

        // Wait at the barrier (finish line) until all test threads have been finished
         _barrier.SignalAndWait();

                 
    }
    catch (Exception failure)
    {
        Console.WriteLine(failure);
        Assert.Fail();
    }

    return putS;
}
#endregion Producer Lamda declaration

void testPutTakeConsumer()
{

}

*******************************************************************************************************/









//BeginInvoke(this, e, new AsyncCallback(GetResult), handler);

//public void GetResult(IAsyncResult result)
//{
//    // string format = (string) result.AsyncState;
//    AsyncResult delegateResult = (AsyncResult)result;
//    ReceiveFixMsgDel delegateInstance = (ReceiveFixMsgDel)delegateResult.AsyncDelegate;

//    delegateInstance.EndInvoke(result);
//}
















//_bwProducer.DoWork += bwProducer_DoWork;
//_bwProducer.ProgressChanged += _bwProducer_ProgressChanged;
//_bwProducer.RunWorkerCompleted += _bwProducer_RunWorkerCompleted;
//_bwProducer.RunWorkerAsync ("Hello to worker");
//Console.WriteLine ("Press Enter in the next 5 seconds to cancel");
//Console.ReadLine();
//if (_bwProducer.IsBusy) _bwProducer.CancelAsync();
//Console.ReadLine();
//}
//static void bwProducer_DoWork (object sender, DoWorkEventArgs e)
//{
//for (int i = 0; i <= 100; i += 20)
//{
//if (_bwProducer.CancellationPending) { e.Cancel = true; return; }
//_bwProducer.ReportProgress (i);
//Thread.Sleep (1000); // Just for the demo... don't go sleeping
//} // for real in pooled threads!
//e.Result = 123; // This gets passed to RunWorkerCompleted
//}
//static void _bwProducer_RunWorkerCompleted (object sender,
//RunWorkerCompletedEventArgs e)
//{
//if (e.Cancelled)
//Console.WriteLine ("You canceled!");
//else if (e.Error != null)
//Console.WriteLine ("Worker exception: " + e.Error.ToString());
//else
//Console.WriteLine ("Complete: " + e.Result); // from DoWork
//}
//static void _bwProducer_ProgressChanged (object sender,
//ProgressChangedEventArgs e)
//{
//Console.WriteLine ("Reached " + e.ProgressPercentage + "%");
//}
//}
//public class _05_int_args
//{
//    public _05_int_args(MMChannel mm, Barrier b, int n, Random r)
//    {
//        mmMain = mm;
//        barrier = b;
//        nTrials = n;
//        rand = r;
//    }

//    // Automatically implemented properties backed by compiler-generated variables
//    public MMChannel mmMain { get; set; }
//    public Barrier barrier { get; set; }
//    public int nTrials { get; set; }
//    public Random rand { get; set; }
//}

//                    Task<long>[] tasks = new Task<long>[nPairs];
//            _05_int_args args = new _05_int_args(mmMain, _barrier, nTrials, rand);

//            tasks[i] = Task.Factory.StartNew<long> (() => _05_IntProducerTask(new _05_int_args(mmMain, _barrier, nTrials, rand)));


//public long _05_IntProducerTask(_05_int_args args)
//{
//    try
//    {
//        // B.Goetz's Java version used "this.hashCode()" and this method was in a Runnable inner class
//        // Creating an inner (nested) class inside a method may be possible in C# but seems to me all we 
//        // need is an Object so we can get a hash code
//        // http://msdn.microsoft.com/en-us/library/system.datetime.ticks.aspx
//        // TimeSpan elapsedSpan = new TimeSpan(elapsedTicks);
//        // Java = seed = (this.hashCode() ^ (int) System.nanoTime());
//        DateTime centuryBegin = new DateTime(2001, 1, 1);
//        DateTime currentDate = DateTime.Now;

//        int elapsedTicks = (int)(currentDate.Ticks - centuryBegin.Ticks);

//        #region  WARNING - THIS TEST IS FOR INTEGERS ONLY!
//        // IT IS THE PROGRAMMER'S RESPONSIBILITY TO ENSURE THAT THE COMPUTED
//        // RESULT DOES NOT EXCEED THE MAX SIZE OF AN INTEGER
//        // The result depends on the product of number of trials and the max size of the random number generated
//        // In this case nTrials and maxIntRandomSeed respectively.
//        // Choose values that will not exceed the max size or you will get corrupted results
//        // An example is the Put sum is positive and the Take sum is negative because the result 
//        // overflowed the integer size and wrote a 1 to the sign bit or the Put sum is very large but
//        // the Take sum is orders of magnitude smaller because it overflowed but wrote a zero to the sign bit
//        #endregion WARNING - THIS TEST IS FOR INTEGERS ONLY!

//        int result = 0; long putSum = 0;

//        // Wait at the barrier (start line) until all test threads have been created and are ready to go
//        args.barrier.SignalAndWait();

//        for (int j = args.nTrials; j > 0; --j)
//        {
//            // enqueue the random value
//            // If the RNG is sound then this proves that the data enqueued was dequeued
//            int r = args.rand.Next(maxIntRandomSeed);
//            args.mmMain.Put(r);
//            result += r;
//        }
//        // Atomically store the computed checksum
//        Interlocked.Add(ref putSum, result);

//        // Wait at the barrier (finish line) until all test threads have been finished
//        args.barrier.SignalAndWait();

//        // putSum is the result to be returned from the task and atomically added to putSum in _05_testPutTakeInt()
//        ///==========================================================================================================
//        return putSum;

//    }
//    catch (Exception unexpected)
//    {
//        Console.WriteLine("_05_IntProducerTask() = An unexpected Exception was thrown");
//        Console.WriteLine(unexpected);
//    }
//}



















