using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Solana.Unity;
using Solana.Unity.Programs.Abstract;
using Solana.Unity.Programs.Utilities;
using Solana.Unity.Rpc;
using Solana.Unity.Rpc.Builders;
using Solana.Unity.Rpc.Core.Http;
using Solana.Unity.Rpc.Core.Sockets;
using Solana.Unity.Rpc.Types;
using Solana.Unity.Wallet;
using BombermanProgram;
using BombermanProgram.Program;
using BombermanProgram.Errors;
using BombermanProgram.Accounts;
using BombermanProgram.Types;

namespace BombermanProgram
{
    namespace Accounts
    {
        public partial class Game
        {
            public static ulong ACCOUNT_DISCRIMINATOR => 1331205435963103771UL;
            public static ReadOnlySpan<byte> ACCOUNT_DISCRIMINATOR_BYTES => new byte[]{27, 90, 166, 125, 74, 100, 121, 18};
            public static string ACCOUNT_DISCRIMINATOR_B58 => "5aNQXizG8jB";
            public PublicKey Authority { get; set; }

            public PublicKey[] Players { get; set; }

            public byte PlayerCount { get; set; }

            public GameState GameState { get; set; }

            public byte GridSize { get; set; }

            public long CreatedAt { get; set; }

            public static Game Deserialize(ReadOnlySpan<byte> _data)
            {
                int offset = 0;
                ulong accountHashValue = _data.GetU64(offset);
                offset += 8;
                if (accountHashValue != ACCOUNT_DISCRIMINATOR)
                {
                    return null;
                }

                Game result = new Game();
                result.Authority = _data.GetPubKey(offset);
                offset += 32;
                result.Players = new PublicKey[4];
                for (uint resultPlayersIdx = 0; resultPlayersIdx < 4; resultPlayersIdx++)
                {
                    if (_data.GetBool(offset++))
                    {
                        result.Players[resultPlayersIdx] = _data.GetPubKey(offset);
                        offset += 32;
                    }
                }

                result.PlayerCount = _data.GetU8(offset);
                offset += 1;
                result.GameState = (GameState)_data.GetU8(offset);
                offset += 1;
                result.GridSize = _data.GetU8(offset);
                offset += 1;
                result.CreatedAt = _data.GetS64(offset);
                offset += 8;
                return result;
            }
        }

        public partial class PlayerState
        {
            public static ulong ACCOUNT_DISCRIMINATOR => 14119929072670475064UL;
            public static ReadOnlySpan<byte> ACCOUNT_DISCRIMINATOR_BYTES => new byte[]{56, 3, 60, 86, 174, 16, 244, 195};
            public static string ACCOUNT_DISCRIMINATOR_B58 => "ANPnGXvXEkr";
            public PublicKey Player { get; set; }

            public PublicKey Game { get; set; }

            public byte X { get; set; }

            public byte Y { get; set; }

            public bool IsAlive { get; set; }

            public byte PlayerIndex { get; set; }

            public static PlayerState Deserialize(ReadOnlySpan<byte> _data)
            {
                int offset = 0;
                ulong accountHashValue = _data.GetU64(offset);
                offset += 8;
                if (accountHashValue != ACCOUNT_DISCRIMINATOR)
                {
                    return null;
                }

                PlayerState result = new PlayerState();
                result.Player = _data.GetPubKey(offset);
                offset += 32;
                result.Game = _data.GetPubKey(offset);
                offset += 32;
                result.X = _data.GetU8(offset);
                offset += 1;
                result.Y = _data.GetU8(offset);
                offset += 1;
                result.IsAlive = _data.GetBool(offset);
                offset += 1;
                result.PlayerIndex = _data.GetU8(offset);
                offset += 1;
                return result;
            }
        }
    }

    namespace Errors
    {
        public enum BombermanProgramErrorKind : uint
        {
            GameFull = 6000U,
            GameNotWaiting = 6001U,
            PlayerAlreadyJoined = 6002U,
            PlayerNotFound = 6003U,
            InvalidPosition = 6004U,
            GameNotActive = 6005U,
            InvalidAuthority = 6006U
        }
    }

    namespace Types
    {
        public enum GameState : byte
        {
            WaitingForPlayers,
            Active,
            Finished
        }
    }

