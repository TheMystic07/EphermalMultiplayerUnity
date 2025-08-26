using System;
using System.Text;
using System.Threading.Tasks;
using Solana.Unity.Rpc;
using Solana.Unity.Rpc.Core.Http;
using Solana.Unity.Rpc.Types;
using Solana.Unity.Wallet;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ReSharper disable once CheckNamespace
public class BombermanUIManager : MonoBehaviour
{
    #region Constants
    private const ulong c_LamportsPerSol = 1_000_000_000UL;
    #endregion

    #region Serialized Fields
    [Header("References")]
    [SerializeField] private BombermanClient m_BombermanClient;

    [Header("UI Buttons")]
    [SerializeField] private Button m_CreateWalletButton;
    [SerializeField] private Button m_JoinGameButton;
    [SerializeField] private Button m_LogPdasButton;
    [SerializeField] private Button m_DelegateButton;
    [SerializeField] private Button m_UndelegateButton;
    [Header("Move Buttons")]
    [SerializeField] private Button m_MoveUpButton;
    [SerializeField] private Button m_MoveDownButton;
    [SerializeField] private Button m_MoveLeftButton;
    [SerializeField] private Button m_MoveRightButton;
    [Header("Rollup Move Buttons")]
    [SerializeField] private Button m_RollupMoveUpButton;
    [SerializeField] private Button m_RollupMoveDownButton;
    [SerializeField] private Button m_RollupMoveLeftButton;
    [SerializeField] private Button m_RollupMoveRightButton;

    [Header("UI Output")]
    [SerializeField] private TextMeshProUGUI m_StatusText;
    #endregion

