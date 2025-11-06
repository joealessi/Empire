using System;
using System.Collections.Generic;

namespace EmpireGame
{
    public enum MessageType
    {
        Info,       // White/Gray text
        Success,    // Green text
        Warning,    // Yellow text
        Error,      // Red text
        Combat,     // Orange text
        Movement,   // Cyan text
        Production, // Blue text
        Enemy       // Bright red text
    }

    public class GameMessage
    {
        public string Text { get; set; }
        public MessageType Type { get; set; }
        public DateTime Timestamp { get; set; }

        public GameMessage(string text, MessageType type)
        {
            Text = text;
            Type = type;
            Timestamp = DateTime.Now;
        }
    }

    public class MessageLog
    {
        private List<GameMessage> messages;
        private const int MAX_MESSAGES = 1000;

        public MessageLog()
        {
            messages = new List<GameMessage>();
        }

        public void AddMessage(string text, MessageType type = MessageType.Info)
        {
            messages.Add(new GameMessage(text, type));
            
            // Keep log from growing too large
            if (messages.Count > MAX_MESSAGES)
            {
                messages.RemoveAt(0);
            }
        }

        public List<GameMessage> GetMessages()
        {
            return new List<GameMessage>(messages);
        }

        public void Clear()
        {
            messages.Clear();
        }
    }
}