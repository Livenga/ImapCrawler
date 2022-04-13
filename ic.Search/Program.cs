namespace ic.Search;

using ic.Data;
using ic.IO;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


class Program {
  static async Task Main(string[] args) {
    var config = await Json.ReadAsync<ic.Data.Config>(args[0]);

    using var imap = new ImapClient();
    await imap.ConnectAsync(
        host: config.Host,
        port: config.Port,
        options: config.Options);

    await imap.AuthenticateAsync(
        encoding: Encoding.UTF8,
        userName: config.UserName,
        password: config.Password);

    await imap.Inbox.OpenAsync(FolderAccess.ReadOnly);

    var uniqueIds = await imap.Inbox.SearchAsync(SearchQuery.All);
    foreach(var id in uniqueIds.Select(uid => uid.Id)) {
      Console.Error.WriteLine($"{id}");
    }

    /*
    // 検索
    var query = new TextSearchQuery(
        term: SearchTerm.BodyContains,
        text: string.Empty/);

    var targetUniqueIds = await imap.Inbox.SearchAsync(uniqueIds, query);
    Console.Error.WriteLine($"{uniqueIds.Count()} => {targetUniqueIds.Count()}");
    foreach(var uniqueId in targetUniqueIds) {
      var message = await imap.Inbox.GetMessageAsync(uniqueId);
    }*/

    await imap.DisconnectAsync(true);
  }
}
