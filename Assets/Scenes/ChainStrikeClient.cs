// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Text;
// using System.Threading.Tasks;
// using Cysharp.Threading.Tasks;
// using KamikazeJoe;
// using KamikazeJoe.Accounts;
// using KamikazeJoe.Program;
// using KamikazeJoe.Types;
// using MoreMountains.Tools;
// using MoreMountains.TopDownEngine;
// using Solana.Unity.Programs;
// using Solana.Unity.Rpc.Core.Http;
// using Solana.Unity.Rpc.Core.Sockets;
// using Solana.Unity.Rpc.Messages;
// using Solana.Unity.Rpc.Models;
// using Solana.Unity.Rpc.Types;
// using Solana.Unity.SDK;
// using Solana.Unity.Soar;
// using Solana.Unity.Soar.Program;
// using Solana.Unity.Wallet;
// using TMPro;
// using UnityEngine;
// using UnityEngine.UI;
// using InitializeGameAccounts = KamikazeJoe.Program.InitializeGameAccounts;
// using Random = System.Random;

// // ReSharper disable once CheckNamespace

// namespace KamikazeJoe.Types
// {
//     public enum Cell : byte
//     {
//         Empty,
//         Block,
//         Recharge
//     }
// }

// public class ChainStrikeClient : MonoBehaviour
// {
//     [SerializeField]
//     private Button joinGameBtn;
    
//     [SerializeField]
//     private Button newGameBtn;
    
//     [SerializeField]
//     private Button joinRandomArenaBtn;
    
//     [SerializeField]
//     private MMTouchButton publicCheckBox;
    
//     [SerializeField]
//     private TMP_InputField txtArenaSize;
    
//     [SerializeField]
//     private TMP_InputField txtPricePool;
    
    
//     private PublicKey _gameInstanceId;
//     private PublicKey _userPda;

//     private readonly PublicKey _kamikazeJoeProgramId = new("MV1U5NcXJY5Xmrg1UyeAWsVERkBLHyoAVLCPFwdW8k6");
    
//     // Kamikaze Joe client
//     private KamikazeJoeClient _kamikazeJoeClient;
//     private KamikazeJoeClient KamikazeJoeClient => _kamikazeJoeClient ??= 
//         new KamikazeJoeClient(Web3.Rpc, Web3.WsRpc, _kamikazeJoeProgramId);
    
//     // Soar client
//     private SoarClient _soarClient;
//     private SoarClient SoarClient => _soarClient ??= new SoarClient(Web3.Rpc, Web3.WsRpc);

//     private static readonly int[][] SpawnPoints = {
//         new[] { 1, 1}, new[] { 27, 26},
//         new[] { 1, 26}, new[] { 26, 1},
//         new[] { 1, 15}, new[] { 26, 15},
//         new[] { 7, 7}, new[] { 3, 3},
//         new[] { 1, 15}, new[] { 26, 15} 
//     };
//     private bool _isMoving;
//     private bool _initPlayer = true;
//     private Facing _prevMove;
    
//     private int _prevPlayersLength = 0;

//     // RPC switching
//     private string _defaultRpcUrl = "https://alvira-k01xm4-fast-devnet.helius-rpc.com";
//     private string _defaultWsUrl = "wss://alvira-k01xm4-fast-devnet.helius-rpc.com";
//     private string _magicblockRpcUrl = "https://devnet.magicblock.app";
//     private string _magicblockWsUrl = "wss://devnet.magicblock.app";
//     private SubscriptionState _gameSubscription;



//     private void OnEnable()
//     {
//         CharacterGridMovement.OnGridMovementEvent += OnMove;
//         DetectEnergyChange.OnExplosion += OnExplosion;
//         Web3.OnLogin += OnLogin;
//     }

//     private void OnDisable()
//     {
//         CharacterGridMovement.OnGridMovementEvent -= OnMove;
//         DetectEnergyChange.OnExplosion -= OnExplosion;
//         Web3.OnLogin -= OnLogin;
//         UnsubscribeFromGame().Forget();
//     }

//     private void OnLogin(Account account)
//     {
//         var prevMatch = PlayerPrefs.GetString("gameID", null);
//         if (prevMatch != null && account.PublicKey.ToString().Equals(PlayerPrefs.GetString("pkPlayer", null)))
//         {
//             JoinGame(prevMatch).Forget();
//         }
//         else
//         {
//             UIManger.Instance.ToogleMenu();
//         }
//     }


//     private void OnMove(CharacterGridMovement.GridDirections direction)
//     {
//         if(Web3.Account == null) return;
//         if(_gameInstanceId == null) return;
//         if(direction == CharacterGridMovement.GridDirections.None) return;
//         MakeMove(UIManger.UnMapFacing(direction), CharacterGridMovement.EnergyToUse).Forget();
//     }
    
//     private void OnExplosion()
//     {
//         if(Web3.Account == null) return;
//         if(_gameInstanceId == null) return;
//         MakeExplosion().Forget();
//     }

//     void Start()
//     {
//         if (newGameBtn != null) newGameBtn.onClick.AddListener(CallCreateGame);
//         if (joinGameBtn != null) joinGameBtn.onClick.AddListener(CallJoinGame);
//         if (joinRandomArenaBtn != null) joinRandomArenaBtn.onClick.AddListener(CallJoinRandomArena);
//         //_toast = GetComponent<Toast>();
//         txtArenaSize.onEndEdit.AddListener(ClampArenaSize);
//     }

//     private void Update()
//     {
//         if (Input.GetKeyDown(KeyCode.Alpha1))
//         {
//             Debug.Log("Delegate pressed [1] 🪄");
//             Delegate().Forget();
//         }
//         else if (Input.GetKeyDown(KeyCode.Alpha0))
//         {
//             Debug.Log("Undelegate pressed [0] 🧹");
//             Undelegate().Forget();
//         }
//     }

