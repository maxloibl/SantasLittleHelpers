using System;
using Unity.Netcode;

namespace SantasLittleHelpers.Delivery
{
    public enum MissionState
    {
        NotStarted,
        Delivering,
        Returning,
        Completed,
        Failed,
        Abandoned
    }

    public readonly struct MissionSnapshot
    {
        public MissionState State { get; }
        public Guid RoundId { get; }
        public string MissionId { get; }
        public int DeliveredCount { get; }
        public int RequiredCount { get; }

        public MissionSnapshot(MissionState state, Guid roundId, string missionId, int deliveredCount, int requiredCount)
        {
            State = state;
            RoundId = roundId;
            MissionId = missionId;
            DeliveredCount = deliveredCount;
            RequiredCount = requiredCount;
        }
    }

    /// <summary>Host-owned completion fact emitted once when the existing delivery rules reach success.</summary>
    public readonly struct MissionCompleted
    {
        public Guid RoundId { get; }
        public string MissionId { get; }
        public DateTimeOffset CompletedAtUtc { get; }

        public MissionCompleted(Guid roundId, string missionId, DateTimeOffset completedAtUtc)
        {
            RoundId = roundId;
            MissionId = missionId;
            CompletedAtUtc = completedAtUtc;
        }
    }

    /// <summary>Small mission-owned surface for the session owner and the legacy DeliveryHouse entry.</summary>
    public interface IMissionAdapter
    {
        MissionSnapshot Current { get; }
        event Action<MissionCompleted> Completed;
        bool ConfigureNetworkManager(NetworkManager manager);
        void PrepareClientWorld();
        bool BeginSoloRound(Guid roundId);
        bool BeginServerRound(NetworkManager manager, Guid roundId);
        bool Cancel();
    }
}
