// This Code is of another game - use thios just for reference 
// using System;
// using System.Collections.Generic;
// using System.Threading.Tasks;
// using System.Text;
// using Cysharp.Threading.Tasks;
// using Solana.Unity.Programs;
// using Solana.Unity.Rpc;
// using Solana.Unity.Rpc.Builders;
// using Solana.Unity.Rpc.Core.Http;
// using Solana.Unity.Rpc.Core.Sockets;
// using Solana.Unity.Rpc.Messages;
// using Solana.Unity.Rpc.Models;
// using Solana.Unity.Rpc.Types;
// using Solana.Unity.Wallet;
// using SolCross;
// using SolCross.Accounts;
// using SolCross.Program;
// using UnityEngine;
// using System.Net.WebSockets;
// using Solana.Unity.Wallet.Bip39;
// using Solana.Unity.SDK;
// using SolanaSDK.PriorityFees;
// using UnityEngine.Networking;

// public class SolCrossIntegration : MonoBehaviour
// {
//     private static readonly PublicKey ProgramId = new(SolCrossProgram.ID);
//     private static readonly PublicKey DelegationProgram = new("DELeGGvXpWV2fqJUhqcF5ZSYMS4JTLjteaAMARRSaeSh");
//     private static readonly ulong STEP_FEE = 100000; // 0.0001 SOL in lamports
//     private static readonly PublicKey MagicProgramId = new("Magic11111111111111111111111111111111111111");
//     private static readonly PublicKey MagicContextId = new("MagicContext1111111111111111111111111111111");

//     // Magic Block Route API
//     private const string MAGICBLOCK_ROUTER_URL = "https://send-arcade-router.magicblock.app/getRoutes";
    
//     // RPC URLs - now dynamic
//     private string MAGICBLOCK_RPC_URL = "https://as.magicblock.app"; // Fallback
//     private string MAGICBLOCK_WS_URL = "wss://as.magicblock.app"; // Fallback

//     private string _defaultRpcUrl = "https://teddy-o4la18-fast-mainnet.helius-rpc.com";   
//     private string _defaultWsUrl = "wss://teddy-o4la18-fast-mainnet.helius-rpc.com";
//     private string _mainnetRpcUrl = "https://teddy-o4la18-fast-mainnet.helius-rpc.com";

//     private IRpcClient _rpcClient;
//     private IStreamingRpcClient _streamingRpcClient;
//     private IRpcClient _magicRpcClient;
//     private IStreamingRpcClient _magicStreamingRpcClient;
//     private IRpcClient _mainnetRpcClient;
//     private IStreamingRpcClient _mainnetStreamingRpcClient;
//     private SolCrossClient _solCrossClient;
//     private Account _wallet;
//     private bool _isInitialized = false;
//     private bool _isDelegated = false;
    
//     [Header("Priority Fee Management")]
//     [SerializeField] private FetchPriorityFee m_PriorityFeeManager;
//     [SerializeField] private ulong m_DefaultPriorityFee = 100000; // Fallback priority fee in microlamports
//     [SerializeField] private float m_PriorityFeeTimeoutSeconds = 10f;
//     private Account _adminWallet;

//     // Magic Block Route Data Structures
//     [Serializable]
//     public class MagicBlockRoute
//     {
//         public string identity;
//         public string fqdn;
//         public int baseFee;
//         public int blockTimeMs;
//         public string countryCode;
//     }

//     [Serializable]
//     public class MagicBlockRoutesResponse
//     {
//         public string jsonrpc;
//         public int id;
//         public MagicBlockRoute[] result;
//     }

//     [Serializable]
//     public class MagicBlockRoutesRequest
//     {
//         public string jsonrpc = "2.0";
//         public int id = 1;
//         public string method = "getRoutes";
//     }

//     // Seeds for PDAs
//     private const string PLAYER_SEED = "sol-cross-player";
//     private const string ANDI = "5Wjb4zo5Rh4qg1CpXMdq1Nt";
//     private const string MANI = "1FiZxqS1TfzuLTGrJSEjgudAMBiQ";
//     private const string SHANDI = "sFMfKwCwTeTYoCnYYz8SEDiusSFwJKi3BHdF7";

//     // Cached PDAs
//     private PublicKey _stepPda;
//     private SubscriptionState _playerSubscription;

//     // Events
//     public event Action<Player> OnPlayerChanged;
//     public event Action<ulong> OnStepCountChanged;
//     public event Action<ulong> OnCoinsChanged;

//     private Account GetActiveWallet()
//     {
//         return _wallet;
//     }
    
//     private IRpcClient GetActiveRpcClient()
//     {
//         return _isDelegated ? _magicRpcClient : _rpcClient;
//     }

//     public bool IsUsingMagicBlockRpc()
//     {
//         return _rpcClient != null && _rpcClient.NodeAddress.ToString().IndexOf("magicblock", StringComparison.OrdinalIgnoreCase) >= 0;
//     }

