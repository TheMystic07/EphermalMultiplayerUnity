using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using BombermanProgram;
using BombermanProgram.Accounts;
using BombermanProgram.Program;
using Solana.Unity.Programs;
using Solana.Unity.Rpc;
using Solana.Unity.Rpc.Core.Http;
using Solana.Unity.Rpc.Types;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Wallet;
using UnityEngine;

// ReSharper disable once CheckNamespace
public class NewBombermanClient : MonoBehaviour
{
    #region Serialized Fields (RPC & Compute)
    [Header("RPC Configuration")]
    [SerializeField] private string m_RpcUrl = "https://api.devnet.solana.com";
    [SerializeField] private string m_WebsocketUrl = "wss://api.devnet.solana.com";
    [SerializeField] private string m_MagicBlockRpcUrl = "https://devnet.magicblock.app";

    [Header("Compute Budget")]
    [SerializeField] private bool m_UseComputeBudget = true;
    [SerializeField] private uint m_ComputeUnitLimit = 100_000;
    [SerializeField] private ulong m_ComputeUnitPriceMicroLamports = 1_000; // 0.000001 SOL per 1,000,000 CUs
    [Header("Funding")]
    [SerializeField] private string m_FunderPrivateKeyBase58 = ""; // Optional inspector key for funding
    [SerializeField] private ulong m_DefaultFundingLamports = 1_000_000_000UL; // 1 SOL
    #endregion

    #region Delegation
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

    public async Task<RequestResult<string>> DelegatePlayerAuto(PublicKey _playerState)
    {
        var buffer = FindBufferPda("buffer", _playerState, s_ProgramId);
        var record = FindDelegationProgramPda("delegation", _playerState, s_DelegationProgram);
        var metadata = FindDelegationProgramPda("delegation-metadata", _playerState, s_DelegationProgram);
        return await DelegatePlayer(buffer, record, metadata, _playerState);
    }

    public async Task<RequestResult<string>> DelegatePlayer(PublicKey _bufferPlayerState, PublicKey _delegationRecordPlayerState, PublicKey _delegationMetadataPlayerState, PublicKey _playerState)
    {
        if (m_Wallet == null) return ErrorResult("Wallet not initialized");

        var tx = await BuildBaseTransaction(m_ActiveRpcClient);
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
        return await SignAndSend(tx, m_ActiveRpcClient);
    }

    public async Task<RequestResult<string>> UndelegatePlayer(PublicKey _playerState)
    {
        if (m_Wallet == null) return ErrorResult("Wallet not initialized");
        var tx = await BuildBaseTransaction(m_MagicRpcClient);
        var accounts = new UndelegatePlayerAccounts
        {
            PlayerState = _playerState,
            Player = m_Wallet.PublicKey
        };

        var ix = BombermanProgramProgram.UndelegatePlayer(accounts);
        tx.Add(ix);
        return await SignAndSend(tx, m_MagicRpcClient);
    }