//     private void CallCreateGame()
//     {
//         if (!int.TryParse(txtArenaSize.text, out int arenaSize))
//         {
//             arenaSize = 30;
//         }
//         if (!float.TryParse(txtPricePool.text, out float pricePool))
//         {
//             pricePool = 0;
//         }
//         CreateGame(arenaSize, (ulong)(pricePool * Math.Pow(10, 9))).Forget();
//     }
    
//     private void CallJoinGame()
//     {
//         var gameId = UIManger.Instance.GetGameID();
//         JoinGame(gameId).Forget();
//     }
    
//     private void CallJoinRandomArena()
//     {
//         JoinRandomArena().Forget();
//     }

//     private async UniTask ReloadGame()
//     {
//         Debug.Log("Reloading game");
//         var game = (await KamikazeJoeClient.GetGameAsync(_gameInstanceId, Commitment.Processed)).ParsedResult;
//         SetGame(game);
//     }

//     private async UniTask JoinGame(string gameId)
//     {
//         if(Web3.Account == null) return;
//         Loading.StartLoading();
//         var game = (await KamikazeJoeClient.GetGameAsync(gameId, Commitment.Confirmed)).ParsedResult;
//         if(game == null) return;

//         var mustJoin = !game.Players.Select(p => p.Address).Contains(Web3.Account);
//         if (mustJoin)
//         {
//             var res = await CreateGameTransaction(gameId);
//             Debug.Log($"Signature: {res.Result}");
//             if (res.WasSuccessful)
//             {
//                 await Web3.Rpc.ConfirmTransaction(res.Result, Commitment.Confirmed);
//             }
//             Debug.Log("Joined Game");
//             // Delegation is manual now
//         }
//         game = (await KamikazeJoeClient.GetGameAsync(gameId, Commitment.Confirmed)).ParsedResult;
//         Loading.StopLoading();
//         _gameInstanceId = new PublicKey(gameId);
//         UIManger.Instance.SetGameID(_gameInstanceId);
//         _initPlayer = true;
//         SetGame(game);
//         Debug.Log($"Subscribing to game");
//         SubscribeToGame(new PublicKey(gameId)).Forget();
//         Debug.Log($"Game Id: {gameId}");
//     }
    
//     private async UniTask CreateGame(int arenaSize, ulong pricePoolLamports)
//     {
//         if(Web3.Account == null) return;
//         Loading.StartLoading();
//         var userPda = FindUserPda(Web3.Account);
//         User userAccount = null;
//         if (await IsPdaInitialized(userPda))
//         {
//             userAccount = (await KamikazeJoeClient.GetUserAsync(userPda, Commitment.Confirmed)).ParsedResult;
//         }
//         var gamePdaIdx = userAccount == null ? 0 : userAccount.Games;
//         Debug.Log($"Searching game PDA");
//         PublicKey gamePda = FindGamePda(userPda, gamePdaIdx);
//         Debug.Log($"Sending transaction new Game");
//         var res = await CreateGameTransaction(
//             gamePda, 
//             publicMatch: publicCheckBox == null || publicCheckBox.GetComponent<Image>().sprite.name.Equals("CheckboxOff"),
//             arenaSize,
//             pricePoolLamports);
//         Debug.Log($"Signature: {res.Result}");
//         if (res.WasSuccessful)
//         {
//             await Web3.Rpc.ConfirmTransaction(res.Result, Commitment.Confirmed);
            
//             Game game = null;
//             var retry = 5;
//             while (game == null && retry > 0)
//             {
//                 game = (await KamikazeJoeClient.GetGameAsync(gamePda, Commitment.Confirmed)).ParsedResult;
//                 retry--;
//                 await UniTask.Delay(TimeSpan.FromSeconds(1));
//             }
//             Debug.Log($"Game retrieved");
//             _gameInstanceId = gamePda;
//             UIManger.Instance.SetGameID(_gameInstanceId);
//             Debug.Log($"Setting game");
//             _initPlayer = true;
//             SetGame(game);
//             Debug.Log($"Subcribing to game");
//             SubscribeToGame(gamePda).Forget();
//             Debug.Log($"Game Id: {gamePda}");
//         }
//         Loading.StopLoading();
//         UIManger.Instance.StartReceivingInput();
//         // Delegation is manual now
//     }
    
//     private async UniTask JoinRandomArena()
//     {
//         if(Web3.Account == null) return;
//         Loading.StartLoading();
//         var gameToJoin = await FindGameToJoin();
//         if (gameToJoin != null)
//         {
//             await JoinGame(gameToJoin);
//         }
//         if(gameToJoin == null) Debug.Log("Unable to find a game to join");
//         Loading.StopLoading();
//     }
    
//     private async UniTask ClaimReward(string gameId)
//     {
//         if(Web3.Account == null) return;
//         var game = (await KamikazeJoeClient.GetGameAsync(gameId, Commitment.Confirmed)).ParsedResult;
//         if(game == null) return;
//         if(game.TicketPrice == 0 || game.PrizeClaimed || game.GameState?.WonValue?.Winner != Web3.Account.PublicKey) return;

//         var res = await ClaimRewardTransaction();
//         Debug.Log($"Signature: {res?.Result}");
//         if(res == null) return;
//         if (res.WasSuccessful)
//         {
//             await Web3.Rpc.ConfirmTransaction(res.Result, Commitment.Confirmed);
//         }
//         Debug.Log("Claimed Reward");
//     }
    
