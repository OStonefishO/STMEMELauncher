using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Xml.Linq;

namespace Aurora.Core.Network;

public sealed class UpnpResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string ExternalIp { get; init; } = string.Empty;
    public string LocalIp { get; init; } = string.Empty;
    public int Port { get; init; }
}

/// <summary>
/// 用 UPnP（SSDP 发现 + WANIPConnection SOAP）在路由器上开一个端口映射，
/// 这样有公网 IPv4 的家宽可以不走穿透直接连。
/// </summary>
public sealed class UpnpClient
{
    private const string MulticastAddress = "239.255.255.250";
    private const int MulticastPort = 1900;

    private static readonly string[] SearchTargets =
    {
        "urn:schemas-upnp-org:device:InternetGatewayDevice:1",
        "urn:schemas-upnp-org:device:InternetGatewayDevice:2",
        "urn:schemas-upnp-org:service:WANIPConnection:1",
        "urn:schemas-upnp-org:service:WANPPPConnection:1",
    };

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

    private string _controlUrl = string.Empty;
    private string _serviceType = string.Empty;

    /// <summary>发现网关设备并定位 WANIPConnection 的控制地址。</summary>
    public async Task<bool> DiscoverAsync(CancellationToken ct = default)
    {
        var location = await FindDeviceLocationAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrEmpty(location))
        {
            return false;
        }

