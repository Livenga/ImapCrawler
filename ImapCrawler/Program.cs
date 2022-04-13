namespace ImapCrawler;

using ic.Data;
using ic.IO;
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
  private const string InvalidUniqueIdPath = "invalid_unique_id.json";
  /// <summary></summary>
  static async Task Main(string[] args) {
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

    // 接続と認証
    await imap.ConnectAsync(
        host: config.Host,
        port: config.Port,
        options: config.Options);
    await imap.AuthenticateAsync(
        encoding: Encoding.UTF8,
        userName: config.UserName,
        password: config.Password);

    await imap.Inbox.OpenAsync(FolderAccess.ReadOnly);

    // 固有ID一覧の取得
    var uniqueIds = await imap.Inbox.SearchAsync(MailKit.Search.SearchQuery.All);

    // もし前回のダウンロードが失敗で終了した場合,
    // その途中からダウンロードを再開するため対象となる固有IDまでのリストを再生成する.
    InvalidUniqueId? invalidUniqueId = null;
    try {
      if(File.Exists(InvalidUniqueIdPath)) {
        invalidUniqueId = await Json.ReadAsync<InvalidUniqueId>(InvalidUniqueIdPath);
      }
    } catch { }

    if(invalidUniqueId != null) {
      try {
        var target = uniqueIds.Select((id, idx) => new { Index = idx, UniqueId = id })
          .First(o => o.UniqueId.Id == invalidUniqueId.UniqueId);
        uniqueIds = uniqueIds.Skip(target.Index).ToList();

        Console.Error.WriteLine($"# Target Unique ID[{target.Index}] : {target.UniqueId.Id}");
      } catch(Exception except) {
        Console.Error.WriteLine($"{except.GetType().FullName} {except.Message}");
      }
    }

    bool isCompleted = true;
    int cnt = 0;
    int uniqueIdCount = uniqueIds.Count();
    Console.Error.WriteLine($"* Unique IDs Count: {uniqueIdCount}");

    var partialMessage = new List<ic.Data.PartialMail>();
    foreach(var uniqueId in uniqueIds) {
      try {
        var message = await imap.Inbox.GetMessageAsync(uniqueId);
        Console.Error.WriteLine($"{++cnt} / {uniqueIdCount}\t{message.Date.DateTime.ToString("yyyy-MM-dd HH:mm:ss")}\t{message.MessageId} {message.Subject}");

        partialMessage.Add(ToPartialMail(uniqueId, message));

      } catch(Exception except) {
        // 例外が発生した場合, foreach から抜け出しプログラムを終了させる.
        Console.Error.WriteLine($"Invalid Mesage: {uniqueId}");
        Console.Error.WriteLine($"{except.GetType().FullName} {except.Message}");
        Console.Error.WriteLine(except.StackTrace);

        isCompleted = false;
        invalidUniqueId = new () {
          UniqueId  = uniqueId.Id,
          CreatedAt = DateTime.Now,
        };

        // 例外発生時の固有IDを記録.
        try {
          await Json.WriteAsync(InvalidUniqueIdPath, invalidUniqueId);
        } catch(Exception) { }
        break;
      }
    }

    // 正常終了した場合, ダウンロード失敗時に記録された固有IDデータの削除を行う.
    if(isCompleted) {
      if(File.Exists(InvalidUniqueIdPath)) {
        File.Delete(InvalidUniqueIdPath);
      }
    }

    // 取得したデータを Csv 形式で保存.
    var path = Path.Combine("csv", $"{config.UserName}@{config.Host}.{DateTime.Now.Ticks}.csv");
    await Csv.WriteRecordsAsync(path, partialMessage);

    await imap.DisconnectAsync(true);
  }

  /// <summary></summary>
  private static ic.Data.PartialMail ToPartialMail(
      UniqueId uniqueId,
      MimeKit.MimeMessage msg) =>
    new ic.Data.PartialMail() {
      UniqueId         = uniqueId.Id,
      MessageId        = msg.MessageId,
      From             = ConvertAddressList(msg.From),
      To               = ConvertAddressList(msg.To),
      Cc               = ConvertAddressList(msg.Cc),
      Bcc              = ConvertAddressList(msg.Bcc),
      AttachmentsCount = msg.Attachments?.Count() ?? 0,
      Subject          = msg.Subject,
      MailBody         = ((msg.TextBody?.Length ?? 0) == 0) ? msg.HtmlBody ?? string.Empty : msg.TextBody ?? string.Empty,
      Date             = msg.Date.DateTime,
      RecentDate       = msg.ResentDate.DateTime,
    };

  /// <summary>
  /// InternetAddressList を文字列に変換
  /// </summary>
  private static string ConvertAddressList(
      MimeKit.InternetAddressList addrs,
      string separator = ", ") =>
    string.Join(separator, addrs.Select(addr => ConvertAddress(addr)).ToArray());

  /// <summary>
  /// InternetAddress クラスを都合の良い文字列へ変換.
  /// </summary>
  private static string ConvertAddress(MimeKit.InternetAddress addr) =>
    addr switch {
      MimeKit.MailboxAddress _addr => $"{_addr.Name}<{_addr.Address}>",
      MimeKit.GroupAddress _addr => $"{_addr.Name}({ConvertAddressList(_addr.Members)})",
      _ => throw new NotSupportedException(),
    };

  /// <summary>
  /// 初期化処理
  /// プログラムにおいて必要なものを作成.
  /// </summary>
  static void init() {
    CreateDirectory("logs");
    CreateDirectory("csv");
  }

  /// <summary>
  /// 作成対象のディレクトが存在する場合および例外発生を考慮した CreateDirectory
  /// </summary>
  /// <param name="path">作成ディレクトリパス</param>
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
