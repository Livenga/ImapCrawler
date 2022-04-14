namespace ic.Extract;

using MailKit;
using MailKit.Search;
using MailKit.Net.Imap;
using System;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


class Program {
  static async Task Main(string[] args) {
    if(args.Length == 0 || ! Directory.Exists(args[0]))
      return;

    var targets = Directory.GetFiles(path: args[0])
      .Select(p => new {
          Path = p,
          Username = GetUsername(Path.GetFileName(p)) })
      .Where(o => o.Username != null)
      .GroupBy(
          kvp => kvp.Username ?? throw new NullReferenceException(),
          kvp => kvp.Path);

    foreach(var t in targets) {
      Console.Error.WriteLine($"{t.Key}");
      var mails = (await Task.WhenAll(t.Select(p => ic.IO.Csv.ReadRecordsAsync<ic.Data.PartialMail>(p))))
        .SelectMany(m => m)
        .Where(m => m.AttachmentsCount > 0 &&
            (m.Subject.Contains("見積") || m.Subject.Contains("発注") || m.Subject.Contains("注文")));

      var uniqueIds = mails.Select(m => m.UniqueId);

      try {
        var config = await ic.IO.Json.ReadAsync<ic.Data.Config>(Path.Combine("json", $"{t.Key}.json"));
        await GetAttemptFilesAsync(config, uniqueIds);
      } catch(Exception e) {
        Console.Error.WriteLine($"#\t{e.GetType().FullName} {e.Message}");
        Console.Error.WriteLine(e.StackTrace);
      }
    }
  }

  //
  private static string? GetUsername(string fileName) {
    var match = new Regex(
        @"(?'userName'.*)@(?'domain'[- 0-9a-zA-Z]+\.[0-9a-zA-Z]+)\.[0-9]+\.csv",
        RegexOptions.Compiled).Match(fileName);

    return match.Success
      ?  $"{match.Groups["userName"].Value}@{match.Groups["domain"].Value}"
      : null;
  }

  /// <summary></summary>
  private static async Task GetAttemptFilesAsync(
      ic.Data.Config config,
      IEnumerable<uint> uniqueIds) {
    using var imap = new ImapClient();

    await imap.ConnectAsync(
        host: config.Host,
        port: config.Port,
        options: config.Options);

    await imap.AuthenticateAsync(
        userName: config.UserName,
        password: config.Password);

    await imap.Inbox.OpenAsync(FolderAccess.ReadOnly);
    var targetUniqueIds = await imap.Inbox.SearchAsync(
        uids: uniqueIds.Select(u => new UniqueId(u)).ToList(),
        query: SearchQuery.All);

    var mCnt = uniqueIds.Count();
    int cnt = 0;
    foreach(var uid in uniqueIds.Select(u => new UniqueId(u))) {
      var msg = await imap.Inbox.GetMessageAsync(uid);
      Console.Error.WriteLine($"\t{++cnt} / {mCnt}\t{msg.Subject} {msg.MessageId}");

      foreach(var a in msg.Attachments
          .Where(a => a.IsAttachment && a is MimeKit.MimePart)
          .Cast<MimeKit.MimePart>()) {
        var toFileName = Path.Combine("attachments", $"{uid}.{a.FileName}");

        byte[] buffer;
        using(var ms = new MemoryStream()) {
          ms.Position = 0;
          await a.WriteToAsync(
              stream: ms,
              contentOnly: true);
          await ms.FlushAsync();
          ms.Position = 0;

          buffer = System.Convert
            .FromBase64String(s: System.Text.Encoding.UTF8.GetString(ms.ToArray()));
        }

        using(var io = File.Open(toFileName,
              File.Exists(toFileName) ? FileMode.Truncate : FileMode.CreateNew,
              FileAccess.Write)) {
          await io.WriteAsync(buffer, 0, buffer.Length);
          await io.FlushAsync();
        }
      }
    }

    await imap.DisconnectAsync(true);
  }
}
