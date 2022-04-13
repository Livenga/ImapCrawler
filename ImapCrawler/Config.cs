namespace ImapCrawler;

using MailKit.Security;

/// <summary></summary>
public class Config {
  /// <summary></summary>
  public string Host { set; get; } = string.Empty;

  /// <summary></summary>
  public int Port { set; get; } = 143;

  /// <summary></summary>
  public SecureSocketOptions Options { set; get; } = SecureSocketOptions.None;

  /// <summary></summary>
  public string UserName { set; get; } = string.Empty;

  /// <summary></summary>
  public string Password { set; get; } = string.Empty;
}
