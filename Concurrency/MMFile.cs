using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Text;
using System.Diagnostics;
using System.ComponentModel;
using System.IO.MemoryMappedFiles;

namespace com.alphaSystematics.concurrency
{


    public sealed class MMFile
    {
        // The view accessors will not accept data that is not 'non-nullable value type' which is defined here by the 
        // constraint ' where E : struct'. 

        #region variable declarations

        private Mutex _mutexLockQueue;

        ControlData control;

        private int _fileSize = 0, _viewSize = 0, _capacity = 0;
        private string _ipcFileName;

        // Extra space in a memory mapped file for the shutdown control variables
        const int _controlDataFileSize = 1000; 

        // Memory mapped file for IPC
        private MemoryMappedFile _memoryMappedDataFile; private String _memoryMappedDataFileName;
        private MemoryMappedFile _memoryMappedControlFile; private String _memoryMappedControlFileName;

        // Random Access views of the memory mapped data file.
        private MemoryMappedViewAccessor[] _viewAccessor;

        // Random Access views of the memory mapped control file
        private MemoryMappedViewAccessor _controlDataAccessor;

        // Append to _ipcName to form name 
        private static string _memoryMappeDataFileNameAppend = "_memoryMappedDataFileNameAppend_";
        private static string _memoryMappedControlFileNameAppend = "_memoryMappedControlFileNameAppend_";
        private static string strGUID = Guid.NewGuid().ToString("N");

        #endregion variable declarations

        #region constructor
        public MMFile(string ipcFileName, int fileSize, int viewSize, int capacity, bool createdNew) 
        {
            // Must be called from MMChannel with the mutex acquired in order to make instantiation of the IPC artifacts atomic

            _ipcFileName = ipcFileName;

            control.shutdownFlag = false;
            control.reservations = 0;
            control.queueAddPosition = 0;
            control.queueTakePosition = 0;
            control.initialCount = 0;
            control.stackAddTakePosition = 0;

            _fileSize = fileSize;
            _viewSize = viewSize;
            _capacity = capacity;

            Initialize(createdNew);
        }
        #endregion constructor

        public void Initialize(bool createdNew)    
        {
            // Must be called from MMChannel with the mutex acquired in order to make instantiation of the IPC artifacts atomic
            try
            {
                // We received _ipcFileName, fileSize, viewSize and capacity in the constructor
                if (_ipcFileName.Length == 0 || _fileSize <= 0 || _viewSize <= 0 || _capacity <= 0)
                { throw new Exception("Invalid arguments (zero or null) passed to MemMappedFile constructor"); }

                // The queue name is used to create the memory mapped file name along with a GUID to ensure that the name 
                // remains encapsulated within this class
                // Create the IPC artifact names by adding the pre-defined names to the user requested queue name

                _memoryMappedDataFileName = IPCName + _memoryMappeDataFileNameAppend + strGUID;
                _memoryMappedControlFileName = IPCName + _memoryMappedControlFileNameAppend + strGUID;

                // The capacity is the number of views, effectively elements in a queue, to be created in the file
                // so the number of elements times the size of each element must not be greater than the sixe of the file
                int cap_times_view = _capacity * _viewSize;
                if (_capacity * _viewSize >_fileSize)
                {
                    string msg = "Invalid arguments (Capacity * ViewSize " + cap_times_view +
                                    " > FileSize " + _fileSize + ") passed to MemMappedFile constructor";
                    throw new Exception(msg);
                }

                // Create the data file or if it already exists then open it
                //if (createdNew)
                //{
                    _memoryMappedDataFile = MemoryMappedFile.CreateOrOpen(_memoryMappedDataFileName, _fileSize);
                    _memoryMappedControlFile = MemoryMappedFile.CreateOrOpen(_memoryMappedControlFileName, _controlDataFileSize);
                //}
                //else
                //{
                //    _memoryMappedDataFile = MemoryMappedFile.OpenExisting(_memoryMappedDataFileName);
                //    _memoryMappedControlFile = MemoryMappedFile.OpenExisting(_memoryMappedControlFileName);
                //}
                // Create an array of views to access the data file
                _viewAccessor = (MemoryMappedViewAccessor[])new MemoryMappedViewAccessor[_capacity];

                // Populate the array of views from the memory mapped file. 
                // Each view starts at an offset calculated as the index times the size of the view and the size is specified as _viewSize
                for (int i = 0; i < _capacity; i++)
                {
                    _viewAccessor[i] = _memoryMappedDataFile.CreateViewAccessor(i * _viewSize, _viewSize);
                }

                // Create a view to access the control file
                _controlDataAccessor = _memoryMappedControlFile.CreateViewAccessor(0, _controlDataFileSize);

            }
            catch (AbandonedMutexException ame)
            {
                // Need to take some action to raise alert that a mutex was abandoned probably as a result of some thread
                // or process aborting machine-wide. Could have left some data in an inconsistant state
                // TODO replace with logging
                Console.WriteLine(ame);
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                // Not quite like Java. Can either use the keyword 'throw' to re-throw this exception or 
                // throw new xxxxxException ("message", ex); to wrap it in a new Exception but don't do 'throw ex;'
                throw;
            }
            // Any Exceptions will be caught and handled by MMChannel after being re-thrown here. 
            // MMChannel has a finally block to deal with releasing the mutexe as it was acquired in that class
        }

