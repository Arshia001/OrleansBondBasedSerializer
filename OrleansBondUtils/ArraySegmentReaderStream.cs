using System;
using System.Collections.Generic;
using System.IO;

namespace OrleansBondUtils
{
    // Taken mostly from the framework's reference source for MemoryStream
    public sealed class ArraySegmentReaderStream : Stream
    {
        private ArraySegment<byte> _buffer;    // Either allocated internally or externally.
        private int _position;     // read/write head.
        private int _length;       // Number of bytes within the memory stream

        public ArraySegmentReaderStream(ArraySegment<byte> buffer)
        {
            _buffer = buffer;
            _length = buffer.Count;
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override void Flush() { }

        public ArraySegment<byte> GetBuffer()
        {
            return _buffer;
        }

        public override long Length => _length;

        public override long Position
        {
            get => _position;
            set => _position = (int)value;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int n = _length - _position;
            if (n > count) n = count;
            if (n <= 0)
                return 0;

            if (n <= 8)
            {
                int byteCount = n;
                while (--byteCount >= 0)
                    buffer[offset + byteCount] = ((IList<byte>)_buffer)[_position + byteCount];
            }
            else
                Buffer.BlockCopy(_buffer.Array, _position + _buffer.Offset, buffer, offset, n);
            _position += n;

            return n;
        }

        public override int ReadByte()
        {
            return ((IList<byte>)_buffer)[_position++];
        }


        public override long Seek(long offset, SeekOrigin loc)
        {
            switch (loc)
            {
                case SeekOrigin.Begin:
                    {
                        _position = unchecked((int)offset);
                        break;
                    }
                case SeekOrigin.Current:
                    {
                        _position = unchecked(_position + (int)offset);
                        break;
                    }
                case SeekOrigin.End:
                    {
                        _position = unchecked(_length + (int)offset);
                        break;
                    }
                default:
                    throw new ArgumentException();
            }

            return _position;
        }

        // Sets the length of the stream to a given value.  The new
        // value must be nonnegative and less than the space remaining in
        // the array, Int32.MaxValue - origin
        // Origin is 0 in all cases other than a MemoryStream created on
        // top of an existing array and a specific starting offset was passed 
        // into the MemoryStream constructor.  The upper bounds prevents any 
        // situations where a stream may be created on top of an array then 
        // the stream is made longer than the maximum possible length of the 
        // array (Int32.MaxValue).
        // 
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public byte[] ToArray()
        {
            byte[] copy = new byte[_length];
            Buffer.BlockCopy(_buffer.Array, _buffer.Offset, copy, 0, _length);
            return copy;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override void WriteByte(byte value)
        {
            throw new NotSupportedException();
        }

        // Writes this MemoryStream to another stream.
        public void WriteTo(Stream stream)
        {
            stream.Write(_buffer.Array, _buffer.Offset, _length);
        }
    }
}