        try
        {
            var xml = await _http.GetStringAsync(location, ct).ConfigureAwait(false);
            var doc = XDocument.Parse(xml);
            XNamespace ns = doc.Root?.Name.Namespace ?? XNamespace.None;

            var baseUri = new Uri(location);
            var urlBase = doc.Descendants(ns + "URLBase").FirstOrDefault()?.Value;
            if (!string.IsNullOrWhiteSpace(urlBase) && Uri.TryCreate(urlBase, UriKind.Absolute, out var b))
            {
                baseUri = b;
            }

            foreach (var service in doc.Descendants(ns + "service"))
            {
                var type = service.Element(ns + "serviceType")?.Value ?? string.Empty;
                if (!type.Contains("WANIPConnection", StringComparison.OrdinalIgnoreCase) &&
                    !type.Contains("WANPPPConnection", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var control = service.Element(ns + "controlURL")?.Value;
                if (string.IsNullOrWhiteSpace(control))
                {
                    continue;
                }

                _serviceType = type;
                _controlUrl = new Uri(baseUri, control).ToString();
                return true;
            }
        }
        catch
        {
            // 解析失败当作不支持
        }

        return false;
    }

    private static async Task<string?> FindDeviceLocationAsync(CancellationToken ct)
    {
        for (var round = 0; round < 3; round++)
        foreach (var target in SearchTargets)
        {
            try
            {
                using var udp = new UdpClient(AddressFamily.InterNetwork);
                udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udp.Client.Bind(new IPEndPoint(IPAddress.Any, 0));

                var request = string.Join("\r\n", new[]
                {
                    "M-SEARCH * HTTP/1.1",
                    $"HOST: {MulticastAddress}:{MulticastPort}",
                    "MAN: \"ssdp:discover\"",
                    "MX: 2",
                    "ST: " + target,
                    "",
                    "",
                });

                var payload = Encoding.ASCII.GetBytes(request);
                var endpoint = new IPEndPoint(IPAddress.Parse(MulticastAddress), MulticastPort);
                await udp.SendAsync(payload, payload.Length, endpoint).ConfigureAwait(false);

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(3));

                for (var i = 0; i < 5; i++)
                {
                    UdpReceiveResult result;
                    try
                    {
                        result = await udp.ReceiveAsync(timeout.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    var text = Encoding.ASCII.GetString(result.Buffer);
                    foreach (var line in text.Split('\n'))
                    {
                        if (line.StartsWith("LOCATION:", StringComparison.OrdinalIgnoreCase))
                        {
                            return line[("LOCATION:".Length)..].Trim();
                        }
                    }
                }
            }
            catch
            {
                // 换下一个 ST
            }
        }

        return null;
    }

    public async Task<string> GetExternalIpAsync(CancellationToken ct = default)
    {
        var body = SoapEnvelope(_serviceType, "GetExternalIPAddress", string.Empty);
        var response = await CallAsync("GetExternalIPAddress", body, ct).ConfigureAwait(false);
        return ExtractTag(response, "NewExternalIPAddress");
    }

    public async Task<UpnpResult> MapPortAsync(int port, string description, string? localIp = null,
        CancellationToken ct = default)
    {
        localIp ??= NetworkProbe.GetAddresses()
            .Where(a => a.Family == "IPv4" && !a.Address.StartsWith("169.254."))
            .OrderByDescending(a => a.Address.StartsWith("192.168.") || a.Address.StartsWith("10."))
            .FirstOrDefault()
            ?.Address;

        if (string.IsNullOrEmpty(localIp))
        {
            return new UpnpResult { Success = false, Message = "找不到本机内网 IPv4 地址", Port = port };
        }

        if (string.IsNullOrEmpty(_controlUrl) && !await DiscoverAsync(ct).ConfigureAwait(false))
        {
            return new UpnpResult { Success = false, Message = "路由器没有响应 UPnP 发现（可能关了这个功能）", Port = port };
        }

        var externalIp = string.Empty;
        try
        {
            externalIp = await GetExternalIpAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            // 拿不到也不影响映射
        }

        var args =
            "<NewRemoteHost></NewRemoteHost>" +
            $"<NewExternalPort>{port}</NewExternalPort>" +
            "<NewProtocol>TCP</NewProtocol>" +
            $"<NewInternalPort>{port}</NewInternalPort>" +
            $"<NewInternalClient>{localIp}</NewInternalClient>" +
            "<NewEnabled>1</NewEnabled>" +
            $"<NewPortMappingDescription>{SecurityElement(description)}</NewPortMappingDescription>" +
            "<NewLeaseDuration>0</NewLeaseDuration>";

        try
        {
            await CallAsync("AddPortMapping", SoapEnvelope(_serviceType, "AddPortMapping", args), ct).ConfigureAwait(false);
            return new UpnpResult
            {
                Success = true,
                Message = $"已映射 {localIp}:{port} → 外网 {port}",
                ExternalIp = externalIp,
                LocalIp = localIp,
                Port = port,
            };
        }
        catch (Exception ex)
        {
            return new UpnpResult
            {
                Success = false,
                Message = "AddPortMapping 失败: " + ex.Message,
                ExternalIp = externalIp,
                LocalIp = localIp,
                Port = port,
            };
        }
    }

    public async Task<bool> UnmapPortAsync(int port, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_controlUrl))
        {
            return false;
        }

        var args =
            "<NewRemoteHost></NewRemoteHost>" +
            $"<NewExternalPort>{port}</NewExternalPort>" +
            "<NewProtocol>TCP</NewProtocol>";

        try
        {
            await CallAsync("DeletePortMapping", SoapEnvelope(_serviceType, "DeletePortMapping", args), ct)
                .ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<string> CallAsync(string action, string body, CancellationToken ct)
    {
        using var content = new StringContent(body, Encoding.UTF8, "text/xml");
        content.Headers.Add("SOAPAction", $"\"{_serviceType}#{action}\"");

        using var response = await _http.PostAsync(_controlUrl, content, ct).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"HTTP {(int)response.StatusCode}");
        }

        return text;
    }

    private static string SoapEnvelope(string serviceType, string action, string args) =>
        "<?xml version=\"1.0\"?>" +
        "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" " +
        "s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">" +
        "<s:Body>" +
        $"<u:{action} xmlns:u=\"{serviceType}\">" +
        args +
        $"</u:{action}>" +
        "</s:Body></s:Envelope>";

    private static string ExtractTag(string xml, string tag)
    {
        var start = xml.IndexOf("<" + tag + ">", StringComparison.Ordinal);
        if (start < 0)
        {
            return string.Empty;
        }

        start += tag.Length + 2;
        var end = xml.IndexOf("</" + tag + ">", start, StringComparison.Ordinal);
        return end < 0 ? string.Empty : xml[start..end];
    }

    private static string SecurityElement(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
