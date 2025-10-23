using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using NativeWebSocket;
using TMPro;

public class WsRouterClientNative : MonoBehaviour
{
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
    public Button btnPairWithSelected;   // tombol untuk connect antar client (PAIR)

    [Header("UI - Client List & Message")]
    public TMP_Dropdown clientsDropdown;
    public TMP_InputField messageInput;

    [Header("UI - Status Text (debug/status)")]
    public TMP_Text statusText;          // tampil di panel yang aktif (termasuk ClientStatusText)

    [Header("Panels")]
    public GameObject ParentIpDanPort;   // Panel input server (IP/Port)
    public GameObject ParentStatusText;  // Panel "Menghubungkan..." / reconnect
    public GameObject ClientStatusText;  // Panel setelah connect (daftar client, tombol pair, dsb)

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

    private Coroutine currentTransition;
    private Coroutine reconnectCo;
    private Coroutine heartbeatCo;

    // PlayerPrefs keys
    const string PREF_IP_KEY = "WsRouter_LastIP";
    const string PREF_PORT_KEY = "WsRouter_LastPort";

    [Serializable]
    public class ClientDetail
    {
        public string ip = "-";
        public string id = "-";
    }

    // ====== CONNECT-on-pair state ======
    private string _pairedIp = null;
    private string _pairedId = null;
    private bool _connectSent = false;

    // simpan pilihan terakhir ketika klik Pair (supaya tahu target saat balasan PAIR_OK tiba)
    private string _lastPickIp = null;
    private string _lastPickId = null;

    // Expose opsional buat script lain
    public bool IsConnected => ws != null && ws.State == WebSocketState.Open;
    public bool HasActiveTarget => !string.IsNullOrEmpty(_pairedId) || !string.IsNullOrEmpty(_pairedIp);

