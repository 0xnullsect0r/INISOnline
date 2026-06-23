using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Godot;
using Inis.Core.Net;

namespace INISOnline.Lan;

/// <summary>A LAN game advertised over UDP broadcast: enough to connect (host name + ws port).</summary>
public sealed record LanBeacon(string Name, int Port, int Capacity, int Filled)
{
    /// <summary>Resolves to a connectable ws URL given the sender's address.</summary>
    public string Url(string address) => $"ws://{address}:{Port}";
}

/// <summary>
/// LAN discovery over UDP broadcast on a fixed port: the host announces a beacon every second; the
/// browser listens and lists the games it sees. Keeps LAN zero-config — no central server.
/// </summary>
public static class LanDiscovery
{
    public const int BroadcastPort = 47654;
    private const string Magic = "INIS-LAN/1";

    /// <summary>Broadcasts a host beacon; call ~once per second while hosting.</summary>
    public sealed class Announcer
    {
        private readonly PacketPeerUdp _udp = new();

        public Announcer()
        {
            _udp.SetBroadcastEnabled(true);
            _udp.SetDestAddress("255.255.255.255", BroadcastPort);
        }

        public void Announce(LanBeacon beacon)
        {
            var payload = JsonSerializer.Serialize(new { magic = Magic, beacon }, InisJson.Options);
            _udp.PutPacket(Encoding.UTF8.GetBytes(payload));
        }

        public void Close() => _udp.Close();
    }

    /// <summary>Listens for host beacons; poll <see cref="Poll"/> each frame and read <see cref="Seen"/>.</summary>
    public sealed class Browser
    {
        private readonly PacketPeerUdp _udp = new();
        private readonly Dictionary<string, (LanBeacon Beacon, string Address, ulong At)> _seen = new();

        public Browser() => _udp.Bind(BroadcastPort, "*");

        public void Poll()
        {
            while (_udp.GetAvailablePacketCount() > 0)
            {
                var json = Encoding.UTF8.GetString(_udp.GetPacket());
                var address = _udp.GetPacketIP();
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.GetProperty("magic").GetString() != Magic) continue;
                    var beacon = doc.RootElement.GetProperty("beacon").Deserialize<LanBeacon>(InisJson.Options);
                    if (beacon is not null)
                        _seen[$"{address}:{beacon.Port}"] = (beacon, address, Time.GetTicksMsec());
                }
                catch (JsonException) { /* ignore malformed packets */ }
            }
        }

        /// <summary>Hosts seen in the last few seconds, with their connectable URL.</summary>
        public IReadOnlyList<(LanBeacon Beacon, string Url)> Seen
        {
            get
            {
                var now = Time.GetTicksMsec();
                var list = new List<(LanBeacon, string)>();
                foreach (var (beacon, address, at) in _seen.Values)
                    if (now - at < 4000) list.Add((beacon, beacon.Url(address)));
                return list;
            }
        }

        public void Close() => _udp.Close();
    }
}
