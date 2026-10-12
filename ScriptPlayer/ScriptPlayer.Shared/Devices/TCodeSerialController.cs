using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;
using System.Linq;
using System.Text;
using ScriptPlayer.Shared.Scripts;

namespace ScriptPlayer.Shared.Devices
{
    /// <summary>
    /// Streams a synchronized set of funscript axes to an OSR2/SR6 compatible
    /// TCode v0.3 device over serial or Wi-Fi / UDP.
    /// </summary>
    public sealed class TCodeSerialController : IDisposable
    {
        public const int DefaultBaudRate = 115200;
        public const int UpdateIntervalMilliseconds = 25;

        private static readonly string[] AxisOrder =
            { "L0", "L1", "L2", "R0", "R1", "R2", "V0", "A0", "A1", "A2" };

        private readonly object _syncRoot = new object();
        private readonly Stopwatch _lastWrite = Stopwatch.StartNew();
        private Dictionary<string, List<FunScriptAction>> _scripts =
            new Dictionary<string, List<FunScriptAction>>(StringComparer.OrdinalIgnoreCase);
        private TCodeTransport _serialPort;
        private string _lastPayload;

        public bool IsConnected
        {
            get
            {
                lock (_syncRoot)
                    return _serialPort != null && _serialPort.IsOpen;
            }
        }

        public string PortName
        {
            get
            {
                lock (_syncRoot)
                    return _serialPort?.PortName;
            }
        }

        public string[] LoadedAxes
        {
            get
            {
                lock (_syncRoot)
                    return AxisOrder.Where(axis => _scripts.ContainsKey(axis)).ToArray();
            }
        }

        public void Connect(string portName, int baudRate = DefaultBaudRate)
        {
            if (string.IsNullOrWhiteSpace(portName))
                throw new ArgumentException("必须指定串口或 UDP 地址。", nameof(portName));

            lock (_syncRoot)
            {
                DisconnectInternal();

                TCodeTransport port = new TCodeTransport(portName, baudRate, Parity.None, 8, StopBits.One)
                {
                    Encoding = Encoding.ASCII,
                    NewLine = "\n",
                    WriteTimeout = 250,
                    ReadTimeout = 250,
                    DtrEnable = false,
                    RtsEnable = false
                };

                try
                {
                    port.Open();
                    port.DiscardOutBuffer();
                    _lastPayload = null;
                    _lastWrite.Restart();

                    // TCode v0.3 devices understand D1 as a version query. It is
                    // harmless on devices that do not return a response.
                    port.WriteLine("D1");
                    _serialPort = port;
                }
                catch
                {
                    port.Dispose();
                    throw;
                }
            }
        }

        public void Disconnect()
        {
            lock (_syncRoot)
                DisconnectInternal();
        }

        public void SetScripts(IDictionary<string, IEnumerable<FunScriptAction>> scripts)
        {
            Dictionary<string, List<FunScriptAction>> normalized =
                new Dictionary<string, List<FunScriptAction>>(StringComparer.OrdinalIgnoreCase);

            if (scripts != null)
            {
                foreach (KeyValuePair<string, IEnumerable<FunScriptAction>> pair in scripts)
                {
                    if (!AxisOrder.Contains(pair.Key, StringComparer.OrdinalIgnoreCase) || pair.Value == null)
                        continue;

                    List<FunScriptAction> actions = pair.Value
                        .Where(action => action != null && action.TimeStamp >= TimeSpan.Zero)
                        .OrderBy(action => action.TimeStamp)
                        .Select(action => action.Duplicate())
                        .ToList();

                    if (actions.Count > 0)
                        normalized[pair.Key] = actions;
                }
            }

            lock (_syncRoot)
            {
                string[] removedAxes = _scripts.Keys
                    .Where(axis => !normalized.ContainsKey(axis))
                    .ToArray();
                _scripts = normalized;
                _lastPayload = null;

                if (_serialPort != null && _serialPort.IsOpen && removedAxes.Length > 0)
                {
                    try
                    {
                        string centerPayload = string.Join(" ", removedAxes.Select(axis => axis + "5000I1000"));
                        _serialPort.WriteLine(centerPayload);
                        _lastWrite.Restart();
                    }
                    catch
                    {
                        DisconnectInternal();
                    }
                }
            }
        }

        public void ClearScripts()
        {
            SetScripts(null);
        }

        public void Update(TimeSpan mediaProgress, TimeSpan scriptDelay, bool force = false)
        {
            lock (_syncRoot)
            {
                if (_serialPort == null || !_serialPort.IsOpen || _scripts.Count == 0)
                    return;

                if (!force && _lastWrite.ElapsedMilliseconds < UpdateIntervalMilliseconds)
                    return;

                TimeSpan scriptProgress = mediaProgress - scriptDelay;
                if (scriptProgress < TimeSpan.Zero)
                    scriptProgress = TimeSpan.Zero;

                List<string> commands = new List<string>();
                int movementInterval = force ? 1000 : UpdateIntervalMilliseconds * 2;
                foreach (string axis in AxisOrder)
                {
                    if (!_scripts.TryGetValue(axis, out List<FunScriptAction> actions))
                        continue;

                    double position = GetInterpolatedPosition(actions, scriptProgress);
                    int tcodePosition = (int)Math.Round(position / 99.0 * 9999.0);
                    tcodePosition = Math.Max(0, Math.Min(9999, tcodePosition));
                    commands.Add(axis + tcodePosition.ToString("D4", CultureInfo.InvariantCulture) +
                                 "I" + movementInterval.ToString(CultureInfo.InvariantCulture));
                }

                string payload = string.Join(" ", commands);
                if (payload.Length == 0)
                    return;

                // While an axis is moving, payloads naturally differ. For a long
                // static section, a 250 ms keep-alive avoids unnecessary traffic.
                if (!force && payload == _lastPayload && _lastWrite.ElapsedMilliseconds < 250)
                    return;

                try
                {
                    _serialPort.WriteLine(payload);
                    _lastPayload = payload;
                    _lastWrite.Restart();
                }
                catch
                {
                    DisconnectInternal();
                    throw;
                }
            }
        }

        internal static double GetInterpolatedPosition(IList<FunScriptAction> actions, TimeSpan progress)
        {
            if (actions == null || actions.Count == 0)
                return 49.5;
            if (progress <= actions[0].TimeStamp)
                return Math.Min(99, (int)actions[0].Position);
            if (progress >= actions[actions.Count - 1].TimeStamp)
                return Math.Min(99, (int)actions[actions.Count - 1].Position);

            int low = 0;
            int high = actions.Count - 1;
            while (low + 1 < high)
            {
                int middle = low + (high - low) / 2;
                if (actions[middle].TimeStamp <= progress)
                    low = middle;
                else
                    high = middle;
            }

            FunScriptAction from = actions[low];
            FunScriptAction to = actions[high];
            double duration = (to.TimeStamp - from.TimeStamp).TotalMilliseconds;
            if (duration <= 0)
                return Math.Min(99, (int)to.Position);

            double amount = (progress - from.TimeStamp).TotalMilliseconds / duration;
            return from.Position + (to.Position - from.Position) * amount;
        }

        private void DisconnectInternal()
        {
            TCodeTransport port = _serialPort;
            _serialPort = null;
            _lastPayload = null;

            if (port == null)
                return;

            try
            {
                if (port.IsOpen)
                    port.Close();
            }
            finally
            {
                port.Dispose();
            }
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
