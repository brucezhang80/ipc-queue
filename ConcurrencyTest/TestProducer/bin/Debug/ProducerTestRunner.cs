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
using System.Runtime.InteropServices;

namespace TestMMFile_Source
{
    class ProducerTestRunner
    {
        // The hardest part of writing tests is that when they fail you don't know if it is the test or the application 
        // thats broken unless you have confidence that the tests themselves have been tested thoroughly
        // in this case we are lucky in that we are trying to mimic the functionality of an existing library class but extend
        // it to use inter process.

        static DataStructureType initTestDataStructureType = default(DataStructureType);
        const long AVERAGE_THROUGHPUT_THRESHOLD_TICKS = 1000;
        int initNoOfTrials = 0; int initTestRunNumber = 0; int initTestSuiteNumber = 0; int maxLongRandomSeed = 0; int maxIntRandomSeed = 0;
        const int defaultNoOfTrials = 1000000;
        static bool TEST = false;

        static int Menu()
        {
            string result = ""; int choice = 0; bool valid = false;

            while (!valid)
            {
                Console.Clear();
                Console.WriteLine("Memory Mapped Message Channel test suite (Producers). Please choose from the following options:\n");

                Console.WriteLine("1: Test menu for the Producers\n");
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
                if (result.ToUpper() == "Q") { result = "99"; }

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

            if (!(choice > 0 && choice <= 1000000))
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

                int choice = 0;

                do
                {
                    ProducerTestRunner producer = null;
                    producer = new ProducerTestRunner();

                    choice = Menu();

                    if (choice > 0) {
                        String channelType = queueOrStack();
                        int numberOftrials = numberOfTrials();
                        producer.Init(channelType, numberOftrials); 
                    }

                    switch (choice)
                    {
                        case 1:
                            Console.WriteLine("Press ENTER to complete the Menu test for the Producers");
                            Console.ReadLine();

                            // Console.WriteLine("Press ENTER to execute Test _01_testEmptyWhenConstructed();");
                            // Console.ReadLine();
                            // producer._01_testEmptyWhenConstructed();
                            // Console.WriteLine("Press ENTER to EXIT Test _01_testEmptyWhenConstructed();");
                            // Console.ReadLine();
                            break;

                        //case 2:
                        //    Console.WriteLine("Press ENTER to execute Test _02_testIsFullAfterPutsAndEmptyAfterTakes();");
                        //    Console.ReadLine();
                        //    producer._02_testIsFullAfterPutsAndEmptyAfterTakes();
                        //    Console.WriteLine("Press ENTER to EXIT Test _02_testIsFullAfterPutsAndEmptyAfterTakes();");
                        //    Console.ReadLine();
                        //    break;

                        // case 3:

                        case 4:
                            TEST = false;
                            Console.WriteLine("Press ENTER to execute the producer component of Test _04_testTakeIsUnblockedWhenElementAdded();");
                            Console.ReadLine();
                            producer._04_testTakeIsUnblockedWhenElementAdded();
                            Console.WriteLine("Press ENTER to EXIT the producer component of Test _04_testTakeIsUnblockedWhenElementAdded();");
                            Console.ReadLine();
                            break;

                        case 5:
                            TEST = true;
                            Console.WriteLine("Press ENTER to execute the producer component of Test _05_testPutTakeInt();");
                            Console.ReadLine();
                            producer._05_testPutTakeInt();
                            Console.WriteLine("Press ENTER to EXIT the producer component of Test _05_testPutTakeInt();");
                            Console.ReadLine();
                            break;

                        case 6:
                            TEST = true;
                            Console.WriteLine("Press ENTER to execute the producer component of Test _05_testPutTakeLong();");
                            Console.ReadLine();
                            producer._05_testPutTakeLong();
                            Console.WriteLine("Press ENTER to EXIT the producer component of Test _05_testPutTakeLong();");
                            Console.ReadLine();
                            break;

                        case 7:
                            TEST = false;
                            Console.WriteLine("Press ENTER to execute the producer component of Test_07_testPutTakeString");
                            Console.ReadLine();
                            producer._07_testPutTakeString();
                            Console.WriteLine("Press ENTER to EXIT the producer component of Test _07_testPutTakeString");
                            Console.ReadLine();
                            break;

                        case 8:
                            TEST = false;
                            Console.WriteLine("Press ENTER to execute the producer component of Test _08_testPutTake_fixed");
                            Console.ReadLine();
                            producer._08_testPutTake_fixed();
                            Console.WriteLine("Press ENTER to EXIT the producer component of Test _08_testPutTake_fixed");
                            Console.ReadLine();
                            break;

                        case 10:
                            TEST = true;
                            Console.WriteLine("Press ENTER to execute the producer component of Test Group 00 - Integers");
                            Console.ReadLine();
                            producer.test_group_00();
                            Console.WriteLine("Press ENTER to EXIT the producer component of Test Group 00 - Integers");
                            Console.ReadLine();
                            break;

                        case 11:
                            TEST = true;
                            Console.WriteLine("Press ENTER to execute the producer component of Test Group 01 - Longs");
                            Console.ReadLine();
                            producer.test_group_01();
                            Console.WriteLine("Press ENTER to EXIT the producer component of Test Group 01 = Longs");
                            Console.ReadLine();
                            break;

                        case 12:
                            TEST = true;
                            Console.WriteLine("Press ENTER to execute the producer component of Test Group 02 - Strings");
                            Console.ReadLine();
                            producer.test_group_02();
                            Console.WriteLine("Press ENTER to EXIT the producer component of Test Group 02 - Strings");
                            Console.ReadLine();
                            break;

                        case 13:
                            TEST = true;
                            Console.WriteLine("Press ENTER to execute the producer component of Test Group 03 - Structs");
                            Console.ReadLine();
                            producer.test_group_03();
                            Console.WriteLine("Press ENTER to EXIT the producer component of Test Group 03 - Structs");
                            Console.ReadLine();
                            break;

                        case 14:
                            TEST = true;
                            Console.WriteLine("Press ENTER to execute the consumer component of Test Groups 00, 01, 02 and 03");
                            Console.ReadLine();
                            producer.test_group_00();
                            producer.test_group_01();
                            producer.test_group_02();
                            producer.test_group_03();
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

        public void Init(string channelType, int numberOfTrials)
        {
            // Configure all tests to be run on a queue or a stack type channel

            if (channelType.ToUpper() == "S")
            {
                initTestDataStructureType = DataStructureType.Stack;
            }
            else
            {
                initTestDataStructureType = DataStructureType.Queue;
            }

            initNoOfTrials = numberOfTrials;

            // These values are not used in the Consumers
            maxIntRandomSeed = 1000;
            maxLongRandomSeed = 1000000;
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            // Set up uncaught exception handler in case some dodgy code throws a RunTimeException 
            // This won't work if the exception is passed to some even more dodgy 3rd party code that swallows the exception. 
            Console.Write(e.ExceptionObject.ToString());
        }

        public void _01_testEmptyWhenConstructed()
        {
            TEST = false;

            MMChannel mmMain = null;

            try
            {
                int initialCount = 0;
                string QueueName = "_01_testEmptyWhenConstructed";
                int viewSize = 1000;
                int fileSize = 1000000;
                int capacity = 500;

                mmMain = MMChannel.GetInstance(QueueName, fileSize, viewSize, capacity, TEST, initTestDataStructureType);

                ControlData controlData = mmMain.MMFControlData;

                Console.WriteLine("Result of _01_testEmptyWhenConstructed() = (Count {1} == initialCount {2}) = {0}",
                    controlData.totalItemsEnqueued == initialCount, controlData.totalItemsEnqueued, initialCount);
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

        public void _02_testIsFullAfterPutsAndEmptyAfterTakes()
        {
            TEST = false;

            MMChannel mmMain = null;
            try
            {
                int initialCount = 0;
                string QueueName = "_02_testIsFullAfterPutsAndEmptyAfterTakes";
                int viewSize = 1000;
                int fileSize = 1000000;
                int capacity = 500;

                mmMain = MMChannel.GetInstance(QueueName, fileSize, viewSize, capacity, TEST, initTestDataStructureType);

                // Fill the queue
                // for (int i = 0; i < capacity; i++) { mmq.Add(i.ToString()); }
                for (int i = 0; i < capacity; i++) { mmMain.Put((char)i); }

                // Verify that the queue is full
                ControlData controlData = mmMain.MMFControlData;

                Console.WriteLine("_02_testIsFullAfterPutsAndEmptyAfterTakes count = {0} capacity = {1}", controlData.totalItemsEnqueued, capacity);

                // Empty the queue
                for (int i = 0; i < capacity; i++) { mmMain.Take<char>(); }

                // Verify that the queue is empty
                controlData = mmMain.MMFControlData;

                Console.WriteLine("Result of _02_testIsFullAfterPutsAndEmptyAfterTakes() = (reservations {1} == initialCount {2}) = {0} after enqueuing and dequeueing {3} items",
                   controlData.totalItemsEnqueued - controlData.totalItemsDequeued == initialCount, controlData.totalItemsEnqueued, initialCount, capacity);
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
        private void _04_EnqueueData(_04_args arg)
        {
            try
            {
                // Local 'data' or its members cannot have their address taken and be used inside an anonymous 
                // method or lambda expression - Error when trying to enqueue a struct
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

            _04_MMData data; // A struct containing data to be enqueued and dequeued 
            _04_args arg;    // A struct containing the data struct and the Memory Mapped File View Accessor to be passed as a parameter
            // to a parameterized threadstart

            string QueueName = "_04_testTakeIsUnblockedWhenElementAdded";
            mmMain = MMChannel.GetInstance(QueueName, fileSize, viewSize, capacity, TEST, initTestDataStructureType);

            // Populate the struct of data to be enqueued and dequeued 
            data.Value = 1;
            data.Letter = 'A';
            data.NumbersLength = 5;
            for (int i = 0; i < data.NumbersLength; i++) { unsafe { data.Numbers[i] = i; } }

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

            // perform the test from the main thread
            try
            {
                ControlData controlData = mmMain.MMFControlData;

                Console.WriteLine("Verify that the queue is empty");
                Console.WriteLine("_04_testTakeIsUnblockedWhenElementAdded() Queue is empty? = (Count {1} == initialCount {2}) = {0}\n",
                     controlData.totalItemsEnqueued - controlData.totalItemsDequeued == initialCount,
                     controlData.totalItemsEnqueued - controlData.totalItemsDequeued, initialCount);

                // Start a thread to enqueue an element
                Producer_1.Start();

                // Wait for a period for the thread to die
                Thread.Sleep(LOCKUP_DETECT_TIMEOUT_MILLIS);
                Producer_1.Join(LOCKUP_DETECT_TIMEOUT_MILLIS);
                // Verify the thread has died
                Console.WriteLine("_04_testTakeIsUnblockedWhenElementAdded() = Join main thread to Producer thread returned before {0} ms and Isalive = {1}",
                     LOCKUP_DETECT_TIMEOUT_MILLIS, Producer_1.IsAlive);

                Console.WriteLine("Verify the queue now contains one element");
                controlData = mmMain.MMFControlData;

                Console.WriteLine("_04_testTakeIsUnblockedWhenElementAdded() = (Count {1} == initialCount {2}) = {0} after enqueuing {3} items\n",
                     controlData.totalItemsEnqueued - controlData.totalItemsDequeued == initialCount,
                     controlData.totalItemsEnqueued - controlData.totalItemsDequeued, initialCount, controlData.totalItemsEnqueued);

                Console.WriteLine("Run the consumer then press ENTER to continue with the producer test runner");
                Console.ReadLine();


                // Start a thread to enqueue an element
                Producer_2.Start();

                // Wait for a period for the thread to die
                Thread.Sleep(LOCKUP_DETECT_TIMEOUT_MILLIS);
                Producer_2.Join(LOCKUP_DETECT_TIMEOUT_MILLIS);
                Console.WriteLine("Verify the Producer thread has died");
                Console.WriteLine("_04_testTakeIsUnblockedWhenElementAdded() = Joining the main thread to the Producer thread returned before {0} ms and Isalive = {1}\n",
                     LOCKUP_DETECT_TIMEOUT_MILLIS, Producer_2.IsAlive);

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


        private static int xOrShift(int y)
        {
            // Java Concurrency in Practice page 253. Listing 12.4 Medium quality RNG suitable for testing
            // Java Version
            // y ^= (y << 6);   ^= means ' y = y XORshift 6 - does not exist in C#
            // y ^= (y >>> 21); unsigned right shift operator - does not exist in C#
            // y ^= (y << 7);

            y = y ^ (y << 6);
            y = y ^ (int)((uint)y >> 21); // Have to cast to uint to simulate unsigned right shift operator
            y = y ^ (y << 7);

            return y;
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
                long putSum = 0;
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

                                // Original RNG
                                int elapsedTicks = (int)(currentDate.Ticks - centuryBegin.Ticks);

                                #region  WARNING - THIS TEST IS FOR INTEGERS ONLY!
                                // IT IS THE PROGRAMMER'S RESPONSIBILITY TO ENSURE THAT THE COMPUTED RESULT DOES NOT EXCEED THE MAX SIZE OF AN INTEGER
                                // The result depends on the product of number of trials and the max size of the random number generated
                                // In this case nTrials and maxIntRandomSeed respectively.
                                // Choose values that will not exceed the max size or you will get corrupted results
                                // An example is the Put sum is positive and the Take sum is negative because the result 
                                // overflowed the integer size and wrote a 1 to the sign bit or the Put sum is very large but
                                // the Take sum is orders of magnitude smaller because it overflowed but wrote a zero to the sign bit
                                #endregion WARNING - THIS TEST IS FOR INTEGERS ONLY!

                                int result = 0;

                                // Console.WriteLine("producer wait at the start barrier");

                                // Wait at the barrier (start line) until all test threads have been created and are ready to go
                                _barrier.SignalAndWait();

                                // The Producer's sum should equal the Consumenr's sum at the end of the test
                                for (int j = nTrials; j > 0; --j)
                                {
                                    // enqueue the random value
                                    // If the RNG is sound then this proves that the data enqueued was dequeued
                                    // mmq.Add(Convert.ToString(seed));
                                    // Original RNG
                                    int r = rand.Next(maxIntRandomSeed);

                                    // mmq.Add(Convert.ToString(r));

                                    // Original RNG
                                    mmMain.Put((long)r);
                                    // New RNG
                                    // mmMain.Put((long) elapsedTicks );

                                    // Original RNG
                                    result += r;
                                    // New RNG
                                    // result += elapsedTicks;

                                    // elapsedTicks = xOrShift(elapsedTicks);
                                }
                                // Atomically store the computed checksum
                                // Comment out for Test 01 as we have already incremented it
                                Interlocked.Add(ref putSum, result);

                                // Wait at the barrier (finish line) until all test threads have been finished
                                _barrier.SignalAndWait();

                            }
                            catch (Exception unexpected)
                            {
                                Console.Write(unexpected);
                                throw;
                            }
                            finally
                            {
                                // No need to dispose of these thread local queues as they will be garbase collected 
                                // when they go out scope though could consider closing them without disposing of the 
                                // IPC artefacts. These must remain in existance until all the queue has been drained by the consumers
                                // mmq.Close();
                            }
                        }
                    )).Start();

                    #endregion Producer Lamda declaration

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
                    Interlocked.Read(ref putSum), nTrials, nPairs);

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
                long putSum = 0;
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

                                // Console.WriteLine("producer wait at the start barrier");

                                // Wait at the barrier (start line) until all test threads have been created and are ready to go
                                _barrier.SignalAndWait();

                                // The Producer's sum should equal the Consumenr's sum at the end of the test
                                for (int j = nTrials; j > 0; --j)
                                {
                                    // enqueue the random value
                                    // If the RNG is sound then this proves that the data enqueued was dequeued
                                    long r = rand.Next(maxLongRandomSeed);
                                    mmMain.Put((long)r);
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
                                Console.Write(unexpected);
                                throw;
                            }
                            finally
                            {
                                // No need to dispose of these thread local queues as they will be garbase collected 
                                // when they go out scope though could consider closing them without disposing of the 
                                // IPC artefacts. These must remain in existance until all the queue has been drained by the consumers
                                // mmq.Close();
                            }
                        }
                    )).Start();

                    #endregion Producer Lamda declaration

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
                    Interlocked.Read(ref putSum), nTrials, nPairs);

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
                // This will be fixed by waiting in an interrupible loop for the mutex inside the queue and checking if shutdown
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
                long putSum = 0;
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
                                    int r = rand.Next(maxLongRandomSeed);
                                    // byte[] encodedData = MMChannel.StringToByteArray(Convert.ToString(r));
                                    char[] encodedData = Convert.ToString(r).ToCharArray();
                                    mmMain.Put(encodedData);
                                    result += r;

                                    // Java version - re-compute the random number
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
                                Console.Write(unexpected);
                                throw;
                            }
                        }
                    )).Start();


