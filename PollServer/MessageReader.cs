using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace PollServer
{
    public class MessageReader
    {
        private const string EndMarker = "<|END|>";
        private Stream _stream;
        private StringBuilder _buffer = new StringBuilder();
        private byte[] _bytes = new byte[4096];

        public MessageReader(Stream stream)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        }

        public async Task<string?> ReadMessageAsync()
        {
            while (true)
            {
                string current = _buffer.ToString();
                int markerIndex = current.IndexOf(EndMarker, StringComparison.Ordinal);

                if (markerIndex >= 0)
                {
                    string message = current.Substring(0, markerIndex);
                    _buffer.Remove(0, markerIndex + EndMarker.Length);
                    return message;
                }

                int count = await _stream.ReadAsync(_bytes, 0, _bytes.Length);

                if (count == 0)
                {
                    return null;
                }

                _buffer.Append(Encoding.UTF8.GetString(_bytes, 0, count));
            }
        }
    }
}
