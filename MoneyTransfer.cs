using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Mirror;
using UnityEngine;

namespace SephiriaTogether
{
    internal struct MoneyTransferRequest : NetworkMessage
    {
        public uint requestId;
        public uint targetNetId;
        public int amount;
    }

    internal struct MoneyTransferResult : NetworkMessage
    {
        public uint requestId;
        public byte result;
        public bool incoming;
        public int amount;
        public int balance;
        public string otherName;
    }

    internal static class MoneyTransfer
    {
        private const int MaximumTransfer = int.MaxValue;
        private static readonly Dictionary<int, double> LastRequests = new Dictionary<int, double>();
        private static readonly Dictionary<int, uint> LastRequestIds = new Dictionary<int, uint>();
        private static uint nextRequestId;
        private static uint pendingRequestId;
        private static float pendingSince;

        internal static bool IsPending => pendingRequestId != 0;
        internal static string Status { get; private set; } = "";
        internal static bool IsAvailable => NetworkClient.active && NetworkClient.ready &&
                                             (NetworkServer.active || CatchUpRewards.HostSupportsProtocol());

        internal static void RegisterServerMessages()
        {
            ConfigureSerialization();
            LastRequests.Clear();
            LastRequestIds.Clear();
            NetworkServer.RegisterHandler<MoneyTransferRequest>(OnServerRequest, true);
        }

        internal static void RegisterClientMessages()
        {
            ConfigureSerialization();
            NetworkClient.RegisterHandler<MoneyTransferResult>(OnClientResult, true);
            ClearClient();
        }

        internal static bool TrySend(PlayerAvatar target, int amount)
        {
            if (target == null || target == LocalPlayer() ||
                amount <= 0 || amount > MaximumTransfer)
            {
                Status = MenuText.Get("TransferInvalidAmount");
                return false;
            }
            if (!IsAvailable)
            {
                Status = MenuText.Get("TransferUnavailable");
                return false;
            }
            if (IsPending)
            {
                Status = MenuText.Get("TransferPending");
                return false;
            }

            pendingRequestId = ++nextRequestId;
            if (pendingRequestId == 0) pendingRequestId = ++nextRequestId;
            pendingSince = Time.unscaledTime;
            Status = MenuText.Get("TransferPending");
            Plugin.LogInfo($"Leaf transfer request sent: request={pendingRequestId}, " +
                           $"sender={SafeName(LocalPlayer()?.Name)}#{LocalPlayer()?.netId ?? 0}, " +
                           $"target={SafeName(target.Name)}#{target.netId}, amount={amount}.");
            NetworkClient.Send(new MoneyTransferRequest
            {
                requestId = pendingRequestId,
                targetNetId = target.netId,
                amount = amount
            });
            return true;
        }

        internal static void Tick()
        {
            if (IsPending && pendingSince > 0f && Time.unscaledTime - pendingSince > 8f)
            {
                Plugin.LogInfo($"Leaf transfer timed out: request={pendingRequestId}.");
                pendingSince = -1f;
                Status = MenuText.Get("TransferTimeout");
            }
        }

        internal static void ClearClient()
        {
            pendingRequestId = 0;
            pendingSince = 0f;
            Status = "";
        }

        internal static void ClearServer()
        {
            LastRequests.Clear();
            LastRequestIds.Clear();
        }

        internal static void RemoveConnection(NetworkConnectionToClient connection)
        {
            if (connection == null) return;
            LastRequests.Remove(connection.connectionId);
            LastRequestIds.Remove(connection.connectionId);
        }