//     private async UniTask MakeMove(Facing facing, int energy, int retry = 5)
//     {
//         if(Web3.Account == null) return;
//         if(_gameInstanceId == null) return;
//         if(_isMoving && retry == 5) return;
//         UIManger.Instance.StopReceivingInput();
//         Loading.StartLoadingSmall();
//         _isMoving = true; 
//         var res = await MakeMoveTransaction(facing, energy, useCache: retry == 5 && facing != _prevMove);
//         _prevMove = facing;
//         Debug.Log($"Signature: {res.Result}");
//         if (res.WasSuccessful)
//         {
//             await Web3.Rpc.ConfirmTransaction(res.Result, Commitment.Confirmed);
//             _prevMove = facing;
//             Debug.Log("Made a move");
//         }
//         else
//         {
//             if (retry > 0)
//             {
//                 Debug.Log("Retrying to move");
//                 await MakeMove(facing, energy, retry - 1);
//                 await ReloadGame(); 
//             }
//             else
//             {
//                 Debug.Log("Failed to move");
//                 await ReloadGame(); 
//             }
//         }
//         await UIManger.Instance.WaitCharacterIdle();
//         await ReloadGame();
//         Loading.StopLoadingSmall();
//         _isMoving = false;
//         UIManger.Instance.StartReceivingInput();
//     }
    
//     private async UniTask MakeExplosion()
//     {
//         if(Web3.Account == null) return;
//         if(_gameInstanceId == null) return;
//         var res = await MakeExplosionTransaction();
//         Debug.Log($"Signature: {res.Result}");
//         if (res.WasSuccessful)
//         {
//             await Web3.Rpc.ConfirmTransaction(res.Result, Commitment.Confirmed);
//             Debug.Log("Exploded");
//             await ReloadGame();
//         }
//         else
//         {
//             Debug.Log("Failed to explode");
//             await ReloadGame();
//         }
//     }
    
//     private async UniTask SubscribeToGame(PublicKey gameId)
//     {
//         await UnsubscribeFromGame();
//         _gameSubscription = await KamikazeJoeClient.SubscribeGameAsync(gameId, OnGameUpdate, Commitment.Processed);
//     }

//     private async UniTask UnsubscribeFromGame()
//     {
//         try
//         {
//             if (_gameSubscription != null && Web3.WsRpc != null)
//             {
//                 await Web3.WsRpc.UnsubscribeAsync(_gameSubscription);
//                 _gameSubscription = null;
//             }
//         }
//         catch (Exception e)
//         {
//             Debug.LogWarning($"Unsubscribe failed: {e.Message}");
//         }
//     }

//     private void OnGameUpdate(SubscriptionState subState, ResponseValue<AccountInfo> gameInfo, Game game)
//     {
//         Debug.Log("Game updated");
//         SetGame(game);
//     }

//     private async void SetGame(Game game)
//     {
//         if (_initPlayer) UIManger.Instance.ResetLevel();
//         UIManger.Instance.SetGrid(BuildCells(game.Width, game.Height, game.Seed));
//         UIManger.Instance.SetCharacters(game.Players);
//         if (_initPlayer)
//         {
//             UIManger.Instance.ResetEnergy();
//             UIManger.Instance.StartReceivingInput();
//             _initPlayer = false;
//         }

//         if (game.Players.Length != _prevPlayersLength)
//         {
//             var pricePool = game.TicketPrice / Math.Pow(10, 9) * game.Players.Length * 0.9;
//             UIManger.Instance.SetPrizePool((float)Math.Round(pricePool, 2));
//             _prevPlayersLength = game.Players.Length;
//         }
//         if(game.GameState?.Type == GameStateType.Won && game.GameState.WonValue.Winner == Web3.Account?.PublicKey)
//         {
//             UIManger.Instance.ShowWinningScreen();
//             ClaimReward(_gameInstanceId).Forget();
//         }

//         // Session wallet removed
//     }
    
//     #region Transactions

//         private async Task<RequestResult<string>> CreateGameTransaction(
//             string gameId, 
//             bool publicMatch = true, 
//             int arenaSize = 30, 
//             ulong pricePoolLamports = 0)
//         {
//             var tx = new Transaction()
//             {
//                 FeePayer = Web3.Account,
//                 Instructions = new List<TransactionInstruction>(),
//                 RecentBlockHash = await Web3.BlockHash(useCache: false, commitment: Commitment.Confirmed)
//             };
            
//             var userPda = FindUserPda(Web3.Account);
//             var matchesPda = FindMatchesPda();
//             var vaultPda = FindVaultPda();
            
//             if (!await IsPdaInitialized(vaultPda))
//             {
//                 var initializeAccounts = new InitializeAccounts()
//                 {
//                     Payer = Web3.Account,
//                     Matches = matchesPda,
//                     Vault = vaultPda,
//                     SystemProgram = SystemProgram.ProgramIdKey
//                 };
//                 var initIx = KamikazeJoeProgram.Initialize(accounts: initializeAccounts, _kamikazeJoeProgramId);
//                 tx.Add(initIx);
//             }
            
//             if (!await IsPdaInitialized(userPda))
//             {
//                 var accountsInitUser = new InitializeUserAccounts()
//                 {
//                     Payer = Web3.Account,
//                     User = userPda,
//                     SystemProgram = SystemProgram.ProgramIdKey
//                 };
//                 var initUserIx = KamikazeJoeProgram.InitializeUser(accounts: accountsInitUser, _kamikazeJoeProgramId);
//                 tx.Add(initUserIx);
//             }

//             var gamePda = new PublicKey(gameId);
//             if (!await IsPdaInitialized(gamePda))
//             {
//                 var accountsInitGame = new InitializeGameAccounts()
//                 {
//                     Creator = Web3.Account,
//                     User = userPda,
//                     Game = gamePda,
//                     Matches = publicMatch ? FindMatchesPda() : null,
//                     SystemProgram = SystemProgram.ProgramIdKey
//                 };
//                 var initGameIx = KamikazeJoeProgram.InitializeGame(
//                     accounts: accountsInitGame, 
//                     (byte?)arenaSize,
//                     (byte?)arenaSize,
//                     null,
//                     (ulong?)pricePoolLamports,
//                     _kamikazeJoeProgramId
//                 );
//                 tx.Add(initGameIx);
//             }

//             var joinGameAccounts = new JoinGameAccounts()
//             {
//                 Player = Web3.Account,
//                 User = userPda,
//                 Game = gamePda,
//                 Vault = FindVaultPda(),
//                 SystemProgram = SystemProgram.ProgramIdKey
//             };

