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
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Threading;
using com.alphaSystematics.concurrency;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Diagnostics;
// using QuickFix;
using System.Runtime.InteropServices;

namespace TestMMFile_Destination
{
    public class ConsumerTestRunner
    {

        // The hardest part of writing tests is that when they fail you don't know if it is the test or the application 
        // thats broken unless you have confidence that the tests themselves have been tested thoroughly
        // in this case we are lucky in that we are trying to mimic the functionality of an existing library class but extend
        // it to use inter process.

        static DataStructureType initTestDataStructureType = default(DataStructureType);
        const long AVERAGE_THROUGHPUT_THRESHOLD_TICKS = 1000;
        int initNoOfTrials = 0; int initTestRunNumber = 0; int initTestSuiteNumber = 0;
        const int defaultNoOfTrials = 1000000;
        static bool TEST = false;

        static int Menu()
        {
            string result = ""; int choice = 0; bool valid = false;

            while (!valid)
            {
                Console.Clear();
                Console.WriteLine("Memory Mapped Message Channel test suite (Consumers). Please choose from the following options:\n");

                Console.WriteLine("1: Test menu for the Consumers\n");
                // Console.WriteLine("1: Test that the queue is empty when constructed\n");
                // Console.WriteLine("2: Test that the queue is full after Puts and empty after Takes\n");
                // Console.WriteLine("3: Test that the Take method blocks when the queue is empty\n");
                Console.WriteLine("4: Test that the Take method is unblocked when an item is added\n");
                Console.WriteLine("5: Test Put and Take methods with Int data and equal numbers of producers and consumers\n");
                Console.WriteLine("6: Test Put and Take methods with Long data and equal numbers of producers and consumers\n");
                Console.WriteLine("7: Test Put and Take methods with array data (chars) and equal numbers of producers and consumers\n");
                Console.WriteLine("8: Test Put and Take methods with struct data and equal numbers of producers and consumers\n");
                Console.WriteLine("10: Execute Test Group No. 00 - 1 Billion Integers\n");
                Console.WriteLine("11: Execute Test Group No. 01 - 1 Billion Longs\n");
                Console.WriteLine("12: Execute Test Group No. 02 - 1 Billion Strings\n");
                Console.WriteLine("13: Execute Test Group No. 03 - 1 Billion Structs\n");
                Console.WriteLine("14: Execute Test Groups 00, 01, 02 and 03\n");


                Console.WriteLine("Q: Quit\n");

                // get the 1st character of input and quit if it is "Q"
                result = Console.ReadLine();
                if (result.ToUpper().Equals("Q")) { result = "99"; }

                try
                {
                    choice = int.Parse(result);
                }
                catch (ArgumentException) { }
                catch (FormatException) { }

                switch (choice)
                {
                    case 0:
                        Console.WriteLine("Quitting test harness {0} please wait...", result);
                        valid = true;
                        break; 
                    case 1:
                    // case 2:
                    // case 3:
                    case 4:
                    case 5:
                    case 6:
                    case 7:
                    case 8:
                    case 10:
                    case 11:
                    case 12:
                    case 13:
                    case 14:
                        Console.WriteLine("Executing test {0} please wait...", result);
                        valid = true;
                        break; 

                    default:
                        Console.WriteLine("Invalid selection {0}. Please select 1, 4, 5, 6, 7, 8, 9, 10, 11. 12, 13, 14  or Quit.\n\n\n\n\n", result);
                        break;
                }
            }
            return choice;
        }

        static string queueOrStack()
        {
            Console.WriteLine("Please choose to test a Queue or a Stack (Default = Queue)");
            string result = Console.ReadLine();
            if (result.ToUpper().Equals("S")) { result = "S"; } else { result = "Q"; }

            return result;
        }

        static int numberOfTrials()
        {
            Console.WriteLine("Please a number of trials, between 1 and 1,000,000, to test (Default = 1,000,000)");
            string result = Console.ReadLine();
            int choice = 0;

            try
            {
                choice = int.Parse(result);
            }
            catch (ArgumentException) { }
            catch (FormatException) { }

            if (!(choice > 0 && choice < 1000000))
            {
                Console.WriteLine(choice + " is invalid. Defaulting to 1,000,000)");
                choice = defaultNoOfTrials;
            }
            return choice;
        }

