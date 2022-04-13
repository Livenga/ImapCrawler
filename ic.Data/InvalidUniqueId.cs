namespace ic.Data;

/// <summary></summary>
public class InvalidUniqueId {
  /// <summary></summary>
  public uint UniqueId { set; get ;} = 0;

  /// <summary></summary>
  public DateTime CreatedAt { set; get; } = DateTime.Now;
}