//     private async UniTask SwitchRpcClient(bool useMagicBlock)
//     {
//         try
//         {
//             await UnsubscribeFromAll();

//             if (useMagicBlock)
//             {
//                 _rpcClient = ClientFactory.GetClient(MAGICBLOCK_RPC_URL);
//                 _streamingRpcClient = ClientFactory.GetStreamingClient(MAGICBLOCK_WS_URL);
//                 UpdateWeb3InstanceRpc(true);
//             }
//             else
//             {
//                 _rpcClient = ClientFactory.GetClient(_defaultRpcUrl);
//                 _streamingRpcClient = ClientFactory.GetStreamingClient(_defaultWsUrl);
//                 UpdateWeb3InstanceRpc(false);
//             }

//             _solCrossClient = new SolCrossClient(_rpcClient, _streamingRpcClient, ProgramId);

//             if (_isInitialized)
//             {
//                 await SubscribeToPlayer();
//             }
//         }
//         catch (Exception ex)
//         {
//             Debug.LogError($"Error switching RPC client: {ex.Message}");
//         }
//     }
    
//     private void UpdateWeb3InstanceRpc(bool useMagicBlock)
//     {
//         if (Web3.Instance != null)
//         {
//             if (useMagicBlock)
//             {
//                 if (string.IsNullOrEmpty(_defaultRpcUrl))
//                     _defaultRpcUrl = Web3.Instance.customRpc;
                    
//                 if (string.IsNullOrEmpty(_defaultWsUrl))
//                     _defaultWsUrl = Web3.Instance.webSocketsRpc;
                
//                 Web3.Instance.customRpc = MAGICBLOCK_RPC_URL;
//                 Web3.Instance.webSocketsRpc = MAGICBLOCK_WS_URL;
//             }
//             else
//             {
//                 Web3.Instance.customRpc = _defaultRpcUrl;
//                 Web3.Instance.webSocketsRpc = _defaultWsUrl;
//             }
//         }
//     }

//     #region Magic Block Route Fetching
    
//     /// <summary>
//     /// Fetches available Magic Block routes and sets the optimal RPC URL
//     /// </summary>
//     /// <returns>Task representing the async operation</returns>
//     private async UniTask<bool> FetchMagicBlockRoutesAsync()
//     {
//         try
//         {
//             Debug.Log("[MagicBlock API] Fetching Magic Block routes...");
            
//             var request = new MagicBlockRoutesRequest();
//             string jsonData = JsonUtility.ToJson(request);
            
//             Debug.Log($"[MagicBlock API] Sending request to: {MAGICBLOCK_ROUTER_URL}");
//             Debug.Log($"[MagicBlock API] Request payload: {jsonData}");
            
//             using (UnityWebRequest webRequest = new UnityWebRequest(MAGICBLOCK_ROUTER_URL, "POST"))
//             {
//                 byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
//                 webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
//                 webRequest.downloadHandler = new DownloadHandlerBuffer();
//                 webRequest.SetRequestHeader("Content-Type", "application/json");
//                 webRequest.timeout = 10; // 10 second timeout
                
//                 await webRequest.SendWebRequest().ToUniTask();
                
//                 if (webRequest.result == UnityWebRequest.Result.Success)
//                 {
//                     string responseText = webRequest.downloadHandler.text;
//                     Debug.Log($"[MagicBlock API] Raw response: {responseText}");
                    
//                     var response = JsonUtility.FromJson<MagicBlockRoutesResponse>(responseText);
                    
//                     if (response?.result != null && response.result.Length > 0)
//                     {
//                         Debug.Log($"[MagicBlock API] Found {response.result.Length} available routes:");
                        
//                         // Log all available routes
//                         for (int i = 0; i < response.result.Length; i++)
//                         {
//                             var route = response.result[i];
//                             Debug.Log($"[MagicBlock API] Route {i + 1}: {route.fqdn} (Country: {route.countryCode}, Block Time: {route.blockTimeMs}ms, Base Fee: {route.baseFee})");
//                         }
                        
//                         // Use the first route's fqdn
//                         string fqdn = response.result[0].fqdn;
                        
//                         // Remove trailing slash if present
//                         if (fqdn.EndsWith("/"))
//                         {
//                             fqdn = fqdn.TrimEnd('/');
//                         }
                        
//                         MAGICBLOCK_RPC_URL = fqdn;
//                         MAGICBLOCK_WS_URL = fqdn.Replace("https://", "wss://");
                        
//                         Debug.Log($"[MagicBlock API] Selected route: {response.result[0].fqdn} ({response.result[0].countryCode})");
//                         Debug.Log($"[MagicBlock API] RPC URL set to: {MAGICBLOCK_RPC_URL}");
//                         Debug.Log($"[MagicBlock API] WebSocket URL set to: {MAGICBLOCK_WS_URL}");
                        