        static void Main(String[] args)
        {
            try
            {
                // If using forms etc add the event handler for handling UI thread exceptions to the event. 
                // Application.ThreadException += new
                //    ThreadExceptionEventHandler(ErrorHandlerForm.Form1_UIThreadException);
                // Set the unhandled exception mode to force all Windows Forms  
                // errors to go through our handler. 
                // Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException); 

                // Add the event handler for handling non-UI thread exceptions to the event.  
                AppDomain.CurrentDomain.UnhandledException +=
                    new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException); 

                // ConsumerTestRunner consumer = new ConsumerTestRunner();
                int choice = 0;

                do {
                    ConsumerTestRunner consumer = null;
                    consumer = new ConsumerTestRunner();

                    choice = Menu();

                    if (choice > 0) {
                        String channelType = queueOrStack();
                        int numberOftrials = numberOfTrials();                        
                        consumer.Init(channelType, numberOftrials); 
                    }

                    switch (choice)
                    {
                        case 1:
                        Console.WriteLine("Press ENTER to complete the Menu test for the Consumers");
                        Console.ReadLine();
                        break;

                        // case 2:
                        //case 3:
                        //Console.WriteLine("Press ENTER to execute Test _03_testTakeBlocksWhenEmpty();");
                        //Console.ReadLine();
                        //consumer._03_testTakeBlocksWhenEmpty();
                        //Console.WriteLine("Press ENTER to EXIT Test _03_testTakeBlocksWhenEmpty();");
                        //Console.ReadLine();
                        //break;

                        case 4:
                        TEST = false;
                        Console.WriteLine("Run the Producer FIRST then press ENTER to execute the consumer component of Test _04_testTakeIsUnblockedWhenElementAdded();");
                        Console.ReadLine();
                        consumer._04_testTakeIsUnblockedWhenElementAdded();
                        Console.WriteLine("Press ENTER to EXIT the consumer component of Test _04_testTakeIsUnblockedWhenElementAdded();");
                        Console.ReadLine();
                        break;

                        case 5:
                        TEST = true;
                        Console.WriteLine("Press ENTER to execute the consumer component of Test _05_testPutTakeInt();");
                        Console.ReadLine();
                        consumer._05_testPutTakeInt();
                        Console.WriteLine("Press ENTER to EXIT the consumer component of Test _05_testPutTakInt();");
                        Console.ReadLine();
                        break;

                        case 6:
                        TEST = true;
                        Console.WriteLine("Press ENTER to execute the consumer component of Test _05_testPutTakeLong();");
                        Console.ReadLine();
                        consumer._05_testPutTakeLong();
                        Console.WriteLine("Press ENTER to EXIT the consumer component of Test _05_testPutTakeLong();");
                        Console.ReadLine();
                        break;

                        case 7:
                        TEST = false;
                        Console.WriteLine("Press ENTER to execute the consumer component of Test_07_testPutTakeString");
                        Console.ReadLine();
                        consumer._07_testPutTakeString();
                        Console.WriteLine("Press ENTER to EXIT the consumer component of Test _07_testPutTakeString");
                        Console.ReadLine();
                        break;

                        case 8:
                        TEST = false;
                        Console.WriteLine("Press ENTER to execute the consumer component of Test _08_testPutTake_fixed");
                        Console.ReadLine();
                        consumer._08_testPutTake_fixed();
                        Console.WriteLine("Press ENTER to EXIT the consumer component of Test _08_testPutTake_fixed");
                        Console.ReadLine();
                        break;

                        case 10:
                        TEST = true;
                        Console.WriteLine("Press ENTER to execute the consumer component of Test Group 00 - Integers");
                        Console.ReadLine();
                        consumer.test_group_00();
                        Console.WriteLine("Press ENTER to EXIT the consumer component of Test Group 00 - Integers");
                        Console.ReadLine();
                        break;

                        case 11:
                        TEST = true;
                        Console.WriteLine("Press ENTER to execute the consumer component of Test Group 01 - Longs");
                        Console.ReadLine();
                        consumer.test_group_01();
                        Console.WriteLine("Press ENTER to EXIT the consumer component of Test Group 01 - Longs");
                        Console.ReadLine();
                        break;

                        case 12:
                        TEST = true;
                        Console.WriteLine("Press ENTER to execute the consumer component of Test Group 02 - Strings");
                        Console.ReadLine();
                        consumer.test_group_02();
                        Console.WriteLine("Press ENTER to EXIT the consumer component of Test Group 02 - Strings");
                        Console.ReadLine();
                        break;

                        case 13:
                        TEST = true;
                        Console.WriteLine("Press ENTER to execute the consumer component of Test Group 03 - Structs");
                        Console.ReadLine();
                        consumer.test_group_03();
                        Console.WriteLine("Press ENTER to EXIT the consumer component of Test Group 03 - Structs");
                        Console.ReadLine();
                        break;

                        case 14:
                        TEST = true;
                        Console.WriteLine("Press ENTER to execute the consumer component of Test Groups 00, 01, 02 and 03");
                        Console.ReadLine();
                        consumer.test_group_00();
                        consumer.test_group_01();
                        consumer.test_group_02();
                        consumer.test_group_03();
                        Console.WriteLine("Press ENTER to EXIT the consumer component of Test Groups 00, 01, 02 and 03");
                        Console.ReadLine();
                        break;

                        default:
                        Console.WriteLine("No valid test selection was made. Shutting down...");
                        break;
                    }
                }
                while (choice > 0);
            }
            catch (Exception ex)
            {
                // Ignore ex - We should have displayed it in the individual TEST that failed
                Console.Write(ex.Message);
            }
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            // Set up uncaught exception handler in case some dodgy code throws a RunTimeException 
            // This won't work if the exception is passed to some even more dodgy 3rd party code that swallows
            // the exception. 
            Console.Write(e.ExceptionObject.ToString());
        }

