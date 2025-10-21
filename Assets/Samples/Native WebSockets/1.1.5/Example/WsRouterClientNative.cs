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
    public Button btnPairWithSelected;   // << tombol untuk connect antar client (PAIR)

    [Header("UI - Client List & Message")]
    public TMP_Dropdown clientsDropdown;
    public TMP_InputField messageInput;

    [Header("UI - Status Text (debug/status)")]
    public TMP_Text statusText;          // tampil di panel yang aktif (termasuk ClientStatusText)

    [Header("Panels")]
    public GameObject ParentIpDanPort;   // Panel input server (IP/Port)
    public GameObject ParentStatusText;  // Panel "Menghubungkan..." (ditampilkan saat proses connect)
    public GameObject ClientStatusText;  // Panel setelah connect (daftar client, tombol pair, dsb)

    [Header("Opsi")]
    public bool hideSelfInLists = true;

    // ================== Internal ==================
    private WebSocket ws;
    private string myIpFromServer = "-";

    private readonly Dictionary<string, ClientDetail> clientsByIp = new();
    private readonly List<string> dropdownIndexToIp = new();

    private Coroutine currentTransition;

    // PlayerPrefs keys
    const string PREF_IP_KEY   = "WsRouter_LastIP";
    const string PREF_PORT_KEY = "WsRouter_LastPort";

    [Serializable]
    public class ClientDetail
    {
        public string ip = "-";
        public string id = "-";
    }

    // ================== Unity Lifecycle ==================
    async void Start()
    {
        Application.runInBackground = true;

        // Load prefs
        LoadServerPrefs();

        // Prefill input
        if (ipInput)   ipInput.text  = serverHost;
        if (portInput) portInput.text = serverPort.ToString();

        WireButtons();

        // State awal UI
        ShowPanel_Input(); // tampilkan panel IP/Port
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

        if (btnRefresh)        btnRefresh.onClick.AddListener(RequestClients);
        if (btnSendToID)       btnSendToID.onClick.AddListener(SendToSelectedID);
        if (btnSendToIP)       btnSendToIP.onClick.AddListener(SendToSelectedIP);
        if (btnPairWithSelected) btnPairWithSelected.onClick.AddListener(PairWithSelected);

        // Simpan otomatis saat selesai edit
        if (ipInput)   ipInput.onEndEdit.AddListener(_ => { ReadInputsIntoDefaults(); SaveServerPrefs(); });
        if (portInput) portInput.onEndEdit.AddListener(_ => { ReadInputsIntoDefaults(); SaveServerPrefs(); });
    }

    // ================== Connect Flow ==================
    public async Task Connect()
    {
        await SafeClose();

        // Pastikan ambil nilai terbaru dari input
        ReadInputsIntoDefaults();
        SaveServerPrefs();

        // Tampilkan panel "Menghubungkan..."
        ShowPanel_Connecting($"🔌 Menghubungkan ke ws://{serverHost}:{serverPort} ...");

        ws = new WebSocket($"ws://{serverHost}:{serverPort}");

        ws.OnOpen += () =>
        {
            // Setelah berhasil open, delay 3 detik lalu tampilkan panel client
            StartPanelTransition(DelayShowClientPanel(3f));
        };

        ws.OnClose += (code) =>
        {
            // Saat terputus: tampilkan dulu panel status, delay 3 detik, lalu balik ke panel input
            SetStatus("⚠️ Koneksi terputus. Mengalihkan tampilan...");
            StartPanelTransition(DelayShowDisconnectedPanel(3f));
        };

        ws.OnError += (err) =>
        {
            SetStatus($"❌ Error: {err}. Mengalihkan tampilan...");
            StartPanelTransition(DelayShowDisconnectedPanel(3f));
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
            SetStatus($"❌ Gagal terhubung: {ex.Message}. Mengalihkan tampilan...");
            StartPanelTransition(DelayShowDisconnectedPanel(3f));
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
        // tampilkan panel status (proses transisi)
        if (ParentStatusText) ParentStatusText.SetActive(true);
        if (ClientStatusText) ClientStatusText.SetActive(false);

        yield return new WaitForSeconds(delaySec);

        // kembali ke panel IP/Port
        ShowPanel_Input();
        currentTransition = null;
    }

    void ShowPanel_Input()
    {
        if (ParentIpDanPort)  ParentIpDanPort.SetActive(true);
        if (ParentStatusText) ParentStatusText.SetActive(false);
        if (ClientStatusText) ClientStatusText.SetActive(false);
    }

    void ShowPanel_Connecting(string status)
    {
        if (ParentIpDanPort)  ParentIpDanPort.SetActive(false);
        if (ParentStatusText) ParentStatusText.SetActive(true);
        if (ClientStatusText) ClientStatusText.SetActive(false);
        SetStatus(status);
    }

    void ShowPanel_Client()
    {
        if (ParentIpDanPort)  ParentIpDanPort.SetActive(false);
        if (ParentStatusText) ParentStatusText.SetActive(false);
        if (ClientStatusText) ClientStatusText.SetActive(true);
    }

    // ================== Server Messages ==================
    void HandleServerText(string msg)
    {
        // Debug ke status
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

        // Pairing feedback
        if (msg.StartsWith("PAIR_OK:"))
        {
            var id = msg.Substring("PAIR_OK:".Length).Trim();
            SetStatus($"✅ Berhasil terhubung dengan ID: {id}");
            // Sembunyikan panel client status bila diminta (UI client status text hilang)
            if (ClientStatusText) ClientStatusText.SetActive(false);
            return;
        }
        if (msg.StartsWith("PAIR_OK_IP:"))
        {
            var ip = msg.Substring("PAIR_OK_IP:".Length).Trim();
            SetStatus($"✅ Berhasil terhubung dengan IP: {ip}");
            if (ClientStatusText) ClientStatusText.SetActive(false);
            return;
        }

        if (msg.StartsWith("PAIR_ERR:"))
        {
            SetStatus($"❌ Pairing gagal: {msg}");
            return;
        }

        if (msg.StartsWith("PAIR_OK_WITH") || msg.StartsWith("PAIR_OK_WITH_IP"))
        {
            // notifikasi ke sisi partner—cukup log
            SetStatus($"ℹ️ Notifikasi pasangan: {msg}");
            return;
        }

        // Pesan lain
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
        if (ws == null || ws.State != WebSocketState.Open) return;
        await ws.SendText(text);
        Debug.Log("[WS->SRV] " + text);
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