    public partial class BombermanProgramClient : TransactionalBaseClient<BombermanProgramErrorKind>
    {
        public BombermanProgramClient(IRpcClient rpcClient, IStreamingRpcClient streamingRpcClient, PublicKey programId = null) : base(rpcClient, streamingRpcClient, programId ?? new PublicKey(BombermanProgramProgram.ID))
        {
        }

        public async Task<Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<Game>>> GetGamesAsync(string programAddress = BombermanProgramProgram.ID, Commitment commitment = Commitment.Confirmed)
        {
            var list = new List<Solana.Unity.Rpc.Models.MemCmp>{new Solana.Unity.Rpc.Models.MemCmp{Bytes = Game.ACCOUNT_DISCRIMINATOR_B58, Offset = 0}};
            var res = await RpcClient.GetProgramAccountsAsync(programAddress, commitment, memCmpList: list);
            if (!res.WasSuccessful || !(res.Result?.Count > 0))
                return new Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<Game>>(res);
            List<Game> resultingAccounts = new List<Game>(res.Result.Count);
            resultingAccounts.AddRange(res.Result.Select(result => Game.Deserialize(Convert.FromBase64String(result.Account.Data[0]))));
            return new Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<Game>>(res, resultingAccounts);
        }

        public async Task<Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<PlayerState>>> GetPlayerStatesAsync(string programAddress = BombermanProgramProgram.ID, Commitment commitment = Commitment.Confirmed)
        {
            var list = new List<Solana.Unity.Rpc.Models.MemCmp>{new Solana.Unity.Rpc.Models.MemCmp{Bytes = PlayerState.ACCOUNT_DISCRIMINATOR_B58, Offset = 0}};
            var res = await RpcClient.GetProgramAccountsAsync(programAddress, commitment, memCmpList: list);
            if (!res.WasSuccessful || !(res.Result?.Count > 0))
                return new Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<PlayerState>>(res);
            List<PlayerState> resultingAccounts = new List<PlayerState>(res.Result.Count);
            resultingAccounts.AddRange(res.Result.Select(result => PlayerState.Deserialize(Convert.FromBase64String(result.Account.Data[0]))));
            return new Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<PlayerState>>(res, resultingAccounts);
        }

        public async Task<Solana.Unity.Programs.Models.AccountResultWrapper<Game>> GetGameAsync(string accountAddress, Commitment commitment = Commitment.Finalized)
        {
            var res = await RpcClient.GetAccountInfoAsync(accountAddress, commitment);
            if (!res.WasSuccessful)
                return new Solana.Unity.Programs.Models.AccountResultWrapper<Game>(res);
            var resultingAccount = Game.Deserialize(Convert.FromBase64String(res.Result.Value.Data[0]));
            return new Solana.Unity.Programs.Models.AccountResultWrapper<Game>(res, resultingAccount);
        }

        public async Task<Solana.Unity.Programs.Models.AccountResultWrapper<PlayerState>> GetPlayerStateAsync(string accountAddress, Commitment commitment = Commitment.Finalized)
        {
            var res = await RpcClient.GetAccountInfoAsync(accountAddress, commitment);
            if (!res.WasSuccessful)
                return new Solana.Unity.Programs.Models.AccountResultWrapper<PlayerState>(res);
            var resultingAccount = PlayerState.Deserialize(Convert.FromBase64String(res.Result.Value.Data[0]));
            return new Solana.Unity.Programs.Models.AccountResultWrapper<PlayerState>(res, resultingAccount);
        }

        public async Task<SubscriptionState> SubscribeGameAsync(string accountAddress, Action<SubscriptionState, Solana.Unity.Rpc.Messages.ResponseValue<Solana.Unity.Rpc.Models.AccountInfo>, Game> callback, Commitment commitment = Commitment.Finalized)
        {
            SubscriptionState res = await StreamingRpcClient.SubscribeAccountInfoAsync(accountAddress, (s, e) =>
            {
                Game parsingResult = null;
                if (e.Value?.Data?.Count > 0)
                    parsingResult = Game.Deserialize(Convert.FromBase64String(e.Value.Data[0]));
                callback(s, e, parsingResult);
            }, commitment);
            return res;
        }

