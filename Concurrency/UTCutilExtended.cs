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

        // T.Tapper MOD 0002.2 Change dates from local to UTC
        static public Nullable<DateTime> convertLocalToUTC(String sDate)
        {
            DateTime utcTime;

            try
            {
                DateTime dtEntered = DateTime.Parse(sDate, CultureInfo.InvariantCulture);
                dtEntered = dtEntered.Date;
                //this sets the time to 00:00:00 if one was passed in in the string
                // if not then concatenate " 00:00:00" to the date part of sDate string - sDate += " 00:00:00";
                utcTime = dtEntered.ToUniversalTime();
                return utcTime;
            }
            catch (System.ArgumentException)
            {
                // log invalid date string passed in;
            }
            catch (System.FormatException)
            {
                // log invalid date string passed in;
            }
            // test return value 
            //if (!value.HasValue)
            return null;

        }

        static public Nullable<DateTime> convertLocalToUTC(DateTime dtDate)
        {
            DateTime utcTime;

            try
            {
                utcTime = dtDate.ToUniversalTime();
                return utcTime;
            }
            catch (System.ArgumentException)
            {
                // log invalid date string passed in;
            }
            catch (System.FormatException)
            {
                // log invalid date string passed in;
            }
            //if (!utcTime.value.HasValue)
            return null;
            // test return value if (!value.HasValue)
        }

        static public Nullable<DateTime> convertLocalToUTCFromSOD(DateTime dtDate)
        {
            DateTime utcTime;

            try
            {
                // Remove the time component to determine "00:00:00" on the requested day i.e S.O.D
                utcTime = dtDate.Date.ToUniversalTime();
                // Return the UTC that is equivalent to "00:00:00" on the requested day
                return utcTime;
            }
            catch (System.ArgumentException)
            {
                // log invalid date string passed in;
            }
            catch (System.FormatException)
            {
                // log invalid date string passed in;
            }
            return null;
            // test return value if (!value.HasValue)
        }

        static public Nullable<DateTime> convertLocalToUTCToEOD(DateTime dtDate)
        {
            DateTime utcTime;

            try
            {
                dtDate = dtDate.AddDays(1);
                // Remove the time component, to determine "00:00:00" on the day after the requested day i.e S.O.D of the following day
                utcTime = dtDate.Date.ToUniversalTime();
                // To filter up to EOD of the requested day you must specify "<" the returned value
                // e.g "toDate < (dateTime) UTCutil.convertLocalToUTCToEOD( dtRequired )"
                // If you say "toDate < (dateTime) UTCutil.convertLocalToUTCToEOD( dtRequired )" you will also get anything 
                // with a timestamp of "00:00:00" on the day following the one you requested.
                // Return the UTC that is equivalent to "00:00:00" on the day following the requested day
                // Ex. Requested date to = 1 December EDT (Aus). We want everything up to midnight on 1 December
                // or before "00:00:00 2 December" EDT, in other words
                // UTC equivalent = 13:00 1 December. You must specify " todate < 13:00 1 December" 
                // " todate <= 13:00 1 December " would also select anything that was actually equivalent to  "00:00:00 2 December" EDT
                return utcTime;
            }
            catch (System.ArgumentException)
            {
                // log invalid date string passed in;
            }
            catch (System.FormatException)
            {
                // log invalid date string passed in;
            }
            return null;
            // test return value if (!value.HasValue)
        }

        // INT
        public static int toInt(object i)
        {
            bool wasNull = false;
            return toInt(i, out wasNull);
        }
        public static int toInt(object i, out bool wasNull)
        {
            // have to initialize output parameters
            // this is just in case you're interested in knowing whether the value you got back was originally null
            wasNull = false;

            if (i == System.DBNull.Value)
            {
                wasNull = true;
                return 0;
            }
            else
            {
                try
                {
                    return Convert.ToInt32(i);
                }
                catch (System.FormatException e)
                {
                    return 0;
                }
            }
        }

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




        // DOUBLE
        public static double toDouble(object d)
        {
            bool wasNull = false;
            return toDouble(d, out wasNull);
        }
        public static double toDouble(object d, out bool wasNull)
        {
            // have to initialize output parameters
            // this is just in case you're interested in knowing whether the value you got back was originally null
            wasNull = false;

            if (d == System.DBNull.Value)
            {
                wasNull = true;
                return 0;
            }
            else
            {
                try
                {
                    return Convert.ToDouble(d);
                }
                catch (System.FormatException e)
                {
                    return 0;
                }
            }
        }



        // DECIMAL
        public static decimal toDecimal(object d)
        {
            bool wasNull = false;
            return toDecimal(d, out wasNull);
        }
        public static decimal toDecimal(object d, out bool wasNull)
        {
            // have to initialize output parameters
            // this is just in case you're interested in knowing whether the value you got back was originally null
            wasNull = false;

            if (d == System.DBNull.Value)
            {
                wasNull = true;
                return 0;
            }
            else
            {
                try
                {
                    return Convert.ToDecimal(d);
                }
                catch (System.FormatException e)
                {
                    return 0;
                }
            }
        }

        // DATETIME
        public static Nullable<DateTime> toDateTime(object d)
        {
            bool wasNull = false;
            return toDateTime(d, out wasNull);
        }
        public static Nullable<DateTime> toDateTime(object d, out bool wasNull)
        {
            // have to initialize output parameters
            // this is just in case you're interested in knowing whether the value you got back was originally null
            wasNull = false;

            DateTime dt = new DateTime();
            if (d == System.DBNull.Value)
            {
                wasNull = true;
                return dt;
            }
            else
            {
                try
                {
                    return Convert.ToDateTime(d);
                }
                catch (System.FormatException e)
                {
                    return null;
                }
            }
        }

        // STRING
        public static string toString(object s)
        {
            bool wasNull = false;
            return toString(s, out wasNull);
        }
        public static string toString(object s, out bool wasNull)
        {
            // have to initialize output parameters
            // this is just in case you're interested in knowing whether the value you got back was originally null
            wasNull = false;

            if (s == System.DBNull.Value)
            {
                wasNull = true;
                return string.Empty;
            }
            else
            {
                try
                {
                    return Convert.ToString(s);
                }
                catch (System.FormatException e)
                {
                    return String.Empty;
                }
            }
        }


    }
}