        private static void OnServerRequest(NetworkConnectionToClient connection, MoneyTransferRequest message)
        {
            if (!VersionCompatibility.IsProtocolCompatibleConnection(connection)) return;
            PlayerSpawner senderSpawner = connection?.identity != null
                ? connection.identity.GetComponent<PlayerSpawner>()
                : null;
            PlayerAvatar sender = senderSpawner?.PlayerAvatar != null
                ? senderSpawner.PlayerAvatar
                : connection?.identity != null ? connection.identity.GetComponent<PlayerAvatar>() : null;
            Plugin.LogInfo($"Leaf transfer request received: conn={connection?.connectionId ?? -1}, " +
                           $"request={message.requestId}, sender={SafeName(sender?.Name)}#{sender?.netId ?? 0}, " +
                           $"target={message.targetNetId}, amount={message.amount}, " +
                           $"modHandshake={CatchUpRewards.IsModdedConnection(connection)}.");
            if (sender == null || senderSpawner == null || connection == null || !connection.isAuthenticated)
            {
                Plugin.LogInfo($"Leaf transfer rejected: reason=invalid-sender, conn={connection?.connectionId ?? -1}, " +
                               $"request={message.requestId}.");
                SendResult(connection, message, 3, sender?.Money ?? 0, "");
                return;
            }

            LastRequestIds.TryGetValue(connection.connectionId, out uint lastId);
            if (message.requestId == 0 || message.requestId <= lastId)
            {
                Plugin.LogInfo($"Leaf transfer rejected: reason=request-order, conn={connection.connectionId}, " +
                               $"request={message.requestId}, last={lastId}.");
                SendResult(connection, message, 6, sender.Money, "");
                return;
            }
            LastRequestIds[connection.connectionId] = message.requestId;

            double now = NetworkTime.time;
            if (LastRequests.TryGetValue(connection.connectionId, out double last) && now - last < 1d)
            {
                Plugin.LogInfo($"Leaf transfer rejected: reason=rate-limit, conn={connection.connectionId}, " +
                               $"request={message.requestId}.");
                SendResult(connection, message, 6, sender.Money, "");
                return;
            }
            LastRequests[connection.connectionId] = now;

            if (message.amount <= 0 || message.amount > MaximumTransfer)
            {
                Plugin.LogInfo($"Leaf transfer rejected: reason=invalid-amount, conn={connection.connectionId}, " +
                               $"request={message.requestId}, amount={message.amount}.");
                SendResult(connection, message, 2, sender.Money, "");
                return;
            }

            NetworkServer.spawned.TryGetValue(message.targetNetId, out NetworkIdentity targetIdentity);
            PlayerSpawner targetSpawner = targetIdentity != null
                ? targetIdentity.GetComponent<PlayerSpawner>()
                : null;
            PlayerAvatar target = targetSpawner?.PlayerAvatar;
            string targetName = SafeName(target?.Name);
            bool targetOnline = targetIdentity != null && targetIdentity.isServer && targetSpawner != null &&
                                PlayerSpawner.MultiplayerList != null &&
                                PlayerSpawner.MultiplayerList.Contains(targetSpawner);
            if (!targetOnline || target == null || target == sender)
            {
                Plugin.LogInfo($"Leaf transfer rejected: reason=invalid-target, conn={connection.connectionId}, " +
                               $"request={message.requestId}, target={message.targetNetId}, " +
                               $"spawned={targetIdentity != null}, online={targetOnline}.");
                SendResult(connection, message, 3, sender.Money, targetName);
                return;
            }
            if (sender.Money < message.amount)
            {
                Plugin.LogInfo($"Leaf transfer rejected: reason=insufficient, conn={connection.connectionId}, " +
                               $"request={message.requestId}, balance={sender.Money}, amount={message.amount}.");
                SendResult(connection, message, 4, sender.Money, targetName);
                return;
            }
            if ((long)target.Money + message.amount > int.MaxValue)
            {
                Plugin.LogInfo($"Leaf transfer rejected: reason=target-limit, conn={connection.connectionId}, " +
                               $"request={message.requestId}, targetBalance={target.Money}, amount={message.amount}.");
                SendResult(connection, message, 5, sender.Money, targetName);
                return;
            }

            int senderBefore = sender.Money;
            int targetBefore = target.Money;
            sender.GiveMoney(target, message.amount);
            Plugin.LogInfo($"Leaf transfer completed: sender={SafeName(sender.Name)}#{sender.netId} " +
                           $"{senderBefore}->{sender.Money}, target={targetName}#{target.netId} " +
                           $"{targetBefore}->{target.Money}, amount={message.amount}.");

            try
            {
                senderSpawner?.SaveCurrentSessionData();
                targetSpawner.SaveCurrentSessionData();
                SaveManager.Save(saveCurrent: false, saveCurrentRun: true);
            }
            catch (Exception exception)
            {
                Plugin.LogInfo("Leaf transfer save scheduling failed: " + exception.Message);
            }

            SendResult(connection, message, 1, sender.Money, targetName);
            NetworkConnectionToClient targetConnection = targetSpawner.connectionToClient ??
                                                         (targetSpawner.isHost ? NetworkServer.localConnection : null);
            if (targetConnection != null && targetConnection.isReady &&
                (targetConnection == NetworkServer.localConnection || CatchUpRewards.IsModdedConnection(targetConnection)))
                targetConnection.Send(new MoneyTransferResult
                {
                    requestId = 0,
                    result = 1,
                    incoming = true,
                    amount = message.amount,
                    balance = target.Money,
                    otherName = SafeName(sender.Name)
                });
        }

