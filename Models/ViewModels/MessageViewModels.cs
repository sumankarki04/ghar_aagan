using System.ComponentModel.DataAnnotations;

namespace GharAagan.Models.ViewModels;

/// <summary>One row in the inbox: a booking conversation summary.</summary>
public class ConversationSummary
{
    public int BookingId { get; set; }
    public string OtherPartyName { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public BookingStatus BookingStatus { get; set; }
    public DateTime ScheduledDateTime { get; set; }
    public string? LastMessage { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public int UnreadCount { get; set; }
}

public class MessageThreadViewModel
{
    public Booking Booking { get; set; } = null!;
    public string OtherPartyName { get; set; } = string.Empty;
    public string OtherPartyPhone { get; set; } = string.Empty;
    public string CurrentUserId { get; set; } = string.Empty;
    public List<Message> Messages { get; set; } = new();
    public SendMessageViewModel NewMessage { get; set; } = new();
}

public class SendMessageViewModel
{
    public int BookingId { get; set; }

    [Required(ErrorMessage = "Message cannot be empty.")]
    [StringLength(1000)]
    public string Content { get; set; } = string.Empty;
}