// Get local time for info. May need at some point
// string timeZoneName = TimeZoneInfo.Local.DisplayName;

// Convert the datetime of the start of the chosen day (00:00:00) to UTC
// DateTime utcTime = TimeZoneInfo.ConvertTime(startDateTime, TimeZoneInfo.Local, TimeZoneInfo.Utc);
// DateTime estTime = new DateTime(2007, 1, 1, 00, 00, 00);
// string timeZoneName = "Eastern Standard Time";
// whatever date is chosen, we want everything > 00:00 on that day
// string dateString = sDate;
// this is just for testing
// string dateString = "5/1/2008 0:00:00 AM";
// DateTime startDateTime = DateTime.Parse(dateString, CultureInfo.InvariantCulture);

/************************************
Console.WriteLine("At {0} {1}, the time is {2} {3}.",
        estTime,
        est,
        utcTime,
        TimeZoneInfo.Utc.StandardName);


                
TimeZoneInfo localZone = TimeZoneInfo.Local;
Console.WriteLine("Local Time Zone ID: {0}", localZone.Id);
Console.WriteLine("   Display Name is: {0}.", localZone.DisplayName);
Console.WriteLine("   Standard name is: {0}.", localZone.StandardName);
Console.WriteLine("   Daylight saving name is: {0}.", localZone.DaylightName);


TimeZoneInfo utcZone = TimeZoneInfo.utc;
Console.WriteLine("Local Time Zone ID: {0}", utcZone.Id);
Console.WriteLine("   Display Name is: {0}.", utcZone.DisplayName);
Console.WriteLine("   Standard name is: {0}.", utcZone.StandardName);
Console.WriteLine("   Daylight saving name is: {0}.", utcZone.DaylightName);


DateTime date1 = DateTime.Now;
DateTime date2 = DateTime.UtcNow;
              
                


TimeZoneInfo local = TimeZoneInfo.Local;
TimeZoneInfo localTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneName);
// Convert EST to local time
DateTime localTime = TimeZoneInfo.ConvertTime(estTime, est, TimeZoneInfo.Local);
Console.WriteLine("At {0} {1}, the local time is {2} {3}.",
        estTime,
        est,
        localTime,
        TimeZoneInfo.Local.IsDaylightSavingTime(localTime) ?
                  TimeZoneInfo.Local.DaylightName :
                  TimeZoneInfo.Local.StandardName);
**********************************/

