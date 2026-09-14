using UnityEngine;
using System;

public class ChatBubbleSpawner : MonoBehaviour
{
    [SerializeField] private string initialMessage = "Welcome!";
    [SerializeField] private ChatBubble userChatBubblePrefab = null;
    [SerializeField] private ChatBubble aiChatBubblePrefab = null;

    [SerializeField] private Transform chatBubblesParent= null;
    [Header(" Events ")]
    public Action OnMessageReceived;

    void Start()
    {
        SpawnChatBubble(initialMessage, false);
    }

    public void SpawnChatBubble(string message, bool isUserMessage)
    {
        Debug.Log($"Spawning chat bubble. User message: {isUserMessage}, Message: {message}");
        if(isUserMessage)
        {
            var chatBubble = Instantiate(userChatBubblePrefab, chatBubblesParent);
            chatBubble.Configure(message, isUserMessage);
        }
        else
        {
            var chatBubble = Instantiate(aiChatBubblePrefab, chatBubblesParent);
            chatBubble.Configure(message, isUserMessage);
        }
        OnMessageReceived?.Invoke();
    }
}
