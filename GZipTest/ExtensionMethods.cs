using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace GZipTest
{
    /// <summary>
    /// Contains custom extension methods for types
    /// example FileStream.MoveToNextGZip - move FileStream to next gzipped part
    /// </summary>
    public static class ExtensionMethods
    {
        /// <summary> Minimum length, nessesary to determine gzip header </summary>
        const int HeaderLength = 10;

        /// <summary>
        /// Reads a number of one compressed bytes part into specific byte array and move FileStream to next gzipped part
        /// </summary>
        /// <param name="BMass">The array used to store one compressed part bytes. If stream is only one parted will be null</param>
        /// <param name="ReadLocker">Lock object</param>
        /// <returns>Returns true if file stream not ends</returns>
        public static bool MoveToNextGZip(this FileStream fs, out byte[] BMass)
        {
            bool result = false;
            int bytesRead = 0;
            int countAllBytesRead = 0;
            BMass = new byte[HeaderLength];

            while ((bytesRead = fs.ReadByte()) != -1)
            {
                //stream have data, return true because we need compare
                result = true;

                //Skip first read because it always header
                if (countAllBytesRead == 0)
                {
                    countAllBytesRead++;
                    continue;
                }

                //try find next header
                if (bytesRead == 0x1F)
                    if (IsGZipHeader(fs))
                        break;

                countAllBytesRead++;
            }

            //return to first enter in stream
            fs.Seek(-countAllBytesRead, SeekOrigin.Current);

            //this need for check files from another program, that not concatinate streams
            if (countAllBytesRead == fs.Length)
            {
                //mark output massive so we need compress all stream
                BMass = null;
                return result;
            }

            BMass = new byte[countAllBytesRead];
            fs.Read(BMass, 0, countAllBytesRead);

            return result;
        }

        /// <summary>
        /// Thread safe overload. Read a number of one compressed bytes part into specific byte array and move FileStream to next gzipped part
        /// Get previous event handler into WaitEventRead, create new event handler for next reader into CurEventRead (and PrevEvent as buffer) 
        /// Lock stream while reading
        /// </summary>
        /// <param name="BMass">The array used to store one compressed part bytes. If stream is only one parted will be null</param>
        /// <param name="ReadLocker">Lock object for only one in time read</param>
        /// <param name="CurEventRead">Handler what say to next thread - wait wille i write my info</param>
        /// <param name="PrevEvent">Handler what store info about previous handler who read stream</param>
        /// <param name="WaitEventRead">Output for previous handler, current stream will wait it</param>
        /// <param name="ReadLocker">Lock object</param>
        public static bool MoveToNextGZip(this FileStream fs, out byte[] BMass, Object ReadLocker
            , ref ManualResetEvent CurEventRead, ref ManualResetEvent PrevEvent, ref ManualResetEvent WaitEventRead)
        {
            lock (ReadLocker)
            {
                CurEventRead = new ManualResetEvent(false);
                WaitEventRead = PrevEvent;
                PrevEvent = CurEventRead;
                return fs.MoveToNextGZip(out BMass);
            }
        }

        /// <summary>
        /// Check current file stream postion for header
        /// use this only if current position byte = 0x1F
        /// </summary>
        /// <param name="fs">The stream to check.</param>
        /// <returns>Returns true if the stream a valid gzip header at the current position.</returns>
        public static bool IsGZipHeader(FileStream fs)
        {
            // Read the first ten bytes of the stream
            byte[] header = new byte[HeaderLength - 1];

            int bytesRead = fs.Read(header, 0, header.Length);
            fs.Seek(-bytesRead, SeekOrigin.Current);

            if (bytesRead < header.Length)
            {
                return false;
            }

            // Check the id tokens and compression algorithm
            if (header[0] != 0x8B || header[1] != 0x8)
            {
                return false;
            }

            // Extract the GZIP flags, of which only 5 are allowed (2 pow. 5 = 32)
            if (header[2] > 32)
            {
                return false;
            }

            // Check the extra compression flags, which is either 2 or 4 with the Deflate algorithm
            if (header[7] != 0x0 && header[7] != 0x2 && header[7] != 0x4)
            {
                return false;
            }

            fs.Seek(-1, SeekOrigin.Current);

            return true;
        }

        /// <summary>
        /// Thread safe. Reads a block of bytes from the stream and writes the data in a given buffer
        /// Lock stream while reading
        /// </summary>
        /// <param name="array">Contains the specified bytes array with the values between offset and (offset + count - 1) replaced by the bytes readed from the current source</param>
        /// <param name="offset">The byte offset in array at which the read bytes will be placed</param>
        /// <param name="count">The maximum number of bytes to read</param>
        /// <param name="ReadLocker">Lock object for only one in time read</param>
        /// <param name="CurEventRead">Handler what say to next thread - wait wille i write my info</param>
        /// <param name="PrevEvent">Handler what store info about previous handler who read stream</param>
        /// <param name="WaitEventRead">Output for previous handler, current stream will wait it</param>
        /// <returns>Returns count of readed bytes</returns>
        public static int LockRead(this FileStream fs, byte[] array, int offset, int count, Object ReadLocker
            , ref ManualResetEvent CurEventRead, ref ManualResetEvent PrevEvent, ref ManualResetEvent WaitEventRead)
        {
            lock (ReadLocker)
            {
                CurEventRead = new ManualResetEvent(false);
                WaitEventRead = PrevEvent;
                PrevEvent = CurEventRead;
                return fs.Read(array, offset, count);
            }
        }

    }
}