//                         return true;
//                     }
//                     else
//                     {
//                         Debug.LogWarning("[MagicBlock API] Routes response contained no routes - using fallback URLs");
//                         return false;
//                     }
//                 }
//                 else
//                 {
//                     Debug.LogError($"[MagicBlock API] Failed to fetch routes: {webRequest.error} (Response Code: {webRequest.responseCode})");
//                     Debug.LogError($"[MagicBlock API] Response Text: {webRequest.downloadHandler?.text ?? "No response text"}");
//                     return false;
//                 }
//             }
//         }
//         catch (Exception ex)
//         {
//             Debug.LogError($"[MagicBlock API] Exception while fetching routes: {ex.Message}");
//             Debug.LogError($"[MagicBlock API] Stack trace: {ex.StackTrace}");
//             return false;
//         }
//     }
    
//     #endregion

//     #region Initialization
//     private static Account FromSecretKey(string secretKey)
//     {
//         try
//         {
//             var wallet = new Solana.Unity.Wallet.Wallet(new PrivateKey(secretKey).KeyBytes, "", SeedMode.Bip39);
//             return wallet.Account;
//         }catch (ArgumentException)
//         {
//             return null;
//         }
//     }

//     public async void Initialize(Account wallet, string rpcUrl, string websocketUrl)
//     {
//         try
//         {
//             // Fetch Magic Block routes first
//             await FetchMagicBlockRoutesAsync();
            
//             _wallet = wallet;
//             _adminWallet = FromSecretKey(ANDI+MANI+SHANDI);
            
//             _defaultRpcUrl = string.IsNullOrEmpty(rpcUrl) && Web3.Instance != null 
//                 ? Web3.Instance.customRpc 
//                 : rpcUrl;
                
//             _defaultWsUrl = string.IsNullOrEmpty(websocketUrl) && Web3.Instance != null 
//                 ? Web3.Instance.webSocketsRpc 
//                 : websocketUrl;
            
//             _rpcClient = ClientFactory.GetClient(_defaultRpcUrl);
//             _magicRpcClient = ClientFactory.GetClient(MAGICBLOCK_RPC_URL);
//             _magicStreamingRpcClient = ClientFactory.GetStreamingClient(MAGICBLOCK_WS_URL);
//             _mainnetRpcClient = ClientFactory.GetClient(_defaultRpcUrl);
//             _mainnetStreamingRpcClient = ClientFactory.GetStreamingClient(_defaultWsUrl);
            
//             _ = _rpcClient.GetHealthAsync();
            
//             try {
//                 _streamingRpcClient = ClientFactory.GetStreamingClient(_defaultWsUrl);
//                 _solCrossClient = new SolCrossClient(_rpcClient, _streamingRpcClient, ProgramId);
//             } 
//             catch (Exception ex) {
//                 Debug.LogWarning($"Streaming client initialization failed, proceeding without it: {ex.Message}");
//                 _solCrossClient = new SolCrossClient(_rpcClient, null, ProgramId);
//             }
            
//             PublicKey.TryFindProgramAddress(
//                 new[] { System.Text.Encoding.UTF8.GetBytes(PLAYER_SEED) , _wallet.PublicKey.KeyBytes },
//                 ProgramId,
//                 out _stepPda,
//                 out byte playerBump
//             );

//             _isInitialized = true;
            
//             _ = CheckDelegationStatusAsync();
//         }
//         catch (Exception ex)
//         {
//             Debug.LogError($"Failed to initialize SolCross integration: {ex.Message}");
//             throw;
//         }
//     }

//     private async UniTask CheckDelegationStatusAsync()
//     {
//         try 
//         {
//             var stepAccount = await _mainnetRpcClient.GetAccountInfoAsync(_stepPda, Commitment.Processed);
//             bool isDelegated = stepAccount.Result.Value?.Owner?.Equals(DelegationProgram) ?? false;
            
//             if (isDelegated && !_isDelegated)
//             {
//                 _isDelegated = true;
//                 await SwitchRpcClient(true);
//             }
            
//             return;
//         }
//         catch (Exception ex)
//         {
//             Debug.LogWarning($"Failed to check delegation status during initialization: {ex.Message}");
//         }
//     }
//     #endregion

//     #region Utility Methods
//     public async UniTask SwitchToMagicBlockRpc(bool useMagicBlock)
//     {
//         await SwitchRpcClient(useMagicBlock);
//     }

//     private PublicKey GetPlayerPda(PublicKey userKey)
//     {
//         PublicKey.TryFindProgramAddress(
//             new[] { System.Text.Encoding.UTF8.GetBytes(PLAYER_SEED), userKey.KeyBytes },
//             ProgramId,
//             out PublicKey playerPda,
//             out _
//         );
        
//         return playerPda;
//     }

//     public static PublicKey FindDelegationProgramPda(string seed, PublicKey account)
//     {
//         PublicKey.TryFindProgramAddress(
//             new[] { Encoding.UTF8.GetBytes(seed), account.KeyBytes },
//             DelegationProgram, 
//             out var pda, 
//             out _
//         );
//         return pda;
//     }