        public async Task<SubscriptionState> SubscribePlayerStateAsync(string accountAddress, Action<SubscriptionState, Solana.Unity.Rpc.Messages.ResponseValue<Solana.Unity.Rpc.Models.AccountInfo>, PlayerState> callback, Commitment commitment = Commitment.Finalized)
        {
            SubscriptionState res = await StreamingRpcClient.SubscribeAccountInfoAsync(accountAddress, (s, e) =>
            {
                PlayerState parsingResult = null;
                if (e.Value?.Data?.Count > 0)
                    parsingResult = PlayerState.Deserialize(Convert.FromBase64String(e.Value.Data[0]));
                callback(s, e, parsingResult);
            }, commitment);
            return res;
        }

        protected override Dictionary<uint, ProgramError<BombermanProgramErrorKind>> BuildErrorsDictionary()
        {
            return new Dictionary<uint, ProgramError<BombermanProgramErrorKind>>{{6000U, new ProgramError<BombermanProgramErrorKind>(BombermanProgramErrorKind.GameFull, "Game is full (max 4 players)")}, {6001U, new ProgramError<BombermanProgramErrorKind>(BombermanProgramErrorKind.GameNotWaiting, "Game is not waiting for players")}, {6002U, new ProgramError<BombermanProgramErrorKind>(BombermanProgramErrorKind.PlayerAlreadyJoined, "Player already joined this game")}, {6003U, new ProgramError<BombermanProgramErrorKind>(BombermanProgramErrorKind.PlayerNotFound, "Player not found in game")}, {6004U, new ProgramError<BombermanProgramErrorKind>(BombermanProgramErrorKind.InvalidPosition, "Invalid position")}, {6005U, new ProgramError<BombermanProgramErrorKind>(BombermanProgramErrorKind.GameNotActive, "Game is not active")}, {6006U, new ProgramError<BombermanProgramErrorKind>(BombermanProgramErrorKind.InvalidAuthority, "Invalid authority")}, };
        }
    }

    namespace Program
    {
        public class DelegatePlayerAccounts
        {
            public PublicKey BufferPlayerState { get; set; }

            public PublicKey DelegationRecordPlayerState { get; set; }

            public PublicKey DelegationMetadataPlayerState { get; set; }

            public PublicKey PlayerState { get; set; }

            public PublicKey Player { get; set; }

            public PublicKey OwnerProgram { get; set; } = new PublicKey("bomZvwYRFaWpUBGsKYTkRXqRP9cordVUYmWH5Bg6M24");
            public PublicKey DelegationProgram { get; set; } = new PublicKey("DELeGGvXpWV2fqJUhqcF5ZSYMS4JTLjteaAMARRSaeSh");
            public PublicKey SystemProgram { get; set; } = new PublicKey("11111111111111111111111111111111");
        }

        public class InitializeGameAccounts
        {
            public PublicKey Game { get; set; }

            public PublicKey Authority { get; set; }

            public PublicKey SystemProgram { get; set; } = new PublicKey("11111111111111111111111111111111");
        }

        public class JoinGameAccounts
        {
            public PublicKey Game { get; set; }

            public PublicKey PlayerState { get; set; }

            public PublicKey Player { get; set; }

            public PublicKey SystemProgram { get; set; } = new PublicKey("11111111111111111111111111111111");
        }

        public class MovePlayerAccounts
        {
            public PublicKey PlayerState { get; set; }

            public PublicKey Player { get; set; }
        }

        public class ProcessUndelegationAccounts
        {
            public PublicKey BaseAccount { get; set; }

            public PublicKey Buffer { get; set; }

            public PublicKey Payer { get; set; }

            public PublicKey SystemProgram { get; set; }
        }

        public class UndelegatePlayerAccounts
        {
            public PublicKey PlayerState { get; set; }

            public PublicKey Player { get; set; }

            public PublicKey MagicProgram { get; set; } = new PublicKey("Magic11111111111111111111111111111111111111");
            public PublicKey MagicContext { get; set; } = new PublicKey("MagicContext1111111111111111111111111111111");
        }

