using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using BombermanProgram;
using BombermanProgram.Accounts;
using BombermanProgram.Program;
using BombermanProgram.Types;
using Solana.Unity.Programs;
using Solana.Unity.Rpc;
using Solana.Unity.Rpc.Builders;
using Solana.Unity.Rpc.Core.Http;
using Solana.Unity.Rpc.Core.Sockets;
using Solana.Unity.Rpc.Types;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Wallet;
using UnityEngine;
using Solana.Unity.SDK;

// ReSharper disable once CheckNamespace
public class BombermanClient : MonoBehaviour
{
    #region Private Fields
    [Header("RPC Configuration")]
    [SerializeField] private string m_RpcUrl = "https://api.devnet.solana.com";
    [SerializeField] private string m_WebsocketUrl = "wss://api.devnet.solana.com";
    [SerializeField] private string m_MagicBlockRpcUrl = "https://devnet.magicblock.app";
    [SerializeField] private string m_MagicBlockWsUrl = "wss://devnet.magicblock.app";
    [SerializeField] private bool m_UseComputeBudget = true;
    [SerializeField] private uint m_ComputeUnitLimit = 100_000;
    [SerializeField] private ulong m_ComputeUnitPriceMicroLamports = 1000; // 0.000001 SOL per 1,000,000 CUs
    [Header("Funding")]
    [SerializeField] private string m_FunderPrivateKeyBase58 = ""; // Optional inspector key for funding
    [SerializeField] private ulong m_DefaultFundingLamports = 1_000_000_000UL; // 1 SOL

    private IRpcClient m_RpcClient; // active
    private IStreamingRpcClient m_StreamingRpcClient; // active
    private IRpcClient m_DefaultRpcClient;
    private IStreamingRpcClient m_DefaultStreamingRpcClient;
    private IRpcClient m_MagicRpcClient;
    private IStreamingRpcClient m_MagicStreamingRpcClient;
    private BombermanProgramClient m_Client;
    private Account m_Wallet;

    private SubscriptionState m_GameSubscription;
    private SubscriptionState m_PlayerStateSubscription;
    private PublicKey m_LastGameSub;
    private PublicKey m_LastPlayerStateSub;
    private bool m_IsDelegated = false;

    private static readonly PublicKey s_ProgramId = new PublicKey(BombermanProgramProgram.ID);
    private static readonly PublicKey s_DelegationProgram = new PublicKey("DELeGGvXpWV2fqJUhqcF5ZSYMS4JTLjteaAMARRSaeSh");
    #endregion

    #region Public Events
    public event Action<Game> OnGameChanged;
    public event Action<PlayerState> OnPlayerStateChanged;
    public event Action<bool> OnDelegationStatusChanged; // true if delegated to rollup
    public event Action<string, string> OnRpcSwitched; // (rpcUrl, wsUrl)
    #endregion

    #region Unity Lifecycle
    private void OnDisable()
    {
        _ = UnsubscribeAll();
    }
    #endregion

    #region Initialization
    public void Initialize(Account _wallet, string _rpcUrl = null, string _wsUrl = null)
    {
        try
        {
            m_Wallet = _wallet;
            if (!string.IsNullOrWhiteSpace(_rpcUrl)) m_RpcUrl = _rpcUrl;
            if (!string.IsNullOrWhiteSpace(_wsUrl)) m_WebsocketUrl = _wsUrl;

            // Build default clients
            m_DefaultRpcClient = ClientFactory.GetClient(m_RpcUrl);
            try { m_DefaultStreamingRpcClient = ClientFactory.GetStreamingClient(m_WebsocketUrl); }
            catch (Exception ex) { Debug.LogWarning($"Default WS init failed: {ex.Message}"); m_DefaultStreamingRpcClient = null; }

            // Build magicblock clients
            m_MagicRpcClient = ClientFactory.GetClient(m_MagicBlockRpcUrl);
            try { m_MagicStreamingRpcClient = ClientFactory.GetStreamingClient(m_MagicBlockWsUrl); }
            catch (Exception ex) { Debug.LogWarning($"Magic WS init failed: {ex.Message}"); m_MagicStreamingRpcClient = null; }

            // Start on default
            m_RpcClient = m_DefaultRpcClient;
            m_StreamingRpcClient = m_DefaultStreamingRpcClient;
            m_Client = new BombermanProgramClient(m_RpcClient, m_StreamingRpcClient);

            _ = m_RpcClient.GetHealthAsync();
            Debug.Log("BombermanClient initialized");
            UpdateWeb3InstanceRpc(false);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to initialize BombermanClient: {ex.Message}");
            throw;
        }
    }
    #endregion