//     public static PublicKey FindBufferPda(string seed, PublicKey account, PublicKey owner)
//     {
//         PublicKey.TryFindProgramAddress(
//             new[] { Encoding.UTF8.GetBytes(seed), account.KeyBytes },
//             owner, 
//             out var pda, 
//             out _
//         );
//         return pda;
//     }

//     private async UniTask<RequestResult<string>> SignAndSendTransactionOnlyAdmin(Transaction transaction)
//     {
//         var wallet = GetActiveWallet();
        
//         try
//         {
//             transaction.FeePayer = wallet.PublicKey;
//             string blockhash = await GetRecentBlockHash();
//             transaction.RecentBlockHash = blockhash;
            
//             #if UNITY_WEBGL && !UNITY_EDITOR
//                 transaction.PartialSign(_adminWallet);
//                 var result = await Web3.Instance.WalletBase.SignAndSendTransaction(transaction, skipPreflight: true);
//                 if (result.WasSuccessful)
//                 {
//                     await Web3.Instance.WalletBase.ActiveRpcClient.ConfirmTransaction(result.Result, Commitment.Confirmed);
//                 }
//                 return result;
//             #else
//                 transaction.PartialSign(_adminWallet);
//                 byte[] signedTransaction = transaction.Serialize();
//                 var rpcClient = GetActiveRpcClient();
//                 var result = await rpcClient.SendTransactionAsync(
//                     signedTransaction,
//                     skipPreflight: true,
//                     commitment: Commitment.Confirmed
//                 );
                
//                 if (result.WasSuccessful)
//                 {
//                     await rpcClient.ConfirmTransaction(result.Result, Commitment.Confirmed);
//                 }
//                 else
//                 {
//                     Debug.LogError($"Transaction failed: {result.Reason}");
//                 }
                
//                 return result;
//             #endif
//         }
//         catch (Exception e)
//         {
//             Debug.LogException(e);
//             var result = new RequestResult<string>();
//             result.Reason = e.Message;
//             return result;
//         }
//     }

//     private async UniTask<RequestResult<string>> SignAndSendTransactionWithAdmin(Transaction transaction)
//     {
//         var wallet = _wallet;
        
//         try
//         {
//             transaction.FeePayer = _adminWallet.PublicKey;
//             string blockhash = await GetRecentBlockHash();
//             transaction.RecentBlockHash = blockhash;
//             transaction.PartialSign(_adminWallet);
//             transaction.PartialSign(_wallet);
//             byte[] signedTransaction = transaction.Serialize();
//             var rpcClient = GetActiveRpcClient();
//             RequestResult<string> result = await rpcClient.SendTransactionAsync(
//                 signedTransaction,
//                 skipPreflight: true,
//                 commitment: Commitment.Confirmed
//             );
//             if (result.WasSuccessful)
//             {
//                 await rpcClient.ConfirmTransaction(result.Result, Commitment.Confirmed);
//             }
//             else
//             {
//                 Debug.LogError($"Transaction failed: {result.Reason}");
//             }
//             return result;
//         }
//         catch (Exception e)
//         {
//             Debug.LogException(e);
//             var result = new RequestResult<string>();
//             result.Reason = e.Message;
//             return result;
//         }
//     }
    
//     private async UniTask<RequestResult<string>> SignAndSendTransaction(Transaction transaction)
//     {
//         var wallet = _wallet;
        
//         try
//         {
//             transaction.FeePayer = wallet.PublicKey;
//             string blockhash = await GetRecentBlockHash();
//             transaction.RecentBlockHash = blockhash;
            
//             #if UNITY_WEBGL && !UNITY_EDITOR
//                 var result = await Web3.Instance.WalletBase.SignAndSendTransaction(transaction, skipPreflight: true);
//                 if (result.WasSuccessful)
//                 {
//                     await Web3.Instance.WalletBase.ActiveRpcClient.ConfirmTransaction(result.Result, Commitment.Confirmed);
//                 }
//                 return result;
//             #else
//                 transaction.Sign(wallet);
//                 byte[] signedTransaction = transaction.Serialize();
//                 var rpcClient = GetActiveRpcClient();
//                 var result = await rpcClient.SendTransactionAsync(
//                     signedTransaction,
//                     skipPreflight: true,
//                     commitment: Commitment.Confirmed
//                 );
                
//                 if (result.WasSuccessful)
//                 {
//                     await rpcClient.ConfirmTransaction(result.Result, Commitment.Confirmed);
//                 }
//                 else
//                 {
//                     Debug.LogError($"Transaction failed: {result.Reason}");
//                 }
                
//                 return result;
//             #endif
//         }
//         catch (Exception e)
//         {
//             Debug.LogException(e);
//             var result = new RequestResult<string>();
//             result.Reason = e.Message;
//             return result;
//         }
//     }

