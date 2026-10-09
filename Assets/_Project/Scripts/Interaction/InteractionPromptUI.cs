using BlackoutProtocol.Player;
using UnityEngine;
using UnityEngine.UI;

namespace BlackoutProtocol.Interaction
{
    [RequireComponent(typeof(Text))]
    public sealed class InteractionPromptUI : MonoBehaviour
    {
        [SerializeField] private PlayerInteractor playerInteractor;
        [SerializeField] private Text promptText;

        private void Awake()
        {
            if (promptText == null)
            {
                promptText = GetComponent<Text>();
            }
        }

        private void Update()
        {
            string prompt = playerInteractor.CurrentPrompt;
            bool shouldShow = !string.IsNullOrWhiteSpace(prompt);

            promptText.enabled = shouldShow;

            if (shouldShow)
            {
                promptText.text = prompt;
            }
        }
    }
}
