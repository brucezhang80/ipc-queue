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

namespace TestMMFile_Shutdown
{
    public class ShutdownTestRunner
    {

        // The hardest part of writing tests is that when they fail you don't know if it is the test or the application 
        // thats broken unless you have confidence that the tests themselves have been tested thoroughly
        // in this case we are lucky in that we are trying to mimic the functionality of an existing library class but extend
        // it to use inter process.
        // We can drop in the library class here in order to test the test because we have confidence that the library class
        // works so if the tests fail when using library class then the tests are broken - For NUnit tests in a single process

        static DataStructureType initTestDataStructureType = default(DataStructureType);
        const long AVERAGE_THROUGHPUT_THRESHOLD_TICKS = 1000;
        int initNoOfTrials = 0; 
        const int defaultNoOfTrials = 1000000;
        const bool DEBUG = true; static bool TEST = false;

        static int Menu()
        {
            string result = ""; int choice = 0; bool valid = false;

            while (!valid)
            {
                Console.Clear();
                Console.WriteLine("Memory Mapped Message Channel test suite (Shutdown). Please choose from the following options:\n");

                Console.WriteLine("1: Test menu for the Shutdown\n");

                Console.WriteLine("3: Shutdown the Channel. Currently only implemented for Test No. 8: \nTest Put and Take methods with struct data and equal numbers of producers and consumers\n");

                Console.WriteLine("Q: Quit\n");

                // get the 1st character of input and quit if it is "Q"
                result = Console.ReadLine();
                if (result.ToUpper().Equals("Q")) { result = "0"; }

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
                        Console.WriteLine("Press ENTER to test the Menu for Shutdown");
                        Console.ReadLine();
                        valid = true;
                        break; 

                    case 3:
                        Console.WriteLine(" Shutting down Channel (Option {0}). Please wait...", result);
                        valid = true;
                        break; 

                    default:
                        Console.WriteLine("Invalid selection {0}. Please select 1 or 3 or Quit.\n\n\n\n\n", result);
                        break;
                }
            }
            return choice;
        }

        static string queueOrStack()
        {
            Console.WriteLine("Please choose to test a Queue or a Stack (Default = Queue)");
            string result = Console.ReadLine();
            if (result.ToUpper().Equals("S")) 
            { 
                result = "S"; 
            } 
            else {
                // if ( ! result.ToUpper().Equals("Q")) { Console.WriteLine(result + " is invalid. Defaulting to Queue)"); }
                result = "Q"; 
            }

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
                // Add the event handler for handling UI thread exceptions to the event. 
                // Application.ThreadException += new
                //    ThreadExceptionEventHandler(ErrorHandlerForm.Form1_UIThreadException);
                // Set the unhandled exception mode to force all Windows Forms  
                // errors to go through our handler. 
                // Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException); 

                // Add the event handler for handling non-UI thread exceptions to the event.  
                AppDomain.CurrentDomain.UnhandledException +=
                    new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException); 

                int choice = 0;

                do {
                    ShutdownTestRunner shutdown = null;
                    shutdown = new ShutdownTestRunner();

                    choice = Menu();

                    if (choice > 0) {
                        String channelType = queueOrStack();
                        int numberOftrials = numberOfTrials();                        
                        shutdown.Init(channelType, numberOftrials); 
                    }

                    switch (choice)
                    {
                        case 1:
                            Console.WriteLine("Press ENTER to complete the Menu test for Shutdown");
                        Console.ReadLine();
                        break;

                        case 3:
                        TEST = true;
                        shutdown._03_shutdown();
                        Console.WriteLine("Press ENTER to EXIT the shutdown component");
                        Console.ReadLine();
                        break;

                        default:
                        Console.WriteLine("No valid test selection was made. Shutting down...");
                        break;
                    }
                }
                while (choice > 1);
            }
            catch (Exception ex)
            {
                // Ignore ex - We should have displayed it in the individual TEST that failed
                Console.Write(ex);
            }
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
            Console.Write(e.ExceptionObject);
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

            public void _03_shutdown()
            {
                int initialCount = 0;
                int viewSize = 1000;
                int fileSize = 1000000;
                int capacity = 500;

                TEST = false;
                MMChannel mmMain = null;

                string QueueName = "_08_testPutTake_fixed";

                // Create the MMChannel which will instantiate the memory mapped files, mutexes, semaphores etc ... 
                mmMain = MMChannel.GetInstance(QueueName, fileSize, viewSize, capacity, DEBUG, TEST, initTestDataStructureType);

                // perform the test from the main thread
                try
                {
                    ControlData controlData = mmMain.MMFControlData;

                    // verify that the queue is empty
                    Console.WriteLine("_03_shutdown() Queue is empty? = (Count {1} == initialCount {2}) = {0}",
                         controlData.totalItemsEnqueued - controlData.totalItemsDequeued == initialCount,
                         controlData.totalItemsEnqueued - controlData.totalItemsDequeued, initialCount);

                    Console.WriteLine("Press ENTER to shutdown the Channel");
                    Console.ReadLine();
                    mmMain.shutdown();

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
                    Console.WriteLine("\n");
                    mmMain.Report();
                    mmMain.Dispose();
                }
            }

    }
}