//             var spawnPoint = FindValidSpawnPoint(30, 30, 0);
//             var joinGameIx = KamikazeJoeProgram.JoinGame(accounts: joinGameAccounts, (byte) spawnPoint[0], (byte) spawnPoint[1], _kamikazeJoeProgramId);
//             tx.Instructions.Add(joinGameIx);
            
//             #region Soar initialization
            
//             // Soar initialization (safe-guarded)
//             try
//             {
//                 var playerAccount = SoarPda.PlayerPda(Web3.Account);
//                 var leaderboardResult = await KamikazeJoeClient.GetLeaderboardAsync(FindSoarPda(), Commitment.Confirmed);
//                 if (leaderboardResult.WasSuccessful && leaderboardResult.ParsedResult != null)
//                 {
//                     var leaderboard = leaderboardResult.ParsedResult;
//                     var playerScores = SoarPda.PlayerScoresPda(playerAccount, leaderboard.LeaderboardField);

//                     if (!await IsPdaInitialized(SoarPda.PlayerPda(Web3.Account)))
//                     {
//                         var accountsInitPlayer = new InitializePlayerAccounts()
//                         {
//                             Payer = Web3.Account,
//                             User = Web3.Account,
//                             PlayerAccount = playerAccount,
//                             SystemProgram = SystemProgram.ProgramIdKey
//                         };
//                         var initPlayerIx = SoarProgram.InitializePlayer(
//                             accounts: accountsInitPlayer,
//                             username: PlayerPrefs.GetString("web3AuthUsername", ""),
//                             nftMeta: PublicKey.DefaultPublicKey,
//                             SoarProgram.ProgramIdKey
//                         );
//                         tx.Add(initPlayerIx);
//                     }

//                     if (!await IsPdaInitialized(playerScores))
//                     {
//                         var registerPlayerAccounts = new RegisterPlayerAccounts()
//                         {
//                             Payer = Web3.Account,
//                             User = Web3.Account,
//                             PlayerAccount = playerAccount,
//                             Game = leaderboard.Game,
//                             Leaderboard = leaderboard.LeaderboardField,
//                             NewList = playerScores,
//                             SystemProgram = SystemProgram.ProgramIdKey
//                         };
//                         var registerPlayerIx = SoarProgram.RegisterPlayer(
//                             registerPlayerAccounts,
//                             SoarProgram.ProgramIdKey
//                         );
//                         tx.Add(registerPlayerIx);
//                     }
//                 }
//             }
//             catch (Exception e)
//             {
//                 Debug.LogWarning($"Skipping Soar init: {e.Message}");
//             }
            
//             #endregion

//             return await SignAndSendWithToast(tx, "Join Game");
//         }

//         // Session wallet removed

//         private int[] FindValidSpawnPoint(int width, int height, uint seed)
//         {
//             var r = new Random();
//             var point = SpawnPoints[r.Next(0, SpawnPoints.Length)];
//             while (!IsValidCell(point[0], point[1], width, height, seed))
//             {
//                 point = SpawnPoints[r.Next(0, SpawnPoints.Length)];
//             }
//             return point;
//         }

//         private async Task<RequestResult<string>> MakeMoveTransaction(Facing facing, int energy, bool useCache = true)
//         {
//             UpdateWeb3InstanceRpc(true);
//             var tx = new Transaction()
//             {
//                 FeePayer = Web3.Account,
//                 Instructions = new List<TransactionInstruction>(),
//                 RecentBlockHash = await Web3.BlockHash(commitment: Commitment.Confirmed, useCache: useCache)
//             };
        
//             var accounts = new MakeMoveAccounts()
//             {
//                 Payer = Web3.Account,
//                 User = _userPda != null ? _userPda : FindUserPda(Web3.Account),
//                 Game = _gameInstanceId,
//             };
            
//             var movePieceIx = KamikazeJoeProgram.MakeMove(accounts, facing, (byte) energy, _kamikazeJoeProgramId);
        
//             //tx.Instructions.Add(ComputeBudgetProgram.SetComputeUnitLimit(600000));
//             tx.Instructions.Add(movePieceIx);

//             return await SignAndSendWithToast(tx, $"Move {facing}");
//         }
    

//         private async Task<RequestResult<string>> MakeExplosionTransaction()
//         {
//             var tx = new Transaction()
//             {
//                 FeePayer = Web3.Account,
//                 Instructions = new List<TransactionInstruction>(),
//                 RecentBlockHash = await Web3.BlockHash(commitment: Commitment.Confirmed, useCache: false)
//             };
        
//             var accounts = new ExplodeAccounts()
//             {
//                 Payer = Web3.Account,
//                 User = _userPda != null ? _userPda : FindUserPda(Web3.Account),
//                 Game = _gameInstanceId
//             };
//             var explodeIx = KamikazeJoeProgram.Explode(accounts, _kamikazeJoeProgramId);
        
//             tx.Instructions.Add(ComputeBudgetProgram.SetComputeUnitLimit(600000));
//             tx.Instructions.Add(explodeIx);

//             return await SignAndSendWithToast(tx, "Explosion");
//         }

//         private async Task<RequestResult<string>> ClaimRewardTransaction()
//         {
//             var game = (await KamikazeJoeClient.GetGameAsync(_gameInstanceId, Commitment.Confirmed)).ParsedResult;
//             if (game == null || game.GameState?.WonValue?.Winner == null)
//             {
//                 Debug.LogError($"Can't claim price for game: {_gameInstanceId}");
//                 return null;
//             }
//             var tx = new Transaction()
//             {
//                 FeePayer = Web3.Account,
//                 Instructions = new List<TransactionInstruction>(),
//                 RecentBlockHash = await Web3.BlockHash(commitment: Commitment.Confirmed, useCache: false)
//             };