    public async Task<bool> IsPlayerDelegated(PublicKey _playerState)
    {
        try
        {
            var info = await m_ActiveRpcClient.GetAccountInfoAsync(_playerState, Commitment.Processed);
            return info.WasSuccessful && info.Result?.Value?.Owner != null && info.Result.Value.Owner.Equals(s_DelegationProgram);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"IsPlayerDelegated error: {ex.Message}");
            return false;
        }
    }
    #endregion

    #region Private Fields
    private IRpcClient m_DefaultRpcClient;
    private IRpcClient m_MagicRpcClient;
    private IRpcClient m_ActiveRpcClient;
    private BombermanProgramClient m_ProgramClient;
    private BombermanProgramClient m_ProgramClientMagic;
    private Account m_Wallet;

    private static readonly PublicKey s_ProgramId = new PublicKey(BombermanProgramProgram.ID);
    private static readonly PublicKey s_DelegationProgram = new PublicKey("DELeGGvXpWV2fqJUhqcF5ZSYMS4JTLjteaAMARRSaeSh");
    #endregion

    #region Public Accessors
    public PublicKey ActiveWalletPublicKey => m_Wallet?.PublicKey;
    public IRpcClient ActiveRpcClient => m_ActiveRpcClient;
    #endregion

    #region Unity Lifecycle
    private void OnDisable()
    {
        // No streaming subscriptions in this streamlined client
    }
    #endregion

    #region Initialization
    public void Initialize(Account _wallet, string _rpcUrl = null, string _wsUrl = null)
    {
        if (_wallet == null)
        {
            Debug.LogError("NewBombermanClient.Initialize: wallet is null");
            return;
        }

        m_Wallet = _wallet;
        if (!string.IsNullOrWhiteSpace(_rpcUrl)) m_RpcUrl = _rpcUrl;
        if (!string.IsNullOrWhiteSpace(_wsUrl)) m_WebsocketUrl = _wsUrl;

        m_DefaultRpcClient = ClientFactory.GetClient(m_RpcUrl);
        m_MagicRpcClient = ClientFactory.GetClient(m_MagicBlockRpcUrl);
        m_ActiveRpcClient = m_DefaultRpcClient;
        m_ProgramClient = new BombermanProgramClient(m_ActiveRpcClient, null);
        m_ProgramClientMagic = new BombermanProgramClient(m_MagicRpcClient, null);

        Debug.Log("NewBombermanClient initialized");
    }
    #endregion

    #region PDA Helpers
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
    #endregion

    #region Simple Queries
    public async Task<PlayerState> GetPlayerState(PublicKey _playerState, Commitment _commitment = Commitment.Processed)
    {
        if (_playerState == null || string.IsNullOrEmpty(_playerState.Key)) return null;
        try
        {
            // Null-safe pre-check to avoid parsing on missing accounts
            var info = await m_ActiveRpcClient.GetAccountInfoAsync(_playerState, _commitment);
            if (!info.WasSuccessful || info.Result?.Value?.Data == null || info.Result.Value.Data.Count == 0)
            {
                return null;
            }

            var res = await m_ProgramClient.GetPlayerStateAsync(_playerState.ToString(), _commitment);
            return res.WasSuccessful ? res.ParsedResult : null;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"GetPlayerState error: {ex.Message}");
            return null;
        }
    }

    public async Task<PlayerState> GetPlayerStateOnRollup(PublicKey _playerState, Commitment _commitment = Commitment.Processed)
    {
        if (_playerState == null || string.IsNullOrEmpty(_playerState.Key)) return null;
        try
        {
            var info = await m_MagicRpcClient.GetAccountInfoAsync(_playerState, _commitment);
            if (!info.WasSuccessful || info.Result?.Value?.Data == null || info.Result.Value.Data.Count == 0)
            {
                return null;
            }
            var res = await m_ProgramClientMagic.GetPlayerStateAsync(_playerState.ToString(), _commitment);
            return res.WasSuccessful ? res.ParsedResult : null;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"GetPlayerStateOnRollup error: {ex.Message}");
            return null;
        }
    }
    #endregion

    #region Ensure Helpers
    public async Task EnsureGameAndPlayer(PublicKey _authority, PublicKey _player)
    {
        var gamePda = DeriveGamePda(_authority);
        var playerPda = DerivePlayerStatePda(_player);

        // Ensure game exists
        var gameInfo = await m_ActiveRpcClient.GetAccountInfoAsync(gamePda, Commitment.Processed);
        if (!gameInfo.WasSuccessful || gameInfo.Result?.Value == null)
        {
            var initTx = await BuildBaseTransaction(m_ActiveRpcClient);
            var initAccounts = new InitializeGameAccounts
            {
                Game = gamePda,
                Authority = _authority,
                SystemProgram = SystemProgram.ProgramIdKey
            };
            initTx.Add(BombermanProgramProgram.InitializeGame(initAccounts));
            await SignAndSend(initTx, m_ActiveRpcClient);
        }

        // Ensure player joined
        var playerInfo = await m_ActiveRpcClient.GetAccountInfoAsync(playerPda, Commitment.Processed);
        if (!playerInfo.WasSuccessful || playerInfo.Result?.Value == null)
        {
            var joinTx = await BuildBaseTransaction(m_ActiveRpcClient);
            var joinAccounts = new JoinGameAccounts
            {
                Game = gamePda,
                PlayerState = playerPda,
                Player = _player,
                SystemProgram = SystemProgram.ProgramIdKey
            };
            // Authority is _authority
            joinTx.Add(BombermanProgramProgram.JoinGame(joinAccounts, _authority));
            await SignAndSend(joinTx, m_ActiveRpcClient);
        }
    }
    #endregion

    #region Explicit Init/Join
    public async Task<RequestResult<string>> InitializeGameForAuthority(PublicKey _authority)
    {
        if (_authority == null || string.IsNullOrEmpty(_authority.Key)) return ErrorResult("Authority is null");
        try
        {
            var gamePda = DeriveGamePda(_authority);
            var info = await m_ActiveRpcClient.GetAccountInfoAsync(gamePda, Commitment.Processed);
            if (info.WasSuccessful && info.Result?.Value != null)
            {
                return ErrorResult("Game already exists");
            }
            var initTx = await BuildBaseTransaction(m_ActiveRpcClient);
            var initAccounts = new InitializeGameAccounts { Game = gamePda, Authority = _authority, SystemProgram = SystemProgram.ProgramIdKey };
            initTx.Add(BombermanProgramProgram.InitializeGame(initAccounts));
            return await SignAndSend(initTx, m_ActiveRpcClient);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            return ErrorResult(ex.Message);
        }
    }

    public async Task<RequestResult<string>> JoinGameForPlayer(PublicKey _authority, PublicKey _player)
    {
        if (_authority == null || string.IsNullOrEmpty(_authority.Key)) return ErrorResult("Authority is null");
        if (_player == null || string.IsNullOrEmpty(_player.Key)) return ErrorResult("Player is null");
        try
        {
            var gamePda = DeriveGamePda(_authority);
            var playerPda = DerivePlayerStatePda(_player);

            // Ensure game exists
            var gameInfo = await m_ActiveRpcClient.GetAccountInfoAsync(gamePda, Commitment.Processed);
            if (!gameInfo.WasSuccessful || gameInfo.Result?.Value == null)
            {
                var initRes = await InitializeGameForAuthority(_authority);
                if (!initRes.WasSuccessful) return initRes;
            }

            // Skip if player already joined
            var playerInfo = await m_ActiveRpcClient.GetAccountInfoAsync(playerPda, Commitment.Processed);
            if (playerInfo.WasSuccessful && playerInfo.Result?.Value != null)
            {
                return ErrorResult("Player already joined");
            }

            var joinTx = await BuildBaseTransaction(m_ActiveRpcClient);
            var joinAccounts = new JoinGameAccounts { Game = gamePda, PlayerState = playerPda, Player = _player, SystemProgram = SystemProgram.ProgramIdKey };
            joinTx.Add(BombermanProgramProgram.JoinGame(joinAccounts, _authority));
            return await SignAndSend(joinTx, m_ActiveRpcClient);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            return ErrorResult(ex.Message);
        }
    }
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
        if (m_Wallet == null) return ErrorResult("Active wallet not initialized");
        if (string.IsNullOrWhiteSpace(m_FunderPrivateKeyBase58))
        {
            return ErrorResult("Funder private key is empty");
        }
        return await FundWalletFromPrivateKey(m_FunderPrivateKeyBase58, m_Wallet.PublicKey, lamports == 0 ? m_DefaultFundingLamports : lamports);
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
            var tx = await BuildBaseTransaction(m_ActiveRpcClient, funder.PublicKey);
            var ix = SystemProgram.Transfer(funder.PublicKey, destination, lamports);
            tx.Add(ix);
            return await SignAndSendWithSigner(tx, m_ActiveRpcClient, funder);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            return ErrorResult(ex.Message);
        }
    }
    #endregion

    #region Moves (Chain and Rollup)
    public async Task<RequestResult<string>> MovePlayer(PublicKey _playerStatePda, byte _newX, byte _newY)
    {
        if (m_Wallet == null) return ErrorResult("Wallet not initialized");
        var tx = await BuildBaseTransaction(m_ActiveRpcClient);
        var accounts = new MovePlayerAccounts { PlayerState = _playerStatePda, Player = m_Wallet.PublicKey };
        tx.Add(BombermanProgramProgram.MovePlayer(accounts, _newX, _newY));
        return await SignAndSend(tx, m_ActiveRpcClient);
    }

    public async Task<RequestResult<string>> MovePlayerOnRollup(PublicKey _playerStatePda, byte _newX, byte _newY)
    {
        if (m_Wallet == null) return ErrorResult("Wallet not initialized");
        var tx = await BuildBaseTransaction(m_MagicRpcClient);
        var accounts = new MovePlayerAccounts { PlayerState = _playerStatePda, Player = m_Wallet.PublicKey };
        tx.Add(BombermanProgramProgram.MovePlayer(accounts, _newX, _newY));
        return await SignAndSend(tx, m_MagicRpcClient);
    }
    #endregion

    #region Transaction Utilities
    private async Task<Transaction> BuildBaseTransaction(IRpcClient _client)
    {
        var tx = new Transaction
        {
            FeePayer = m_Wallet.PublicKey,
            Instructions = new List<Solana.Unity.Rpc.Models.TransactionInstruction>(),
            RecentBlockHash = await GetRecentBlockHash(_client)
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

    private async Task<Transaction> BuildBaseTransaction(IRpcClient _client, PublicKey feePayer)
    {
        var tx = new Transaction
        {
            FeePayer = feePayer,
            Instructions = new List<Solana.Unity.Rpc.Models.TransactionInstruction>(),
            RecentBlockHash = await GetRecentBlockHash(_client)
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

    private async Task<string> GetRecentBlockHash(IRpcClient _client)
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

    private async Task<RequestResult<string>> SignAndSend(Transaction _tx, IRpcClient _client)
    {
        try
        {
            _tx.Sign(m_Wallet);
            var result = await _client.SendTransactionAsync(_tx.Serialize(), skipPreflight: true, commitment: Commitment.Confirmed);
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
            var result = await _client.SendTransactionAsync(_tx.Serialize(), skipPreflight: true, commitment: Commitment.Confirmed);
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
}


