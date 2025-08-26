using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Solana.Unity.Wallet;
using Solana.Unity.Rpc.Types;
using BombermanProgram;
using BombermanProgram.Accounts;
using BombermanProgram.Types;
using BombermanProgram.Program;
using BombermanProgram.Errors;

// ReSharper disable once CheckNamespace
public class GamePlayersController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NewBombermanClient m_Client;
    [SerializeField] private Transform m_PlayersRoot;
    [SerializeField] private GameObject m_PlayerPrefab;

    [Header("Config")] 
    [SerializeField] private int m_GridSize = 50;
    [SerializeField] private float m_CellSize = 1f;
    [SerializeField] private float m_PollIntervalSeconds = 0.35f;
    [SerializeField] private bool m_UseRollup = true;
    [SerializeField] private bool m_CreateDefaultsIfMissing = true;

    [Header("Local Player")] 
    [SerializeField] private bool m_HandleKeyboardInput = true;

    private PublicKey m_GamePda;
    private PublicKey m_LocalPlayerPda;
    private readonly Dictionary<string, GameObject> m_PlayerKeyToObject = new Dictionary<string, GameObject>();
    private bool m_IsPolling;

    public void Initialize(PublicKey gamePda, PublicKey localPlayerPda)
    {
        m_GamePda = gamePda;
        m_LocalPlayerPda = localPlayerPda;
        if (m_PlayersRoot == null && m_CreateDefaultsIfMissing)
        {
            var root = new GameObject("PlayersRoot");
            root.transform.SetParent(transform, false);
            m_PlayersRoot = root.transform;
        }
        if (!m_IsPolling)
        {
            m_IsPolling = true;
            _ = PollLoop();
        }
        // Kick a first refresh immediately
        _ = RefreshPlayers();
    }

    private async Task PollLoop()
    {
        var wait = new WaitForSeconds(m_PollIntervalSeconds);
        while (m_IsPolling)
        {
            try
            {
                await RefreshPlayers();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Players poll error: {ex.Message}");
            }
            await AwaitSeconds(m_PollIntervalSeconds);
        }
    }

    private static async Task AwaitSeconds(float seconds)
    {
        var ms = Mathf.Max(0, Mathf.RoundToInt(seconds * 1000f));
        await Task.Delay(ms);
    }

    private async Task RefreshPlayers()
    {
        if (m_Client == null || m_GamePda == null || string.IsNullOrEmpty(m_GamePda.Key)) return;
        var players = await m_Client.GetPlayersForGame(m_GamePda, m_UseRollup, Commitment.Processed);
        if (players == null) players = Array.Empty<PlayerState>();
        Debug.Log($"[GamePlayersController] players fetched: {players.Count} (rollup={m_UseRollup}) game={m_GamePda}");
        // Fallback: if rollup returns zero, try chain once
        if (players.Count == 0 && m_UseRollup)
        {
            // Try direct PDA fetch for local player like Anchor fetch
            if (m_LocalPlayerPda != null && !string.IsNullOrEmpty(m_LocalPlayerPda.Key))
            {
                var psRollup = await m_Client.FetchPlayerOnRollupByPda(m_LocalPlayerPda, Commitment.Processed);
                if (psRollup != null)
                {
                    players = new List<PlayerState> { psRollup };
                    Debug.Log("[GamePlayersController] fetched local player directly from rollup by PDA");
                }
            }
            // Then fallback to chain list
            if (players.Count == 0)
            {
                var chainPlayers = await m_Client.GetPlayersForGame(m_GamePda, false, Commitment.Processed);
                if (chainPlayers != null && chainPlayers.Count > 0)
                {
                    players = chainPlayers;
                    Debug.Log("[GamePlayersController] fallback to chain players list");
                }
            }
        }
        var seen = new HashSet<string>();

        foreach (var ps in players)
        {
            if (ps.Player == null) continue;
            var key = ps.Player.Key;
            seen.Add(key);
            if (!m_PlayerKeyToObject.TryGetValue(key, out var go) || go == null)
            {
                go = CreatePlayerVisual();
                if (m_PlayersRoot != null) go.transform.SetParent(m_PlayersRoot, false);
                m_PlayerKeyToObject[key] = go;
                go.name = $"Player_{key.Substring(0, 6)}";
            }
            var worldPos = GridToWorld(ps.X, ps.Y);
            go.transform.localPosition = worldPos;
            // If this is the local player, optionally tint
            if (m_LocalPlayerPda != null && !string.IsNullOrEmpty(m_LocalPlayerPda.Key) && ps.Player != null && ps.Player.Equals(m_LocalPlayerPda))
            {
                var r = go.GetComponent<Renderer>();
                if (r != null) r.material.color = Color.yellow;
            }
        }

        // Remove stale
        var toRemove = new List<string>();
        foreach (var kvp in m_PlayerKeyToObject)
        {
            if (!seen.Contains(kvp.Key)) toRemove.Add(kvp.Key);
        }
        foreach (var k in toRemove)
        {
            if (m_PlayerKeyToObject.TryGetValue(k, out var go) && go != null) Destroy(go);
            m_PlayerKeyToObject.Remove(k);
        }
    }

    private Vector3 GridToWorld(int x, int y)
    {
        return new Vector3(x * m_CellSize, 0f, y * m_CellSize);
    }

    private GameObject CreatePlayerVisual()
    {
        if (m_PlayerPrefab != null)
        {
            return Instantiate(m_PlayerPrefab);
        }
        // Fallback: create a colored sphere
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.localScale = Vector3.one * (m_CellSize * 0.8f);
        var rnd = new System.Random(Guid.NewGuid().GetHashCode());
        var color = new Color((float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble());
        var renderer = sphere.GetComponent<Renderer>();
        if (renderer != null)
        {
            var mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            renderer.material = mat;
        }
        return sphere;
    }

    private async void Update()
    {
        if (!m_HandleKeyboardInput || m_Client == null || m_LocalPlayerPda == null || string.IsNullOrEmpty(m_LocalPlayerPda.Key)) return;

        int dx = 0, dy = 0;
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) dy = 1;
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) dy = -1;
        else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) dx = -1;
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) dx = 1;
        else return;

        try
        {
            var state = m_UseRollup ? await m_Client.GetPlayerStateOnRollup(m_LocalPlayerPda, Commitment.Processed)
                                     : await m_Client.GetPlayerState(m_LocalPlayerPda, Commitment.Processed);
            if (state == null) { Debug.Log("Local player not initialized. Initialize/Join first."); return; }

            int targetX = Mathf.Clamp(state.X + dx, 0, Mathf.Max(0, m_GridSize - 1));
            int targetY = Mathf.Clamp(state.Y + dy, 0, Mathf.Max(0, m_GridSize - 1));
            int dist = Mathf.Abs(targetX - state.X) + Mathf.Abs(targetY - state.Y);
            if (dist != 1) return;

            var res = m_UseRollup ? await m_Client.MovePlayerOnRollup(m_LocalPlayerPda, (byte)targetX, (byte)targetY)
                                   : await m_Client.MovePlayer(m_LocalPlayerPda, (byte)targetX, (byte)targetY);
            if (!res.WasSuccessful)
            {
                Debug.LogWarning($"Move failed: {res.Reason}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }
}


