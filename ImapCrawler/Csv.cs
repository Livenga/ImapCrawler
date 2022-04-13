namespace ImapCrawler;

using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


/// <summary></summary>
public static class Csv {
  /// <summary></summary>
  public static async Task WriteRecordsAsync<T>(
      Stream stream,
      IEnumerable<T> records) {
    using var writer = new StreamWriter(stream, Encoding.UTF8);
    using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

    csv.Context.TypeConverterCache.RemoveConverter<DateTime>();
    csv.Context.TypeConverterCache.AddConverter<DateTime>(new CsvDateTimeConverter());

    await csv.WriteRecordsAsync(records);
    await stream.FlushAsync();
  }

  /// <summary></summary>
  public static async Task WriteRecordsAsync<T>(
      string path,
      IEnumerable<T> records) {
    var mode = File.Exists(path) ? FileMode.Truncate : FileMode.CreateNew;

    using var stream = File.Open(path, mode, FileAccess.Write);
    await WriteRecordsAsync(stream, records);
  }
}

/// <summary></summary>
public class CsvDateTimeConverter : CsvHelper.TypeConversion.DefaultTypeConverter {
  /// <summary></summary>
  public string Format => _format;

  private readonly string _format;

  /// <summary></summary>
  public CsvDateTimeConverter(string format = "yyyy-MM-ddTHH:mm:ss") {
    _format = format;
  }

  /// <summary></summary>
  public override object ConvertFromString(
      string text,
      IReaderRow row,
      MemberMapData memberMapData) =>
    DateTime.ParseExact(text, _format, null);

  public override string ConvertToString(
      object value,
      IWriterRow row,
      MemberMapData memberMapData) =>
    value switch {
      DateTime dt => dt.ToString(_format),
      _ => throw new InvalidOperationException(),
    };
}