        #region Properties (getter/setter methods)

        private Mutex MLockQueue { get { return _mutexLockQueue; } set { _mutexLockQueue = value; } }
        public string IPCName { get { return _ipcFileName; } set { _ipcFileName = value; } }
        public int FileSize { get { return _fileSize; } set { _fileSize = value; } }
        public int ViewSize { get { return _viewSize; } set { _viewSize = value; } }
        public int Capacity { get { return _capacity; } set { _capacity = value; } }
        //public int FileSize { get { return control.fileSize; } set { control.fileSize = value; } }
        //public int ViewSize { get { return control.viewSize; } set { control.viewSize = value; } }
        //public int Capacity { get { return control.capacity; } set { control.capacity = value; } }
        public int Reservations { get { return control.reservations; } set { control.reservations = value; } }

        #region Control Data properties

        public ControlData MMFControlData
        {
            get { _controlDataAccessor.Read(0, out control); return control; }
            set { control = value; _controlDataAccessor.Write(0, ref control); }
        }

        #endregion Control Data properties

        #region Non-threadsafe public accessor methods that must be called with a lock held
        public bool IsFull() { return control.reservations == Capacity; }
        public int Length { get { return _viewAccessor.Length; } }

        public void Put<T>(int viewIndex, T element) where T : struct
        {
            // Console.WriteLine("Enqueue position = {0}", viewIndex);
            try
            {
                _viewAccessor[viewIndex].Write(0, ref element);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public T Take<T>(int viewIndex) where T : struct
        {
            // Console.WriteLine("Dequeue position = {0}", viewIndex);
            T item = default(T);

            try
            {
                _viewAccessor[viewIndex].Read<T>(0, out item);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            return item;
        }

        public void Put<T>(int viewIndex, T[] writeData) where T : struct
        {
            // How to pass a string into this method
            // byte[] element = MMFile_array_type.EncodeData((string)(object) 'some string');

            try
            {
                // Write the array length so the ReadArray method knows how much to read out or you'll get a load of junk in the 
                // rest of the array
                _viewAccessor[viewIndex].Write(0, writeData.Length);
                // Write the array
                _viewAccessor[viewIndex].WriteArray(4, writeData, 0, writeData.Length);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            #region WriteArray Specification

            // Summary: Writes structures from an array of type T into the accessor
            // Parameters: 
            //   position:  The index of the first structure in array to write.
            //   array:     The array to write into the accessor.
            //   offset:    The byte in the accessor at which to begin writing
            //   count:     The number of structures in array to write.
            //
            // Type parameters: T: The type of structure.
            //
            // Returns:
            //     The number of structures read into array. This value can be less than count if there are fewer structures 
            //      available, or zero if the end of the accessor is reached.
            //
            // Exceptions:
            //   System.ArgumentException: array is not large enough to contain count of structures (starting from position).
            //   System.ArgumentNullException:  array is null.
            //   System.ArgumentOutOfRangeException: position is less than zero or greater than the capacity of the accessor.
            //   System.NotSupportedException: The accessor does not support writing.
            //   System.ObjectDisposedException: The accessor has been disposed.

            #endregion ReadArray Specification
        }

        public int Take<T>(int viewIndex, out T[] readData) where T : struct
        {
            readData = new T[_viewAccessor[viewIndex].ReadInt32(0)];
            int count = 0;
            try
            {
                count = _viewAccessor[viewIndex].ReadArray(4, readData, 0, readData.Length);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            return count;

            // How to process an array of bytes returned from this method
            // string retval = (string)(object)MMFile_value_type.DecodeData(encodedData);

            #region ReadArray Specification

            // Summary: Reads structures of type T from the accessor into an array of type T.
            // Parameters: 
            //   position:  The index in array in which to place the first copied structure.
            //   array:     The array to contain the structures read from the accessor.
            //   offset:    The number of bytes in the accessor at which to begin reading.
            //   count:     The number of structures of type T to read from the accessor.
            //
            // Type parameters: T: The type of structure.
            //
            // Returns:
            //     The number of structures read into array. This value can be less than count if there are fewer structures 
            //      available, or zero if the end of the accessor is reached.
            //
            // Exceptions:
            //   System.ArgumentException: array is not large enough to contain count of structures (starting from position).
            //   System.ArgumentNullException:  array is null.
            //   System.ArgumentOutOfRangeException: position is less than zero or greater than the capacity of the accessor.
            //   System.NotSupportedException: The accessor does not support reading.
            //   System.ObjectDisposedException: The accessor has been disposed.

            #endregion ReadArray Specification
        }
        #endregion Non-threadsafe public accessor methods that must be called with a lock held

        #endregion Properties (getter/setter methods)

        // Decodes byte array to unicode string.
        public static string DecodeData(byte[] data)
        {
            Encoding utf16 = Encoding.Unicode;
            return utf16.GetString(data);
        }

        // Encodes, unicode, string to byte array.
        public static byte[] EncodeData(string data)
        {
            Encoding utf16 = Encoding.Unicode;
            return utf16.GetBytes(data);
        }

        #region Dispose of IPC artefacts
        // using (MemoryMappedFile mmFile = MemoryMappedFile.CreateNew("Demo", 500))
        // using (MemoryMappedViewAccessor accessor = mmFile.CreateViewAccessor())
        public void Close()
        {
            _memoryMappedDataFile.Dispose();
            _viewAccessor[0].Dispose();
        }
        #endregion Dispose of IPC artefacts



        #region C# in a Nutshell Chapter 14 Memory Mapped Files

        // Memory-Mapped Files and Shared Memory
        // 
        // You can also use memory-mapped files as a means of sharing memory between processes on the same computer. 
        // One process creates a shared memory block by calling MemoryMappedFile.CreateNew, while other processes subscribe 
        // to that same memory block by calling MemoryMappedFile.OpenExisting with the same name. Although it’s still referred 
        // to as a memory-mapped “file,” it lives entirely in memory and has no disk presence.
        // The following creates a 500-byte shared memory-mapped file, and writes the integer 12345 at position 0:
        // 
        // using (MemoryMappedFile mmFile = MemoryMappedFile.CreateNew ("Demo", 500))
        // using (MemoryMappedViewAccessor accessor = mmFile.CreateViewAccessor())
        // {
        //      accessor.Write (0, 12345);
        //      Console.ReadLine(); // Keep shared memory alive until user hits Enter.
        // }
        // while the following opens that same memory-mapped file and reads that integer:
        // This can run in a separate EXE:
        // 
        // using (MemoryMappedFile mmFile = MemoryMappedFile.OpenExisting ("Demo"))
        // using (MemoryMappedViewAccessor accessor = mmFile.CreateViewAccessor())
        // Console.WriteLine (accessor.ReadInt32 (0)); // 12345
        // 
        // Working with View Accessors
        // Calling CreateViewAccessor on a MemoryMappedFile gives you a view accessor that lets you read/write values at 
        // random positions.
        // The Read*/Write* methods accept numeric types, bool, and char, as well as arrays and structs that contain 
        // value-type elements or fields. Reference types—and arrays or structs that contain reference types—are prohibited 
        // because they cannot map into unmanaged memory. So if you want to write a string, you must encode it into
        // an array of bytes:
        // byte[] data = Encoding.UTF8.GetBytes ("This is a test");
        // accessor.Write (0, data.Length);
        // accessor.WriteArray (4, data, 0, data.Length);
        // 
        // Notice that we wrote the length first. This means we know how many bytes to read back later:
        // byte[] data = new byte [accessor.ReadInt32 (0)];
        // accessor.ReadArray (4, data, 0, data.Length);
        // Console.WriteLine (Encoding.UTF8.GetString (data)); // This is a test
        // Here’s an example of reading/writing a struct:
        // struct Data { public int X, Y; }
        // ...
        // var data = new Data { X = 123, Y = 456 };
        // accessor.Write (0, ref data);
        // accessor.Read (0, out data);
        // Console.WriteLine (data.X + " " + data.Y); // 123 456
        // 
        // You can also directly access the underlying unmanaged memory via a pointer. Following on from the previous example:
        // unsafe
        // {
        //     byte* pointer = null;
        //     accessor.SafeMemoryMappedViewHandle.AcquirePointer (ref pointer);
        //     int* intPointer = (int*) pointer;
        //     Console.WriteLine (*intPointer); // 123
        // }
        // Pointers can be advantageous when working with large structures: they let you work
        // directly with the raw data rather than using Read/Write to copy data between managed
        // and unmanaged memory. We explore this further in Chapter 25.

        #endregion C# in a Nutshell Chapter 14 Memory Mapped Files















        /*******************************************************************

		// Closes the memory mapped file and releases its resouces.
		// Callers should release their MmFile reference after calling
		// this method.
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
			Close();
		}
         
		// Closes the memory mapped file and releases its resouces.
		// Callers can still use their MmFile reference to create a new
		// memory mapped file.
		public void Close()
		{
			if (_mmf != null)
			{
				_mmf.Dispose();
				_mmf = null;
			}
			if (_mutex != null)
			{
				_mutex.Dispose();
				_mutex = null;
			}
			_id = string.Empty;
			_fileName = string.Empty;
			_mutexName = string.Empty;
			_segments.Clear();
			_isOpen = false;
		}
         
		#region Protected methods
		// Disposes the memory mapped file.
		protected void Dispose(bool disposing)
		{
			if (disposing)
			{
				if (_mmf != null)
				{
					_mmf.Dispose();
				}
				_mmf = null;
				if (_mutex != null)
				{
					_mutex.Dispose();
				}
				_mutex = null;
			}
		}
		#endregion
        *********************************************************************/


        /**********************************************************************************************************
        for (int i = 0; i < _capacity; i++)
        {
            unsafe
            {
                MemoryMappedViewAccessor accessor = _memoryMappedFile.CreateViewAccessor(i * _viewSize, _viewSize);
                byte* pointer = null;
                accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
                E* intPointer = (E*)pointer;
                Console.WriteLine(*intPointer); // 123
                _viewAccessor[i] = 

            }
        }
        *********************************************************************************************************/

        /*****************************************************************************************************************
[StructLayout(LayoutKind.Sequential)]
unsafe struct StructMMData
{

    // Allocate space for 200 chars (i.e., 400 bytes).
    const int MessageSize = 200;
    fixed char message[MessageSize];
    // One would most likely put this code into a helper class:
    public string Message
    {
        get { fixed (char* cp = message) return new string(cp); }
        set
        {
            fixed (char* cp = message)
            {
                int i = 0;
                for (; i < value.Length && i < MessageSize - 1; i++)
                    cp[i] = value[i];
                // Add the null terminator
                cp[i] = '\0';
            }
        }
    }
}
*****************************************************************************************************************/
    }
}