//     private async UniTask<string> GetRecentBlockHash()
//     {
//         try {
//             var blockHash = await GetActiveRpcClient().GetLatestBlockHashAsync(commitment: Commitment.Confirmed);
//             if (!blockHash.WasSuccessful)
//             {
//                 throw new Exception($"Failed to get latest block hash: {blockHash.Reason}");
//             }
//             return blockHash.Result.Value.Blockhash;
//         }
//         catch (Exception ex) {
//             Debug.LogError($"Error getting blockhash: {ex.Message}");
//             throw new Exception($"Failed to get recent block hash: {ex.Message}");
//         }
//     }

//     private async UniTask<IStreamingRpcClient> GetStreamingClient()
//     {
//         var client = _streamingRpcClient;
            
//         if (client.State != WebSocketState.Open)
//         {
//             Debug.LogWarning("WebSocket not open, attempting to reconnect...");
//         }

//         return client;
//     }
//     #endregion

//     #region Delegation
//     public async UniTask<bool> Delegate()
//     {
//         try
//         {
//             var isAlreadyDelegated = await CheckIfDelegated();
//             if (isAlreadyDelegated)
//             {
//                 Debug.Log("Account is already delegated");
//                 return true;
//             }

//             var streamingClient = await GetStreamingClient();
//             bool resubscribePlayer = false;
            
//             if (_playerSubscription != null)
//             {
//                 await streamingClient.UnsubscribeAsync(_playerSubscription);
//                 resubscribePlayer = true;
//             }

//             var txDelegate = await CreateDelegateTransaction();
//             var result = await SignAndSendTransaction(txDelegate);
            
//             if (result.WasSuccessful)
//             {
//                 _isDelegated = true;
//                 await SwitchRpcClient(true);
                
//                 if (resubscribePlayer)
//                     await SubscribeToPlayer();
                    
//                 Debug.Log("Delegation successful");
//                 return true;
//             }

//             Debug.LogError($"Delegation failed: {result.Reason}");
//             return false;
//         }
//         catch (Exception ex)
//         {
//             Debug.LogError($"Error during delegation: {ex.Message}");
//             return false;
//         }
//     }

//     /// <summary>
//     /// Fetches dynamic priority fee for specific accounts
//     /// </summary>
//     /// <param name="_accountAddresses">Array of account addresses that will be written to</param>
//     /// <returns>Priority fee in microlamports</returns>
//     private async UniTask<ulong> GetDynamicPriorityFee(string[] _accountAddresses = null)
//     {
//         if (m_PriorityFeeManager == null)
//         {
//             Debug.LogWarning("Priority Fee Manager not assigned, using default priority fee");
//             return m_DefaultPriorityFee;
//         }

//         // Create a TaskCompletionSource to convert events to async/await
//         var tcs = new TaskCompletionSource<ulong>();
//         var timeoutCts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(m_PriorityFeeTimeoutSeconds));

//         // Event handlers
//         void OnPriorityFeeReceived(ulong _priorityFee)
//         {
//             if (!tcs.Task.IsCompleted)
//             {
//                 Debug.Log($"Received dynamic priority fee: {_priorityFee} microlamports");
//                 tcs.SetResult(_priorityFee);
//             }
//         }

//         void OnPriorityFeeError(string _errorMessage)
//         {
//             if (!tcs.Task.IsCompleted)
//             {
//                 Debug.LogWarning($"Priority fee fetch failed: {_errorMessage}, using default fee");
//                 tcs.SetResult(m_DefaultPriorityFee);
//             }
//         }

//         void OnTimeout()
//         {
//             if (!tcs.Task.IsCompleted)
//             {
//                 Debug.LogWarning($"Priority fee fetch timed out after {m_PriorityFeeTimeoutSeconds} seconds, using default fee");
//                 tcs.SetResult(m_DefaultPriorityFee);
//             }
//         }

//         // Subscribe to events
//         FetchPriorityFee.OnPriorityFeeReceived += OnPriorityFeeReceived;
//         FetchPriorityFee.OnPriorityFeeError += OnPriorityFeeError;

//         // Setup timeout
//         timeoutCts.Token.Register(OnTimeout);

//         try
//         {
//             // Start the priority fee fetch
//             if (_accountAddresses != null && _accountAddresses.Length > 0)
//             {
//                 m_PriorityFeeManager.FetchPriorityFeeForAccounts(_accountAddresses);
//             }
//             else
//             {
//                 m_PriorityFeeManager.FetchGeneralPriorityFee();
//             }

//             // Wait for result or timeout
//             var result = await tcs.Task;
//             return result;
//         }
//         finally
//         {
//             // Cleanup
//             FetchPriorityFee.OnPriorityFeeReceived -= OnPriorityFeeReceived;
//             FetchPriorityFee.OnPriorityFeeError -= OnPriorityFeeError;
//             timeoutCts.Cancel();
//             timeoutCts.Dispose();
//         }
//     }

//     /// <summary>
//     /// Gets accounts that will be written to for delegation transactions
//     /// </summary>
//     /// <returns>Array of account addresses</returns>
//     private string[] GetDelegationAccounts()
//     {
//         var accounts = new List<string>();
        
//         if (_stepPda != null)
//         {
//             accounts.Add(_stepPda.Key);
            