    // ================== Unity Lifecycle ==================
    async void Start()
    {
        Application.runInBackground = true;

        LoadServerPrefs();

        if (ipInput) ipInput.text = serverHost;
        if (portInput) portInput.text = serverPort.ToString();

        WireButtons();

        // State awal UI
        ShowPanel_Input();
        SetStatus("Masukkan IP & Port server untuk mulai koneksi.");

        if (autoConnectOnStart)
            await Connect();
    }

    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        ws?.DispatchMessageQueue();
#endif
    }

    void OnDestroy() => _ = SafeClose();

    // ================== Wiring Buttons ==================
    void WireButtons()
    {
        if (btnConnect) btnConnect.onClick.AddListener(async () =>
        {
            ReadInputsIntoDefaults();
            SaveServerPrefs();
            await Connect();
        });

        if (btnRefresh) btnRefresh.onClick.AddListener(RequestClients);
        if (btnSendToID) btnSendToID.onClick.AddListener(SendToSelectedID);
        if (btnSendToIP) btnSendToIP.onClick.AddListener(SendToSelectedIP);
        if (btnPairWithSelected) btnPairWithSelected.onClick.AddListener(PairWithSelected);

        // Simpan otomatis saat selesai edit
        if (ipInput) ipInput.onEndEdit.AddListener(_ => { ReadInputsIntoDefaults(); SaveServerPrefs(); });
        if (portInput) portInput.onEndEdit.AddListener(_ => { ReadInputsIntoDefaults(); SaveServerPrefs(); });
    }

    // ================== Connect Flow ==================
    public async Task Connect()
    {
        await SafeClose();

        ReadInputsIntoDefaults();
        SaveServerPrefs();

        // Panel "Connecting"
        ShowPanel_Connecting($"🔌 Menghubungkan ke ws://{serverHost}:{serverPort} ...");

        ws = new WebSocket($"ws://{serverHost}:{serverPort}");

        ws.OnOpen += () =>
        {
            // reset pairing/flag setiap koneksi baru
            _pairedIp = null; _pairedId = null; _connectSent = false;

            // START heartbeat saat connected
            if (enableHeartbeat)
            {
                if (heartbeatCo != null) StopCoroutine(heartbeatCo);
                heartbeatCo = StartCoroutine(HeartbeatLoop());
            }

            // Setelah berhasil open, delay 3 detik lalu tampilkan panel client
            StartPanelTransition(DelayShowClientPanel(3f));

            // (Opsional) auto re-pair ke target terakhir
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
            _pairedIp = null; _pairedId = null; _connectSent = false;

            // STOP heartbeat
            if (heartbeatCo != null) { StopCoroutine(heartbeatCo); heartbeatCo = null; }

            SetStatus("⚠️ Koneksi terputus. Mencoba menghubungkan kembali...");
            // Tampilkan panel status selama mencoba reconnect
            if (ParentStatusText) ParentStatusText.SetActive(true);
            if (ClientStatusText) ClientStatusText.SetActive(false);
            if (ParentIpDanPort) ParentIpDanPort.SetActive(false);

            if (autoReconnect)
            {
                if (reconnectCo != null) StopCoroutine(reconnectCo);
                reconnectCo = StartCoroutine(ReconnectLoop());
            }
            else
            {
                StartPanelTransition(DelayShowDisconnectedPanel(3f));
            }
        };

        ws.OnError += (err) =>
        {
            _pairedIp = null; _pairedId = null; _connectSent = false;

            if (heartbeatCo != null) { StopCoroutine(heartbeatCo); heartbeatCo = null; }

            SetStatus($"❌ Error: {err}. Mencoba menghubungkan kembali...");
            if (ParentStatusText) ParentStatusText.SetActive(true);
            if (ClientStatusText) ClientStatusText.SetActive(false);
            if (ParentIpDanPort) ParentIpDanPort.SetActive(false);

            if (autoReconnect)
            {
                if (reconnectCo != null) StopCoroutine(reconnectCo);
                reconnectCo = StartCoroutine(ReconnectLoop());
            }
            else
            {
                StartPanelTransition(DelayShowDisconnectedPanel(3f));
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
            SetStatus($"❌ Gagal terhubung: {ex.Message}. Mencoba ulang...");
            if (autoReconnect)
            {
                if (ParentStatusText) ParentStatusText.SetActive(true);
                if (ClientStatusText) ClientStatusText.SetActive(false);
                if (ParentIpDanPort) ParentIpDanPort.SetActive(false);

                if (reconnectCo != null) StopCoroutine(reconnectCo);
                reconnectCo = StartCoroutine(ReconnectLoop());
            }
            else
            {
                StartPanelTransition(DelayShowDisconnectedPanel(3f));
            }
        }
    }

    async Task SafeClose()
    {
        if (ws != null)
        {
            try { await ws.Close(); } catch { }
            ws = null;
        }
    }

    // ================== Panel Transitions ==================
    void StartPanelTransition(IEnumerator routine)
    {
        if (currentTransition != null) StopCoroutine(currentTransition);
        currentTransition = StartCoroutine(routine);
    }

    IEnumerator DelayShowClientPanel(float delaySec)
    {
        SetStatus("✅ Terhubung ke server! Menyiapkan tampilan...");
        yield return new WaitForSeconds(delaySec);

        if (ws != null && ws.State == WebSocketState.Open)
        {
            ShowPanel_Client();   // sembunyikan ParentStatusText, tampilkan ClientStatusText
            _ = SendRaw("LISTCLIENTS");
        }
        currentTransition = null;
    }

    IEnumerator DelayShowDisconnectedPanel(float delaySec)
    {
        if (ParentStatusText) ParentStatusText.SetActive(true);
        if (ClientStatusText) ClientStatusText.SetActive(false);

        yield return new WaitForSeconds(delaySec);

        ShowPanel_Input();
        currentTransition = null;
    }

    void ShowPanel_Input()
    {
        if (ParentIpDanPort) ParentIpDanPort.SetActive(true);
        if (ParentStatusText) ParentStatusText.SetActive(false);
        if (ClientStatusText) ClientStatusText.SetActive(false);
    }

    void ShowPanel_Connecting(string status)
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

    // ================== Reconnect & Heartbeat ==================
    private IEnumerator ReconnectLoop()
    {
        int attempt = 0;

        if (ParentStatusText) ParentStatusText.SetActive(true);
        if (ClientStatusText) ClientStatusText.SetActive(false);
        if (ParentIpDanPort) ParentIpDanPort.SetActive(false);

        while (attempt < maxReconnectAttempts && !IsConnected)
        {
            attempt++;
            SetStatus($"🔁 Reconnect percobaan {attempt}/{maxReconnectAttempts} ...");

            var _ = Connect(); // jalankan Connect flow

            float wait = 0f;
            while (wait < reconnectIntervalSec)
            {
                if (IsConnected) break;
                wait += Time.deltaTime;
                yield return null;
            }

            if (IsConnected)
            {
                SetStatus("✅ Reconnect berhasil.");
                reconnectCo = null;
                yield break;
            }

            float remain = Mathf.Max(0f, reconnectIntervalSec - wait);
            if (remain > 0f) yield return new WaitForSeconds(remain);
        }

        if (!IsConnected)
        {
            SetStatus("⛔ Gagal menghubungkan kembali. Silakan cek IP/Port server.");
            if (ParentIpDanPort) ParentIpDanPort.SetActive(true);
            if (ParentStatusText) ParentStatusText.SetActive(false);
            if (ClientStatusText) ClientStatusText.SetActive(false);
        }

        reconnectCo = null;
    }

    private IEnumerator HeartbeatLoop()
    {
        var wait = new WaitForSeconds(heartbeatSec);
        while (IsConnected)
        {
            // Fire-and-forget; bila gagal mengirim, SendRaw akan mencoba close dan OnClose akan memulai reconnect
            _ = SendRaw("PING");
            _ = SendRaw("LISTCLIENTS");
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

        // Pairing feedback (kita yang request)
        if (msg.StartsWith("PAIR_OK:"))
        {
            var id = msg.Substring("PAIR_OK:".Length).Trim();
            SetStatus($"✅ Berhasil terhubung dengan ID: {id}");

            _pairedId = string.IsNullOrEmpty(id) ? _lastPickId : id;
            _pairedIp = _lastPickIp;

            _connectSent = false;
            TrySendConnectOnce();

            // Delay 3 detik sebelum sembunyikan panel Client
            if (ClientStatusText) StartCoroutine(HideClientPanelAfterDelay(3f));
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

            if (ClientStatusText) StartCoroutine(HideClientPanelAfterDelay(3f));
            return;
        }

        // Partner pairing notifies us
        if (msg.StartsWith("PAIR_OK_WITH"))
        {
            // contoh format: "PAIR_OK_WITH:<id>"
            var parts = msg.Split(':');
            if (parts.Length > 1)
                _pairedId = string.IsNullOrWhiteSpace(parts[1]) ? _pairedId : parts[1].Trim();

            _connectSent = false;
            TrySendConnectOnce();
            SetStatus($"ℹ️ Partner pairing (ID). CONNECT dikirim.");
            return;
        }

        if (msg.StartsWith("PAIR_OK_WITH_IP"))
        {
            // contoh format: "PAIR_OK_WITH_IP:<ip>"
            var parts = msg.Split(':');
            if (parts.Length > 1)
                _pairedIp = string.IsNullOrWhiteSpace(parts[1]) ? _pairedIp : parts[1].Trim();

            _connectSent = false;
            TrySendConnectOnce();
            SetStatus($"ℹ️ Partner pairing (IP). CONNECT dikirim.");
            return;
        }

        if (msg.StartsWith("PAIR_ERR:"))
        {
            SetStatus($"❌ Pairing gagal: {msg}");
            return;
        }

        // Pesan lain
        SetStatus($"📨 {msg}");
    }

    private IEnumerator HideClientPanelAfterDelay(float delaySec)
    {
        yield return new WaitForSeconds(delaySec);
        if (ParentStatusText) ParentStatusText.SetActive(false);
        if (ClientStatusText) ClientStatusText.SetActive(false);
        SetStatus("🔗 Berhasil Terhubung — Simulasi siap dimainkan.");
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
        if (statusText)
        {
            statusText.text = s;
            if (!statusText.gameObject.activeSelf) statusText.gameObject.SetActive(true);
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
        if (string.IsNullOrEmpty(msg)) { SetStatus("Pesan kosong."); return; }

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
        if (string.IsNullOrEmpty(msg)) { SetStatus("Pesan kosong."); return; }

        _ = SendRaw($"TOIP:{ip}|{msg}");
        SetStatus($"📨 Dikirim ke {ip}");
    }

    void PairWithSelected()
    {
        if (!EnsureConnected()) return;
        if (!TryGetSelected(out var ip, out var cd)) return;

        // simpan pilihan terakhir
        _lastPickIp = ip;
        _lastPickId = (!string.IsNullOrEmpty(cd.id) && cd.id != "-") ? cd.id : null;

        // ganti pasangan => izinkan "connect" terkirim lagi
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
            SetStatus("Daftar klien kosong."); return false;
        }

        int idx = clientsDropdown.value;
        if (idx < 0 || idx >= dropdownIndexToIp.Count)
        {
            SetStatus("Pilih salah satu klien."); return false;
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
            return false;
        }
        return true;
    }

    async Task SendRaw(string text)
    {
        if (ws == null || ws.State != WebSocketState.Open)
            throw new InvalidOperationException("WS not open");

        try
        {
            await ws.SendText(text);
            Debug.Log("[WS->SRV] " + text);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[WS->SRV] send failed: " + ex.Message);
            // Paksa close supaya OnClose terpanggil → ReconnectLoop aktif
            try { await ws.Close(); } catch { }
            throw;
        }
    }

    // ================== CONNECT once logic ==================
    private async void TrySendConnectOnce()
    {
        if (_connectSent) return;
        if (!IsConnected) return;
        if (!HasActiveTarget) return;

        // prioritas kirim ke ID; fallback IP
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

    /// <summary>
    /// API publik: bila ada script lain ingin memicu ulang pengiriman 'connect' (jika siap).
    /// </summary>
    public void SendConnectIfReadyFromOther()
    {
        _connectSent = false;   // izinkan kirim lagi
        TrySendConnectOnce();
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

    void OnApplicationQuit()
    {
        ReadInputsIntoDefaults();
        SaveServerPrefs();
    }
}
