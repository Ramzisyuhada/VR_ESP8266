using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using NativeWebSocket;
using TMPro;
using System.Net.Sockets;
using System.Text;

public class WsRouterClientNative : MonoBehaviour
{
    // ================== UDP Discovery ==================
    [Header("UDP")]
    private UdpClient udpListener;
    private bool udpRunning = false;
    private bool serverDiscovered = false;        // ✅ supaya tidak connect berkali-kali dari UDP
    public int udpListenPort = 4210;              // harus sama dgn server Python
    public bool autoConnectWhenDiscovered = true;
    public Button btnUdpScan;                     // tombol di UI buat start/stop UDP

    [Header("Server WebSocket (default)")]
    public string serverHost = "192.168.1.23";
    public int serverPort = 8765;
    public bool autoConnectOnStart = true;

    [Header("UI - Inputs")]
    public TMP_InputField ipInput;
    public TMP_InputField portInput;

    [Header("UI - Buttons")]
    public Button btnConnect;
    public Button btnRefresh;
    public Button btnSendToID;
    public Button btnSendToIP;
    public Button btnPairWithSelected;

    [Header("UI - Client List & Message")]
    public TMP_Dropdown clientsDropdown;
    public TMP_InputField messageInput;

    [Header("UI - Status Text (debug/status)")]
    public TMP_Text statusText;

    [Header("Panels")]
    public GameObject ParentIpDanPort;   // form awal
    public GameObject ParentStatusText;  // "menghubungkan..." / scan
    public GameObject ClientStatusText;  // sudah connect

    [Header("Opsi")]
    public bool hideSelfInLists = true;

    [Header("Reconnect")]
    public bool autoReconnect = true;
    public int maxReconnectAttempts = 5;
    public float reconnectIntervalSec = 3f;

    [Header("Heartbeat")]
    public bool enableHeartbeat = true;
    public float heartbeatSec = 5f;

    // ================== Internal ==================
    private WebSocket ws;
    private string myIpFromServer = "-";

    private readonly Dictionary<string, ClientDetail> clientsByIp = new();
    private readonly List<string> dropdownIndexToIp = new();

    private Coroutine reconnectCo;
    private Coroutine heartbeatCo;

    const string PREF_IP_KEY = "WsRouter_LastIP";
    const string PREF_PORT_KEY = "WsRouter_LastPort";

    [Serializable]
    public class ClientDetail
    {
        public string ip = "-";
        public string id = "-";
    }

    private string _pairedIp = null;
    private string _pairedId = null;
    private bool _connectSent = false;
    private string _lastPickIp = null;
    private string _lastPickId = null;

    private bool isConnecting = false;    // ✅ cegah double connect

    public bool IsConnected => ws != null && ws.State == WebSocketState.Open;
    public bool HasActiveTarget => !string.IsNullOrEmpty(_pairedId) || !string.IsNullOrEmpty(_pairedIp);

    // debouncer status
    private float _lastStatusTime = 0f;
    private const float STATUS_MIN_INTERVAL = 0.12f;