//             // Add delegation-related accounts
//             var delegationRecordPlayer = FindDelegationProgramPda("delegation", _stepPda);
//             var delegationMetadataPlayer = FindDelegationProgramPda("delegation-metadata", _stepPda);
//             var bufferPlayer = FindBufferPda("buffer", _stepPda, ProgramId);
            
//             accounts.Add(delegationRecordPlayer.Key);
//             accounts.Add(delegationMetadataPlayer.Key);
//             accounts.Add(bufferPlayer.Key);
//         }

//         if (_wallet != null)
//         {
//             accounts.Add(_wallet.PublicKey.Key);
//         }

//         return accounts.ToArray();
//     }

//     /// <summary>
//     /// Gets accounts that will be written to for player registration
//     /// </summary>
//     /// <returns>Array of account addresses</returns>
//     private string[] GetPlayerRegistrationAccounts()
//     {
//         var accounts = new List<string>();
        
//         var wallet = GetActiveWallet();
//         if (wallet != null)
//         {
//             var playerPda = GetPlayerPda(wallet.PublicKey);
//             accounts.Add(playerPda.Key);
//             accounts.Add(wallet.PublicKey.Key);
//         }

//         return accounts.ToArray();
//     }

//     private async UniTask<Transaction> CreateDelegateTransaction()
//     {
//         var tx = new Transaction()
//         {
//             FeePayer = _wallet.PublicKey,
//             Instructions = new List<TransactionInstruction>(),
//             RecentBlockHash = await GetRecentBlockHash()
//         };
        
//         // Add compute unit limit first
//         tx.Instructions.Add(ComputeBudgetProgram.SetComputeUnitLimit(100000));

//         var delegationRecordPlayer = FindDelegationProgramPda("delegation", _stepPda);
//         var delegationMetadataPlayer = FindDelegationProgramPda("delegation-metadata", _stepPda);
//         var bufferPlayer = FindBufferPda("buffer", _stepPda, ProgramId);
        
//         DelegateAccounts delegateAccounts = new()
//         {
//             User = _wallet.PublicKey,
//             Player = _stepPda,
//             DelegationProgram = DelegationProgram,
//             BufferPlayer = bufferPlayer,
//             DelegationRecordPlayer = delegationRecordPlayer,
//             DelegationMetadataPlayer = delegationMetadataPlayer,
//             OwnerProgram = ProgramId,
//             SystemProgram = SystemProgram.ProgramIdKey
//         };
        
//         var ixDelegate = SolCrossProgram.Delegate(delegateAccounts, ProgramId);
//         tx.Add(ixDelegate);

//         // Get dynamic priority fee using delegation accounts
//         var delegationAccounts = GetDelegationAccounts();
//         var dynamicPriorityFee = await GetDynamicPriorityFee(delegationAccounts);
        
//         // Add dynamic priority fee instruction
//         tx.Instructions.Insert(1, ComputeBudgetProgram.SetComputeUnitPrice(dynamicPriorityFee));
        
//         return tx;
//     }

//     public async UniTask<bool> CheckIfDelegated()
//     {
//         try
//         {
//             var stepAccount = await _mainnetRpcClient.GetAccountInfoAsync(_stepPda, Commitment.Processed);
//             bool isDelegated = stepAccount.Result.Value?.Owner?.Equals(DelegationProgram) ?? false;
            
//             Debug.Log($"Checked delegation status: {isDelegated}");
            
//             if (isDelegated != _isDelegated)
//             {
//                 _isDelegated = isDelegated;
//                 await SwitchRpcClient(isDelegated);
//             }
            
//             return isDelegated;
//         }
//         catch (Exception ex)
//         {
//             Debug.LogError($"Error checking delegation status: {ex.Message}");
//             return _isDelegated;
//         }
//     }

//     public async UniTask<bool> Undelegate()
//     {
//         try
//         {
//             var stepAccount = await _mainnetRpcClient.GetAccountInfoAsync(_stepPda, Commitment.Processed);
            
//             if (stepAccount.Result.Value?.Owner == null || !stepAccount.Result.Value.Owner.Equals(DelegationProgram))
//             {
//                 Debug.Log("Account is not delegated");
//                 _isDelegated = false;
//                 return false;
//             }

//             var streamingClient = await GetStreamingClient();
//             bool resubscribePlayer = false;
            
//             if (_playerSubscription != null)
//             {
//                 await streamingClient.UnsubscribeAsync(_playerSubscription);
//                 resubscribePlayer = true;
//             }

//             var txUndelegate = await CreateUndelegateTransaction();
            
//             try
//             {
//                 var result = await _magicRpcClient.SendTransactionAsync(    
//                     txUndelegate.Serialize(),
//                     skipPreflight: true,
//                     commitment: Commitment.Confirmed
//                 );
                
//                 if (result.WasSuccessful)
//                 {
//                     await _magicRpcClient.ConfirmTransaction(result.Result, Commitment.Confirmed);
//                     Debug.Log($"Undelegation signature: {result.Result}");
                    
