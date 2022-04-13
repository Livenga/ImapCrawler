namespace ImapCrawler;

using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


/// <summary></summary>
static class Program {
  /// <summary></summary>
  static async Task Main(string[] args) {
#if DISPLAY_SECURE_SOCK_OPTIONS
    foreach(var opt in Enum.GetValues(typeof(SecureSocketOptions)).Cast<SecureSocketOptions>()) {
      Console.Error.WriteLine($"{opt}: {(short)opt}");
    }
#endif
    if(args.Length == 0)
      return;

    init();

    var config = await Json.ReadAsync<Config>(args[0]);

    var protocolLogger = new ProtocolLogger(
        fileName: Path.Combine("logs", $"{DateTime.Now.ToString("yyyy-MM-dd")}.log"),
        append: true);
    //using var imap = new ImapClient(protocolLogger);
    using var imap = new ImapClient();

    imap.Connected     += OnImapClientConnected;
    imap.Disconnected  += OnImapClientDisconnected;
    imap.Authenticated += OnImapClientAuthenticated;

    await imap.ConnectAsync(
        host: config.Host,
        port: config.Port,
        options: config.Options);
    await imap.AuthenticateAsync(
        encoding: Encoding.UTF8,
        userName: config.UserName,
        password: config.Password);

    await imap.Inbox.OpenAsync(FolderAccess.ReadOnly);

    //var partialMessage = imap.Inbox.Select(m => ToCsvMail(m));

    var partialMessage = new List<CsvMail>();
    int cnt = 0;
    foreach(var m in imap.Inbox) {
      Console.Error.WriteLine($"# {++cnt}\t{m.Date.DateTime.ToString("yyyy-MM-dd HH:mm:ss")}\t{m.MessageId} {m.Subject}");
      partialMessage.Add(ToCsvMail(m));
    }

    var path = Path.Combine("csv", $"{config.Host}-{config.UserName}.csv");
    await Csv.WriteRecordsAsync(path, partialMessage);

    await imap.DisconnectAsync(true);
  }

  /// <summary></summary>
  private static CsvMail ToCsvMail(MimeKit.MimeMessage msg) =>
    new CsvMail() {
      Id               = msg.MessageId,
      From             = ConvertAddressList(msg.From),
      To               = ConvertAddressList(msg.To),
      Cc               = ConvertAddressList(msg.Cc),
      Bcc              = ConvertAddressList(msg.Bcc),
      AttachmentsCount = msg.Attachments?.Count() ?? 0,
      Subject          = msg.Subject,
      MailBody         = msg.TextBody,
      Date             = msg.Date.DateTime,
      RecentDate       = msg.ResentDate.DateTime,
    };

  /// <summary></summary>
  private static string ConvertAddressList(
      MimeKit.InternetAddressList addrs,
      string separator = ", ") =>
    string.Join(separator, addrs.Select(addr => ConvertAddress(addr)).ToArray());

  /// <summary></summary>
  private static string ConvertAddress(MimeKit.InternetAddress addr) =>
    addr switch {
      MimeKit.MailboxAddress _addr => $"{_addr.Name}<{_addr.Address}>",
      _ => $"!!! Not Supported(${addr.GetType().FullName}) !!!"
    };

  /// <summary></summary>
  static void init() {
    CreateDirectory("logs");
    CreateDirectory("csv");
  }

  /// <summary></summary>
  private static void CreateDirectory(string path) {
    try {
      if(! Directory.Exists(path))
        Directory.CreateDirectory(path);
    } catch { }
  }

  /// <summary></summary>
  private static void OnImapClientConnected(object? source, ConnectedEventArgs e) {
#if DEBUG
    Console.Error.WriteLine($"# {source?.GetType().FullName}.{nameof(OnImapClientConnected)}");
    Console.Error.WriteLine($"\t{e.Host}:{e.Port} {e.Options}");
#endif
  }


  /// <summary></summary>
  private static void OnImapClientDisconnected(object? source, DisconnectedEventArgs e) {
#if DEBUG
    Console.Error.WriteLine($"# {source?.GetType().FullName}.{nameof(OnImapClientDisconnected)}");
    Console.Error.WriteLine($"\t{e.Host}:{e.Port} {e.IsRequested} {e.Options}");
#endif
  }

  /// <summary></summary>
  private static void OnImapClientAuthenticated(object? source, AuthenticatedEventArgs e) {
#if DEBUG
    Console.Error.WriteLine($"# {source?.GetType().FullName}.{nameof(OnImapClientAuthenticated)}");
    Console.Error.WriteLine($"\t{e.Message}");
#endif
  }
}