        public static class BombermanProgramProgram
        {
            public const string ID = "bomZvwYRFaWpUBGsKYTkRXqRP9cordVUYmWH5Bg6M24";
            public static Solana.Unity.Rpc.Models.TransactionInstruction DelegatePlayer(DelegatePlayerAccounts accounts, PublicKey programId = null)
            {
                programId ??= new(ID);
                List<Solana.Unity.Rpc.Models.AccountMeta> keys = new()
                {Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.BufferPlayerState, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.DelegationRecordPlayerState, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.DelegationMetadataPlayerState, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.PlayerState, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.Player, true), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.OwnerProgram, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.DelegationProgram, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SystemProgram, false)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(6484840009491128299UL, offset);
                offset += 8;
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solana.Unity.Rpc.Models.TransactionInstruction{Keys = keys, ProgramId = programId.KeyBytes, Data = resultData};
            }

            public static Solana.Unity.Rpc.Models.TransactionInstruction InitializeGame(InitializeGameAccounts accounts, PublicKey programId = null)
            {
                programId ??= new(ID);
                List<Solana.Unity.Rpc.Models.AccountMeta> keys = new()
                {Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Game, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Authority, true), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SystemProgram, false)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(15529203708862021164UL, offset);
                offset += 8;
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solana.Unity.Rpc.Models.TransactionInstruction{Keys = keys, ProgramId = programId.KeyBytes, Data = resultData};
            }

            public static Solana.Unity.Rpc.Models.TransactionInstruction JoinGame(JoinGameAccounts accounts, PublicKey game_authority, PublicKey programId = null)
            {
                programId ??= new(ID);
                List<Solana.Unity.Rpc.Models.AccountMeta> keys = new()
                {Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Game, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.PlayerState, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Player, true), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SystemProgram, false)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(9240450992125931627UL, offset);
                offset += 8;
                _data.WritePubKey(game_authority, offset);
                offset += 32;
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solana.Unity.Rpc.Models.TransactionInstruction{Keys = keys, ProgramId = programId.KeyBytes, Data = resultData};
            }

            public static Solana.Unity.Rpc.Models.TransactionInstruction MovePlayer(MovePlayerAccounts accounts, byte new_x, byte new_y, PublicKey programId = null)
            {
                programId ??= new(ID);
                List<Solana.Unity.Rpc.Models.AccountMeta> keys = new()
                {Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.PlayerState, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.Player, true)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(16684840164937447953UL, offset);
                offset += 8;
                _data.WriteU8(new_x, offset);
                offset += 1;
                _data.WriteU8(new_y, offset);
                offset += 1;
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solana.Unity.Rpc.Models.TransactionInstruction{Keys = keys, ProgramId = programId.KeyBytes, Data = resultData};
            }

            public static Solana.Unity.Rpc.Models.TransactionInstruction ProcessUndelegation(ProcessUndelegationAccounts accounts, byte[][] account_seeds, PublicKey programId = null)
            {
                programId ??= new(ID);
                List<Solana.Unity.Rpc.Models.AccountMeta> keys = new()
                {Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.BaseAccount, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.Buffer, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Payer, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SystemProgram, false)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(12048014319693667524UL, offset);
                offset += 8;
                _data.WriteS32(account_seeds.Length, offset);
                offset += 4;
                foreach (var account_seedsElement in account_seeds)
                {
                    _data.WriteS32(account_seedsElement.Length, offset);
                    offset += 4;
                    _data.WriteSpan(account_seedsElement, offset);
                    offset += account_seedsElement.Length;
                }

                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solana.Unity.Rpc.Models.TransactionInstruction{Keys = keys, ProgramId = programId.KeyBytes, Data = resultData};
            }

            public static Solana.Unity.Rpc.Models.TransactionInstruction UndelegatePlayer(UndelegatePlayerAccounts accounts, PublicKey programId = null)
            {
                programId ??= new(ID);
                List<Solana.Unity.Rpc.Models.AccountMeta> keys = new()
                {Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.PlayerState, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.Player, true), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.MagicProgram, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.MagicContext, false)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(17543519979493716710UL, offset);
                offset += 8;
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solana.Unity.Rpc.Models.TransactionInstruction{Keys = keys, ProgramId = programId.KeyBytes, Data = resultData};
            }
        }
    }
}