//             var claimPrizeAccounts = new ClaimPrizeAccounts()
//             {
//                 Payer = Web3.Account,
//                 User = FindUserPda(game.GameState.WonValue.Winner),
//                 Receiver = game.GameState.WonValue.Winner,
//                 Game = _gameInstanceId,
//                 Vault = FindVaultPda(),
//                 SystemProgram = SystemProgram.ProgramIdKey
//             };
        
//             var claimPrizeIx = KamikazeJoeProgram.ClaimPrize(accounts: claimPrizeAccounts, _kamikazeJoeProgramId);
//             tx.Instructions.Add(claimPrizeIx);
        
//             return await SignAndSendWithToast(tx, "Claim Prize");
//         }
        
       
//         private async Task<RequestResult<string>> SignAndSendTransaction(Transaction tx)
//         {
//             return await SignAndSendWithToast(tx, "Transaction");
//         }

//         private async Task<RequestResult<string>> SignAndSendWithToast(Transaction tx, string actionLabel)
//         {
//             // Use a more unique sender key to reduce collisions
//             string sender = $"{actionLabel} {DateTime.UtcNow:HH:mm:ss.fff} #{UnityEngine.Random.Range(0, 1000)}";
//             try { global::NotificationManager.Instance?.ShowTransactionToast(sender); } catch {}
//             var res = await Web3.Wallet.SignAndSendTransaction(tx, skipPreflight: true, commitment: Commitment.Confirmed);
//             if (!string.IsNullOrEmpty(res?.Result))
//             {
//                 try { global::NotificationManager.Instance?.UpdateTransactionToast(sender, res.Result); } catch {}
//             }
//             return res;
//         }
    
//         #endregion

//         #region PDAs utils
    
//         private async UniTask<PublicKey> FindGameToJoin()
//         {
//             PublicKey gameToJoin = null;
//             var matchesPda = FindMatchesPda();
//             var matches = await KamikazeJoeClient.GetMatchesAsync(matchesPda, Commitment.Confirmed);
//             if (matches.WasSuccessful && matches.ParsedResult != null)
//             {
//                 foreach (var activeGame in matches.ParsedResult.ActiveGames.Reverse())
//                 {
//                     var game = await KamikazeJoeClient.GetGameAsync(activeGame, Commitment.Confirmed);
//                     if(game != null && game.WasSuccessful && game.ParsedResult != null
//                        && game.ParsedResult.GameState.Type is GameStateType.Active or GameStateType.Waiting
//                        && !game.ParsedResult.Players.Select(p => p.Address).Contains(Web3.Account.PublicKey)
//                        && game.ParsedResult.Players.Length < 10)
//                     {
//                         gameToJoin = activeGame;
//                         break;
//                     }
//                 }
//             }
//             return gameToJoin;
//         }

//         private async UniTask<bool> IsPdaInitialized(PublicKey pda)
//         {
//             var accountInfoAsync = await Web3.Rpc.GetAccountInfoAsync(pda);
//             return accountInfoAsync.WasSuccessful && accountInfoAsync.Result?.Value != null;
//         }
    
//         private PublicKey FindGamePda(PublicKey accountPublicKey, uint gameId = 0)
//         {
//             PublicKey.TryFindProgramAddress(new[]
//             {
//                 Encoding.UTF8.GetBytes("game"), accountPublicKey, BitConverter.GetBytes(gameId).Reverse().ToArray()
//             }, _kamikazeJoeProgramId, out var pda, out _);
//             return pda;
//         }
    
//         private PublicKey FindUserPda(PublicKey accountPublicKey)
//         {
//             PublicKey.TryFindProgramAddress(new[]
//             {
//                 Encoding.UTF8.GetBytes("user-pda"), accountPublicKey
//             }, _kamikazeJoeProgramId, out var pda, out _);
//             return pda;
//         }
    
//         private PublicKey FindMatchesPda()
//         {
//             PublicKey.TryFindProgramAddress(new[]
//             {
//                 Encoding.UTF8.GetBytes("matches")
//             }, _kamikazeJoeProgramId, out var pda, out _);
//             return pda;
//         }
        
//         private PublicKey FindSoarPda()
//         {
//             PublicKey.TryFindProgramAddress(new[]
//             {
//                 Encoding.UTF8.GetBytes("soar-leaderboard")
//             }, _kamikazeJoeProgramId, out var pda, out _);
//             return pda;
//         }
    
//         private PublicKey FindVaultPda()
//         {
//             PublicKey.TryFindProgramAddress(new[]
//             {
//                 Encoding.UTF8.GetBytes("vault")
//             }, _kamikazeJoeProgramId, out var pda, out _);
//             return pda;
//         }

//         #endregion
    
//         #region Build Grid

//         private Cell[][] BuildCells(uint width, uint height, uint seed)
//         {
//             var w = (int)width;
//             var h = (int)height;
//             Cell[][] cells = new Cell[w][];
//             for (int x = 0; x < w; x++)
//             {
//                 cells[x] = new Cell[h];
//                 for (int y = 0; y < h; y++)
//                 {
//                     if (IsRecharger(x, y, seed))
//                     {
//                         cells[x][y] = Cell.Recharge;
//                     }
//                     else if (IsBlock(x, y, seed))
//                     {
//                         cells[x][y] = Cell.Block;
//                     }
//                     else
//                     {
//                         cells[x][y] = Cell.Empty;
//                     }
//                 }
//             }
//             return cells;
//         }

//         private bool IsValidCell(int x, int y, int width, int height, uint seed)
//         {
//             return x < width && y < height && (IsRecharger(x, y, seed) || !IsBlock(x, y, seed));
//         }

//         private bool IsRecharger(int x, int y, uint seed)
//         {  
//             uint shift = seed % 14;
//             long xPlusShift = x + shift;
//             long yMinusShift = y - shift;

//             long xMod13 = xPlusShift % 13;
//             long yMod14 = yMinusShift % 14;
//             long xMod28 = xPlusShift % 28;
//             long yMod28 = yMinusShift % 28;

//             return xMod13 == yMod14
//                    && (xMod28 is 27 || xPlusShift == 1)
//                    && xPlusShift != yMinusShift 
//                    && xMod28 - yMod28 < 15;
//         }