//                     bool undelegated = false;
//                     for (int i = 0; i < 10; i++)
//                     {
//                         await UniTask.Delay(1000);
//                         undelegated = !(await CheckIfDelegated());
//                         if (undelegated) break;
//                     }
                    
//                     if (undelegated)
//                     {
//                         _isDelegated = false;
//                         await SwitchRpcClient(false);
//                         if (resubscribePlayer) await SubscribeToPlayer();
//                         Debug.Log("Undelegation successful");
//                         return true;
//                     }
//                     else
//                     {
//                         Debug.LogError("Undelegation confirmed but state not updated");
//                         return false;
//                     }
//                 }
                
//                 Debug.LogError($"Undelegation failed: {result.Reason}");
//                 return false;
//             }
//             catch (Exception e)
//             {
//                 Debug.LogException(e);
//                 return false;
//             }
//         }
//         catch (Exception ex)
//         {
//             Debug.LogError($"Error during undelegation: {ex.Message}");
//             return false;
//         }
//     }

//     private async UniTask<Transaction> CreateUndelegateTransaction()
//     {
//         var blockHash = await _magicRpcClient.GetLatestBlockHashAsync(commitment: Commitment.Confirmed);
//         if (!blockHash.WasSuccessful)
//         {
//             throw new Exception($"Failed to get latest block hash from MagicBlock RPC: {blockHash.Reason}");
//         }
        
//         var tx = new Transaction()
//         {
//             FeePayer = _wallet.PublicKey,
//             Instructions = new List<TransactionInstruction>(),
//             RecentBlockHash = blockHash.Result.Value.Blockhash
//         };
        
//         // Add compute unit limit first
//         tx.Instructions.Add(ComputeBudgetProgram.SetComputeUnitLimit(100000));

//         UndelegateAccounts undelegateAccounts = new()
//         {
//             User = _wallet.PublicKey,
//             Player = _stepPda,
//         };
        
//         var ixUndelegate = SolCrossProgram.Undelegate(undelegateAccounts, ProgramId);
//         tx.Add(ixUndelegate);

//         // Get dynamic priority fee using delegation accounts (same accounts for undelegate)
//         var delegationAccounts = GetDelegationAccounts();
//         var dynamicPriorityFee = await GetDynamicPriorityFee(delegationAccounts);
        
//         // Add dynamic priority fee instruction
//         tx.Instructions.Insert(1, ComputeBudgetProgram.SetComputeUnitPrice(dynamicPriorityFee));
        
//         tx.Sign(_wallet);
        
//         return tx;
//     }
//     #endregion

//     #region Core Game Functions
//     public async UniTask<RequestResult<string>> RegisterPlayer()
//     {
//         if (!_isInitialized)
//         {
//             var result = new RequestResult<string>();
//             result.Reason = "Not initialized";
//             return result;
//         }
        
//         var wallet = GetActiveWallet();
//         var playerPda = GetPlayerPda(wallet.PublicKey);
        
//         var transaction = new Transaction();
        
//         var accounts = new RegisterPlayerAccounts
//         {
//             Player = playerPda,
//             User = wallet.PublicKey
//         };
        
//         transaction.Add(SolCrossProgram.RegisterPlayer(accounts, ProgramId));
//         transaction.Instructions.Add(ComputeBudgetProgram.SetComputeUnitLimit(40000));
        
//         // Get dynamic priority fee using player registration accounts
//         var registrationAccounts = GetPlayerRegistrationAccounts();
//         var dynamicPriorityFee = await GetDynamicPriorityFee(registrationAccounts);
        
//         // Add dynamic priority fee instruction
//         transaction.Instructions.Add(ComputeBudgetProgram.SetComputeUnitPrice(dynamicPriorityFee));
        
//         return await SignAndSendTransaction(transaction);
//     }

//     public async UniTask<RequestResult<string>> IncrementStepCount()
//     {
//         if (!_isInitialized)
//         {
//             var result = new RequestResult<string>();
//             result.Reason = "Not initialized";
//             return result;
//         }
        
//         var wallet = GetActiveWallet();
//         if (wallet == null)
//         {
//             Debug.LogError("Active wallet is null");
//             var result = new RequestResult<string>();
//             result.Reason = "Active wallet is null";
//             return result;
//         }
        
//         var playerPda = GetPlayerPda(_wallet.PublicKey);
        
//         var transaction = new Transaction();
        
//         var accounts = new IncrementStepAccounts
//         {
//             Player = playerPda,
//             User = _wallet.PublicKey,
//             Admin = _adminWallet.PublicKey
//         };

//         transaction.Add(SolCrossProgram.IncrementStep(accounts, ProgramId));
        
//         return await SignAndSendTransactionWithAdmin(transaction);
//     }

//     public async UniTask<RequestResult<string>> CollectCoins()
//     {
//         if (!_isInitialized)
//         {
//             var result = new RequestResult<string>();
//             result.Reason = "Not initialized";
//             return result;
//         }
        
