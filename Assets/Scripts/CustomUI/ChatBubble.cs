using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChatBubble : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private Image bubbleBackground;

    [Header(" Settings ")]
    [SerializeField] private Color userBubbleColor;

    public void Configure(string message, bool isUserMessage)
    {
        if(isUserMessage)
        {
            bubbleBackground.color = userBubbleColor;
            messageText.color = Color.white;
        }

        messageText.text = message;
        messageText.ForceMeshUpdate();
    }
}
