using System;
using System.IO;
using System.Threading;

namespace DaxStudio.Core.DeltaAnalyzer
{
    /// <summary>
    /// A read-only, seekable <see cref="Stream"/> over a OneLake file URL. Reads are serviced via HTTP
    /// Range requests through <see cref="OneLakeHttpClient"/>. This allows Parquet.Net to read only the
    /// footer / metadata of a (potentially very large) parquet file instead of downloading the whole file.
    /// A small last-block cache avoids repeated round-trips for sequential reads within the same block.
    /// </summary>
    public class HttpRangeStream : Stream
    {
        private readonly OneLakeHttpClient _client;
        private readonly string _url;
        private readonly CancellationToken _ct;
        private long _length = -1;
        private long _position;

        // Simple single-block cache.
        private const int BlockSize = 1 << 20; // 1 MB
        private byte[] _cacheBuffer;
        private long _cacheStart = -1;
        private int _cacheLength;

        public HttpRangeStream(OneLakeHttpClient client, string url, long length, CancellationToken ct)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _url = url ?? throw new ArgumentNullException(nameof(url));
            _length = length;
            _ct = ct;
        }

        /// <summary>Creates an <see cref="HttpRangeStream"/>, fetching the content length via a HEAD request.</summary>
        public static HttpRangeStream Create(OneLakeHttpClient client, string url, CancellationToken ct)
        {
            var length = client.GetContentLengthAsync(url, ct).GetAwaiter().GetResult();
            return new HttpRangeStream(client, url, length, ct);
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;

        public override long Length
        {
            get
            {
                if (_length < 0)
                {
                    _length = _client.GetContentLengthAsync(_url, _ct).GetAwaiter().GetResult();
                }
                return _length;
            }
        }

        public override long Position
        {
            get => _position;
            set => _position = value;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    _position = offset;
                    break;
                case SeekOrigin.Current:
                    _position += offset;
                    break;
                case SeekOrigin.End:
                    _position = Length + offset;
                    break;
            }
            if (_position < 0) _position = 0;
            return _position;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (count <= 0) return 0;
            var len = Length;
            if (_position >= len) return 0;

            // Clamp to end of stream.
            var toRead = (int)Math.Min(count, len - _position);
            int totalRead = 0;

            while (totalRead < toRead)
            {
                EnsureCacheContains(_position);
                if (_cacheBuffer == null || _cacheLength == 0) break;

                var cacheOffset = (int)(_position - _cacheStart);
                if (cacheOffset < 0 || cacheOffset >= _cacheLength)
                {
                    // Cache didn't cover the requested position; bail to avoid an infinite loop.
                    break;
                }

                var available = _cacheLength - cacheOffset;
                var copy = Math.Min(available, toRead - totalRead);
                Buffer.BlockCopy(_cacheBuffer, cacheOffset, buffer, offset + totalRead, copy);
                totalRead += copy;
                _position += copy;
            }

            return totalRead;
        }

        private void EnsureCacheContains(long position)
        {
            if (_cacheBuffer != null && position >= _cacheStart && position < _cacheStart + _cacheLength)
            {
                return; // already cached
            }

            var len = Length;
            // Align the block to BlockSize boundaries.
            var blockStart = (position / BlockSize) * BlockSize;
            var blockCount = (int)Math.Min(BlockSize, len - blockStart);
            if (blockCount <= 0)
            {
                _cacheBuffer = null;
                _cacheLength = 0;
                _cacheStart = -1;
                return;
            }

            var data = _client.ReadRangeAsync(_url, blockStart, blockCount, _ct).GetAwaiter().GetResult();
            _cacheBuffer = data;
            _cacheLength = data?.Length ?? 0;
            _cacheStart = blockStart;
        }

        public override void Flush() { }

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