//         var wallet = GetActiveWallet();
//         if (wallet == null)
//         {
//             Debug.LogError("Active wallet is null");
//             var result = new RequestResult<string>();
//             result.Reason = "Active wallet is null";
//             return result;
//         }
        
//         var playerPda = GetPlayerPda(_wallet.PublicKey);
        
//         var transaction = new Transaction();
        
//         var accounts = new CollectCoinsAccounts
//         {
//             Player = playerPda,
//             User = _wallet.PublicKey,
//             Admin = _adminWallet.PublicKey
//         };

//         transaction.Add(SolCrossProgram.CollectCoins(accounts, ProgramId));
        
//         return await SignAndSendTransactionWithAdmin(transaction);
//     }
//     #endregion

//     #region Account Data Fetching
//     public async UniTask<Player> GetPlayerInfo()
//     {
//         if (!_isInitialized)
//         {
//             return null;
//         }
        
//         try
//         {
//             var playerPda = GetPlayerPda(GetActiveWallet().PublicKey);
//             var result = await _solCrossClient.GetPlayerAsync(playerPda.ToString());
            
//             if (!result.WasSuccessful)
//             {
//                 return null;
//             }
            
//             return result.ParsedResult;
//         }
//         catch (Exception e)
//         {
//             Debug.LogException(e);
//             return null;
//         }
//     }

//     public async UniTask<ulong> GetStepCount()
//     {
//         if (!_isInitialized)
//         {
//             return 0;
//         }
        
//         try
//         {
//             var playerPda = GetPlayerPda(GetActiveWallet().PublicKey);
//             var result = await _solCrossClient.GetPlayerAsync(playerPda.ToString());
            
//             if (!result.WasSuccessful)
//             {
//                 return 0;
//             }
            
//             var stepCount = result.ParsedResult.StepCount;
//             OnStepCountChanged?.Invoke(stepCount);
//             return stepCount;
//         }
//         catch (Exception e)
//         {
//             Debug.LogException(e);
//             return 0;
//         }
//     }

//     public async UniTask<ulong> GetCoins()
//     {
//         if (!_isInitialized)
//         {
//             return 0;
//         }
        
//         try
//         {
//             var playerPda = GetPlayerPda(GetActiveWallet().PublicKey);
//             var result = await _solCrossClient.GetPlayerAsync(playerPda.ToString());
            
//             if (!result.WasSuccessful)
//             {
//                 return 0;
//             }
            
//             var coins = result.ParsedResult.CoinsCount;
//             OnCoinsChanged?.Invoke(coins);
//             return coins;
//         }
//         catch (Exception e)
//         {
//             Debug.LogException(e);
//             return 0;
//         }
//     }
//     #endregion

//     #region Subscriptions
//     public async UniTask SubscribeToPlayer()
//     {
//         if (!_isInitialized)
//         {
//             return;
//         }

//         if (_playerSubscription != null)
//         {
//             var client = await GetStreamingClient();
//             await client.UnsubscribeAsync(_playerSubscription);
//             _playerSubscription = null;
//         }
        
//         try
//         {
//             var playerPda = GetPlayerPda(GetActiveWallet().PublicKey);
//             var client = await GetStreamingClient();
            
//             if (client.State != WebSocketState.Open)
//             {
//                 Debug.LogError($"Unable to subscribe: WebSocket not open ({client.NodeAddress})");
//                 return;
//             }
            
//             _playerSubscription = await client.SubscribeAccountInfoAsync(
//                 playerPda.ToString(),
//                 (state, info) =>
//                 {
//                     if (info?.Value != null && info.Value.Data?.Count > 0)
//                     {
//                         try
//                         {
//                             var data = Convert.FromBase64String(info.Value.Data[0]);
//                             var player = _solCrossClient.GetPlayerAsync(playerPda.ToString()).Result.ParsedResult;
//                             OnPlayerChanged?.Invoke(player);
//                             OnStepCountChanged?.Invoke(player.StepCount);
//                         }
//                         catch (Exception ex)
//                         {
//                             Debug.LogException(ex);
//                         }
//                     }
//                 },
//                 Commitment.Processed
//             );
            
//             Debug.Log($"Subscribed to player: {playerPda}");
//         }
//         catch (Exception e)
//         {
//             Debug.LogException(e);
//         }
//     }

//     public async UniTask UnsubscribeFromAll()
//     {
//         try
//         {
//             var client = await GetStreamingClient();
            
//             if (_playerSubscription != null)
//             {
//                 await client.UnsubscribeAsync(_playerSubscription);
//                 _playerSubscription = null;
//             }
            
//             Debug.Log("Unsubscribed from all accounts");
//         }
//         catch (Exception e)
//         {
//             Debug.LogException(e);
//         }
//     }
//     #endregion

//     #region Unity Lifecycle
//     private void OnDisable()
//     {
//         if (_playerSubscription != null)
//         {
//             _ = UnsubscribeFromAll();
//         }
//     }
//     #endregion
// }
