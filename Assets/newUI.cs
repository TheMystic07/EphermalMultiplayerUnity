using System;
using System.Threading.Tasks;
using Solana.Unity.Rpc.Types;
using Solana.Unity.Wallet;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ReSharper disable once CheckNamespace
public class NewUI : MonoBehaviour
{
    #region Serialized Fields
    [Header("References")]
    [SerializeField] private NewBombermanClient m_Client;
    [SerializeField] private TextMeshProUGUI m_StatusText;

    [Header("Buttons - Setup")] 
    [SerializeField] private Button m_CreateWalletButton;
    [SerializeField] private Button m_EnsureButton;
    [SerializeField] private Button m_DelegateButton;
    [SerializeField] private Button m_UndelegateButton;
    [SerializeField] private Button m_InitGameButton;
    [SerializeField] private Button m_JoinGameButton;

    [Header("Buttons - Moves (chain)")]
    [SerializeField] private Button m_MoveUpButton;
    [SerializeField] private Button m_MoveDownButton;
    [SerializeField] private Button m_MoveLeftButton;
    [SerializeField] private Button m_MoveRightButton;

    [Header("Buttons - Moves (rollup)")]
    [SerializeField] private Button m_RollupUpButton;
    [SerializeField] private Button m_RollupDownButton;
    [SerializeField] private Button m_RollupLeftButton;
    [SerializeField] private Button m_RollupRightButton;

    [Header("Grid Settings")] 
    [SerializeField] private int m_GridSize = 50;
    #endregion

    #region Private Fields
    private Account m_Wallet;
    private PublicKey m_GamePda;
    private PublicKey m_PlayerPda;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (m_CreateWalletButton != null) m_CreateWalletButton.onClick.AddListener(CreateWallet);
        if (m_EnsureButton != null) m_EnsureButton.onClick.AddListener(EnsureAll);
        if (m_DelegateButton != null) m_DelegateButton.onClick.AddListener(Delegate);
        if (m_UndelegateButton != null) m_UndelegateButton.onClick.AddListener(Undelegate);
        if (m_InitGameButton != null) m_InitGameButton.onClick.AddListener(InitializeGame);
        if (m_JoinGameButton != null) m_JoinGameButton.onClick.AddListener(JoinGame);

        if (m_MoveUpButton != null) m_MoveUpButton.onClick.AddListener(() => MoveChain(0, 1));
        if (m_MoveDownButton != null) m_MoveDownButton.onClick.AddListener(() => MoveChain(0, -1));
        if (m_MoveLeftButton != null) m_MoveLeftButton.onClick.AddListener(() => MoveChain(-1, 0));
        if (m_MoveRightButton != null) m_MoveRightButton.onClick.AddListener(() => MoveChain(1, 0));