//catch (TimeZoneNotFoundException)
//{
// log invalid dat string zone cannot be found in the registry.", timeZoneName);
//}
//catch (InvalidTimeZoneException)
//{
// log invalid dat string "The registry contains invalid data for the {0} zone.", timeZoneName);

/*******************************************
 // Where is the support for historical DST changes. In my current time zone (EST) 
 // the following will not yield the correct offsets.
Console.WriteLine(DateTimeOffset.Parse(@"4/2/2006 2:59AM").ToString());
Console.WriteLine(DateTimeOffset.Parse(@"4/2/2006 3:00AM").ToString());
Console.WriteLine(DateTimeOffset.Parse(@"10/29/2006 0:59AM").ToString());
Console.WriteLine(DateTimeOffset.Parse(@"10/29/2006 1:00AM").ToString());


// Output:
// 4/2/2006 2:59:00 AM -04:00
// 4/2/2006 3:00:00 AM -04:00
// 10/29/2006 12:59:00 AM -04:00
// 10/29/2006 1:00:00 AM -04:00


// Also, it is important to note that any date/time in the ambiguous fall back hour will be consider not with in DST.
Console.WriteLine(DateTimeOffset.Parse(@"3/9/2008 2:00AM").ToString());
Console.WriteLine(DateTimeOffset.Parse(@"3/9/2008 3:00AM").ToString());
Console.WriteLine(DateTimeOffset.Parse(@"11/02/2008 0:59AM").ToString());
Console.WriteLine(DateTimeOffset.Parse(@"11/02/2008 1:00AM").ToString());


// outputs:
// 3/9/2008 2:00:00 AM -05:00
// 3/9/2008 3:00:00 AM -04:00
// 11/2/2008 12:59:00 AM -04:00
// 11/2/2008 1:00:00 AM -05:00
*******************************************/

        //http://blogs.msdn.com/b/larryosterman/archive/2005/09/08/apis-you-never-heard-of-the-timer-apis.aspx


        //public delegate void TimerEventHandler(UInt32 id, UInt32 msg, ref UInt32 userCtx, UInt32 rsv1, UInt32 rsv2);

        ///// <summary> 
        ///// A multi media timer with millisecond precision 
        ///// </summary> 
        ///// <param name="msDelay">One event every msDelay milliseconds</param> 
        ///// <param name="msResolution">Timer precision indication (lower value is more precise but resource unfriendly)</param> 
        ///// <param name="handler">delegate to start</param> 
        ///// <param name="userCtx">callBack data </param> 
        ///// <param name="eventType">one event or multiple events</param> 
        ///// <remarks>Dont forget to call timeKillEvent!</remarks> 
        ///// <returns>0 on failure or any other value as a timer id to use for timeKillEvent</returns> 
        //[DllImport("winmm.dll", SetLastError = true, EntryPoint = "timeSetEvent")]
        //static extern UInt32 timeSetEvent(UInt32 msDelay, UInt32 msResolution, TimerEventHandler handler, ref UInt32 userCtx, UInt32 eventType);

        ///// <summary> 
        ///// The multi media timer stop function 
        ///// </summary> 
        ///// <param name="uTimerID">timer id from timeSetEvent</param> 
        ///// <remarks>This function stops the timer</remarks> 
        //[DllImport("winmm.dll", SetLastError = true)]
        //static extern void timeKillEvent(UInt32 uTimerID);


        //[DllImport("WinMM.dll", SetLastError = true)]
        //private static extern uint timeSetEvent(int msDelay, int msResolution, TimerEventHandler handler, 
        //    ref int userCtx, int eventType);

        //[DllImport("WinMM.dll", SetLastError = true)]
        //static extern uint timeKillEvent(uint timerEventId);

        //public delegate void TimerEventHandler(uint id, uint msg, ref int userCtx, int rsv1, int rsv2);