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

namespace TestRunner
{
    class RunMMChannelService
    {
        // static DataStructureType TestDataStructureType = DataStructureType.Queue;

        static void Main(string[] args)
        {
            new RunMMChannelService().StartWindowsService();
        }

        public void StartWindowsService()
        {
            //int capacity = 500, fileSize = 1000000, viewSize = 1000;
            //string QueueName = "_07_testPutTakeString"; 
            //MMChannel mmq = new MMChannel(QueueName, fileSize, viewSize, capacity, TestDataStructureType);

            // Console.WriteLine(
            //     "Launched MMChannel windows service with nane {0}, capacity {1}, fileSize {2}, viewSize {3}, type {4}",
            //     QueueName, capacity, fileSize, viewSize, TestDataStructureType);

            // Console.WriteLine("Press ENTER to shutdown");
            // Console.ReadLine();
        }

    }
}