    // ================== Unity Lifecycle ==================
    async void Start()
    {
        Application.runInBackground = true;

        LoadServerPrefs();

        if (ipInput) ipInput.text = serverHost;
        if (portInput) portInput.text = serverPort.ToString();

        WireButtons();

        // state awal
        ShowPanel_Input();
        SetStatus("Masukkan IP & Port server untuk mulai koneksi.");

        // ⛔ jangan tunggu sync → pakai coroutine biar ga lag
        if (autoConnectOnStart)
            ConnectSafe();
    }

    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        ws?.DispatchMessageQueue();
#endif
    }

    void OnDestroy()
    {
        _ = SafeClose();
        StopUdpDiscovery();
    }

    void OnApplicationQuit()
    {
        ReadInputsIntoDefaults();
        SaveServerPrefs();
        StopUdpDiscovery();
    }

    // ================== Panel Helpers ==================
    void ShowPanel_Input()
    {
        if (ParentIpDanPort) ParentIpDanPort.SetActive(true);
        if (ParentStatusText) ParentStatusText.SetActive(false);
        if (ClientStatusText) ClientStatusText.SetActive(false);
    }

    void ShowPanel_Connecting(string status = "🔌 Menghubungkan ke server...")
    {
        if (ParentIpDanPort) ParentIpDanPort.SetActive(false);
        if (ParentStatusText) ParentStatusText.SetActive(true);
        if (ClientStatusText) ClientStatusText.SetActive(false);
        SetStatus(status);
    }

    void ShowPanel_Client()
    {
        if (ParentIpDanPort) ParentIpDanPort.SetActive(false);
        if (ParentStatusText) ParentStatusText.SetActive(false);
        if (ClientStatusText) ClientStatusText.SetActive(true);
    }

    // ================== UDP ==================
    private void HandleUdpDiscovery(string msg)
    {
        // server kirim: "WS:192.168.1.xx:8765"
        if (!msg.StartsWith("WS:")) return;

        // ✅ kalau sudah pernah dapat 1x, jangan proses lagi
        if (serverDiscovered) return;
        serverDiscovered = true;

        var parts = msg.Split(':');
        if (parts.Length < 3) return;

        string ip = parts[1].Trim();
        string portStr = parts[2].Trim();

        if (!int.TryParse(portStr, out int port)) return;

        // isi ke input
        serverHost = ip;
        serverPort = port;
        if (ipInput) ipInput.text = ip;
        if (portInput) portInput.text = port.ToString();
        SaveServerPrefs();

        // ✅ sudah ketemu → hentikan UDP supaya ga spam connect
        StopUdpDiscovery();

        ShowPanel_Connecting($"📡 Ditemukan server lewat UDP: {ip}:{port} → mencoba connect...");

        if (autoConnectWhenDiscovered)
            ConnectSafe();
    }

    private async Task UdpListenLoop()
    {
        while (udpRunning)
        {
            try
            {
                var result = await udpListener.ReceiveAsync();
                string text = Encoding.UTF8.GetString(result.Buffer).Trim();
                HandleUdpDiscovery(text);
            }
            catch (ObjectDisposedException)
            {
                break; // socket ditutup
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[UDP] Error receive: " + ex.Message);
            }
        }
    }

    void StartUdpDiscovery()
    {
        // kalau lagi jalan → klik lagi buat stop
        if (udpRunning)
        {
            StopUdpDiscovery();
            SetStatus("🛑 Pencarian UDP dihentikan.");
            ShowPanel_Input();
            return;
        }

        try
        {
            serverDiscovered = false;       // boleh cari lagi
            udpListener = new UdpClient(udpListenPort);
            udpListener.EnableBroadcast = true;
            udpRunning = true;
            _ = UdpListenLoop();

            ShowPanel_Connecting($"📡 Menyimak broadcast UDP di port {udpListenPort}...");
        }
        catch (Exception ex)
        {
            SetStatus($"❌ Gagal mulai UDP: {ex.Message}");
            udpRunning = false;
            ShowPanel_Input();
        }
    }

    void StopUdpDiscovery()
    {
        udpRunning = false;
        try { udpListener?.Close(); } catch { }
        udpListener = null;
    }

    // ================== Wiring Buttons ==================
    void WireButtons()
    {
        if (btnConnect) btnConnect.onClick.AddListener(() =>
        {
            ReadInputsIntoDefaults();
            SaveServerPrefs();
            ConnectSafe();
        });

        if (btnUdpScan) btnUdpScan.onClick.AddListener(StartUdpDiscovery);

        if (btnRefresh) btnRefresh.onClick.AddListener(RequestClients);
        if (btnSendToID) btnSendToID.onClick.AddListener(SendToSelectedID);
        if (btnSendToIP) btnSendToIP.onClick.AddListener(SendToSelectedIP);
        if (btnPairWithSelected) btnPairWithSelected.onClick.AddListener(PairWithSelected);

        if (ipInput) ipInput.onEndEdit.AddListener(_ => { ReadInputsIntoDefaults(); SaveServerPrefs(); });
        if (portInput) portInput.onEndEdit.AddListener(_ => { ReadInputsIntoDefaults(); SaveServerPrefs(); });
    }

    // ================== Connect Flow (dibungkus) ==================
    public void ConnectSafe()
    {
        if (isConnecting) return;
        StartCoroutine(ConnectRoutine());
    }

    private IEnumerator ConnectRoutine()
    {
        isConnecting = true;

        // tampil connecting sekali
        ShowPanel_Connecting($"🔌 Menghubungkan ke ws://{serverHost}:{serverPort} ...");

        var t = Connect();   // panggil versi async yang panjang di bawah
        while (!t.IsCompleted)
        {
            // biar nggak blok frame
            yield return null;
        }

        isConnecting = false;
    }

    // ================== Connect Flow (asli) ==================
    public async Task Connect()
    {
        // kalau sudah connect, langsung tampilkan panel client
        if (IsConnected)
        {
            ShowPanel_Client();
            return;
        }

        await SafeClose();

        ReadInputsIntoDefaults();

        // bentuk socket
        ws = new WebSocket($"ws://{serverHost}:{serverPort}");

        ws.OnOpen += () =>
        {
            serverDiscovered = false;   // boleh scan UDP lagi
            _pairedIp = null; _pairedId = null; _connectSent = false;

            StartCoroutine(ShowClientPanelNextFrame());
            StartCoroutine(RequestClientsDelayed(0.3f));

            SetStatus("✅ Terhubung ke server.");

            if (enableHeartbeat)
            {
                if (heartbeatCo != null) StopCoroutine(heartbeatCo);
                heartbeatCo = StartCoroutine(HeartbeatLoop());
            }

            // auto re-pair
            if (!string.IsNullOrEmpty(_lastPickId))
            {
                _ = SendRaw($"PAIRWITH:{_lastPickId}");
                SetStatus($"🔗 Mencoba pair ulang ke ID {_lastPickId}...");
            }
            else if (!string.IsNullOrEmpty(_lastPickIp))
            {
                _ = SendRaw($"PAIRWITHIP:{_lastPickIp}");
                SetStatus($"🔗 Mencoba pair ulang ke IP {_lastPickIp}...");
            }
        };

        ws.OnClose += (code) =>
        {
            serverDiscovered = false;

            _pairedIp = null; _pairedId = null; _connectSent = false;

            if (heartbeatCo != null) { StopCoroutine(heartbeatCo); heartbeatCo = null; }

            if (autoReconnect)
            {
                // ⚠️ jangan ganti panel berkali-kali, cukup status
                SetStatus("⚠️ Koneksi terputus. Mencoba menghubungkan kembali...");
                if (reconnectCo != null) StopCoroutine(reconnectCo);
                reconnectCo = StartCoroutine(ReconnectLoop());
            }
            else
            {
                SetStatus("⚠️ Koneksi terputus.");
                ShowPanel_Input();
            }
        };

        ws.OnError += (err) =>
        {
            serverDiscovered = false;

            _pairedIp = null; _pairedId = null; _connectSent = false;

            if (heartbeatCo != null) { StopCoroutine(heartbeatCo); heartbeatCo = null; }

            if (autoReconnect)
            {
                SetStatus($"❌ Error: {err}. Mencoba menghubungkan kembali...");
                if (reconnectCo != null) StopCoroutine(reconnectCo);
                reconnectCo = StartCoroutine(ReconnectLoop());
            }
            else
            {
                SetStatus($"❌ Error: {err}");
                ShowPanel_Input();
            }
        };

        ws.OnMessage += (bytes) =>
        {
            var msg = System.Text.Encoding.UTF8.GetString(bytes);
            HandleServerText(msg);
        };

        try
        {
            await ws.Connect();
        }
        catch (Exception ex)
        {
            if (autoReconnect)
            {
                SetStatus($"❌ Gagal terhubung: {ex.Message}. Mencoba ulang...");
                if (reconnectCo != null) StopCoroutine(reconnectCo);
                reconnectCo = StartCoroutine(ReconnectLoop());
            }
            else
            {
                SetStatus($"❌ Gagal terhubung: {ex.Message}");
                ShowPanel_Input();
            }
        }
    }

    private IEnumerator ShowClientPanelNextFrame()
    {
        yield return null;
        ShowPanel_Client();
    }

    private IEnumerator RequestClientsDelayed(float delaySec)
    {
        yield return new WaitForSeconds(delaySec);
        _ = SendRaw("LISTCLIENTS");
    }

    async Task SafeClose()
    {
        if (ws != null)
        {
            try { await ws.Close(); } catch { }
            ws = null;
        }
    }

    // ================== Reconnect & Heartbeat ==================
    private IEnumerator ReconnectLoop()
    {
        int attempt = 0;

        // cukup status, ga usah ganti panel terus
        while (attempt < maxReconnectAttempts && !IsConnected)
        {
            if (!isConnecting)
            {
                attempt++;
                SetStatus($"🔁 Reconnect percobaan {attempt}/{maxReconnectAttempts} ...");
                ConnectSafe();
            }

            float t = 0f;
            while (t < reconnectIntervalSec)
            {
                if (IsConnected) break;
                t += Time.deltaTime;
                yield return null;
            }

            if (IsConnected)
            {
                SetStatus("✅ Reconnect berhasil.");
                reconnectCo = null;
                yield break;
            }
        }

        if (!IsConnected)
        {
            SetStatus("⛔ Gagal menghubungkan kembali. Silakan cek IP/Port server.");
            ShowPanel_Input();
        }

        reconnectCo = null;
    }

    private IEnumerator HeartbeatLoop()
    {
        var wait = new WaitForSeconds(heartbeatSec);
        int counter = 0;
        while (IsConnected)
        {
            _ = SendRaw("PING");

            // tiap 3 heartbeat aja minta list
            counter++;
            if (counter % 3 == 0)
            {
                _ = SendRaw("LISTCLIENTS");
            }

            yield return wait;
        }
    }

    // ================== Server Messages ==================
    void HandleServerText(string msg)
    {
        Debug.Log("[SRV] " + msg);

        if (msg.StartsWith("YOURIP:"))
        {
            myIpFromServer = msg.Substring("YOURIP:".Length).Trim();
            SetStatus($"IP kamu: {myIpFromServer}");
            return;
        }

        if (msg.StartsWith("CLIENTS:"))
        {
            ParseClientsPayload(msg.Substring("CLIENTS:".Length).Trim());
            RefreshDropdown();
            SetStatus("Daftar klien diperbarui.");
            return;
        }

        if (msg.StartsWith("PAIR_OK:"))
        {
            var id = msg.Substring("PAIR_OK:".Length).Trim();
            SetStatus($"✅ Berhasil terhubung dengan ID: {id}");

            _pairedId = string.IsNullOrEmpty(id) ? _lastPickId : id;
            _pairedIp = _lastPickIp;

            _connectSent = false;
            TrySendConnectOnce();

            ShowPanel_Client();
            return;
        }

        if (msg.StartsWith("PAIR_OK_IP:"))
        {
            var ip = msg.Substring("PAIR_OK_IP:".Length).Trim();
            SetStatus($"✅ Berhasil terhubung dengan IP: {ip}");

            _pairedIp = string.IsNullOrEmpty(ip) ? _lastPickIp : ip;
            _pairedId = _lastPickId;

            _connectSent = false;
            TrySendConnectOnce();

            ShowPanel_Client();
            return;
        }

        if (msg.StartsWith("PAIR_ERR:"))
        {
            SetStatus($"❌ Pairing gagal: {msg}");
            return;
        }

        // pesan lain tetap tampil, tapi ga ngubah panel
        SetStatus($"📨 {msg}");
    }

    void ParseClientsPayload(string payload)
    {
        clientsByIp.Clear();
        if (string.IsNullOrWhiteSpace(payload)) return;

        foreach (var raw in payload.Split(','))
        {
            var item = raw.Trim();
            if (string.IsNullOrEmpty(item)) continue;

            var cols = item.Split('|');
            var ip = cols.Length > 0 ? cols[0].Trim() : "unknown";
            var id = cols.Length > 1 ? cols[1].Trim() : "-";

            if (hideSelfInLists && ip == myIpFromServer) continue;
            clientsByIp[ip] = new ClientDetail { ip = ip, id = id };
        }
    }

    // ================== UI Helpers ==================
    void RefreshDropdown()
    {
        if (!clientsDropdown) return;

        clientsDropdown.ClearOptions();
        dropdownIndexToIp.Clear();

        var ips = new List<string>(clientsByIp.Keys);
        ips.Sort();

        var labels = new List<string>();
        foreach (var ip in ips)
        {
            var c = clientsByIp[ip];
            labels.Add($"{c.id} @ {ip}");
            dropdownIndexToIp.Add(ip);
        }

        if (labels.Count == 0) labels.Add("(tidak ada klien)");
        clientsDropdown.AddOptions(labels);
        clientsDropdown.RefreshShownValue();
    }

    void SetStatus(string s)
    {
        // debounce biar TMP nggak spam
        if (Time.time - _lastStatusTime < STATUS_MIN_INTERVAL)
            return;

        _lastStatusTime = Time.time;

        if (statusText)
        {
            statusText.text = s;
            if (!statusText.gameObject.activeSelf)
                statusText.gameObject.SetActive(true);
        }
        Debug.Log(s);
    }

    // ================== Actions ==================
    void RequestClients() => _ = SendRaw("LISTCLIENTS");

    void SendToSelectedID()
    {
        if (!EnsureConnected()) return;
        if (!TryGetSelected(out var ip, out var cd)) return;
        var msg = messageInput?.text.Trim();
        if (string.IsNullOrEmpty(msg)) { SetStatus("⚠️ Pesan kosong."); return; }

        if (!string.IsNullOrEmpty(cd.id) && cd.id != "-")
        {
            _ = SendRaw($"TOID:{cd.id}|{msg}");
            SetStatus($"📨 Dikirim ke ID {cd.id}");
        }
        else
        {
            _ = SendRaw($"TOIP:{ip}|{msg}");
            SetStatus($"📨 Dikirim ke {ip}");
        }
    }

    void SendToSelectedIP()
    {
        if (!EnsureConnected()) return;
        if (!TryGetSelected(out var ip, out _)) return;
        var msg = messageInput?.text.Trim();
        if (string.IsNullOrEmpty(msg)) { SetStatus("⚠️ Pesan kosong."); return; }

        _ = SendRaw($"TOIP:{ip}|{msg}");
        SetStatus($"📨 Dikirim ke {ip}");
    }

    void PairWithSelected()
    {
        if (!EnsureConnected()) return;
        if (!TryGetSelected(out var ip, out var cd)) return;

        _lastPickIp = ip;
        _lastPickId = (!string.IsNullOrEmpty(cd.id) && cd.id != "-") ? cd.id : null;

        _connectSent = false;

        if (!string.IsNullOrEmpty(cd.id) && cd.id != "-")
        {
            SetStatus($"🔗 Menyambungkan ke ID {cd.id}...");
            _ = SendRaw($"PAIRWITH:{cd.id}");
        }
        else
        {
            SetStatus($"🔗 Menyambungkan ke IP {ip}...");
            _ = SendRaw($"PAIRWITHIP:{ip}");
        }
    }

    bool TryGetSelected(out string ip, out ClientDetail cd)
    {
        ip = null; cd = null;
        if (clientsDropdown == null || dropdownIndexToIp.Count == 0)
        {
            SetStatus("Daftar klien kosong.");
            return false;
        }

        int idx = clientsDropdown.value;
        if (idx < 0 || idx >= dropdownIndexToIp.Count)
        {
            SetStatus("Pilih salah satu klien.");
            return false;
        }

        ip = dropdownIndexToIp[idx];
        cd = clientsByIp[ip];
        return true;
    }

    bool EnsureConnected()
    {
        if (ws == null || ws.State != WebSocketState.Open)
        {
            SetStatus("⛔ Belum terhubung ke server.");
            ShowPanel_Input();
            return false;
        }
        return true;
    }

    // ================== Send ==================
    async Task SendRaw(string text)
    {
        if (ws == null || ws.State != WebSocketState.Open)
            return;

        try
        {
            await ws.SendText(text);
            Debug.Log("[WS->SRV] " + text);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[WS->SRV] send failed: " + ex.Message);
        }
    }

    // ================== CONNECT once logic ==================
    private async void TrySendConnectOnce()
    {
        if (_connectSent) return;
        if (!IsConnected) return;
        if (!HasActiveTarget) return;

        if (!string.IsNullOrEmpty(_pairedId))
        {
            await SendRaw($"TOID:{_pairedId}|connect");
            SetStatus("✅ 'connect' dikirim (by ID).");
        }
        else if (!string.IsNullOrEmpty(_pairedIp))
        {
            await SendRaw($"TOIP:{_pairedIp}|connect");
            SetStatus("✅ 'connect' dikirim (by IP).");
        }

        _connectSent = true;
    }

    // ================== Prefs ==================
    void ReadInputsIntoDefaults()
    {
        if (ipInput && !string.IsNullOrWhiteSpace(ipInput.text))
            serverHost = ipInput.text.Trim();

        if (portInput && !string.IsNullOrWhiteSpace(portInput.text))
        {
            if (int.TryParse(portInput.text.Trim(), out var p) && p > 0 && p <= 65535)
                serverPort = p;
        }
    }

    void SaveServerPrefs()
    {
        PlayerPrefs.SetString(PREF_IP_KEY, serverHost);
        PlayerPrefs.SetInt(PREF_PORT_KEY, serverPort);
        PlayerPrefs.Save();
        Debug.Log($"[Prefs] Saved IP={serverHost}, Port={serverPort}");
    }

    void LoadServerPrefs()
    {
        if (PlayerPrefs.HasKey(PREF_IP_KEY))
            serverHost = PlayerPrefs.GetString(PREF_IP_KEY, serverHost);

        if (PlayerPrefs.HasKey(PREF_PORT_KEY))
            serverPort = PlayerPrefs.GetInt(PREF_PORT_KEY, serverPort);

        Debug.Log($"[Prefs] Loaded IP={serverHost}, Port={serverPort}");
    }

    public async void KirimPesanKeClientTerpilih(string pesan)
    {
        if (string.IsNullOrWhiteSpace(pesan))
        {
            SetStatus("⚠️ Pesan kosong, tidak dikirim.");
            return;
        }

        if (!EnsureConnected())
        {
            SetStatus("⛔ Tidak terhubung ke server, pesan gagal dikirim.");
            return;
        }

        if (!TryGetSelected(out var ip, out var cd))
        {
            SetStatus("⚠️ Belum ada client yang dipilih.");
            return;
        }

        if (!string.IsNullOrEmpty(cd.id) && cd.id != "-")
        {
            await SendRaw($"TOID:{cd.id}|{pesan}");
            SetStatus($"📨 Pesan '{pesan}' dikirim ke ID {cd.id}");
        }
        else
        {
            await SendRaw($"TOIP:{ip}|{pesan}");
            SetStatus($"📨 Pesan '{pesan}' dikirim ke {ip}");
        }
    }
}