        if (m_RollupUpButton != null) m_RollupUpButton.onClick.AddListener(() => MoveRollup(0, 1));
        if (m_RollupDownButton != null) m_RollupDownButton.onClick.AddListener(() => MoveRollup(0, -1));
        if (m_RollupLeftButton != null) m_RollupLeftButton.onClick.AddListener(() => MoveRollup(-1, 0));
        if (m_RollupRightButton != null) m_RollupRightButton.onClick.AddListener(() => MoveRollup(1, 0));
    }

    private void OnDisable()
    {
        if (m_CreateWalletButton != null) m_CreateWalletButton.onClick.RemoveAllListeners();
        if (m_EnsureButton != null) m_EnsureButton.onClick.RemoveAllListeners();
        if (m_MoveUpButton != null) m_MoveUpButton.onClick.RemoveAllListeners();
        if (m_MoveDownButton != null) m_MoveDownButton.onClick.RemoveAllListeners();
        if (m_MoveLeftButton != null) m_MoveLeftButton.onClick.RemoveAllListeners();
        if (m_MoveRightButton != null) m_MoveRightButton.onClick.RemoveAllListeners();
        if (m_DelegateButton != null) m_DelegateButton.onClick.RemoveAllListeners();
        if (m_UndelegateButton != null) m_UndelegateButton.onClick.RemoveAllListeners();
        if (m_InitGameButton != null) m_InitGameButton.onClick.RemoveAllListeners();
        if (m_JoinGameButton != null) m_JoinGameButton.onClick.RemoveAllListeners();
        if (m_RollupUpButton != null) m_RollupUpButton.onClick.RemoveAllListeners();
        if (m_RollupDownButton != null) m_RollupDownButton.onClick.RemoveAllListeners();
        if (m_RollupLeftButton != null) m_RollupLeftButton.onClick.RemoveAllListeners();
        if (m_RollupRightButton != null) m_RollupRightButton.onClick.RemoveAllListeners();
    }
    #endregion

    #region UI Actions
    public void CreateWallet()
    {
        try
        {
            m_Wallet = new Account();
            m_Client.Initialize(m_Wallet);
            m_GamePda = NewBombermanClient.DeriveGamePda(m_Wallet.PublicKey);
            m_PlayerPda = NewBombermanClient.DerivePlayerStatePda(m_Wallet.PublicKey);
            SetStatus($"Wallet created: {m_Wallet.PublicKey}");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            SetStatus($"Create wallet failed: {ex.Message}");
        }
    }

    public async void InitializeGame()
    {
        try
        {
            if (!Precheck()) return;
            var res = await m_Client.InitializeGameForAuthority(m_Wallet.PublicKey);
            SetStatus(res.WasSuccessful ? "Game initialized" : $"Init failed: {res.Reason}");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            SetStatus($"Init error: {ex.Message}");
        }
    }

    public async void JoinGame()
    {
        try
        {
            if (!Precheck()) return;
            var res = await m_Client.JoinGameForPlayer(m_Wallet.PublicKey, m_Wallet.PublicKey);
            SetStatus(res.WasSuccessful ? "Joined game" : $"Join failed: {res.Reason}");
            // Refresh PDAs
            m_GamePda = NewBombermanClient.DeriveGamePda(m_Wallet.PublicKey);
            m_PlayerPda = NewBombermanClient.DerivePlayerStatePda(m_Wallet.PublicKey);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            SetStatus($"Join error: {ex.Message}");
        }
    }
    public async void Delegate()
    {
        try
        {
            if (!Precheck()) return;
            if (m_PlayerPda == null || string.IsNullOrEmpty(m_PlayerPda.Key))
            {
                m_PlayerPda = NewBombermanClient.DerivePlayerStatePda(m_Wallet.PublicKey);
            }
            // Skip if already delegated to rollup (mirrors TS test)
            var delegated = await m_Client.IsPlayerDelegated(m_PlayerPda);
            if (delegated)
            {
                SetStatus("Player PDA is already delegated");
                return;
            }
            var res = await m_Client.DelegatePlayerAuto(m_PlayerPda);
            SetStatus(res.WasSuccessful ? "Delegated to rollup" : $"Delegate failed: {res.Reason}");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            SetStatus($"Delegate error: {ex.Message}");
        }
    }

    public async void Undelegate()
    {
        try
        {
            if (!Precheck()) return;
            if (m_PlayerPda == null || string.IsNullOrEmpty(m_PlayerPda.Key))
            {
                m_PlayerPda = NewBombermanClient.DerivePlayerStatePda(m_Wallet.PublicKey);
            }
            var res = await m_Client.UndelegatePlayer(m_PlayerPda);
            SetStatus(res.WasSuccessful ? "Undelegated" : $"Undelegate failed: {res.Reason}");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            SetStatus($"Undelegate error: {ex.Message}");
        }
    }
    public async void EnsureAll()
    {
        try
        {
            if (m_Wallet == null) { SetStatus("Create wallet first"); return; }
            await m_Client.EnsureGameAndPlayer(m_Wallet.PublicKey, m_Wallet.PublicKey);
            SetStatus("Game and player ensured");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            SetStatus($"Ensure failed: {ex.Message}");
        }
    }

    private async void MoveChain(int dx, int dy)
    {
        try
        {
            if (!Precheck()) return;
            var state = await m_Client.GetPlayerState(m_PlayerPda, Commitment.Processed);
            if (state == null) { SetStatus("Player not found. Ensure first"); return; }
            int targetX = Mathf.Clamp(state.X + dx, 0, Mathf.Max(0, m_GridSize - 1));
            int targetY = Mathf.Clamp(state.Y + dy, 0, Mathf.Max(0, m_GridSize - 1));
            // Manhattan distance guard (<=1)
            int dist = Mathf.Abs(targetX - state.X) + Mathf.Abs(targetY - state.Y);
            if (dist != 1)
            {
                SetStatus("Invalid movement: can only move 1 square at a time");
                return;
            }
            var res = await m_Client.MovePlayer(m_PlayerPda, (byte)targetX, (byte)targetY);
            SetStatus(res.WasSuccessful ? $"Moved to ({targetX},{targetY}) sig: {res.Result}" : $"Move failed: {res.Reason}");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            SetStatus($"Move error: {ex.Message}");
        }
    }

    private async void MoveRollup(int dx, int dy)
    {
        try
        {
            if (!Precheck()) return;
            var state = await m_Client.GetPlayerStateOnRollup(m_PlayerPda, Commitment.Processed);
            if (state == null) { SetStatus("Player not found. Ensure first"); return; }
            int targetX = Mathf.Clamp(state.X + dx, 0, Mathf.Max(0, m_GridSize - 1));
            int targetY = Mathf.Clamp(state.Y + dy, 0, Mathf.Max(0, m_GridSize - 1));
            // Manhattan distance guard (<=1)
            int dist = Mathf.Abs(targetX - state.X) + Mathf.Abs(targetY - state.Y);
            if (dist != 1)
            {
                SetStatus("Invalid movement: can only move 1 square at a time");
                return;
            }
            var res = await m_Client.MovePlayerOnRollup(m_PlayerPda, (byte)targetX, (byte)targetY);
            SetStatus(res.WasSuccessful ? $"Rollup moved to ({targetX},{targetY}) sig: {res.Result}" : $"Rollup move failed: {res.Reason}");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            SetStatus($"Rollup move error: {ex.Message}");
        }
    }
    #endregion

    #region Helpers
    private bool Precheck()
    {
        if (m_Client == null) { SetStatus("Client not set"); return false; }
        if (m_Wallet == null) { SetStatus("No wallet"); return false; }
        if (m_PlayerPda == null || string.IsNullOrEmpty(m_PlayerPda.Key))
        {
            m_PlayerPda = NewBombermanClient.DerivePlayerStatePda(m_Wallet.PublicKey);
        }
        return true;
    }

    private void SetStatus(string _message)
    {
        if (m_StatusText != null) m_StatusText.text = _message;
        Debug.Log(_message);
    }
    #endregion
}


