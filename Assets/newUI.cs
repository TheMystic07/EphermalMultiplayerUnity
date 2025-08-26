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
    [SerializeField] private GamePlayersController m_PlayersController;

    [Header("Buttons - Setup")] 
    [SerializeField] private Button m_CreateWalletButton;
    [SerializeField] private Button m_EnsureButton;
    [SerializeField] private Button m_DelegateButton;
    [SerializeField] private Button m_UndelegateButton;
    [SerializeField] private Button m_InitGameButton;
    [SerializeField] private Button m_JoinGameButton;
    [SerializeField] private Button m_LogPdasButton;
    [SerializeField] private Button m_FetchPdaDataButton;

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
        if (m_LogPdasButton != null) m_LogPdasButton.onClick.AddListener(LogPdas);
        if (m_FetchPdaDataButton != null) m_FetchPdaDataButton.onClick.AddListener(FetchPdaData);

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
        if (m_LogPdasButton != null) m_LogPdasButton.onClick.RemoveAllListeners();
        if (m_FetchPdaDataButton != null) m_FetchPdaDataButton.onClick.RemoveAllListeners();
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

            if (res.WasSuccessful && m_PlayersController != null)
            {
                m_PlayersController.Initialize(m_GamePda, m_PlayerPda);
            }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            SetStatus($"Join error: {ex.Message}");
        }
    }

    public void LogPdas()
    {
        if (m_Wallet == null)
        {
            SetStatus("No wallet. Create one first.");
            return;
        }
        m_GamePda = NewBombermanClient.DeriveGamePda(m_Wallet.PublicKey);
        m_PlayerPda = NewBombermanClient.DerivePlayerStatePda(m_Wallet.PublicKey);
        Debug.Log($"Wallet: {m_Wallet.PublicKey}");
        Debug.Log($"Game PDA: {m_GamePda}");
        Debug.Log($"Player PDA: {m_PlayerPda}");
        SetStatus("PDAs logged to Console.");
    }

    public async void FetchPdaData()
    {
        try
        {
            if (!Precheck()) return;
            await m_Client.DebugDumpPdas(m_Wallet.PublicKey, m_Wallet.PublicKey, true);
            // Fetch game by PDA via RPC (account info raw)
            // Prefer program fetchers over raw account-info
            var gameOnChain = await m_Client.FetchGameOnChainByPda(m_GamePda, Commitment.Processed);
            var playerOnChain = await m_Client.FetchPlayerOnChainByPda(m_PlayerPda, Commitment.Processed);
            var playerOnRollup = await m_Client.FetchPlayerOnRollupByPda(m_PlayerPda, Commitment.Processed);

            Debug.Log($"Game (chain) null? {gameOnChain == null}");
            if (gameOnChain != null)
            {
                Debug.Log($"Game gridSize={gameOnChain.GridSize} playerCount={gameOnChain.PlayerCount} state={gameOnChain.GameState}");
            }
            Debug.Log($"Player (chain) null? {playerOnChain == null}");
            if (playerOnChain != null)
            {
                Debug.Log($"[Chain] Player pos=({playerOnChain.X},{playerOnChain.Y}) alive={playerOnChain.IsAlive} idx={playerOnChain.PlayerIndex}");
            }
            Debug.Log($"Player (rollup) null? {playerOnRollup == null}");
            if (playerOnRollup != null)
            {
                Debug.Log($"[Rollup] Player pos=({playerOnRollup.X},{playerOnRollup.Y}) alive={playerOnRollup.IsAlive} idx={playerOnRollup.PlayerIndex}");
            }

            // Fetch player state on rollup first (if available), else chain
            var psRollup = await m_Client.GetPlayerStateOnRollup(m_PlayerPda, Commitment.Processed);
            var psChain = await m_Client.GetPlayerState(m_PlayerPda, Commitment.Processed);
            if (psRollup != null)
            {
                Debug.Log($"[Rollup] Player ({psRollup.Player}) pos=({psRollup.X},{psRollup.Y}) alive={psRollup.IsAlive} idx={psRollup.PlayerIndex}");
            }
            if (psChain != null)
            {
                Debug.Log($"[Chain] Player ({psChain.Player}) pos=({psChain.X},{psChain.Y}) alive={psChain.IsAlive} idx={psChain.PlayerIndex}");
            }
            SetStatus("Fetched PDA data. See Console.");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            SetStatus($"Fetch PDA data failed: {ex.Message}");
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
            if (state == null)
            {
                await m_Client.EnsureGameAndPlayer(m_Wallet.PublicKey, m_Wallet.PublicKey);
                state = await m_Client.GetPlayerState(m_PlayerPda, Commitment.Processed);
                if (state == null) { SetStatus("Player still not found after ensure."); return; }
            }
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
            if (state == null)
            {
                // Fallback to chain: if present there, user likely needs to delegate
                var chainState = await m_Client.GetPlayerState(m_PlayerPda, Commitment.Processed);
                if (chainState != null)
                {
                    SetStatus("Player not on rollup. Delegate first.");
                    return;
                }
                // Otherwise, ensure then retry once
                await m_Client.EnsureGameAndPlayer(m_Wallet.PublicKey, m_Wallet.PublicKey);
                state = await m_Client.GetPlayerStateOnRollup(m_PlayerPda, Commitment.Processed);
                if (state == null) { SetStatus("Player not found after ensure (rollup)."); return; }
            }
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


