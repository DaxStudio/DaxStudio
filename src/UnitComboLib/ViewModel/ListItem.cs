namespace UnitComboLib.ViewModel
{
  using System.Collections.ObjectModel;
  using UnitComboLib.Unit;

  /// <summary>
  /// One item in the list of unit definition items
  /// </summary>
  public class ListItem
  {
    #region constructor
    /// <summary>
    /// Class constructor.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="displayNameLong"></param>
    /// <param name="displayNameShort"></param>
    /// <param name="defaultValues"></param>
    public ListItem(Itemkey key,
                    string displayNameLong,
                    string displayNameShort,
                    ObservableCollection<string> defaultValues)
    {
      this.Key = key;
      this.DisplayNameLong = (displayNameLong == null ? "(null)" : displayNameLong);
      this.DisplayNameShort = (displayNameShort == null ? "(null)" : displayNameShort);

      this.DefaultValues = defaultValues;
    }

    /// <summary>
    /// Hidden class constructor.
    /// </summary>
    protected ListItem()
    {
    }
    #endregion constructor

    #region properties
    /// <summary>
    /// Get unit of the values stored in this object (<seealso cref="DefaultValues"/>).
    /// </summary>
    public Itemkey Key { get; private set; }

    /// <summary>
    /// Display a long descriptive string of the unit stored in this object.
    /// </summary>
    public string DisplayNameLong { get; private set; }

    /// <summary>
    /// Display a short string of the unit stored in this object.
    /// </summary>
    public string DisplayNameShort { get; private set; }

    /// <summary>
    /// Display a combination of long and short string of the unit stored in this object.
    /// </summary>
    public string DisplayNameLongWithShort
    {
      get
      {
        return string.Format("{0} ({1})", this.DisplayNameShort, this.DisplayNameLong);
      }
    }

    /// <summary>
    /// Get a list of useful default values for the unit stored in this item.
    /// </summary>
    public ObservableCollection<string> DefaultValues
    {
      get;

      private set;
    }
    #endregion properties
  }
}