//         private bool IsBlock(int x, int y, uint seed)
//         {
//             uint len = 4 + seed % 6;
//             int xMod28 = x % 28;
//             int yMod28 = y % 28;

//             if ((yMod28 == 5 && xMod28 > 3 && xMod28 < 3 + len) ||
//                 (yMod28 == 23 && xMod28 > 7 && xMod28 < 7 + Math.Max(5, len)) ||
//                 (yMod28 == 12 && xMod28 > 12 && xMod28 < 12 + len) ||
//                 (xMod28 == 19 && yMod28 > 12 && yMod28 < 12 + Math.Max(5, len)))
//             {
//                 return true;
//             }

//             int xSquaredPlusY = x * x + x * y;
//             int ySquared = y * y;
//             uint divisor = 47 % 60 - seed % 59;
//             long remainder = (xSquaredPlusY + ySquared + seed) % divisor;

//             return remainder == 7;
//         }

//         #endregion

//         #region RPC Switching

//         private void UpdateWeb3InstanceRpc(bool useMagicBlock)
//         {
//             if (Web3.Instance == null) return;

//             if (string.IsNullOrEmpty(_defaultRpcUrl)) _defaultRpcUrl = Web3.Instance.customRpc;
//             if (string.IsNullOrEmpty(_defaultWsUrl)) _defaultWsUrl = Web3.Instance.webSocketsRpc;

//             if (useMagicBlock)
//             {
//                 Web3.Instance.customRpc = _magicblockRpcUrl;
//                 Web3.Instance.webSocketsRpc = _magicblockWsUrl;
//             }
//             else
//             {
//                 Web3.Instance.customRpc = _defaultRpcUrl;
//                 Web3.Instance.webSocketsRpc = _defaultWsUrl;
//             }
//         }

//         public async UniTask SwitchToMagicBlockRpc(bool useMagicBlock)
//         {
//             try
//             {
//                 await UnsubscribeFromGame();
//                 UpdateWeb3InstanceRpc(useMagicBlock);

//                 // Reset clients to pick up new RPC/WS
//                 _kamikazeJoeClient = null;
//                 _soarClient = null;

//                 // Resubscribe if we have an active game
//                 if (_gameInstanceId != null)
//                 {
//                     await SubscribeToGame(_gameInstanceId);
//                     await ReloadGame();
//                 }
//             }
//             catch (Exception e)
//             {
//                 Debug.LogError($"SwitchToMagicBlockRpc error: {e.Message}");
//             }
//         }

//         #endregion

//         #region Delegation

//         private async UniTask EnsureInitializedForDelegation()
//         {
//             if (Web3.Account == null) return;
//             var tx = new Transaction()
//             {
//                 FeePayer = Web3.Account,
//                 Instructions = new List<TransactionInstruction>(),
//                 RecentBlockHash = await Web3.BlockHash(commitment: Commitment.Confirmed, useCache: false)
//             };

//             var matchesPda = FindMatchesPda();
//             var vaultPda = FindVaultPda();
//             var userPda = FindUserPda(Web3.Account);

//             if (!await IsPdaInitialized(vaultPda))
//             {
//                 var initAccounts = new InitializeAccounts()
//                 {
//                     Payer = Web3.Account,
//                     Matches = matchesPda,
//                     Vault = vaultPda,
//                     SystemProgram = SystemProgram.ProgramIdKey
//                 };
//                 tx.Add(KamikazeJoeProgram.Initialize(initAccounts, _kamikazeJoeProgramId));
//             }

//             if (!await IsPdaInitialized(userPda))
//             {
//                 var initUserAccounts = new InitializeUserAccounts()
//                 {
//                     Payer = Web3.Account,
//                     User = userPda,
//                     SystemProgram = SystemProgram.ProgramIdKey
//                 };
//                 tx.Add(KamikazeJoeProgram.InitializeUser(initUserAccounts, _kamikazeJoeProgramId));
//             }

//             if (tx.Instructions.Count > 0)
//             {
//                 var res = await SignAndSendWithToast(tx, "Init Delegation");
//                 if (res.WasSuccessful)
//                 {
//                     await Web3.Rpc.ConfirmTransaction(res.Result, Commitment.Confirmed);
//                 }
//                 else
//                 {
//                     Debug.LogWarning($"Initialization before delegation failed: {res.Reason}");
//                 }
//             }
//         }

//         private async UniTask<bool> DelegateForGame(PublicKey gamePk)
//         {
//             if (Web3.Account == null || gamePk == null) return false;
//             try
//             {
//                 await EnsureInitializedForDelegation();
//                 var tx = new Transaction()
//                 {
//                     FeePayer = Web3.Account,
//                     Instructions = new List<TransactionInstruction>(),
//                     RecentBlockHash = await Web3.BlockHash(commitment: Commitment.Confirmed, useCache: false)
//                 };

//                 tx.Instructions.Add(ComputeBudgetProgram.SetComputeUnitLimit(100000));

//                 var userPda = FindUserPda(Web3.Account);
//                 var delegationProgram = new PublicKey("DELeGGvXpWV2fqJUhqcF5ZSYMS4JTLjteaAMARRSaeSh");

//                 var bufferUser = FindBufferPda("buffer", userPda, _kamikazeJoeProgramId);
//                 var delegationRecordUser = FindDelegationProgramPda("delegation", userPda, delegationProgram);
//                 var delegationMetadataUser = FindDelegationProgramPda("delegation-metadata", userPda, delegationProgram);

//                 var bufferGame = FindBufferPda("buffer", gamePk, _kamikazeJoeProgramId);
//                 var delegationRecordGame = FindDelegationProgramPda("delegation", gamePk, delegationProgram);
//                 var delegationMetadataGame = FindDelegationProgramPda("delegation-metadata", gamePk, delegationProgram);

