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
using System.Text;
using System.Globalization;
using System.Collections.Concurrent;
using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;


namespace com.alphaSystematics.concurrency
{
    public static class UTCutil
    {

        public const String performanceCounter_bytes_in_all_heaps = "bytes_in_all_heaps";
        public const String performanceCounter_gc_handles = "gc_handles";
        public const String performanceCounter_gen_0_heap_size = "gen_0_heap_size";
        public const String performanceCounter_gen_1_heap_size = "gen_1_heap_size";
        public const String performanceCounter_gen_2_heap_size = "gen_2_heap_size";
        public const String performanceCounter_large_object_heap_size = "large_object_heap_size";

        #region Lazy static singleton initialization as per Java Concurrency in Practice Chap 16 Memory Model
        public static class ExecutingAssembly
        {
            private static class InitExecutingAssembly
            {
                // Instantiate a target object and set the Type instance to the target class type
                // Instantiate an Assembly class to the assembly housing the Integer type.  
                public static Assembly assembly = Assembly.GetAssembly(new Int32().GetType());
            }
            public static Assembly GetExecutingAssembly { get { return InitExecutingAssembly.assembly; } }
        }
        #endregion Lazy static singleton initialization as per Java Concurrency in Practice Chap 16 Memory Model
        

        public static string GetInstanceNameForProcessId(int pid)
        {
            // The CLR counters are per instance counters, thus you need to specify the instance name for the process you wish 
            // to query the counters for.
            // Should also use the constructor overload that allows you to specify that you wish to access the instance in 
            // "read-only" mode:
            // new PerformanceCounter(".NET CLR Memory", "# bytes in all heaps", Process.GetCurrentProcess().ProcessName, true); 
            // The instance name is not necessarily the same as Process.ProcessName (or Process.GetCurrentProcess().ProcessName 
            // for that matter). If there are multiple instances of a process, i.e. executable, the process name is created by 
            // appending a #<number>. To figure out the actual instance name of a process you should query the 
            // .NET CLR Memory\Process ID counter.

            var cat = new PerformanceCounterCategory(".NET CLR Memory");
            foreach (var instanceName in cat.GetInstanceNames())
            {
                using (var pcPid = new PerformanceCounter(cat.CategoryName, "Process ID", instanceName))
                {
                    if ((int)pcPid.NextValue() == pid)
                    {
                        return instanceName;
                    }
                }
            }

            throw new ArgumentException(
                string.Format("No performance counter instance found for process id '{0}'", pid),
                "pid");
        }

        public static Dictionary<String, PerformanceCounter> ReadKeyMemoryAndHandlePerformanceCounters(String applicationInstance)
        {
            // Declare a variable of type String named applicationInstance.
            // String applicationInstance = GetInstanceNameForProcessId(Process.GetCurrentProcess().Id);

            // Declare a variable of type ArrayList named performanceCounters.
            // ArrayList performanceCounters = new ArrayList();
            Dictionary<String, PerformanceCounter> performanceCounters = new Dictionary<String, PerformanceCounter>();

            // Instantiate the PeformanceCounters that can indicate memory and handle performance issues. 
            // Add each PerformanceCounter to the performanceCounters ArrayList as it is instantiated.

            // No. of bytes in all heaps
            performanceCounters.Add(performanceCounter_bytes_in_all_heaps, 
                new PerformanceCounter(".NET CLR Memory", "# bytes in all heaps", applicationInstance, true));
            // No. of GC Handles 
            performanceCounters.Add(performanceCounter_gc_handles, new PerformanceCounter(".NET CLR Memory", "# GC Handles", applicationInstance, true));
            // Gen 0 heap Size
            performanceCounters.Add(performanceCounter_gen_0_heap_size, 
                new PerformanceCounter(".NET CLR Memory", "Gen 0 Heap Size", applicationInstance, true));
            // Gen 1 heap Size
            performanceCounters.Add(performanceCounter_gen_1_heap_size, 
                new PerformanceCounter(".NET CLR Memory", "Gen 1 heap Size", applicationInstance, true));
            // Gen 2 heap Size
            performanceCounters.Add(performanceCounter_gen_2_heap_size, 
                new PerformanceCounter(".NET CLR Memory", "Gen 2 heap Size", applicationInstance, true));
            // Large Object heap size
            performanceCounters.Add(performanceCounter_large_object_heap_size, 
                new PerformanceCounter(".NET CLR Memory", "Large Object Heap size", applicationInstance, true));

            //StringBuilder counterSnapshot = new StringBuilder();

            //// Loop through the PerformanceCounters in performanceCounters ArrayList.
            //Dictionary<String, PerformanceCounter>.ValueCollection counters = performanceCounters.Values;

            //foreach (PerformanceCounter typePerformanceCounter in counters)
            //{
            //    // Append the PerformanceCounter's name and its Value to the counterSnapshot.
            //    counterSnapshot.Append(
            //        typePerformanceCounter.CounterName.ToString() + " " + GetCounterValue(typePerformanceCounter).ToString() + "\n");
            //}
            //// Console.WriteLine(counterSnapshot.ToString());

            return performanceCounters;
        }

        public static String GetCounterValue (PerformanceCounter pPerformanceCounter) {

            String retval = "";

            // Retrieve PerformanceCounter result based on its CounterType.
            switch (pPerformanceCounter.CounterType)
            {
                case PerformanceCounterType.NumberOfItems32:
                    retval = pPerformanceCounter.RawValue.ToString();
                    break;

                case PerformanceCounterType.NumberOfItems64:
                    retval = pPerformanceCounter.RawValue.ToString();
                    break;

                case PerformanceCounterType.RateOfCountsPerSecond32:
                    retval = pPerformanceCounter.NextValue().ToString();
                    break;

                case PerformanceCounterType.RateOfCountsPerSecond64:
                    retval = pPerformanceCounter.NextValue().ToString();
                    break;

                case PerformanceCounterType.AverageTimer32:
                    retval = pPerformanceCounter.NextValue().ToString();
                    break;

                default:
                    retval = null;
                    break;
            }

            return retval;
        }

    }
}
