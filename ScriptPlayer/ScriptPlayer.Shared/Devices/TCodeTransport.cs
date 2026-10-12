using System;
using System.ComponentModel;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ScriptPlayer.Shared.Devices
{
    // Matches the serial API used by the existing TCode controller, preserving
    // its axis interpolation, rate limiting, locking and transition behavior.
    public sealed class TCodeTransport : Component
    {
        private readonly SerialPort serial;
        private readonly Uri endpoint;
        private UdpClient udp;
        public Encoding Encoding { get; set; } = Encoding.ASCII;
        public string NewLine { get; set; } = "\n";
        public int WriteTimeout { get; set; } = 250;
        public int ReadTimeout { get; set; } = 250;
        public bool DtrEnable { get; set; }
        public bool RtsEnable { get; set; }
        public bool IsOpen { get { return endpoint != null ? udp != null : serial.IsOpen; } }
        public string PortName { get { return endpoint != null ? "Wi-Fi UDP 就绪 · " + endpoint.Authority : serial.PortName; } }

        public static Uri ParseEndpoint(string input)
        {
            input = (input ?? "").Trim();
            if (input.Length == 0) throw new ArgumentException("请输入设备主机名或 IP 地址。");
            if (!input.Contains("://")) input = "udp://" + input;
            Uri uri;
            if (!Uri.TryCreate(input, UriKind.Absolute, out uri) || uri.Scheme != "udp" ||
                string.IsNullOrWhiteSpace(uri.Host) || uri.UserInfo.Length != 0 ||
                uri.Query.Length != 0 || uri.Fragment.Length != 0 ||
                (uri.AbsolutePath != "" && uri.AbsolutePath != "/"))
                throw new ArgumentException("地址格式：tcode.local:8000 或 udp://192.168.1.100:8000。");
            int port = 8000;
            string authority = input.Substring(input.IndexOf("://", StringComparison.Ordinal) + 3).TrimEnd('/');
            int colon = authority.LastIndexOf(':');
            if (colon >= 0 && colon > authority.LastIndexOf(']'))
            {
                if (!int.TryParse(authority.Substring(colon + 1), out port))
                    throw new ArgumentException("端口必须在 1 到 65535 之间。");
            }
            if (port < 1 || port > 65535) throw new ArgumentException("端口必须在 1 到 65535 之间。");
            return new UriBuilder("udp", uri.Host, port).Uri;
        }

        public TCodeTransport(string portName, int baudRate, Parity parity, int dataBits, StopBits stopBits)
        {
            if (portName.StartsWith("udp://", StringComparison.OrdinalIgnoreCase)) endpoint = ParseEndpoint(portName);
            else serial = new SerialPort(portName, baudRate, parity, dataBits, stopBits);
        }

        public void Open()
        {
            if (IsOpen) throw new InvalidOperationException("连接已打开。");
            if (endpoint == null)
            {
                serial.Encoding = Encoding; serial.NewLine = NewLine;
                serial.WriteTimeout = WriteTimeout; serial.ReadTimeout = ReadTimeout;
                serial.DtrEnable = DtrEnable; serial.RtsEnable = RtsEnable;
                serial.Open(); return;
            }
            IPAddress address;
            if (!IPAddress.TryParse(endpoint.DnsSafeHost, out address))
            {
                var resolve = Dns.GetHostAddressesAsync(endpoint.DnsSafeHost);
                if (!resolve.Wait(5000)) throw new IOException("解析设备地址超时。请改填设备的局域网 IP 地址。");
                address = resolve.Result.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                    ?? resolve.Result.FirstOrDefault();
            }
            if (address == null) throw new IOException("无法解析设备地址。请改填设备的局域网 IP 地址。");
            var candidate = new UdpClient(address.AddressFamily);
            try
            {
                candidate.Client.SendTimeout = WriteTimeout;
                candidate.Connect(new IPEndPoint(address, endpoint.Port));
                udp = candidate;
            }
            catch { candidate.Dispose(); throw; }
        }

        public void WriteLine(string text)
        {
            if (endpoint == null) { serial.WriteLine(text); return; }
            if (udp == null) throw new InvalidOperationException("UDP 连接未打开。");
            var bytes = Encoding.GetBytes(text + NewLine);
            try { udp.Send(bytes, bytes.Length); }
            catch { Close(); throw; }
        }
        public void DiscardOutBuffer() { if (endpoint == null) serial.DiscardOutBuffer(); }
        public void Close()
        {
            if (endpoint == null) { if (serial.IsOpen) serial.Close(); }
            else { var old = udp; udp = null; if (old != null) old.Dispose(); }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { Close(); if (serial != null) serial.Dispose(); }
            base.Dispose(disposing);
        }
    }

}