//                 var accounts = new DeligateAccounts()
//                 {
//                     Payer = Web3.Account,
//                     BufferUser = bufferUser,
//                     DelegationRecordUser = delegationRecordUser,
//                     DelegationMetadataUser = delegationMetadataUser,
//                     User = userPda,
//                     BufferGame = bufferGame,
//                     DelegationRecordGame = delegationRecordGame,
//                     DelegationMetadataGame = delegationMetadataGame,
//                     Game = gamePk,
//                     OwnerProgram = _kamikazeJoeProgramId,
//                     DelegationProgram = delegationProgram,
//                     SystemProgram = SystemProgram.ProgramIdKey
//                 };

//                 var ix = KamikazeJoeProgram.Deligate(accounts, _kamikazeJoeProgramId);
//                 tx.Add(ix);

//                 var res = await SignAndSendWithToast(tx, "Delegate");
//                 if (res.WasSuccessful)
//                 {
//                     await Web3.Rpc.ConfirmTransaction(res.Result, Commitment.Confirmed);
//                     await SwitchToMagicBlockRpc(true);
//                     return true;
//                 }
//                 return false;
//             }
//             catch (Exception e)
//             {
//                 Debug.LogWarning($"DelegateForGame error: {e.Message}");
//                 return false;
//             }
//         }

//         private static PublicKey FindDelegationProgramPda(string seed, PublicKey account, PublicKey delegationProgram)
//         {
//             PublicKey.TryFindProgramAddress(new[] { Encoding.UTF8.GetBytes(seed), account.KeyBytes }, delegationProgram, out var pda, out _);
//             return pda;
//         }

//         private static PublicKey FindBufferPda(string seed, PublicKey account, PublicKey ownerProgram)
//         {
//             PublicKey.TryFindProgramAddress(new[] { Encoding.UTF8.GetBytes(seed), account.KeyBytes }, ownerProgram, out var pda, out _);
//             return pda;
//         }

//         public async UniTask<bool> Delegate()
//         {
//             Debug.Log("Delegate() invoked 🪄");
//             if (Web3.Account == null)
//             {
//                 Debug.LogWarning("Delegate aborted: no Web3.Account 🔐");
//                 return false;
//             }
//             if (_gameInstanceId == null)
//             {
//                 // Try to resolve game id from UI/PlayerPrefs
//                 try
//                 {
//                     var cached = PlayerPrefs.GetString("gameID", null);
//                     if (!string.IsNullOrEmpty(cached))
//                     {
//                         _gameInstanceId = new PublicKey(cached);
//                         Debug.Log($"Delegate fallback: loaded game id from prefs {_gameInstanceId}");
//                     }
//                     else if (UIManger.Instance != null)
//                     {
//                         var s = UIManger.Instance.GetGameID();
//                         if (!string.IsNullOrEmpty(s))
//                         {
//                             _gameInstanceId = new PublicKey(s);
//                             Debug.Log($"Delegate fallback: loaded game id from UI {_gameInstanceId}");
//                         }
//                     }
//                 }
//                 catch (Exception e)
//                 {
//                     Debug.LogWarning($"Delegate fallback failed to parse game id: {e.Message}");
//                 }
//                 if (_gameInstanceId == null)
//                 {
//                     Debug.LogWarning("Delegate aborted: no active game. Join or create a game first.");
//                     return false;
//                 }
//             }
 
//             try
//             {
//                 await EnsureInitializedForDelegation();

//                 // Diagnostics: log all PDAs and owners
//                 var userPdaDiag = _userPda != null ? _userPda : FindUserPda(Web3.Account);
//                 var bufferUserDiag = FindBufferPda("buffer", userPdaDiag, _kamikazeJoeProgramId);
//                 var delegationProgram = new PublicKey("DELeGGvXpWV2fqJUhqcF5ZSYMS4JTLjteaAMARRSaeSh");
//                 var delegationRecordUserDiag = FindDelegationProgramPda("delegation", userPdaDiag, delegationProgram);
//                 var delegationMetadataUserDiag = FindDelegationProgramPda("delegation-metadata", userPdaDiag, delegationProgram);
//                 var bufferGameDiag = FindBufferPda("buffer", _gameInstanceId, _kamikazeJoeProgramId);
//                 var delegationRecordGameDiag = FindDelegationProgramPda("delegation", _gameInstanceId, delegationProgram);
//                 var delegationMetadataGameDiag = FindDelegationProgramPda("delegation-metadata", _gameInstanceId, delegationProgram);

//                 async UniTask LogAcc(string label, PublicKey pk)
//                 {
//                     var info = await Web3.Rpc.GetAccountInfoAsync(pk, Commitment.Processed);
//                     var exists = info.WasSuccessful && info.Result?.Value != null;
//                     var owner = exists ? info.Result.Value.Owner : "<none>";
//                     Debug.Log($"[Delegate ACC] {label}: {pk} exists={exists} owner={owner}");
//                 }
//                 await LogAcc("UserPda", userPdaDiag);
//                 await LogAcc("BufferUser", bufferUserDiag);
//                 await LogAcc("DelegationRecordUser", delegationRecordUserDiag);
//                 await LogAcc("DelegationMetadataUser", delegationMetadataUserDiag);
//                 await LogAcc("BufferGame", bufferGameDiag);
//                 await LogAcc("DelegationRecordGame", delegationRecordGameDiag);
//                 await LogAcc("DelegationMetadataGame", delegationMetadataGameDiag);

//                 var tx = new Transaction()
//                 {
//                     FeePayer = Web3.Account,
//                     Instructions = new List<TransactionInstruction>(),
//                     RecentBlockHash = await Web3.BlockHash(commitment: Commitment.Confirmed, useCache: false)
//                 };
 
//                 // Optional compute limit
//                 tx.Instructions.Add(ComputeBudgetProgram.SetComputeUnitLimit(100000));
 
