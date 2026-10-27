using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace PollClient
{
    public class MessageReader
    {
        private const string Marker = "<|END|>";
        private NetworkStream _stream;
        private Decoder _decoder = Encoding.UTF8.GetDecoder();
        private StringBuilder _buffer = new();
        private byte[] _bytes = new byte[4096];
        private char[] _chars = new char[4096];

        public MessageReader(NetworkStream stream)
        {
            _stream = stream;
        }

        public async Task<string?> ReadMessageAsync()
        {
            while (true)
            {
                string text = _buffer.ToString();
                int index = text.IndexOf(Marker);
                if (index >= 0)
                {
                    string message = text.Substring(0, index);
                    _buffer.Remove(0, index + Marker.Length);
                    return message;
                }

                int count = await _stream.ReadAsync(_bytes, 0, _bytes.Length);
                if (count == 0) return null;

                int chars = _decoder.GetChars(_bytes, 0, count, _chars, 0, false);
                _buffer.Append(_chars, 0, chars);
            }
        }
    }
}