    #region Private Fields
    [SerializeField] private string m_DevnetRpc = "https://api.devnet.solana.com";
    private Account m_CurrentWallet;
    private PublicKey m_GamePda;
    private PublicKey m_PlayerPda;
    [SerializeField] private int m_GridSize = 50;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (m_CreateWalletButton != null) m_CreateWalletButton.onClick.AddListener(CreateWalletAndAirdrop);
        if (m_JoinGameButton != null) m_JoinGameButton.onClick.AddListener(JoinGame);
        if (m_LogPdasButton != null) m_LogPdasButton.onClick.AddListener(LogPdas);
        if (m_DelegateButton != null) m_DelegateButton.onClick.AddListener(Delegate);
        if (m_UndelegateButton != null) m_UndelegateButton.onClick.AddListener(Undelegate);
        if (m_MoveUpButton != null) m_MoveUpButton.onClick.AddListener(() => MoveBy(0, 1));
        if (m_MoveDownButton != null) m_MoveDownButton.onClick.AddListener(() => MoveBy(0, -1));
        if (m_MoveLeftButton != null) m_MoveLeftButton.onClick.AddListener(() => MoveBy(-1, 0));
        if (m_MoveRightButton != null) m_MoveRightButton.onClick.AddListener(() => MoveBy(1, 0));
        if (m_RollupMoveUpButton != null) m_RollupMoveUpButton.onClick.AddListener(() => MoveOnRollup(0, 1));
        if (m_RollupMoveDownButton != null) m_RollupMoveDownButton.onClick.AddListener(() => MoveOnRollup(0, -1));
        if (m_RollupMoveLeftButton != null) m_RollupMoveLeftButton.onClick.AddListener(() => MoveOnRollup(-1, 0));
        if (m_RollupMoveRightButton != null) m_RollupMoveRightButton.onClick.AddListener(() => MoveOnRollup(1, 0));
    }

    private void OnDisable()
    {
        if (m_CreateWalletButton != null) m_CreateWalletButton.onClick.RemoveAllListeners();
        if (m_JoinGameButton != null) m_JoinGameButton.onClick.RemoveAllListeners();
        if (m_LogPdasButton != null) m_LogPdasButton.onClick.RemoveAllListeners();
        if (m_DelegateButton != null) m_DelegateButton.onClick.RemoveAllListeners();
        if (m_UndelegateButton != null) m_UndelegateButton.onClick.RemoveAllListeners();
        if (m_MoveUpButton != null) m_MoveUpButton.onClick.RemoveAllListeners();
        if (m_MoveDownButton != null) m_MoveDownButton.onClick.RemoveAllListeners();
        if (m_MoveLeftButton != null) m_MoveLeftButton.onClick.RemoveAllListeners();
        if (m_MoveRightButton != null) m_MoveRightButton.onClick.RemoveAllListeners();
        if (m_RollupMoveUpButton != null) m_RollupMoveUpButton.onClick.RemoveAllListeners();
        if (m_RollupMoveDownButton != null) m_RollupMoveDownButton.onClick.RemoveAllListeners();
        if (m_RollupMoveLeftButton != null) m_RollupMoveLeftButton.onClick.RemoveAllListeners();
        if (m_RollupMoveRightButton != null) m_RollupMoveRightButton.onClick.RemoveAllListeners();
    }
    #endregion

    #region UI Actions
    public async void CreateWalletAndAirdrop()
    {
        try
        {
            m_CurrentWallet = new Account();
            SetStatus($"Wallet created: {m_CurrentWallet.PublicKey}");

            if (m_BombermanClient == null)
            {
                Debug.LogError("BombermanClient reference not set");
                return;
            }

            m_BombermanClient.Initialize(m_CurrentWallet, m_DevnetRpc, m_DevnetRpc.Replace("https://", "wss://"));

            // Fund from private key via BombermanClient (no airdrop)
            var fundRes = await m_BombermanClient.FundActiveWalletFromInspectorKey();
            if (fundRes.WasSuccessful)
            {
                SetStatus($"Funding complete. Sig: {fundRes.Result}");
            }
            else
            {
                SetStatus($"Funding failed: {fundRes.Reason}");
            }

            // Precompute PDAs
            m_GamePda = BombermanClient.DeriveGamePda(m_CurrentWallet.PublicKey);
            m_PlayerPda = BombermanClient.DerivePlayerStatePda(m_CurrentWallet.PublicKey);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            SetStatus($"Create wallet failed: {ex.Message}");
        }
    }

    public async void JoinGame()
    {
        try
        {
            if (m_CurrentWallet == null)
            {
                SetStatus("No wallet. Create one first.");
                return;
            }

            // Ensure game is initialized first, then join
            await m_BombermanClient.EnsureGameExists(m_CurrentWallet.PublicKey);
            await m_BombermanClient.EnsurePlayerJoined(m_CurrentWallet.PublicKey, m_CurrentWallet.PublicKey);

            // Refresh PDAs
            m_GamePda = BombermanClient.DeriveGamePda(m_CurrentWallet.PublicKey);
            m_PlayerPda = BombermanClient.DerivePlayerStatePda(m_CurrentWallet.PublicKey);

            // Optional: subscribe to updates
            await m_BombermanClient.SubscribeToGame(m_GamePda);
            await m_BombermanClient.SubscribeToPlayerState(m_PlayerPda);

            SetStatus("Joined game and subscribed to updates.");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            SetStatus($"Join game failed: {ex.Message}");
        }
    }

    public void LogPdas()
    {
        if (m_CurrentWallet == null)
        {
            SetStatus("No wallet. Create one first.");
            return;
        }

        m_GamePda = BombermanClient.DeriveGamePda(m_CurrentWallet.PublicKey);
        m_PlayerPda = BombermanClient.DerivePlayerStatePda(m_CurrentWallet.PublicKey);

        var delegationProgram = new PublicKey("DELeGGvXpWV2fqJUhqcF5ZSYMS4JTLjteaAMARRSaeSh");
        var programId = new PublicKey(BombermanProgram.Program.BombermanProgramProgram.ID);

        var bufferPlayer = FindBufferPda("buffer", m_PlayerPda, programId);
        var delegationRecordPlayer = FindDelegationProgramPda("delegation", m_PlayerPda, delegationProgram);
        var delegationMetadataPlayer = FindDelegationProgramPda("delegation-metadata", m_PlayerPda, delegationProgram);

        Debug.Log($"Wallet: {m_CurrentWallet.PublicKey}");
        Debug.Log($"Game PDA: {m_GamePda}");
        Debug.Log($"Player PDA: {m_PlayerPda}");
        Debug.Log($"Buffer Player: {bufferPlayer}");
        Debug.Log($"Delegation Record Player: {delegationRecordPlayer}");
        Debug.Log($"Delegation Metadata Player: {delegationMetadataPlayer}");
        SetStatus("PDAs logged to Console.");
    }

    public async void Delegate()
    {
        try
        {
            if (m_CurrentWallet == null)
            {
                SetStatus("No wallet. Create one first.");
                return;
            }
            if (m_PlayerPda == null || m_PlayerPda.Key == null)
            {
                m_PlayerPda = BombermanClient.DerivePlayerStatePda(m_CurrentWallet.PublicKey);
            }

            var res = await m_BombermanClient.DelegatePlayerAuto(m_PlayerPda);
            SetStatus(res.WasSuccessful ? "Delegated to rollup." : $"Delegate failed: {res.Reason}");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            SetStatus($"Delegate failed: {ex.Message}");
        }
    }

    public async void Undelegate()
    {
        try
        {
            if (m_CurrentWallet == null)
            {
                SetStatus("No wallet. Create one first.");
                return;
            }
            if (m_PlayerPda == null || m_PlayerPda.Key == null)
            {
                m_PlayerPda = BombermanClient.DerivePlayerStatePda(m_CurrentWallet.PublicKey);
            }

            var res = await m_BombermanClient.UndelegatePlayerAuto(m_PlayerPda);
            SetStatus(res.WasSuccessful ? "Undelegated from rollup." : $"Undelegate failed: {res.Reason}");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            SetStatus($"Undelegate failed: {ex.Message}");
        }
    }

    public async void TestRpcConnection()
    {
        try
        {
            if (m_BombermanClient == null)
            {
                SetStatus("BombermanClient not initialized");
                return;
            }

            var status = await m_BombermanClient.GetRpcStatus();
            SetStatus($"RPC Status:\n{status}");
            
            // Also test getting a block hash
            try
            {
                var blockHash = await m_BombermanClient.GetRecentBlockHashWithAutoRecovery();
                SetStatus($"RPC Status:\n{status}\n\nBlock Hash Test: SUCCESS - {blockHash.Substring(0, 8)}...");
            }
            catch (Exception ex)
            {
                SetStatus($"RPC Status:\n{status}\n\nBlock Hash Test: FAILED - {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            SetStatus($"RPC test failed: {ex.Message}");
        }
    }

    public async void SwitchRpcEndpoint()
    {
        try
        {
            if (m_BombermanClient == null)
            {
                SetStatus("BombermanClient not initialized");
                return;
            }

            await m_BombermanClient.SwitchToDifferentRpc();
            SetStatus("RPC endpoint switched. Test connection to verify.");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            SetStatus($"RPC switch failed: {ex.Message}");
        }
    }

    public async void MoveOnRollup(int dx, int dy)
    {
        try
        {
            if (m_CurrentWallet == null)
            {
                SetStatus("No wallet. Create one first.");
                return;
            }
            if (m_BombermanClient == null)
            {
                SetStatus("Client not initialized.");
                return;
            }

            if (m_PlayerPda == null || m_PlayerPda.Key == null)
            {
                m_PlayerPda = BombermanClient.DerivePlayerStatePda(m_CurrentWallet.PublicKey);
            }

            var state = await m_BombermanClient.GetPlayerState(m_PlayerPda, Solana.Unity.Rpc.Types.Commitment.Processed);
            if (state == null)
            {
                // Attempt auto-join if not present
                await m_BombermanClient.EnsurePlayerJoined(m_CurrentWallet.PublicKey, m_CurrentWallet.PublicKey);
                state = await m_BombermanClient.GetPlayerState(m_PlayerPda, Solana.Unity.Rpc.Types.Commitment.Processed);
                if (state == null)
                {
                    SetStatus("Player state not found. Join a game first.");
                    return;
                }
            }

            int newX = Mathf.Clamp(state.X + dx, 0, Mathf.Max(0, m_GridSize - 1));
            int newY = Mathf.Clamp(state.Y + dy, 0, Mathf.Max(0, m_GridSize - 1));

            SetStatus($"Moving on rollup to ({newX},{newY})...");
            var res = await m_BombermanClient.MovePlayerOnRollup((byte)newX, (byte)newY, m_PlayerPda);
            
            if (res.WasSuccessful)
            {
                SetStatus($"Rollup move successful! Position: ({newX},{newY}). Sig: {res.Result}");
            }
            else
            {
                SetStatus($"Rollup move failed: {res.Reason}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            SetStatus($"Rollup move error: {ex.Message}");
        }
    }
    #endregion

    #region Helpers
    private async void MoveBy(int dx, int dy)
    {
        try
        {
            if (m_CurrentWallet == null)
            {
                SetStatus("No wallet. Create one first.");
                return;
            }
            if (m_BombermanClient == null)
            {
                SetStatus("Client not initialized.");
                return;
            }

            if (m_PlayerPda == null || m_PlayerPda.Key == null)
            {
                m_PlayerPda = BombermanClient.DerivePlayerStatePda(m_CurrentWallet.PublicKey);
            }

            var state = await m_BombermanClient.GetPlayerState(m_PlayerPda, Solana.Unity.Rpc.Types.Commitment.Processed);
            if (state == null)
            {
                // Attempt auto-join if not present
                await m_BombermanClient.EnsurePlayerJoined(m_CurrentWallet.PublicKey, m_CurrentWallet.PublicKey);
                state = await m_BombermanClient.GetPlayerState(m_PlayerPda, Solana.Unity.Rpc.Types.Commitment.Processed);
                if (state == null)
                {
                    SetStatus("Player state not found. Join a game first.");
                    return;
                }
            }

            int newX = Mathf.Clamp(state.X + dx, 0, Mathf.Max(0, m_GridSize - 1));
            int newY = Mathf.Clamp(state.Y + dy, 0, Mathf.Max(0, m_GridSize - 1));

            var res = await m_BombermanClient.MovePlayer(m_PlayerPda, (byte)newX, (byte)newY);
            if (res.WasSuccessful)
            {
                SetStatus($"Moved to ({newX},{newY}). Sig: {res.Result}");
            }
            else
            {
                SetStatus($"Move failed: {res.Reason}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            SetStatus($"Move error: {ex.Message}");
        }
    }
    private void SetStatus(string _message)
    {
        if (m_StatusText != null) m_StatusText.text = _message;
        Debug.Log(_message);
    }

    private static PublicKey FindDelegationProgramPda(string seed, PublicKey account, PublicKey delegationProgram)
    {
        PublicKey.TryFindProgramAddress(new[] { Encoding.UTF8.GetBytes(seed), account.KeyBytes }, delegationProgram, out var pda, out _);
        return pda;
    }

    private static PublicKey FindBufferPda(string seed, PublicKey account, PublicKey ownerProgram)
    {
        PublicKey.TryFindProgramAddress(new[] { Encoding.UTF8.GetBytes(seed), account.KeyBytes }, ownerProgram, out var pda, out _);
        return pda;
    }
    #endregion
}