//                 var userPda = _userPda != null ? _userPda : FindUserPda(Web3.Account);
//                 var bufferUser = FindBufferPda("buffer", userPda, _kamikazeJoeProgramId);
//                 var delegationRecordUser = FindDelegationProgramPda("delegation", userPda, new PublicKey("DELeGGvXpWV2fqJUhqcF5ZSYMS4JTLjteaAMARRSaeSh"));
//                 var delegationMetadataUser = FindDelegationProgramPda("delegation-metadata", userPda, new PublicKey("DELeGGvXpWV2fqJUhqcF5ZSYMS4JTLjteaAMARRSaeSh"));
 
//                 var bufferGame = FindBufferPda("buffer", _gameInstanceId, _kamikazeJoeProgramId);
//                 var delegationRecordGame = FindDelegationProgramPda("delegation", _gameInstanceId, new PublicKey("DELeGGvXpWV2fqJUhqcF5ZSYMS4JTLjteaAMARRSaeSh"));
//                 var delegationMetadataGame = FindDelegationProgramPda("delegation-metadata", _gameInstanceId, new PublicKey("DELeGGvXpWV2fqJUhqcF5ZSYMS4JTLjteaAMARRSaeSh"));
 
//                 var accounts = new DeligateAccounts()
//                 {
//                     Payer = Web3.Account,
//                     BufferUser = bufferUser,
//                     DelegationRecordUser = delegationRecordUser,
//                     DelegationMetadataUser = delegationMetadataUser,
//                     User = userPda,
//                     BufferGame = bufferGame,
//                     DelegationRecordGame = delegationRecordGame,
//                     DelegationMetadataGame = delegationMetadataGame,
//                     Game = _gameInstanceId,
//                     OwnerProgram = _kamikazeJoeProgramId,
//                     DelegationProgram = new PublicKey("DELeGGvXpWV2fqJUhqcF5ZSYMS4JTLjteaAMARRSaeSh"),
//                     SystemProgram = SystemProgram.ProgramIdKey
//                 };
 
//                 var ix = KamikazeJoeProgram.Deligate(accounts, _kamikazeJoeProgramId);
//                 tx.Add(ix);
 
//                 var res = await SignAndSendWithToast(tx, "Delegate");
//                 if (res.WasSuccessful)
//                 {
//                     await Web3.Rpc.ConfirmTransaction(res.Result, Commitment.Confirmed);
//                     Debug.Log($"Delegate success ✅ sig: {res.Result}");
//                     await SwitchToMagicBlockRpc(true);
//                     return true;
//                 }
//                 Debug.LogError($"Delegate failed: {res.Reason}");
//                 return false;
//             }
//             catch (Exception e)
//             {
//                 Debug.LogError($"Delegate error: {e.Message}");
//                 return false;
//             }
//         }
 
//         public async UniTask<bool> Undelegate()
//         {
//             Debug.Log("Undelegate() invoked 🧹");
//             if (Web3.Account == null)
//             {
//                 Debug.LogWarning("Undelegate aborted: no Web3.Account 🔐");
//                 return false;
//             }
//             if (_gameInstanceId == null)
//             {
//                 try
//                 {
//                     var cached = PlayerPrefs.GetString("gameID", null);
//                     if (!string.IsNullOrEmpty(cached))
//                     {
//                         _gameInstanceId = new PublicKey(cached);
//                         Debug.Log($"Undelegate fallback: loaded game id from prefs {_gameInstanceId}");
//                     }
//                     else if (UIManger.Instance != null)
//                     {
//                         var s = UIManger.Instance.GetGameID();
//                         if (!string.IsNullOrEmpty(s))
//                         {
//                             _gameInstanceId = new PublicKey(s);
//                             Debug.Log($"Undelegate fallback: loaded game id from UI {_gameInstanceId}");
//                         }
//                     }
//                 }
//                 catch (Exception e)
//                 {
//                     Debug.LogWarning($"Undelegate fallback failed to parse game id: {e.Message}");
//                 }
//                 if (_gameInstanceId == null)
//                 {
//                     Debug.LogWarning("Undelegate aborted: no active game. Join or create a game first.");
//                     return false;
//                 }
//             }
 
//             try
//             {
//                 var tx = new Transaction()
//                 {
//                     FeePayer = Web3.Account,
//                     Instructions = new List<TransactionInstruction>(),
//                     RecentBlockHash = await Web3.BlockHash(commitment: Commitment.Confirmed, useCache: false)
//                 };
 
//                 tx.Instructions.Add(ComputeBudgetProgram.SetComputeUnitLimit(100000));
 
//                 var accounts = new UndeligateAccounts()
//                 {
//                     Payer = Web3.Account,
//                     User = _userPda != null ? _userPda : FindUserPda(Web3.Account),
//                     Game = _gameInstanceId,
//                     MagicProgram = new PublicKey("Magic11111111111111111111111111111111111111"),
//                     MagicContext = new PublicKey("MagicContext1111111111111111111111111111111")
//                 };
 
//                 var ix = KamikazeJoeProgram.Undeligate(accounts, _kamikazeJoeProgramId);
//                 tx.Add(ix);
 
//                 var res = await SignAndSendWithToast(tx, "Undelegate");
//                 if (res.WasSuccessful)
//                 {
//                     await Web3.Rpc.ConfirmTransaction(res.Result, Commitment.Confirmed);
//                     Debug.Log($"Undelegate success ✅ sig: {res.Result}");
//                     await SwitchToMagicBlockRpc(false);
//                     return true;
//                 }
//                 Debug.LogError($"Undelegate failed: {res.Reason}");
//                 return false;
//             }
//             catch (Exception e)
//             {
//                 Debug.LogError($"Undelegate error: {e.Message}");
//                 return false;
//             }
//         }

//         #endregion
//         #region Game Utils

//         private void ClampArenaSize(string value)
//         {
//             if (!int.TryParse(value, out var arenaSize)) return;
//             txtArenaSize.text = arenaSize switch
//             {
//                 < 10 => "25",
//                 > 150 => "150",
//                 _ => txtArenaSize.text
//             };
//         }

//         #endregion

// }
