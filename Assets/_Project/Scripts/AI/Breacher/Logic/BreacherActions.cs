namespace BlackoutProtocol.AI.Breacher.Logic
{
    public sealed class MoveToAction : GoapAction
    {
        public MoveToAction(
            string destinationName,
            WorldState preconditions,
            WorldState arrivalEffects,
            WorldState locationFactsToRemove,
            float pathCost)
            : base(
                $"Move To {destinationName}",
                preconditions,
                arrivalEffects,
                locationFactsToRemove,
                pathCost)
        {
            DestinationName = destinationName;
        }

        public string DestinationName { get; }
    }

    public sealed class OpenDoorAction : GoapAction
    {
        public OpenDoorAction(float cost)
            : base(
                "Open Door",
                WorldState.From(WorldFact.AtDoor),
                WorldState.From(
                    WorldFact.DoorOpen,
                    WorldFact.PathToPlayerOpen),
                WorldState.From(WorldFact.AtDoor),
                cost)
        {
        }
    }

    public sealed class PushBarricadeAction : GoapAction
    {
        public PushBarricadeAction(float cost)
            : base(
                "Push Barricade",
                WorldState.From(WorldFact.AtBarricade),
                WorldState.From(
                    WorldFact.BarricadeCleared,
                    WorldFact.PathToPlayerOpen),
                WorldState.From(WorldFact.AtBarricade),
                cost)
        {
        }
    }

    public sealed class AttackAction : GoapAction
    {
        public AttackAction(float cost)
            : base(
                "Attack",
                WorldState.From(WorldFact.PlayerInRange),
                WorldState.From(WorldFact.PlayerDefeated),
                WorldState.Empty,
                cost)
        {
        }
    }

    public sealed class TakeCoverAction : GoapAction
    {
        public TakeCoverAction(float cost)
            : base(
                "Take Cover",
                WorldState.From(WorldFact.LowHealth),
                WorldState.From(WorldFact.InCover),
                WorldState.From(
                    WorldFact.AtDoor,
                    WorldFact.AtBarricade,
                    WorldFact.AtPlayer,
                    WorldFact.PlayerInRange),
                cost)
        {
        }
    }
}
