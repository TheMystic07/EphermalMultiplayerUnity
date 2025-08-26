using System;
using UnityEngine;
using BombermanProgram.Accounts;
using Solana.Unity.Wallet;

public class PlayerMovement : MonoBehaviour
{
    #region Serialized Fields
    [Header("Network")]
    [SerializeField] private BombermanClient m_BombermanClient;

    [Header("Grid Mapping")]
    [SerializeField] private float m_CellSize = 1f;
    [SerializeField] private Vector3 m_GridOrigin = Vector3.zero;
    [SerializeField] private int m_GridSize = 50;

    [Header("Input")]
    [SerializeField] private float m_MoveCooldownSeconds = 0.2f;
    [SerializeField] private bool m_SmoothMovement = true;
    [SerializeField, Range(0.01f, 1f)] private float m_SmoothDuration = 0.2f;
    #endregion

    #region Private Fields
    private PublicKey m_PlayerPda;
    private byte m_GridX;
    private byte m_GridY;
    private float m_LastMoveTime;
    private Vector3 m_TargetWorldPos;
    private bool m_HasTarget;
    #endregion

    #region Unity Lifecycle
    async void Awake()
    {
        if (m_BombermanClient == null)
        {
            Debug.LogError("PlayerMovement: BombermanClient reference is not set.");
            enabled = false;
            return;
        }

        if (!m_BombermanClient.HasWallet || m_BombermanClient.ActiveWalletPublicKey == null)
        {
            Debug.LogWarning("PlayerMovement: No active wallet found. Initialize BombermanClient first.");
            enabled = false;
            return;
        }

        var authority = m_BombermanClient.ActiveWalletPublicKey;
        await m_BombermanClient.EnsureGameExists(authority);
        await m_BombermanClient.EnsurePlayerJoined(authority, authority);

        m_PlayerPda = BombermanClient.DerivePlayerStatePda(authority);
        await m_BombermanClient.SubscribeToPlayerState(m_PlayerPda);
        m_BombermanClient.OnPlayerStateChanged += OnPlayerStateChanged;

        var state = await m_BombermanClient.GetPlayerState(m_PlayerPda, Solana.Unity.Rpc.Types.Commitment.Processed);
        if (state != null)
        {
            ApplyGridPosition((byte)state.X, (byte)state.Y);
            SnapToTarget();
        }
    }

    void OnDisable()
    {
        if (m_BombermanClient != null)
        {
            m_BombermanClient.OnPlayerStateChanged -= OnPlayerStateChanged;
        }
    }

    void Update()
    {
        HandleMovementInput();
    }

    void LateUpdate()
    {
        if (!m_SmoothMovement || !m_HasTarget) return;
        float t = Mathf.Clamp01(Time.deltaTime / Mathf.Max(0.01f, m_SmoothDuration));
        Vector3 pos = Vector3.Lerp(transform.position, m_TargetWorldPos, t);
        Vector3 dir = (m_TargetWorldPos - transform.position);
        if (dir.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.z), Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, t);
        }
        transform.position = pos;
    }
    #endregion

    #region Network Callbacks
    private void OnPlayerStateChanged(PlayerState _state)
    {
        if (_state == null) return;
        ApplyGridPosition((byte)_state.X, (byte)_state.Y);
    }
    #endregion

    #region Input and Movement
    private void HandleMovementInput()
    {
        if (m_PlayerPda == null || m_PlayerPda.Key == null) return;

        int dx = 0;
        int dy = 0;
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) dy = 1;
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) dy = -1;
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) dx = 1;
        else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) dx = -1;

        if (dx == 0 && dy == 0) return;

        if (Time.time - m_LastMoveTime < m_MoveCooldownSeconds) return;
        m_LastMoveTime = Time.time;

        int newX = Mathf.Clamp(m_GridX + dx, 0, Mathf.Max(0, m_GridSize - 1));
        int newY = Mathf.Clamp(m_GridY + dy, 0, Mathf.Max(0, m_GridSize - 1));

        _ = m_BombermanClient.MovePlayer(m_PlayerPda, (byte)newX, (byte)newY);
    }
    #endregion

    #region Helpers
    private void ApplyGridPosition(byte _x, byte _y)
    {
        m_GridX = _x;
        m_GridY = _y;
        m_TargetWorldPos = GridToWorld(_x, _y);
        m_HasTarget = true;
    }

    private void SnapToTarget()
    {
        if (!m_HasTarget) return;
        transform.position = m_TargetWorldPos;
    }

    private Vector3 GridToWorld(int _x, int _y)
    {
        return m_GridOrigin + new Vector3(_x * m_CellSize, 0f, _y * m_CellSize);
    }
    #endregion
}