            public void Init(string channelType, int numberOfTrials)
            {
                // Configure all tests to be run on a queue or a stack type channel

                if (channelType.ToUpper() == "S")
                { 
                    initTestDataStructureType = DataStructureType.Stack; 
                } else { 
                    initTestDataStructureType = DataStructureType.Queue; 
                }

                initNoOfTrials = numberOfTrials;
                // These values are not used in the Consumers
                // maxIntRandomSeed = 1000;
                // maxLongRandomSeed = 1000000;
            }


            public void _03_testTakeBlocksWhenEmpty()
            {
                int LOCKUP_DETECT_TIMEOUT_MILLIS = 1000;
                int viewSize = 1000;
                int fileSize = 1000000;
                int capacity = 500;

                TEST = false;

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
                            }
                            catch (ThreadInterruptedException success)
                            {
                                Console.WriteLine("_03_testTakeBlocksWhenEmpty() = Pass - ThreadInterruptedException was thrown");
                                Console.WriteLine(success);
                                // DO NOT rethrow. Thes test was a success if Interrupted Exception was thrown
                            }
                            // Any other Exceptions we will not handle. Let them bubble up to the Main() method
                        }
                        )
                    );

                // perform the test from the main thread
                try
                {
                    mmMain = MMChannel.GetInstance(QueueName, fileSize, viewSize, capacity, TEST, initTestDataStructureType);

                    Consumer.Start();
                    Thread.Sleep(LOCKUP_DETECT_TIMEOUT_MILLIS);

                    ControlData controlData = mmMain.MMFControlData;

                    Consumer.Interrupt();
                    Consumer.Join(LOCKUP_DETECT_TIMEOUT_MILLIS);
                    Console.WriteLine("_03_testTakeBlocksWhenEmpty() = Join the main thread to the Consumer thread returned after {0} ms. Consumer thread alive = {1}",
                        LOCKUP_DETECT_TIMEOUT_MILLIS, Consumer.IsAlive);
                }
                catch (Exception unexpected)
                {
                    Console.Write(unexpected);
                    throw;
                }
                finally
                {
                    Thread.Sleep(1000);
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
            private void _04_DequeueData(_04_args arg)
            {
                try
                {
                    _04_MMData unused = arg.mQueue.Take<_04_MMData>();
                    StringBuilder numbers = new StringBuilder();
                    String text;
                    for (int i = 0; i < unused.NumbersLength; i++)
                    {
                        unsafe
                        {
                            numbers.Append(unused.Numbers[i] + ", ");
                        }
                    }

                    char[] txt = new char[unused.TextLength];
                    for (int i = 0; i < unused.TextLength; i++)
                    {
                        unsafe { txt[i] = unused.Text[i]; }
                    }
                    text = new String(txt);

                    Console.WriteLine("_04_testTakeIsUnblockedWhenElementAdded() = data item dequeued = \n'{0}', \n'{1}', \n'{2}', \n'{3}'",
                        unused.Value, unused.Letter, numbers, text);
                }
                catch (Exception unexpected)
                {
                    Console.Write(unexpected);
                    throw;
                }
            }

            public void _04_testTakeIsUnblockedWhenElementAdded()
            {
                int LOCKUP_DETECT_TIMEOUT_MILLIS = 1000;
                int initialCount = 0;
                int viewSize = 1000;
                int fileSize = 1000000;
                int capacity = 500;

                TEST = false;

                MMChannel mmMain = null;

                _04_MMData data = default(_04_MMData); // A struct containing data to be enqueued and dequeued 
                _04_args arg;    // A struct containing the data struct and the Memory Mapped File View Accessor to be passed as a parameter
                // to a parameterized threadstart

                string QueueName = "_04_testTakeIsUnblockedWhenElementAdded";
                mmMain = MMChannel.GetInstance(QueueName, fileSize, viewSize, capacity, TEST, initTestDataStructureType);

                // Assign the data and the view accessor to the struct that we will use for the parameterized threadstart
                arg.mQueue = mmMain;
                arg.dData = data;

                // Create the Consumer threads with anonymous lamda expression
                Thread Consumer_1 = new Thread(() => _04_DequeueData(arg));
                Thread Consumer_2 = new Thread(() => _04_DequeueData(arg));

                // perform the test from the main thread
                try
                {
                    ControlData controlData = mmMain.MMFControlData;

                    // verify that the queue is empty
                    Console.WriteLine("_04_testTakeIsUnblockedWhenElementAdded() Queue is empty? = (Count {1} == initialCount {2}) = {0}",
                         controlData.totalItemsEnqueued - controlData.totalItemsDequeued == initialCount,
                         controlData.totalItemsEnqueued - controlData.totalItemsDequeued, initialCount);

                    // Start a thread to dequeue an element
                    Consumer_1.Start();

                    // Wait for a period for the thread to die        
                    Thread.Sleep(LOCKUP_DETECT_TIMEOUT_MILLIS);
                    Consumer_1.Join(LOCKUP_DETECT_TIMEOUT_MILLIS);
                    // Verify the thread has died
                    Console.WriteLine("_04_testTakeIsUnblockedWhenElementAdded() = Join main thread to Consumer thread returned before {0} ms and Isalive = {1}",
                         LOCKUP_DETECT_TIMEOUT_MILLIS, Consumer_1.IsAlive);

                    Console.WriteLine("verify that the queue is empty");
                    controlData = mmMain.MMFControlData;

                    Console.WriteLine("_04_testTakeIsUnblockedWhenElementAdded() = (Count {1} == initialCount {2}) = {0} after dequeueing {3} items\n",
                         controlData.totalItemsEnqueued - controlData.totalItemsDequeued == initialCount,
                         controlData.totalItemsEnqueued - controlData.totalItemsDequeued, initialCount, controlData.totalItemsDequeued);

                    Console.WriteLine("Press ENTER to run another consumer to block on the empty queue");
                    Console.ReadLine();

                    Consumer_2.Start();
                    // Wait for a period for the thread to die        
                    Thread.Sleep(LOCKUP_DETECT_TIMEOUT_MILLIS);
                    Consumer_2.Join(LOCKUP_DETECT_TIMEOUT_MILLIS);
                    Console.WriteLine("Verify the Consumer thread has NOT died");
                    Console.WriteLine("_04_testTakeIsUnblockedWhenElementAdded() = Joining the main thread to the Consumer thread returned before {0} ms and Isalive = {1}\n",
                         LOCKUP_DETECT_TIMEOUT_MILLIS, Consumer_2.IsAlive);

                    Console.WriteLine("Go to the producer and Press ENTER to unblock the consumer waiting on the empty queue");
                    Console.ReadLine();

                    Console.WriteLine("Press ENTER to FINISH");
                    Console.ReadLine();

                }
                catch (Exception unexpected)
                {
                    Console.Write(unexpected);
                    throw;
                }
                finally
                {
                    Thread.Sleep(1000);
                    mmMain.Report();
                    mmMain.Dispose();
                }
            }



            public void _05_testPutTakeInt()
            {
                // Test that the queue performs correctly under unpredictable concurrent access by using multiple threads to 
                // to to perform Put and Take operations over a period of time and that nothing wnet wrong

                TEST = true;

                Console.WriteLine("\nStart of Test Run No. {0} in Test Suite No. {1}\n", ++initTestRunNumber, initTestSuiteNumber);

                MMChannel mmMain = null;

                try
                {
                    int capacity = 10, fileSize = 1000000, viewSize = 1000;
                    string QueueName = "_05_testPutTakeInt";

                    // If only performing a small number of trials then GC could impact the timing tests so try and request it beforehand
                    // In the case of a small number of trials, hopefully GC won't be required again before the end of the test
                    System.GC.Collect();

                    // INFO Cannot use the Property (get/set) with an Interlocked - 
                    // Store the value of the computed checksums here using Interlocked to ensure atomicty
                    long takeSum = 0;
                    // Start and end times of the test run
                    long timerStartTime = 0, timerEndTime = 0;

                    // test parameters
                    int nPairs = 10, nTrials = initNoOfTrials;

                    // Create the MMChannel which will instantiate the memory mapped files, mutexes, semaphores etc ... 
                    mmMain = MMChannel.GetInstance(QueueName, fileSize, viewSize, capacity, TEST, initTestDataStructureType);


                    #region Barrier and Barrier Action declaration
                    // The barrier will wait for the test runner thread plus a producer and consumer each for the number of pairs
                    // Waits for them all to be ready at the start line and again at the finish
                    Barrier _barrier = new Barrier(nPairs  + 1,
                        actionDelegate =>
                        {
                            // Check to see if the start time variable has been assigned or still = zero
                            // If false then this is the first execution of the barrier action (at the start). Otherwise it is the 
                            // second execution (at the finish)
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
                                        result += Convert.ToInt64(mmMain.Take<long>());
                                    }

                                    // Atomically store the computed checksum
                                    Interlocked.Add(ref takeSum, result);

                                    // Wait at the barrier (finish line) until all test threads have been finished
                                    _barrier.SignalAndWait();

                                    //=================================================================================
                                    // throw new Exception("Test Exception handling!!");
                                    //=================================================================================
                                }
                                catch (Exception unexpected)
                                {
                                    Console.WriteLine("_05_testPutTakeInt() Consumers = An unexpected Exception was thrown");
                                    Console.WriteLine(unexpected);
                                    throw;
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
                    Console.WriteLine("_05_testPutTakeInt() = (data enqueued = {0} after {1} trials each by {2} pairs of producers/consumers",
                        Interlocked.Read(ref takeSum), nTrials, nPairs);

                    Console.WriteLine("_05_testPutTakeInt() = (Average latency = {0} timer ticks <= Threshold value {1}) = {2}",
                        Interlocked.Read(ref ticksPerItem),
                        AVERAGE_THROUGHPUT_THRESHOLD_TICKS,
                        Interlocked.Read(ref ticksPerItem) <= AVERAGE_THROUGHPUT_THRESHOLD_TICKS);

                    Console.WriteLine("_05_testPutTake Throughput = {0} messages per second ", throughput);

                    Console.WriteLine("_05_testPutTake {0} timer ticks = {1} nanoseconds or {2} milliseconds",
                        ticksPerItem, nanoSeconds, milliSeconds);

                    Console.WriteLine("\nEnd of Test Run No. {0} in Test Suite No. {1}\n", initTestRunNumber, initTestSuiteNumber);
                }
                catch (Exception unexpected)
                {
                    Console.Write(unexpected);
                    throw;
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


            public void _05_testPutTakeLong()
            {
                // Test that the queue performs correctly under unpredictable concurrent access by using multiple threads to 
                // to to perform Put and Take operations over a period of time and that nothing wnet wrong

                TEST = true;

                Console.WriteLine("\nStart of Test Run No. {0} in Test Suite No. {1}\n", ++initTestRunNumber, initTestSuiteNumber);

                MMChannel mmMain = null;

                try
                {
                    int capacity = 10, fileSize = 1000000, viewSize = 1000;
                    string QueueName = "_05_testPutTakeLong";

                    // If only performing a small number of trials then GC could impact the timing tests so try and request it beforehand
                    // In the case of a small number of trials, hopefully GC won't be required again before the end of the test
                    System.GC.Collect();

                    // INFO Cannot use the Property (get/set) with an Interlocked - 
                    // Store the value of the computed checksums here using Interlocked to ensure atomicty
                    long takeSum = 0;
                    // Start and end times of the test run
                    long timerStartTime = 0, timerEndTime = 0;

                    // test parameters
                    int nPairs = 10, nTrials = initNoOfTrials;

                    // Create the MMChannel which will instantiate the memory mapped files, mutexes, semaphores etc ... 
                    mmMain = MMChannel.GetInstance(QueueName, fileSize, viewSize, capacity, TEST, initTestDataStructureType);


                    #region Barrier and Barrier Action declaration
                    // The barrier will wait for the test runner thread plus a producer and consumer each for the number of pairs
                    // Waits for them all to be ready at the start line and again at the finish
                    Barrier _barrier = new Barrier(nPairs + 1,
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
                                        result += mmMain.Take<long>();
                                        // result += Convert.ToInt64(mmMain.Take<long>());
                                    }

                                    // Atomically store the computed checksum
                                    Interlocked.Add(ref takeSum, result);

                                    // Wait at the barrier (finish line) until all test threads have been finished
                                    _barrier.SignalAndWait();

                                }
                                catch (Exception unexpected)
                                {
                                    Console.Write(unexpected);
                                    throw;
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
                    Console.WriteLine("_05_testPutTakeLong() = (data enqueued = {0} after {1} trials each by {2} pairs of producers/consumers",
                        Interlocked.Read(ref takeSum), nTrials, nPairs);

                    Console.WriteLine("_05_testPutTakeLong() = (Average latency = {0} timer ticks <= Threshold value {1}) = {2}",
                        Interlocked.Read(ref ticksPerItem),
                        AVERAGE_THROUGHPUT_THRESHOLD_TICKS,
                        Interlocked.Read(ref ticksPerItem) <= AVERAGE_THROUGHPUT_THRESHOLD_TICKS);

                    Console.WriteLine("_05_testPutTakeLong Throughput = {0} messages per second ", throughput);

                    Console.WriteLine("_05_testPutTakeLong {0} timer ticks = {1} nanoseconds or {2} milliseconds",
                        ticksPerItem, nanoSeconds, milliSeconds);

                    Console.WriteLine("\nEnd of Test Run No. {0} in Test Suite No. {1}\n", initTestRunNumber, initTestSuiteNumber);
                }
                catch (Exception unexpected)
                {
                    Console.Write(unexpected);
                    throw;
                }
                finally
                {
                    // Temporarily delay disposing the queue and its IPC artefacts to allow the consumers to finish draining the queue
                    Thread.Sleep(1000);
                    mmMain.Report();
                    mmMain.Dispose();
                }
            }

            public void _07_testPutTakeString()
            {
                // Test that the queue performs correctly under unpredictable concurrent access by using multiple threads to 
                // to to perform Put and Take operations over a period of time and that nothing went wrong

                TEST = false;
                MMChannel mmMain = null;

                Console.WriteLine("\nStart of Test Run No. {0} in Test Suite No. {1}\n", ++initTestRunNumber, initTestSuiteNumber);

                String applicationInstance = UTCutil.GetInstanceNameForProcessId(Process.GetCurrentProcess().Id);
                Dictionary<String, PerformanceCounter> map = UTCutil.ReadKeyMemoryAndHandlePerformanceCounters(applicationInstance);

                PerformanceCounter all_heaps_counter;
                map.TryGetValue(UTCutil.performanceCounter_bytes_in_all_heaps, out all_heaps_counter);
                String name = all_heaps_counter.CounterName.ToString();

                try
                {
                    int capacity = 500, fileSize = 1000000, viewSize = 1000;
                    string QueueName = "_07_testPutTakeString";

                    // If only performing a small number of trials then GC could impact the timing tests so try and request it beforehand
                    // In the case of a small number of trials, hopefully GC won't be required again before the end of the test
                    System.GC.Collect();

                    // INFO Cannot use the Property (get/set) with an Interlocked - 
                    // Store the value of the computed checksums here using Interlocked to ensure atomicty
                    long takeSum = 0;
                    // Start and end times of the test run
                    long timerStartTime = 0, timerEndTime = 0;

                    // test parameters
                    int nPairs = 10, nTrials = initNoOfTrials;

                    Random rand = new Random();

                    // Create the MMChannel which will instantiate the memory mapped files, mutexes, semaphores etc ... 
                    mmMain = MMChannel.GetInstance(QueueName, fileSize, viewSize, capacity, TEST, initTestDataStructureType);

                    #region Barrier and Barrier Action declaration
                    // The barrier will wait for the test runner thread plus a producer and consumer each for the number of pairs
                    // Waits for them all to be ready at the start line and again at the finish
                    Barrier _barrier = new Barrier(nPairs + 1,
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
                                    Console.Write(unexpected);
                                    throw;
                                }
                            }
                        )).Start();
                        #endregion Consumer Lamda declaration

                    }

                    int THRESHOLD = 1000; long diff;

                    #region heap profiling testing notes
                    // I got this from Java Concurrency in Practice, chap 12 Testing Concurrent Programs Page 258
                    // It doesn't work as written though if using NUnit though
                    // Generally, the heap size after testing was fraction of the size before the test
                    // Obviously, processing these huge messages has triggered a GC during the test
                    // Even then you would expect this to result in a false positive where the two snapshot were similar even if
                    // your code was leaking memory so the most likely explanation seems to be that NUnit itself is creating objects
                    // which have not yet been reclaimed before the test starts
                    // Requesting a GC before the initial snapshot solves the problem for NUnit
		    // You have to accept that you cannot completely control managed memory allocation and at some point the GC will probably ignore your request
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
                    Console.WriteLine("1st Result of _07_testPutTakeString() = (data dequeued = {0} after {1} trials each by {2} pairs of producers/consumers",
                        Interlocked.Read(ref takeSum), nTrials, nPairs);

                    Console.WriteLine("2nd Result of _07_testPutTakeString() = (Average latency = {0} timer ticks <= Threshold value {1}) = {2}",
                        Interlocked.Read(ref ticksPerItem),
                        AVERAGE_THROUGHPUT_THRESHOLD_TICKS,
                        Interlocked.Read(ref ticksPerItem) <= AVERAGE_THROUGHPUT_THRESHOLD_TICKS);

                    Console.WriteLine("_07_testPutTakeString Throughput = {0} messages per second ", throughput);

                    Console.WriteLine("_07_testPutTakeString n.b. {0} timer ticks = {1} nanoseconds or {2} milliseconds",
                        ticksPerItem, nanoSeconds, milliSeconds);

                    Console.WriteLine("\nEnd of Test Run No. {0} in Test Suite No. {1}\n", initTestRunNumber, initTestSuiteNumber);
                }
                catch (Exception unexpected)
                {
                    Console.Write(unexpected);
                    throw;
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

            public void _08_testPutTake_fixed()
            {
                TEST = false;

                // Test that the queue performs correctly under unpredictable concurrent access by using multiple threads to 
                // to to perform Put and Take operations over a period of time and that nothing wnet wrong

                MMChannel mmMain = null;

                Console.WriteLine("\nStart of Test Run No. {0} in Test Suite No. {1}\n", ++initTestRunNumber, initTestSuiteNumber);

                try
                {
                    int capacity = 500, fileSize = 1000000, viewSize = 1000;
                    string QueueName = "_08_testPutTake_fixed";

                    // If only performing a small number of trials then GC could impact the timing tests so try and request it beforehand
                    // In the case of a small number of trials, hopefully GC won't be required again before the end of the test
                    System.GC.Collect();

                    // INFO Cannot use the Property (get/set) with an Interlocked - 
                    // Store the value of the computed checksums here using Interlocked to ensure atomicty
                    long takeSum = 0;
                    // Start and end times of the test run
                    long timerStartTime = 0, timerEndTime = 0;

                    // test parameters
                    // Performance nPairs = 10, capacity = 10, nTrials = 1,000,000 = BlockingCollection = 60s, MMQueue = 254s
                    int nPairs = 10, nTrials = initNoOfTrials;

                    Random rand = new Random();

                    // Create the MMChannel which will instantiate the memory mapped files, mutexes, semaphores etc ... 
                    mmMain = MMChannel.GetInstance(QueueName, fileSize, viewSize, capacity, TEST, initTestDataStructureType);

                    #region Barrier and Barrier Action declaration
                    // The barrier will wait for the test runner thread plus a producer and consumer each for the number of pairs
                    // Waits for them all to be ready at the start line and again at the finish
                    Barrier _barrier = new Barrier(nPairs + 1,
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
                                    Console.Write(unexpected);
                                    throw;
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
                    Console.WriteLine("1st Result of _08_testPutTake_fixed() = (data dequeued = {0} after {1} trials each by {2} pairs of producers/consumers",
                        Interlocked.Read(ref takeSum), nTrials, nPairs);

                    Console.WriteLine("2nd Result of _08_testPutTake_fixed() = (Average latency = {0} timer ticks <= Threshold value {1}) = {2}",
                        Interlocked.Read(ref ticksPerItem),
                        AVERAGE_THROUGHPUT_THRESHOLD_TICKS,
                        Interlocked.Read(ref ticksPerItem) <= AVERAGE_THROUGHPUT_THRESHOLD_TICKS);

                    Console.WriteLine("_08_testPutTake_fixed Throughput = {0} messages per second ", throughput);

                    Console.WriteLine("_08_testPutTake_fixed {0} timer ticks = {1} nanoseconds or {2} milliseconds",
                        ticksPerItem, nanoSeconds, milliSeconds);

                    Console.WriteLine("\nEnd of Test Run No. {0} in Test Suite No. {1}\n", initTestRunNumber, initTestSuiteNumber);

                }
                catch (Exception unexpected)
                {
                    Console.Write(unexpected);
                    throw;
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

            #region TEST Groups

            public void test_group_00()
            {
                // No catch block as we've already displayed any exceptions and re-thrown in the tests themselves
                // Allow to bubble up to Main() where they will be caught and the program will exit
                int numTestRuns = 100;

                Console.WriteLine("Start Test Suite No. {0} using Integers - {1} test runs\n", ++initTestSuiteNumber, numTestRuns);

                for (int i = 0; i < numTestRuns; i++)
                {
                    _05_testPutTakeInt();
                }

                Console.WriteLine("End Test Suite No. {0}\n", initTestSuiteNumber);
            }

            public void test_group_01()
            {
                // No catch block as we've already displayed any exceptions and re-thrown in the tests themselves
                // Allow to bubble up to Main() where they will be caught and the program will exit

                int numTestRuns = 100;

                Console.WriteLine("Start Test Suite No. {0} using Long Integers - {1} test runs\n", ++initTestSuiteNumber, numTestRuns);

                for (int i = 0; i < numTestRuns; i++)
                {
                    _05_testPutTakeLong();
                }
                
                Console.WriteLine("End Test Suite No. {0}\n", initTestSuiteNumber);
            }

            public void test_group_02()
            {
                // No catch block as we've already displayed any exceptions and re-thrown in the tests themselves
                // Allow to bubble up to Main() where they will be caught and the program will exit

                int numTestRuns = 100;

                Console.WriteLine("Start Test Suite No. {0} using Strings - {1} test runs\n", ++initTestSuiteNumber, numTestRuns);

                for (int i = 0; i < numTestRuns; i++)
                {
                    _07_testPutTakeString();
                }

                Console.WriteLine("End Test Suite No. {0}\n", initTestSuiteNumber);
            }

            public void test_group_03()
            {
                // No catch block as we've already displayed any exceptions and re-thrown in the tests themselves
                // Allow to bubble up to Main() where they will be caught and the program will exit

                int numTestRuns = 100;

                Console.WriteLine("Start Test Suite No. {0} using Structs inc. Fixed Arrays - {1} test runs\n", ++initTestSuiteNumber, numTestRuns);

                for (int i = 0; i < numTestRuns; i++)
                {
                    _08_testPutTake_fixed();
                }

                Console.WriteLine("End Test Suite No. {0}\n", initTestSuiteNumber);
            }

            #endregion TEST groups

    }
}

