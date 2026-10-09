using UnityEngine;

namespace BlackoutProtocol.Interaction
{
    public interface IInteractable
    {
        string Prompt { get; }

        void Interact(GameObject user);
    }
}