using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using ScriptPlayer.Shared.Devices;
using ScriptPlayer.Shared.Scripts;

class Tests
{
    static int count;
    static void Check(bool condition, string label) { if (!condition) throw new Exception(label); Console.WriteLine("PASS " + label); count++; }
    static string Receive(UdpClient server) { var remote = new IPEndPoint(IPAddress.Any, 0); return Encoding.ASCII.GetString(server.Receive(ref remote)); }
    static IEnumerable<FunScriptAction> Script() { return new[] { new FunScriptAction { TimeStamp=TimeSpan.Zero, Position=0 }, new FunScriptAction { TimeStamp=TimeSpan.FromSeconds(1), Position=99 } }; }
    static int Main()
    {
        try { Run(); return 0; }
        catch (Exception ex) { Console.WriteLine("FAIL " + ex.GetType().FullName + ": " + ex.Message); return 1; }
    }
    static void Run()
    {
        Check(TCodeTransport.ParseEndpoint("tcode.local").Port == 8000, "default port");
        Check(TCodeTransport.ParseEndpoint(" udp://192.168.1.2:9000 ").Port == 9000, "explicit UDP URL");
        Check(TCodeTransport.ParseEndpoint("[::1]:8000").Port == 8000, "IPv6 URL");
        foreach (var input in new[]{"", "http://tcode.local", "host:0", "host:65536", "user@host:8000", "host:8000/path", "host:8000?x=1"})
        {
            bool failed = false; try { TCodeTransport.ParseEndpoint(input); } catch (ArgumentException) { failed=true; }
            Check(failed, "reject invalid endpoint: " + input);
        }
        using (var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
        using (var controller = new TCodeSerialController())
        {
            server.Client.ReceiveTimeout = 1500;
            string endpoint = "udp://127.0.0.1:" + ((IPEndPoint)server.Client.LocalEndPoint).Port;
            controller.Connect(endpoint, 115200);
            Check(controller.IsConnected && controller.PortName.Contains("UDP"), "source controller opens UDP");
            Check(Receive(server) == "D1\n", "identification query framing");
            var scripts = new Dictionary<string,IEnumerable<FunScriptAction>>();
            foreach (var axis in new[]{"L0","L1","L2","R0","R1","R2"}) scripts[axis]=Script();
            controller.SetScripts(scripts);
            controller.Update(TimeSpan.FromMilliseconds(500),TimeSpan.Zero,true);
            string payload=Receive(server);
            Check(payload == "L05000I1000 L15000I1000 L25000I1000 R05000I1000 R15000I1000 R25000I1000\n", "six axes, interpolation, newline and gentle transition");
            controller.Update(TimeSpan.FromMilliseconds(600),TimeSpan.Zero,false);
            Check(server.Available == 0, "25ms rate limit");
            Thread.Sleep(35);
            controller.Update(TimeSpan.FromMilliseconds(750),TimeSpan.FromMilliseconds(250),false);
            Check(Receive(server) == "L05000I50 L15000I50 L25000I50 R05000I50 R15000I50 R25000I50\n", "script delay and normal frame");
            scripts.Remove("R2"); controller.SetScripts(scripts);
            Check(Receive(server).StartsWith("R25000I1000"), "removed axis returns to neutral using existing behavior");
            controller.Disconnect();
            Check(!controller.IsConnected && controller.PortName == null, "disconnect resets state");
            controller.Update(TimeSpan.Zero,TimeSpan.Zero,true);
            Check(server.Available == 0, "disconnected controller sends nothing");
            controller.Connect(endpoint, 115200); Receive(server);
            Check(controller.IsConnected, "reconnect");
            controller.Disconnect();
            bool failed=false; try { controller.Connect("COM99999", 115200); } catch { failed=true; }
            Check(failed && !controller.IsConnected, "serial path retained and failed connection stays closed");
        }
        Console.WriteLine("TOTAL " + count + " checks passed");
    }
}