        private static void SendResult(NetworkConnectionToClient connection, MoneyTransferRequest request,
            byte result, int balance, string otherName)
        {
            if (connection == null || !connection.isReady) return;
            connection.Send(new MoneyTransferResult
            {
                requestId = request.requestId,
                result = result,
                incoming = false,
                amount = request.amount,
                balance = balance,
                otherName = otherName ?? ""
            });
        }

        private static void OnClientResult(MoneyTransferResult message)
        {
            if (!VersionCompatibility.HostSupportsProtocolMetadata()) return;
            Plugin.LogInfo($"Leaf transfer result received: request={message.requestId}, result={message.result}, " +
                           $"incoming={message.incoming}, amount={message.amount}, balance={message.balance}, " +
                           $"other={SafeName(message.otherName)}.");
            if (!message.incoming && message.requestId == pendingRequestId)
            {
                pendingRequestId = 0;
                pendingSince = 0f;
            }

            if (message.result == 1)
                Status = string.Format(MenuText.Get(message.incoming ? "TransferReceived" : "TransferSuccess"),
                    message.otherName, message.amount, message.balance);
            else if (message.result == 2)
                Status = MenuText.Get("TransferInvalidAmount");
            else if (message.result == 4)
                Status = string.Format(MenuText.Get("TransferInsufficient"), message.balance);
            else if (message.result == 5)
                Status = MenuText.Get("TransferTargetLimit");
            else if (message.result == 6)
                Status = MenuText.Get("TransferRateLimited");
            else
                Status = MenuText.Get("TransferUnavailable");

            if (GameLogWriter.Instance != null)
                GameLogWriter.Instance.WriteLog(Status, message.result == 1 ? Color.green : Color.red);
        }

        private static PlayerAvatar LocalPlayer() =>
            CombatManager.Instance != null ? CombatManager.Instance.CurrentPlayer : null;

        private static string SafeName(string value)
        {
            string safe = Regex.Replace(value ?? "", "<[^>]*>", "")
                .Replace("\r", " ").Replace("\n", " ").Trim();
            return safe.Length > 32 ? safe.Substring(0, 32) : safe;
        }

        private static void ConfigureSerialization()
        {
            Writer<MoneyTransferRequest>.write = (writer, value) =>
            {
                writer.WriteUInt(value.requestId);
                writer.WriteUInt(value.targetNetId);
                writer.WriteVarInt(value.amount);
            };
            Reader<MoneyTransferRequest>.read = reader => new MoneyTransferRequest
            {
                requestId = reader.ReadUInt(),
                targetNetId = reader.ReadUInt(),
                amount = reader.ReadVarInt()
            };
            Writer<MoneyTransferResult>.write = (writer, value) =>
            {
                writer.WriteUInt(value.requestId);
                writer.WriteByte(value.result);
                writer.WriteBool(value.incoming);
                writer.WriteVarInt(value.amount);
                writer.WriteVarInt(value.balance);
                writer.WriteString(value.otherName);
            };
            Reader<MoneyTransferResult>.read = reader => new MoneyTransferResult
            {
                requestId = reader.ReadUInt(),
                result = reader.ReadByte(),
                incoming = reader.ReadBool(),
                amount = reader.ReadVarInt(),
                balance = reader.ReadVarInt(),
                otherName = reader.ReadString()
            };
        }
    }
}
