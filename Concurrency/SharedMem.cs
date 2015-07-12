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
using System.Runtime.InteropServices;
using System.Threading;
using System.Text;
using System.Diagnostics;
using System.ComponentModel;

namespace com.alphaSystematics.concurrency
{
    public sealed class SharedMem : IDisposable
    {
        // Here we're using enums because they're safer than constants
        enum FileProtection : uint // constants from winnt.h
        {
            ReadOnly = 2,
            ReadWrite = 4
        }
        enum FileRights : uint // constants from WinBASE.h
        {
            Read = 4,
            Write = 2,
            ReadWrite = Read + Write
        }

        // We set SetLastError=true on the DllImport methods that use the SetLastError protocol for emitting error codes. 
        // This ensures that the Win32Exception is populated with details of the error when that exception is thrown. 
        // (It also allows you to query the error explicitly by calling Marshal.GetLastWin32Error.)
        static readonly IntPtr NoFileHandle = new IntPtr (-1);
        [DllImport ("kernel32.dll", SetLastError = true)]
        static extern IntPtr CreateFileMapping (IntPtr hFile, 
                                                int lpAttributes,
                                                FileProtection flProtect,
                                                uint dwMaximumSizeHigh,
                                                uint dwMaximumSizeLow,
                                                string lpName);

        [DllImport ("kernel32.dll", SetLastError=true)]
        static extern IntPtr OpenFileMapping (FileRights dwDesiredAccess,
                                                bool bInheritHandle,
                                                string lpName);

        [DllImport ("kernel32.dll", SetLastError = true)]
        static extern IntPtr MapViewOfFile (IntPtr hFileMappingObject,
                                                FileRights dwDesiredAccess,
                                                uint dwFileOffsetHigh,
                                                uint dwFileOffsetLow,
                                                uint dwNumberOfBytesToMap);

        [DllImport("Kernel32.dll", SetLastError = true)]
        static extern bool UnmapViewOfFile(IntPtr map);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern int CloseHandle(IntPtr hObject);

        IntPtr fileHandle, fileMap;
        public IntPtr Root { get { return fileMap; } }

        public SharedMem(string name, bool existing, uint sizeInBytes)
        {
            if (existing)
                fileHandle = OpenFileMapping(FileRights.ReadWrite, false, name);
            else
                fileHandle = CreateFileMapping(NoFileHandle, 0, FileProtection.ReadWrite, 0, sizeInBytes, name);

            if (fileHandle == IntPtr.Zero)
                throw new Win32Exception();

            // Obtain a read/write map for the entire file
            fileMap = MapViewOfFile(fileHandle, FileRights.ReadWrite, 0, 0, 0);
            if (fileMap == IntPtr.Zero)
                throw new Win32Exception();
    }
    public void Dispose()
    {
        if (fileMap != IntPtr.Zero) UnmapViewOfFile(fileMap);
        if (fileHandle != IntPtr.Zero) CloseHandle(fileHandle);
        fileMap = fileHandle = IntPtr.Zero;
    }
}









}