    #region Public Accessors
    public PublicKey ActiveWalletPublicKey => m_Wallet?.PublicKey;
    public bool HasWallet => m_Wallet != null;
    #endregion

    #region Funding
    private static Account FromSecretKey(string secretKey)
    {
        try
        {
            var wallet = new Solana.Unity.Wallet.Wallet(new PrivateKey(secretKey).KeyBytes, string.Empty, SeedMode.Bip39);
            return wallet.Account;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    public async Task<RequestResult<string>> FundActiveWalletFromInspectorKey(ulong lamports = 0)
    {
        if (string.IsNullOrWhiteSpace(m_FunderPrivateKeyBase58))
        {
            return ErrorResult("Funder private key is empty");
        }
        return await FundWalletFromPrivateKey(m_FunderPrivateKeyBase58, ActiveWalletPublicKey, lamports == 0 ? m_DefaultFundingLamports : lamports);
    }

    public async Task<RequestResult<string>> FundWalletFromPrivateKey(string privateKey, PublicKey destination, ulong lamports)
    {
        if (destination == null)
        {
            return ErrorResult("Destination is null");
        }

        var funder = FromSecretKey(privateKey);
        if (funder == null)
        {
            return ErrorResult("Invalid funder private key");
        }

        try
        {
            var tx = new Transaction
            {
                FeePayer = funder.PublicKey,
                Instructions = new List<Solana.Unity.Rpc.Models.TransactionInstruction>(),
                RecentBlockHash = await GetRecentBlockHash()
            };

            var ix = SystemProgram.Transfer(funder.PublicKey, destination, lamports);
            tx.Add(ix);

            return await SignAndSendWithSigner(tx, m_RpcClient, funder);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            return ErrorResult(ex.Message);
        }
    }
    #endregion

    #region Public Queries
    public async Task<IReadOnlyList<Game>> GetAllGames(Commitment _commitment = Commitment.Confirmed)
    {
        var res = await m_Client.GetGamesAsync(BombermanProgramProgram.ID, _commitment);
        return res.WasSuccessful && res.ParsedResult != null ? res.ParsedResult : Array.Empty<Game>();
    }

    public async Task<IReadOnlyList<PlayerState>> GetAllPlayerStates(Commitment _commitment = Commitment.Confirmed)
    {
        var res = await m_Client.GetPlayerStatesAsync(BombermanProgramProgram.ID, _commitment);
        return res.WasSuccessful && res.ParsedResult != null ? res.ParsedResult : Array.Empty<PlayerState>();
    }

    public async Task<Game> GetGame(PublicKey _game, Commitment _commitment = Commitment.Finalized)
    {
        var res = await m_Client.GetGameAsync(_game.ToString(), _commitment);
        return res.WasSuccessful ? res.ParsedResult : null;
    }

    public async Task<PlayerState> GetPlayerState(PublicKey _playerState, Commitment _commitment = Commitment.Finalized)
    {
        try
        {
            if (_playerState == null || string.IsNullOrEmpty(_playerState.Key)) return null;
            var info = await m_RpcClient.GetAccountInfoAsync(_playerState, _commitment);
            if (!info.WasSuccessful || info.Result?.Value?.Data == null || info.Result.Value.Data.Count == 0)
            {
                return null;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[BombermanClient] GetPlayerState pre-check error: {ex.Message}");
            return null;
        }

        var res = await m_Client.GetPlayerStateAsync(_playerState.ToString(), _commitment);
        return res.WasSuccessful ? res.ParsedResult : null;
    }
    #endregion

    #region PDA Utilities (Scan-based Helpers)
    // Derive PDAs like in tests: game = ["game", authority], playerState = ["player", player]
    public static PublicKey DeriveGamePda(PublicKey _authority)
    {
        PublicKey.TryFindProgramAddress(new[] { Encoding.UTF8.GetBytes("game"), _authority.KeyBytes }, s_ProgramId, out var pda, out _);
        return pda;
    }

    public static PublicKey DerivePlayerStatePda(PublicKey _player)
    {
        PublicKey.TryFindProgramAddress(new[] { Encoding.UTF8.GetBytes("player"), _player.KeyBytes }, s_ProgramId, out var pda, out _);
        return pda;
    }

    public async Task<Game> FindGameByAuthority(PublicKey _authority, Commitment _commitment = Commitment.Processed)
    {
        var games = await GetAllGames(_commitment);
        return games.FirstOrDefault(g => g.Authority == _authority);
    }

    public async Task<PlayerState> FindPlayerStateFor(PublicKey _game, PublicKey _player, Commitment _commitment = Commitment.Processed)
    {
        var states = await GetAllPlayerStates(_commitment);
        return states.FirstOrDefault(s => s.Game == _game && s.Player == _player);
    }
    #endregion

    #region Ensure Helpers (initialize/join)
    public async Task EnsureGameExists(PublicKey _authority)
    {
        var gamePda = DeriveGamePda(_authority);
        var info = await m_RpcClient.GetAccountInfoAsync(gamePda, Commitment.Processed);
        if (!info.WasSuccessful || info.Result?.Value == null)
        {
            await InitializeGame(gamePda);
        }
    }

    public async Task EnsurePlayerJoined(PublicKey _authority, PublicKey _player)
    {
        var gamePda = DeriveGamePda(_authority);
        // Ensure the game account is initialized before attempting to join
        await EnsureGameExists(_authority);
        var playerPda = DerivePlayerStatePda(_player);
        var info = await m_RpcClient.GetAccountInfoAsync(playerPda, Commitment.Processed);
        if (!info.WasSuccessful || info.Result?.Value == null)
        {
            await JoinGame(gamePda, playerPda, _authority);
        }
    }
    #endregion

    #region Subscriptions
    public async Task SubscribeToGame(PublicKey _game)
    {
        await UnsubscribeGame();
        if (m_StreamingRpcClient == null)
        {
            Debug.LogWarning("SubscribeToGame skipped: streaming RPC not available");
            return;
        }

        m_GameSubscription = await m_Client.SubscribeGameAsync(
            _game.ToString(),
            (state, info, parsed) => { OnGameChanged?.Invoke(parsed); },
            Commitment.Processed
        );
        Debug.Log($"Subscribed to game {_game}");
        m_LastGameSub = _game;
    }

    public async Task SubscribeToPlayerState(PublicKey _playerState)
    {
        await UnsubscribePlayerState();
        if (m_StreamingRpcClient == null)
        {
            Debug.LogWarning("SubscribeToPlayerState skipped: streaming RPC not available");
            return;
        }

        m_PlayerStateSubscription = await m_Client.SubscribePlayerStateAsync(
            _playerState.ToString(),
            (state, info, parsed) => { OnPlayerStateChanged?.Invoke(parsed); },
            Commitment.Processed
        );
        Debug.Log($"Subscribed to player state {_playerState}");
        m_LastPlayerStateSub = _playerState;
    }

    public async Task UnsubscribeGame()
    {
        try
        {
            if (m_GameSubscription != null && m_StreamingRpcClient != null)
            {
                await m_StreamingRpcClient.UnsubscribeAsync(m_GameSubscription);
                m_GameSubscription = null;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"UnsubscribeGame failed: {ex.Message}");
        }
    }

    public async Task UnsubscribePlayerState()
    {
        try
        {
            if (m_PlayerStateSubscription != null && m_StreamingRpcClient != null)
            {
                await m_StreamingRpcClient.UnsubscribeAsync(m_PlayerStateSubscription);
                m_PlayerStateSubscription = null;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"UnsubscribePlayerState failed: {ex.Message}");
        }
    }

    public async Task UnsubscribeAll()
    {
        await UnsubscribeGame();
        await UnsubscribePlayerState();
    }
    #endregion

    #region Transactions
    public async Task<RequestResult<string>> InitializeGame(PublicKey _gamePda)
    {
        if (m_Wallet == null) return ErrorResult("Wallet not initialized");

        var tx = await BuildBaseTransaction();

        var accounts = new InitializeGameAccounts
        {
            Game = _gamePda,
            Authority = m_Wallet.PublicKey,
            SystemProgram = SystemProgram.ProgramIdKey
        };

        var ix = BombermanProgramProgram.InitializeGame(accounts);
        tx.Add(ix);
        return await SignAndSend(tx);
    }

    public async Task<RequestResult<string>> JoinGame(PublicKey _gamePda, PublicKey _playerStatePda, PublicKey _gameAuthority)
    {
        if (m_Wallet == null) return ErrorResult("Wallet not initialized");

        var tx = await BuildBaseTransaction();

        // Ensure the game is initialized before attempting to join
        try
        {
            var gameInfo = await m_RpcClient.GetAccountInfoAsync(_gamePda, Commitment.Processed);
            if (!gameInfo.WasSuccessful || gameInfo.Result?.Value == null)
            {
                var initAccounts = new InitializeGameAccounts
                {
                    Game = _gamePda,
                    Authority = _gameAuthority,
                    SystemProgram = SystemProgram.ProgramIdKey
                };
                var initIx = BombermanProgramProgram.InitializeGame(initAccounts);
                tx.Add(initIx);
            }
        }
        catch (Exception)
        {
            // If the check fails, we proceed with join; RPC may be transient
        }

        var accounts = new JoinGameAccounts
        {
            Game = _gamePda,
            PlayerState = _playerStatePda,
            Player = m_Wallet.PublicKey,
            SystemProgram = SystemProgram.ProgramIdKey
        };

        var ix = BombermanProgramProgram.JoinGame(accounts, _gameAuthority);
        tx.Add(ix);
        return await SignAndSend(tx);
    }

    public async Task<RequestResult<string>> MovePlayer(PublicKey _playerStatePda, byte _newX, byte _newY)
    {
        if (m_Wallet == null) return ErrorResult("Wallet not initialized");

        try
        {
            Debug.Log($"Building move transaction to position ({_newX}, {_newY})");
            var tx = await BuildBaseTransaction();
            Debug.Log($"Transaction built successfully with block hash: {tx.RecentBlockHash}");

            var accounts = new MovePlayerAccounts
            {
                PlayerState = _playerStatePda,
                Player = m_Wallet.PublicKey
            };

            var ix = BombermanProgramProgram.MovePlayer(accounts, _newX, _newY);
            tx.Add(ix);
            Debug.Log("Move instruction added, signing and sending transaction...");
            
            return await SignAndSend(tx);
        }
        catch (Exception ex)
        {
            Debug.LogError($"MovePlayer failed with exception: {ex.Message}");
            return ErrorResult($"Move failed: {ex.Message}");
        }
    }
    #endregion

    #region Rollup (Ephemeral) Helpers
    // Send move to MagicBlock rollup directly (mirrors test using ephemeral provider)
    public async Task<RequestResult<string>> MovePlayerOnRollup(byte _newX, byte _newY, PublicKey _playerStatePda)
    {
        if (m_Wallet == null) return ErrorResult("Wallet not initialized");
        var tx = new Transaction
        {
            FeePayer = m_Wallet.PublicKey,
            Instructions = new List<Solana.Unity.Rpc.Models.TransactionInstruction>(),
            RecentBlockHash = await GetRecentBlockHashWithClient(m_MagicRpcClient)
        };

        var accounts = new MovePlayerAccounts
        {
            PlayerState = _playerStatePda,
            Player = m_Wallet.PublicKey
        };

        // On rollup, we still use the same program instruction
        var ix = BombermanProgramProgram.MovePlayer(accounts, _newX, _newY);
        tx.Add(ix);
        return await SignAndSendWithClient(tx, m_MagicRpcClient);
    }
    #endregion

    #region Delegation (Optional Helpers)
    public async Task<RequestResult<string>> DelegatePlayer(PublicKey _bufferPlayerState, PublicKey _delegationRecordPlayerState, PublicKey _delegationMetadataPlayerState, PublicKey _playerState)
    {
        if (m_Wallet == null) return ErrorResult("Wallet not initialized");

        var tx = await BuildBaseTransaction();
        var accounts = new DelegatePlayerAccounts
        {
            BufferPlayerState = _bufferPlayerState,
            DelegationRecordPlayerState = _delegationRecordPlayerState,
            DelegationMetadataPlayerState = _delegationMetadataPlayerState,
            PlayerState = _playerState,
            Player = m_Wallet.PublicKey
        };

        var ix = BombermanProgramProgram.DelegatePlayer(accounts);
        tx.Add(ix);
        var res = await SignAndSend(tx);
        if (res.WasSuccessful)
        {
            m_IsDelegated = true;
            await SwitchRpcClient(true);
            try { OnDelegationStatusChanged?.Invoke(true); } catch {}
        }
        return res;
    }

    public async Task<RequestResult<string>> UndelegatePlayer(PublicKey _playerState)
    {
        if (m_Wallet == null) return ErrorResult("Wallet not initialized");
        // Build transaction against magic RPC so it executes where delegated
        var tx = await BuildBaseTransaction();
        var accounts = new UndelegatePlayerAccounts
        {
            PlayerState = _playerState,
            Player = m_Wallet.PublicKey
        };

        var ix = BombermanProgramProgram.UndelegatePlayer(accounts);
        tx.Add(ix);
        var res = await SignAndSendWithClient(tx, m_MagicRpcClient);
        if (res.WasSuccessful)
        {
            // Poll delegation status and switch back when cleared
            for (int i = 0; i < 10; i++)
            {
                await Task.Delay(1000);
                var delegated = await IsPlayerDelegated(_playerState);
                if (!delegated) break;
            }
            m_IsDelegated = false;
            await SwitchRpcClient(false);
            try { OnDelegationStatusChanged?.Invoke(false); } catch {}
        }
        return res;
    }

    // Auto-derive PDAs and delegate
    public async Task<RequestResult<string>> DelegatePlayerAuto(PublicKey _playerState)
    {
        var buffer = FindBufferPda("buffer", _playerState, s_ProgramId);
        var record = FindDelegationProgramPda("delegation", _playerState, s_DelegationProgram);
        var metadata = FindDelegationProgramPda("delegation-metadata", _playerState, s_DelegationProgram);
        return await DelegatePlayer(buffer, record, metadata, _playerState);
    }

    public async Task<RequestResult<string>> UndelegatePlayerAuto(PublicKey _playerState)
    {
        return await UndelegatePlayer(_playerState);
    }
    #endregion

    #region Transaction Utilities
    private IRpcClient GetActiveRpcClient()
    {
        return (m_IsDelegated && m_MagicRpcClient != null) ? m_MagicRpcClient : m_RpcClient;
    }
    private async Task<Transaction> BuildBaseTransaction()
    {
        var tx = new Transaction
        {
            FeePayer = m_Wallet.PublicKey,
            Instructions = new List<Solana.Unity.Rpc.Models.TransactionInstruction>(),
            RecentBlockHash = await GetRecentBlockHashWithAutoRecovery()
        };

        if (m_UseComputeBudget)
        {
            tx.Instructions.Add(ComputeBudgetProgram.SetComputeUnitLimit(m_ComputeUnitLimit));
            if (m_ComputeUnitPriceMicroLamports > 0)
            {
                tx.Instructions.Add(ComputeBudgetProgram.SetComputeUnitPrice(m_ComputeUnitPriceMicroLamports));
            }
        }

        return tx;
    }

    private async Task<string> GetRecentBlockHash()
    {
        // Prefer MagicBlock when delegated; otherwise prefer default. Retry and fallback.
        Exception lastEx = null;
        IRpcClient primary = (m_IsDelegated && m_MagicRpcClient != null) ? m_MagicRpcClient : m_RpcClient;
        IRpcClient secondary = (primary == m_MagicRpcClient) ? m_RpcClient : m_MagicRpcClient;

        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                var res = await primary.GetLatestBlockHashAsync(commitment: Commitment.Confirmed);
                if (res.WasSuccessful && res.Result?.Value != null)
                {
                    return res.Result.Value.Blockhash;
                }
                lastEx = new Exception(res.Reason ?? "Unknown error while getting blockhash");
            }
            catch (Exception ex)
            {
                lastEx = ex;
            }
            await Task.Delay(200 * (attempt + 1));
        }

        // fallback to the other client if present
        if (secondary != null)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    var res = await secondary.GetLatestBlockHashAsync(commitment: Commitment.Confirmed);
                    if (res.WasSuccessful && res.Result?.Value != null)
                    {
                        return res.Result.Value.Blockhash;
                    }
                    lastEx = new Exception(res.Reason ?? "Unknown error while getting blockhash (fallback)");
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                }
                await Task.Delay(200 * (attempt + 1));
            }
        }

        throw new Exception($"Failed to get latest block hash: {lastEx?.Message}");
    }

    private async Task<string> GetRecentBlockHashWithClient(IRpcClient _client)
    {
        Exception lastEx = null;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                var res = await _client.GetLatestBlockHashAsync(commitment: Commitment.Confirmed);
                if (res.WasSuccessful && res.Result?.Value != null)
                {
                    return res.Result.Value.Blockhash;
                }
                lastEx = new Exception(res.Reason ?? "Unknown error while getting blockhash");
            }
            catch (Exception ex)
            {
                lastEx = ex;
            }
            await Task.Delay(200 * (attempt + 1));
        }
        throw new Exception($"Failed to get latest block hash: {lastEx?.Message}");
    }

    private async Task<RequestResult<string>> SignAndSend(Transaction _tx)
    {
        try
        {
            _tx.Sign(m_Wallet);
            var client = GetActiveRpcClient();
            var result = await client.SendTransactionAsync(
                _tx.Serialize(),
                skipPreflight: true,
                commitment: Commitment.Confirmed
            );

            if (result.WasSuccessful)
            {
                await client.ConfirmTransaction(result.Result, Commitment.Confirmed);
            }
            else
            {
                Debug.LogError($"Transaction failed: {result.Reason}");
            }

            return result;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            return ErrorResult(ex.Message);
        }
    }

    private async Task<RequestResult<string>> SignAndSendWithClient(Transaction _tx, IRpcClient _client)
    {
        try
        {
            _tx.Sign(m_Wallet);
            var result = await _client.SendTransactionAsync(
                _tx.Serialize(),
                skipPreflight: true,
                commitment: Commitment.Confirmed
            );
            if (result.WasSuccessful)
            {
                await _client.ConfirmTransaction(result.Result, Commitment.Confirmed);
            }
            else
            {
                Debug.LogError($"Transaction failed: {result.Reason}");
            }
            return result;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            return ErrorResult(ex.Message);
        }
    }

    private async Task<RequestResult<string>> SignAndSendWithSigner(Transaction _tx, IRpcClient _client, Account _signer)
    {
        try
        {
            _tx.Sign(_signer);
            var result = await _client.SendTransactionAsync(
                _tx.Serialize(),
                skipPreflight: true,
                commitment: Commitment.Confirmed
            );
            if (result.WasSuccessful)
            {
                await _client.ConfirmTransaction(result.Result, Commitment.Confirmed);
            }
            else
            {
                Debug.LogError($"Transaction failed: {result.Reason}");
            }
            return result;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            return ErrorResult(ex.Message);
        }
    }

    private static RequestResult<string> ErrorResult(string _reason)
    {
        return new RequestResult<string> { Reason = _reason };
    }
    #endregion

    #region Delegation Utilities and RPC Switching
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

    public async Task<bool> IsPlayerDelegated(PublicKey playerState)
    {
        try
        {
            var info = await m_DefaultRpcClient.GetAccountInfoAsync(playerState, Commitment.Processed);
            var isDelegated = info.WasSuccessful && info.Result?.Value?.Owner != null && info.Result.Value.Owner.Equals(s_DelegationProgram);
            return isDelegated;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"IsPlayerDelegated error: {ex.Message}");
            return m_IsDelegated;
        }
    }

    public async Task SwitchRpcClient(bool useMagicBlock)
    {
        try
        {
            await UnsubscribeAll();
            if (useMagicBlock)
            {
                m_RpcClient = m_MagicRpcClient;
                m_StreamingRpcClient = m_MagicStreamingRpcClient;
            }
            else
            {
                m_RpcClient = m_DefaultRpcClient;
                m_StreamingRpcClient = m_DefaultStreamingRpcClient;
            }
            m_Client = new BombermanProgramClient(m_RpcClient, m_StreamingRpcClient);
            UpdateWeb3InstanceRpc(useMagicBlock);
            try { OnRpcSwitched?.Invoke(useMagicBlock ? m_MagicBlockRpcUrl : m_RpcUrl, useMagicBlock ? m_MagicBlockWsUrl : m_WebsocketUrl); } catch {}

            // Resubscribe if needed
            if (m_LastGameSub != null)
            {
                await SubscribeToGame(m_LastGameSub);
            }
            if (m_LastPlayerStateSub != null)
            {
                await SubscribeToPlayerState(m_LastPlayerStateSub);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"SwitchRpcClient error: {ex.Message}");
        }
    }
    #endregion

    #region Web3 SDK RPC Sync
    private void UpdateWeb3InstanceRpc(bool useMagicBlock)
    {
        if (Web3.Instance == null) return;
        if (useMagicBlock)
        {
            Web3.Instance.customRpc = m_MagicBlockRpcUrl;
            Web3.Instance.webSocketsRpc = m_MagicBlockWsUrl;
        }
        else
        {
            Web3.Instance.customRpc = m_RpcUrl;
            Web3.Instance.webSocketsRpc = m_WebsocketUrl;
        }
    }
    #endregion

    #region Public RPC Control
    /// <summary>
    /// Manually switch to a different RPC endpoint
    /// </summary>
    public async Task SwitchToDifferentRpc()
    {
        bool currentIsMagic = (m_RpcClient == m_MagicRpcClient);
        await SwitchRpcClient(!currentIsMagic);
        Debug.Log($"Switched to {(currentIsMagic ? "default" : "MagicBlock")} RPC endpoint");
    }
    #endregion

    #region RPC Health and Validation
    /// <summary>
    /// Validates the current RPC endpoint and switches if needed
    /// </summary>
    public async Task<bool> ValidateAndSwitchRpcIfNeeded()
    {
        try
        {
            Debug.Log("Validating current RPC endpoint...");
            var currentClient = GetActiveRpcClient();
            
            // Test the current endpoint
            var healthCheck = await currentClient.GetHealthAsync();
            if (healthCheck.WasSuccessful)
            {
                Debug.Log("Current RPC endpoint is healthy");
                return true;
            }
            
            Debug.LogWarning("Current RPC endpoint failed health check, attempting to switch...");
            
            // Try to switch to the other endpoint
            bool shouldUseMagicBlock = !(currentClient == m_MagicRpcClient);
            await SwitchRpcClient(shouldUseMagicBlock);
            
            // Test the new endpoint
            var newHealthCheck = await GetActiveRpcClient().GetHealthAsync();
            if (newHealthCheck.WasSuccessful)
            {
                Debug.Log($"Successfully switched to {(shouldUseMagicBlock ? "MagicBlock" : "default")} RPC endpoint");
                return true;
            }
            
            Debug.LogError("Both RPC endpoints appear to be unhealthy");
            return false;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error during RPC validation: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Gets the current RPC endpoint status for debugging
    /// </summary>
    public async Task<string> GetRpcStatus()
    {
        try
        {
            var defaultHealth = await m_DefaultRpcClient.GetHealthAsync();
            var magicHealth = await m_MagicRpcClient.GetHealthAsync();
            
            string status = $"Default RPC ({m_RpcUrl}): {(defaultHealth.WasSuccessful ? "Healthy" : "Unhealthy")}\n";
            status += $"MagicBlock RPC ({m_MagicBlockRpcUrl}): {(magicHealth.WasSuccessful ? "Healthy" : "Unhealthy")}\n";
            status += $"Current active: {(m_RpcClient == m_MagicRpcClient ? "MagicBlock" : "Default")}\n";
            status += $"Delegated: {m_IsDelegated}";
            
            return status;
        }
        catch (Exception ex)
        {
            return $"Error getting RPC status: {ex.Message}";
        }
    }

    /// <summary>
    /// Enhanced block hash retrieval with automatic RPC switching on JSON parsing errors
    /// </summary>
    public async Task<string> GetRecentBlockHashWithAutoRecovery()
    {
        try
        {
            // First try the normal method
            return await GetRecentBlockHash();
        }
        catch (Exception ex) when (ex.Message.Contains("Unable to parse json") || ex.Message.Contains("JSON"))
        {
            Debug.LogWarning("JSON parsing error detected, attempting RPC endpoint recovery...");
            
            // Try to validate and switch RPC if needed
            bool recovered = await ValidateAndSwitchRpcIfNeeded();
            if (recovered)
            {
                Debug.Log("RPC endpoint recovered, retrying block hash retrieval...");
                try
                {
                    return await GetRecentBlockHash();
                }
                catch (Exception retryEx)
                {
                    Debug.LogError($"Block hash retrieval still failed after RPC recovery: {retryEx.Message}");
                    throw;
                }
            }
            else
            {
                Debug.LogError("Failed to recover RPC endpoint, throwing original error");
                throw;
            }
        }
    }
    #endregion
}