                    #endregion Producer Lamda declaration
                }

                int THRESHOLD = 1000; long diff;

                #region heap profiling testing notes
                // I got this from Java Concurrency in Practice, chap 12 Testing Concurrent Programs Page 258
                // It doesn't work as written though when using NUnit though
                // Generally, the heap size after testing was fraction of the size before the test
                // Seems, processing these huge messages has triggered a GC during the test
                // Even then you would expect this to result in a false positive where the two snapshot were similar even if
                // your code was leaking memory so the most likely explanation seems to be that NUnit itself is creating objects
                // which have not yet been reclaimed before the test starts
                // Requesting a GC before the initial snapshot solves the problem in NUnit
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
                Console.WriteLine("1st Result of _07_testPutTakeString() = (data enqueued = {0} after {1} trials each by {2} pairs of producers/consumers",
                    Interlocked.Read(ref putSum), nTrials, nPairs);

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
                long putSum = 0;
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
                                Console.Write(unexpected);
                                throw;
                            }
                        }
                    )).Start();


                    // Start a thread to enqueue an element
                    // Producer.Start();

                    #endregion Producer Lamda declaration

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
                Console.WriteLine("1st Result of _08_testPutTake_fixed() = (data enqueued = {0} after {1} trials each by {2} pairs of producers/consumers",
                    Interlocked.Read(ref putSum), nTrials, nPairs);

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

