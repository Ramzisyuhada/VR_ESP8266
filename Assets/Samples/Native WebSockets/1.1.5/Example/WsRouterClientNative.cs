using System;
using System.Collections;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using NativeWebSocket;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WsRouterClientNative : MonoBehaviour
{
    // ================== KONFIG DASAR ==================
    [Header("Server WebSocket")]
    public string serverHost = "192.168.1.23";
    public int serverPort = 8765;
    public bool autoConnectOnStart = true;

    [Header("UDP Discovery (opsional)")]
    public int udpListenPort = 4210;          // harus sama dgn server Python
    public bool autoConnectWhenDiscovered = true;
    public Button btnUdpScan;

    [Header("UI Input Server")]
    public TMP_InputField ipInput;
    public TMP_InputField portInput;
    public Button btnConnect;

    [Header("UI Kirim Pesan ke Pasangan")]
    public TMP_InputField messageInput;
    public Button btnSendToPaired;

    [Header("UI Status / Panel")]
    public TMP_Text statusText;
    public GameObject panelInput;     // panel isi IP
    public GameObject panelConnecting;
    public GameObject panelConnected;

    [Header("Reconnect")]
    public bool autoReconnect = true;
    public int maxReconnectAttempts = 5;
    public float reconnectIntervalSec = 3f;

    [Header("Heartbeat")]
    public bool enableHeartbeat = true;
    public float heartbeatSec = 5f;

    // ================== INTERNAL ==================
    private WebSocket ws;
    private bool isConnecting = false;
    private Coroutine reconnectCo;
    private Coroutine heartbeatCo;

    // pasangan yg dikasih server
    private string pairedIp = null;
    private string pairedId = null;

    // UDP
    private UdpClient udpListener;
    private bool udpRunning = false;
    private bool serverDiscovered = false;

    // pref
    const string PREF_IP = "WsRouter_IP";
    const string PREF_PORT = "WsRouter_PORT";

    // status debounce
    private float _lastStatusTime = 0f;
    private const float STATUS_MIN_INTERVAL = 0.1f;

    // ================== UNITY ==================
    private void Start()
    {
        Application.runInBackground = true;
        LoadPrefs();

        if (ipInput) ipInput.text = serverHost;
        if (portInput) portInput.text = serverPort.ToString();

        WireUI();

        ShowPanelInput();
        SetStatus("Isi IP & Port, lalu connect.");
        panelConnecting.SetActive(true);
        if (autoConnectOnStart)
            ConnectSafe();
    }

    private void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        ws?.DispatchMessageQueue();
#endif
    }

    private void OnDestroy()
    {
        _ = SafeClose();
        StopUdp();
    }

    private void OnApplicationQuit()
    {
        SavePrefs();
        StopUdp();
    }

    // ================== UI ==================
    void WireUI()
    {
        if (btnConnect) btnConnect.onClick.AddListener(() =>
        {
            ReadInputs();
            SavePrefs();
            ConnectSafe();
        });

        if (btnUdpScan) btnUdpScan.onClick.AddListener(StartUdp);

        if (btnSendToPaired) btnSendToPaired.onClick.AddListener(SendToPaired);
    }

    void ShowPanelInput()
    {
        if (panelInput) panelInput.SetActive(true);
        if (panelConnecting) panelConnecting.SetActive(false);
        if (panelConnected) panelConnected.SetActive(false);
    }

    void ShowPanelConnecting(string msg = "Menghubungkan...")
    {
        if (panelInput) panelInput.SetActive(false);
        if (panelConnecting) panelConnecting.SetActive(true);
        if (panelConnected) panelConnected.SetActive(false);
        SetStatus(msg);
    }

    void ShowPanelConnected()
    {
        if (panelInput) panelInput.SetActive(false);
        if (panelConnecting) panelConnecting.SetActive(false);
        if (panelConnected) panelConnected.SetActive(true);
    }

    void SetStatus(string s)
    {
        if (Time.time - _lastStatusTime < STATUS_MIN_INTERVAL)
            return;
        _lastStatusTime = Time.time;

        if (statusText)
            statusText.text = s;
        Debug.Log("[WS-CLIENT] " + s);
    }

    // ================== PREFS ==================
    void LoadPrefs()
    {
        if (PlayerPrefs.HasKey(PREF_IP))
            serverHost = PlayerPrefs.GetString(PREF_IP, serverHost);
        if (PlayerPrefs.HasKey(PREF_PORT))
            serverPort = PlayerPrefs.GetInt(PREF_PORT, serverPort);
    }

    void SavePrefs()
    {
        PlayerPrefs.SetString(PREF_IP, serverHost);
        PlayerPrefs.SetInt(PREF_PORT, serverPort);
        PlayerPrefs.Save();
    }

    void ReadInputs()
    {
        if (ipInput && !string.IsNullOrWhiteSpace(ipInput.text))
            serverHost = ipInput.text.Trim();
        if (portInput && int.TryParse(portInput.text, out var p) && p > 0)
            serverPort = p;
    }

    // ================== CONNECT WRAPPER ==================
    public void ConnectSafe()
    {
        if (isConnecting) return;
        StartCoroutine(ConnectRoutine());
    }

    private IEnumerator ConnectRoutine()
    {
        isConnecting = true;
        ShowPanelConnecting($"🔌 Menghubungkan ke ws://{serverHost}:{serverPort} ...");
        var t = Connect();
        while (!t.IsCompleted) yield return null;
        isConnecting = false;
    }

    // ================== CONNECT SEBENARNYA ==================
    public async Task Connect()
    {
        if (IsConnected())
        {
            ShowPanelConnected();
            return;
        }

        await SafeClose();

        ws = new WebSocket($"ws://{serverHost}:{serverPort}");

        ws.OnOpen += () =>
        {
            pairedIp = null;
            pairedId = null;
            SetStatus("✅ Terhubung ke server.");
            ShowPanelConnected();

            if (enableHeartbeat)
            {
                if (heartbeatCo != null) StopCoroutine(heartbeatCo);
                heartbeatCo = StartCoroutine(HeartbeatLoop());
            }
        };

        ws.OnClose += (code) =>
        {
            pairedIp = null;
            pairedId = null;

            if (heartbeatCo != null) { StopCoroutine(heartbeatCo); heartbeatCo = null; }

            if (autoReconnect)
            {
                SetStatus("⚠️ Koneksi terputus. Reconnect...");
                if (reconnectCo != null) StopCoroutine(reconnectCo);
                reconnectCo = StartCoroutine(ReconnectLoop());
            }
            else
            {
                SetStatus("⚠️ Koneksi terputus.");
                ShowPanelInput();
            }
        };

        ws.OnError += (err) =>
        {
            pairedIp = null;
            pairedId = null;

            if (heartbeatCo != null) { StopCoroutine(heartbeatCo); heartbeatCo = null; }

            if (autoReconnect)
            {
                SetStatus($"❌ Error: {err}. Reconnect...");
                if (reconnectCo != null) StopCoroutine(reconnectCo);
                reconnectCo = StartCoroutine(ReconnectLoop());
            }
            else
            {
                SetStatus($"❌ Error: {err}");
                ShowPanelInput();
            }
        };

        ws.OnMessage += (bytes) =>
        {
            var msg = Encoding.UTF8.GetString(bytes);
            HandleServerMsg(msg);
        };

        try
        {
            await ws.Connect();
        }
        catch (Exception ex)
        {
            if (autoReconnect)
            {
                SetStatus($"❌ Gagal connect: {ex.Message}. Reconnect...");
                if (reconnectCo != null) StopCoroutine(reconnectCo);
                reconnectCo = StartCoroutine(ReconnectLoop());
            }
            else
            {
                SetStatus($"❌ Gagal connect: {ex.Message}");
                ShowPanelInput();
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

    bool IsConnected() => ws != null && ws.State == WebSocketState.Open;

    // ================== RECONNECT ==================
    private IEnumerator ReconnectLoop()
    {
        int attempt = 0;
        while (attempt < maxReconnectAttempts && !IsConnected())
        {
            attempt++;
            SetStatus($"🔁 Reconnect {attempt}/{maxReconnectAttempts} ...");
            ConnectSafe();

            float t = 0f;
            while (t < reconnectIntervalSec)
            {
                if (IsConnected()) break;
                t += Time.deltaTime;
                yield return null;
            }

            if (IsConnected())
            {
                SetStatus("✅ Reconnect berhasil.");
                reconnectCo = null;
                yield break;
            }
        }

        if (!IsConnected())
        {
            SetStatus("⛔ Gagal reconnect. Cek IP/Port.");
            ShowPanelInput();
        }
        reconnectCo = null;
    }

    // ================== HEARTBEAT ==================
    private IEnumerator HeartbeatLoop()
    {
        var wait = new WaitForSeconds(heartbeatSec);
        while (IsConnected())
        {
            _ = SendRaw("PING");
            yield return wait;
        }
    }

    // ================== HANDLE PESAN SERVER ==================
    void HandleServerMsg(string msg)
    {
        Debug.Log("[SRV] " + msg);

        // 1. Filter heartbeat
        if (msg == "PING" || msg == "PONG" || msg.StartsWith("HEARTBEAT"))
        {
            // jangan tampilkan di status
            return;
        }

        // 2. Info IP kita
        if (msg.StartsWith("YOURIP:"))
        {
            var myip = msg.Substring("YOURIP:".Length).Trim();
            SetStatus("IP kamu: " + myip);
            return;
        }

        // 3. Server memaksa kita pair ke IP tertentu
        if (msg.StartsWith("PAIR_OK_IP:"))
        {
            pairedIp = msg.Substring("PAIR_OK_IP:".Length).Trim();
            pairedId = null;

            SetStatus("✅ Dipasangkan ke IP: " + pairedIp);
            ShowPanelConnected();
            return;
        }

        // 4. Server bilang kita dipasangkan bersama IP ini (kita jadi pasangannya)
        if (msg.StartsWith("PAIR_OK_WITH_IP:"))
        {
            pairedIp = msg.Substring("PAIR_OK_WITH_IP:".Length).Trim();
            pairedId = null;

            SetStatus("✅ Dipasangkan bersama IP: " + pairedIp);
            ShowPanelConnected();
            return;
        }

        // 5. Pairing berbasis ID
        if (msg.StartsWith("PAIR_OK:"))
        {
            // kalau server pakai ID
            pairedId = msg.Substring("PAIR_OK:".Length).Trim();
            pairedIp = null;

            SetStatus("✅ Dipasangkan ke ID: " + pairedId);
            ShowPanelConnected();
            return;
        }

        // 6. Unpair
        if (msg.StartsWith("UNPAIRED_BY_SERVER") || msg.StartsWith("UNPAIRED_BY_PARTNER"))
        {
            pairedIp = null;
            pairedId = null;

            SetStatus("ℹ️ Pair dilepas server.");
            return;
        }

        // 7. Pesan biasa
        SetStatus("📨 " + msg);
    }


    // ================== KIRIM KE PASANGAN ==================
    public void SendToPaired()
    {
        if (!IsConnected())
        {
            SetStatus("⛔ Belum connect ke server.");
            return;
        }

        if (string.IsNullOrEmpty(pairedIp) && string.IsNullOrEmpty(pairedId))
        {
            SetStatus("⚠️ Belum ada pasangan dari server.");
            return;
        }

        var msg = messageInput ? messageInput.text.Trim() : "";
        if (string.IsNullOrEmpty(msg))
        {
            SetStatus("⚠️ Pesan kosong.");
            return;
        }

        if (!string.IsNullOrEmpty(pairedId))
        {
            _ = SendRaw($"TOID:{pairedId}|{msg}");
            SetStatus($"📨 Pesan dikirim ke ID {pairedId}");
        }
        else
        {
            _ = SendRaw($"TOIP:{pairedIp}|{msg}");
            SetStatus($"📨 Pesan dikirim ke IP {pairedIp}");
        }
    }

    // ================== KIRIM RAW ==================
    async Task SendRaw(string text)
    {
        if (ws == null || ws.State != WebSocketState.Open) return;
        try
        {
            await ws.SendText(text);
            Debug.Log("[WS->SRV] " + text);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Send failed: " + ex.Message);
        }
    }

    // ================== UDP ==================
    void StartUdp()
    {
        if (udpRunning)
        {
            StopUdp();
            SetStatus("🛑 UDP stop.");
            return;
        }

        try
        {
            serverDiscovered = false;
            udpListener = new UdpClient(udpListenPort);
            udpListener.EnableBroadcast = true;
            udpRunning = true;
            _ = UdpListenLoop();
            ShowPanelConnecting("📡 Menunggu broadcast server...");
        }
        catch (Exception ex)
        {
            SetStatus("❌ Gagal start UDP: " + ex.Message);
            udpRunning = false;
        }
    }

    async Task UdpListenLoop()
    {
        while (udpRunning)
        {
            try
            {
                var res = await udpListener.ReceiveAsync();
                var txt = Encoding.UTF8.GetString(res.Buffer).Trim();
                HandleUdpMsg(txt);
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[UDP] " + ex.Message);
            }
        }
    }

    void HandleUdpMsg(string msg)
    {
        if (!msg.StartsWith("WS:")) return;
        if (serverDiscovered) return;

        var parts = msg.Split(':');
        if (parts.Length < 3) return;

        var ip = parts[1].Trim();
        var portStr = parts[2].Trim();
        if (!int.TryParse(portStr, out int port)) return;

        serverHost = ip;
        serverPort = port;
        if (ipInput) ipInput.text = ip;
        if (portInput) portInput.text = port.ToString();
        SavePrefs();

        serverDiscovered = true;
        StopUdp();

        ShowPanelConnecting($"📡 Dapat server {ip}:{port}, connect...");

        if (autoConnectWhenDiscovered)
            ConnectSafe();
    }

    void StopUdp()
    {
        udpRunning = false;
        try { udpListener?.Close(); } catch { }
        udpListener = null;
    }

    public void KirimPesanKeClientTerpilih(string pesan)
    {
        if (!IsConnected())
        {
            SetStatus("⛔ Belum connect ke server.");
            return;
        }

        if (string.IsNullOrEmpty(pairedIp) && string.IsNullOrEmpty(pairedId))
        {
            SetStatus("⚠️ Belum ada pasangan dari server.");
            return;
        }

        var msg = pesan;
        if (string.IsNullOrEmpty(msg))
        {
            SetStatus("⚠️ Pesan kosong.");
            return;
        }

        if (!string.IsNullOrEmpty(pairedId))
        {
            _ = SendRaw($"TOID:{pairedId}|{msg}");
           // SetStatus($"📨 Pesan dikirim ke ID {pairedId}");
        }
        else
        {
            _ = SendRaw($"TOIP:{pairedIp}|{msg}");
            //SetStatus($"📨 Pesan dikirim ke IP {pairedIp}");
        }
    }
}
