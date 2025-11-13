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
    // ================== IDENTITY ==================
    [Header("Identity / Device ID")]
    [Tooltip("ID yang dikirim ke server Python via SETID dan HELLO_FROM")]
    public string clientId = "";                      // contoh: "Si-MekaK3-VR-1"
    [Tooltip("Kalau kosong, ambil dari device + angka unik")]
    public bool autoUseDeviceNameIfEmpty = true;

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
    public GameObject GameMenu;

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

    // IP kita (dikasih server)
    private string myIp = null;

    // UDP
    private UdpClient udpListener;
    private bool udpRunning = false;
    private bool serverDiscovered = false;

    // pref
    const string PREF_IP = "WsRouter_IP";
    const string PREF_PORT = "WsRouter_PORT";
    const string PREF_ID = "WsRouter_ID";

    // status debounce
    private float _lastStatusTime = 0f;
    private const float STATUS_MIN_INTERVAL = 0.1f;


    

    // ================== UNITY ==================
    private void Start()
    {
        Application.runInBackground = true;
        LoadPrefs();

        // 🔹 kalau ID kosong, bikin otomatis
        if (string.IsNullOrWhiteSpace(clientId) && autoUseDeviceNameIfEmpty)
        {
            // misal hasil: "MekaK3-VR-QUEST3-742"
            string model = SystemInfo.deviceModel;
            if (string.IsNullOrWhiteSpace(model))
                model = SystemInfo.deviceName;

            model = model.Replace(" ", "").Replace("(", "").Replace(")", "");

            int hash = Mathf.Abs(SystemInfo.deviceUniqueIdentifier.GetHashCode()) % 1000;
            clientId = $"MekaK3-VR-{model}-{hash}";
            Debug.Log("[AUTO-ID] generate: " + clientId);
        }

        if (ipInput) ipInput.text = serverHost;
        if (portInput) portInput.text = serverPort.ToString();

        WireUI();

        // 🔵 permintaanmu: awalnya panel input & panel connected aktif
        if (panelInput) panelInput.SetActive(false);
        if (panelConnected) panelConnected.SetActive(false);
        if (panelConnecting) panelConnecting.SetActive(true);

        SetStatus("Silakan isi IP & Port, atau tunggu server.");

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

            if (panelConnected) panelConnected.SetActive(true);
            if (panelInput) panelInput.SetActive(false);
            if (panelConnecting) panelConnecting.SetActive(false);
        });

        if (btnUdpScan) btnUdpScan.onClick.AddListener(StartUdp);

        if (btnSendToPaired) btnSendToPaired.onClick.AddListener(SendToPaired);
    }

    void ShowPanelInput()
    {
        if (panelInput) panelInput.SetActive(true);
        if (panelConnecting) panelConnecting.SetActive(true);
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
        if (PlayerPrefs.HasKey(PREF_ID))
            clientId = PlayerPrefs.GetString(PREF_ID, clientId);
    }

    void SavePrefs()
    {
        PlayerPrefs.SetString(PREF_IP, serverHost);
        PlayerPrefs.SetInt(PREF_PORT, serverPort);
        PlayerPrefs.SetString(PREF_ID, clientId);
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
        ShowPanelConnecting($"🔌 Menghubungkan ke Server {serverHost}:{serverPort} ...");
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
        }

        await SafeClose();

        ws = new WebSocket($"ws://{serverHost}:{serverPort}");

        ws.OnOpen += () =>
        {
            pairedIp = null;
            pairedId = null;
            myIp = null;

            SetStatus("✅ Terhubung ke server. Tunggu sebentar...");

            // 👉 jangan langsung, tapi tunggu dikit
            StartCoroutine(ShowInputDelayed(0.5f));

            // kirim SETID / HELLO sedikit setelah connect
            StartCoroutine(SendHelloAfterDelay(0.3f));
        };

        ws.OnClose += (code) =>
        {
            pairedIp = null;
            pairedId = null;
            myIp = null;

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
            myIp = null;

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
        // tetap log ke console untuk debug
        Debug.Log("[SRV] " + msg);

        // 1. Filter heartbeat
        if (msg == "PING" || msg == "PONG" || msg.StartsWith("HEARTBEAT"))
        {
            // ini pesan rutin, gak usah ditampilin di UI
            return;
        }

        // 2. Info IP kita
        if (msg.StartsWith("YOURIP:"))
        {
            myIp = msg.Substring("YOURIP:".Length).Trim();
            // gak ditampilin supaya UI gak rame
            // tapi logika lanjut tetap jalan
            if (!string.IsNullOrWhiteSpace(clientId))
            {
                _ = SendRaw($"HELLO_FROM:{clientId} IP:{myIp}");
                Debug.Log($"[WS-CLIENT] SEND -> HELLO_FROM:{clientId} IP:{myIp}");
            }
            return;
        }

        // 3. Pesan pairing dari server
        if (msg.StartsWith("PAIR_OK_IP:"))
        {
            pairedIp = msg.Substring("PAIR_OK_IP:".Length).Trim();
            pairedId = null;

            // kita tetap mau ganti panel, tapi gak usah tulis teksnya
           // ShowPanelConnected();
            return;
        }

        if (msg.StartsWith("PAIR_OK_WITH_IP:"))
        {
            pairedIp = msg.Substring("PAIR_OK_WITH_IP:".Length).Trim();
            pairedId = null;

            SetStatus("📨 " + msg);

            //ShowPanelConnected();
            return;
        }

        if (msg.StartsWith("PAIR_OK:"))
        {
            pairedId = msg.Substring("PAIR_OK:".Length).Trim();
            pairedIp = null;

            //ShowPanelConnected();
            return;
        }

        // 4. Unpair
        if (msg.StartsWith("UNPAIRED_BY_SERVER") || msg.StartsWith("UNPAIRED_BY_PARTNER"))
        {
            pairedIp = null;
            pairedId = null;

            // silent
            return;
        }

        // 5. Pesan internal antar client yg biasanya gak mau ditampilin
        // contoh: HELLO_FROM:..., SETID:..., LISTCLIENTS, LISTDETAIL
        if (msg.StartsWith("HELLO_FROM:")
            || msg.StartsWith("SETID:")
            || msg.StartsWith("LISTCLIENTS")
            || msg.StartsWith("LISTDETAIL"))
        {
            // jangan tampilkan ke UI
            return;
        }

        // 6. Pesan lain yang "beneran" boleh ditampilkan
        // misal kamu nanti kirim "DATA:..." dari server
        // di sini baru kita tampilkan
        //SetStatus("📨 " + msg);
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

    // ================== PANEL SETELAH CONNECT ==================
    void ShowInputAfterConnected()
    {
        // setelah connect, kita gak mau panelConnected
        if (panelConnected) panelConnected.SetActive(true);

        // panel connecting juga dimatiin karena udah connect
        if (panelConnecting) panelConnecting.SetActive(false);

        // yang ditampilin lagi panel input
        if (panelInput) panelInput.SetActive(false);

        SetStatus("✅ . Kamu sudah terhubung Ke Server.");
        StartCoroutine(HideDelayDebug(5));
    }

    private IEnumerator HideDelayDebug(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (panelInput) panelInput.SetActive(false);
        if (panelConnected) panelConnected.SetActive(false);
        if (GameMenu != null) GameMenu.SetActive(true);


    }

    private IEnumerator ShowInputDelayed(float delay)
    {
        // pastikan bener-bener sudah open
        float t = 0f;
        while (!IsConnected() && t < 1f)
        {
            t += Time.deltaTime;
            yield return null;
        }

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        ShowInputAfterConnected();
    }

    private IEnumerator SendHelloAfterDelay(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (!string.IsNullOrWhiteSpace(clientId))
        {
            _ = SendRaw($"SETID:{clientId}");
            Debug.Log("[WS-CLIENT] SEND -> SETID:" + clientId);
        }

        if (enableHeartbeat)
        {
            if (heartbeatCo != null) StopCoroutine(heartbeatCo);
            heartbeatCo = StartCoroutine(HeartbeatLoop());
        }
    }

    // ================== KIRIM PESAN MANUAL ==================
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
        }
        else
        {
            _ = SendRaw($"TOIP:{pairedIp}|{msg}");
        }
    }
}
