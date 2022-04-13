namespace ic.Data;

/// <summary></summary>
public class PartialMail {
  /// <summary></summary>
  public uint UniqueId { set; get; } = 0;

  /// <summary></summary>
  public string MessageId { set; get; } = string.Empty;

  /// <summary></summary>
  public string From { set; get; } = string.Empty;

  /// <summary></summary>
  public string To { set; get; } = string.Empty;

  /// <summary></summary>
  public string Cc { set; get; } = string.Empty;

  /// <summary></summary>
  public string Bcc { set; get; } = string.Empty;

  /// <summary></summary>
  public int AttachmentsCount { set; get; } = 0;

  /// <summary></summary>
  public string Subject { set; get; } = string.Empty;

  /// <summary></summary>
  public string MailBody { set; get; } = string.Empty;

  /// <summary></summary>
  public DateTime Date { set; get; } = DateTime.Now;

  /// <summary></summary>
  public DateTime RecentDate { set; get; } = DateTime.Now;
}
