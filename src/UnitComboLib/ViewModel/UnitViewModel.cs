namespace UnitComboLib.ViewModel
{
  using System.Collections.ObjectModel;
  using System.Linq;
  using System.Windows.Input;

  using UnitComboLib.Command;
  using UnitComboLib.Unit;
  using System.ComponentModel;
  using UnitComboLib.Local;

  /// <summary>
  /// Viewmodel class to manage unit conversion based on default values and typed values.
  /// </summary>
  public class UnitViewModel : BaseViewModel, IDataErrorInfo
  {
    #region fields
    private ListItem mSelectedItem = null;

    private ObservableCollection<ListItem> mUnitList = null;

    private string mValueTip = string.Empty;
    private double mValue = 0;
    private string mstrValue = "0.0";

    private Converter mUnitConverter = null;

    private RelayCommand<Itemkey> mSetSelectedItemCommand = null;

    /// <summary>
    /// Minimum value to be converted for both percentage and pixels
    /// </summary>
    private const double MinFontSizeValue = 2.0;

    /// <summary>
    /// Maximum value to be converted for both percentage and pixels
    /// </summary>
    private const double MaxFontSizeValue = 399;

    /// <summary>
    /// Minimum value to be converted for both percentage and pixels
    /// </summary>
    private const double MinPercentageSizeValue = 24;

    /// <summary>
    /// Maximum value to be converted for both percentage and pixels
    /// </summary>
    private const double MaxPercentageSizeValue = 3325.0;
    #endregion fields

    #region constructor
    /// <summary>
    /// Class constructor to construct complete viewmodel object from listed parameters.
    /// </summary>
    /// <param name="list"></param>
    /// <param name="unitConverter"></param>
    /// <param name="defaultIndex"></param>
    /// <param name="defaultValue"></param>
    public UnitViewModel(ObservableCollection<ListItem> list,
                         Converter unitConverter,
                         int defaultIndex = 0,
                         double defaultValue = 100)
    {
      this.mUnitList = new ObservableCollection<ListItem>(list);
      this.mSelectedItem = this.mUnitList[defaultIndex];

      this.mUnitConverter = unitConverter;
      
      this.mValue = defaultValue;
      this.mstrValue = string.Format("{0:0}", this.mValue);
    }

    /// <summary>
    /// Standard class constructor is hidden in favour of parameterized constructor.
    /// </summary>
    protected UnitViewModel()
    {
    }
    #endregion constructor

    #region properties
    /// <summary>
    /// Currently selected value in screen points. This property is needed because the run-time system
    /// cannot work with percent values directly. Therefore, this property always ensures a correct
    /// font size no matter what the user selected in percent.
    /// </summary>
    public int ScreenPoints
    {
      get
      {
        if (this.SelectedItem != null)
          return (int)this.mUnitConverter.Convert(this.SelectedItem.Key, this.mValue, Itemkey.ScreenFontPoints);

        // Fallback to default if all else fails
        return (int)Unit.Screen.ScreenConverter.OneHundretPercentFont;
      }

      set
      {
        if (this.SelectedItem != null)
        {
          if (this.SelectedItem.Key == Itemkey.ScreenFontPoints)
          {
            if (value != this.Value)
              this.Value = value;
          }
          else
          {
            if (value != (int)this.mUnitConverter.Convert(this.SelectedItem.Key, this.mValue, Itemkey.ScreenFontPoints))
              this.Value = (int)this.mUnitConverter.Convert(Itemkey.ScreenFontPoints, value, this.SelectedItem.Key);
          }
        }
      }
    }

    /// <summary>
    /// Get list of units, their default value lists, itemkeys etc.
    /// </summary>
    public ObservableCollection<ListItem> UnitList
    {
      get
      {
        return this.mUnitList;
      }
    }

    /// <summary>
    /// Get/set currently selected unit key, converter, and default value list.
    /// </summary>
    public ListItem SelectedItem
    {
      get
      {
        return this.mSelectedItem;
      }

      set
      {
        if (this.mSelectedItem != value)
        {
          this.mSelectedItem = value;

          this.RaisePropertyChanged(() => this.SelectedItem);
          this.RaisePropertyChanged(() => this.ScreenPoints);
          this.RaisePropertyChanged(() => this.MinValue);
          this.RaisePropertyChanged(() => this.MaxValue);          
        }
      }
    }

    #region IDataErrorInfo Interface
    /// <summary>
    /// Source: http://joshsmithonwpf.wordpress.com/2008/11/14/using-a-viewmodel-to-provide-meaningful-validation-error-messages/
    /// </summary>
    public string Error
    {
      get
      {
        return null;
      }
    }

    /// <summary>
    /// Standard property that is part of the <seealso cref="IDataErrorInfo"/> interface.
    /// 
    /// Evaluetes whether StringValue parameter represents a value within the expected range
    /// and sets a corresponding errormessage in the ValueTip property if not.
    /// 
    /// Source: http://joshsmithonwpf.wordpress.com/2008/11/14/using-a-viewmodel-to-provide-meaningful-validation-error-messages/
    /// </summary>
    /// <param name="propertyName"></param>
    /// <returns></returns>
    public string this[string propertyName]
    {
      get
      {
        if (propertyName == "StringValue")
        {
          if (string.IsNullOrEmpty(this.mstrValue))
            return SetToolTip(Strings.Integer_Contain_ErrorMessage);

          double dValue;
          if (double.TryParse(this.mstrValue, out dValue) == true)
          {
            string message;

            if (IsDoubleWithinRange(dValue, this.SelectedItem.Key, out message) == true)
            {
              this.Value = dValue;
              return SetToolTip(null);
            }
            else
              return SetToolTip(message);
          }
          else
            return SetToolTip(Strings.Integer_Conversion_ErrorMessage);
        }

        return SetToolTip(null);
      }
    }
    #endregion IDataErrorInfo Interface

    /// <summary>
    /// Get a string that indicates the format of the
    /// expected input or a an error if the current input is not valid.
    /// </summary>
    public string ValueTip
    {
      get
      {
        return this.mValueTip;
      }

      protected set
      {
        if (this.mValueTip != value)
        {
          this.mValueTip = value;
          this.RaisePropertyChanged(() => this.ValueTip);
        }
      }
    }

    /// <summary>
    /// String representation of the double value that
    /// represents the unit scaled value in this object.
    /// </summary>
    public string StringValue
    {
      get
      {
        return this.mstrValue;
      }

      set
      {
        if (this.mstrValue != value)
        {
          this.mstrValue = value;
          this.RaisePropertyChanged(() => this.StringValue);
        }
      }
    }

    /// <summary>
    /// Get double value represented in unit as indicated by SelectedItem.Key.
    /// </summary>
    public double Value
    {
      get
      {
        return this.mValue;
      }

      set
      {
        if (this.mValue != value)
        {
          this.mValue = value;
          this.mstrValue = string.Format("{0:0}", this.mValue);

          this.RaisePropertyChanged(() => this.Value);
          this.RaisePropertyChanged(() => this.StringValue);
          this.RaisePropertyChanged(() => this.ScreenPoints);
        }
      }
    }

    /// <summary>
    /// Get the legal minimum value in dependency of the current unit.
    /// </summary>
    public double MinValue
    {
      get
      {
        return this.GetMinValue(this.SelectedItem.Key);
      }
    }

    /// <summary>
    /// Get the legal maximum value in dependency of the current unit.
    /// </summary>
    public double MaxValue
    {
      get
      {
        return this.GetMaxValue(this.SelectedItem.Key);
      }
    }

    /// <summary>
    /// Get command to be executed when the user has selected a unit
    /// (eg. 'Km' is currently used but user selected 'm' to be used next)
    /// </summary>
    public ICommand SetSelectedItemCommand
    {
      get
      {
        if (this.mSetSelectedItemCommand == null)
          this.mSetSelectedItemCommand = new RelayCommand<Itemkey>(p => this.SetSelectedItemExecuted(p),
                                                                   p => true);

        return this.mSetSelectedItemCommand;
      }
    }
    #endregion properties

    #region methods
    /// <summary>
    /// Convert current double value from current unit to
    /// unit as indicated by <paramref name="unitKey"/> and
    /// set corresponding SelectedItem.
    /// </summary>
    /// <param name="unitKey">New unit to convert double value into and set SelectedItem to.</param>
    /// <returns></returns>
    private object SetSelectedItemExecuted(Itemkey unitKey)
    {
      // Find the next selected item
      ListItem li = this.mUnitList.SingleOrDefault(i => i.Key == unitKey);

      // Convert from current item to find the next selected item
      if (li != null)
      {
        double dValue;
        if (double.TryParse(this.mstrValue, out dValue) == true)
        {
          double tempValue = this.mUnitConverter.Convert(this.SelectedItem.Key, dValue, li.Key);

          if (tempValue < this.GetMinValue(unitKey))
            tempValue = this.GetMinValue(unitKey);
          else
            if (tempValue > this.GetMaxValue(unitKey))
              tempValue = this.GetMaxValue(unitKey);

          this.Value = tempValue;
          this.mstrValue = string.Format("{0:0}", this.mValue);

          this.SelectedItem = li;
          this.ValueTip = this.SetUnitRangeMessage(unitKey);  // Set standard tool tip about valid range
        }

        this.RaisePropertyChanged(() => this.Value);
        this.RaisePropertyChanged(() => this.MinValue);
        this.RaisePropertyChanged(() => this.MaxValue);
        this.RaisePropertyChanged(() => this.StringValue);
        this.RaisePropertyChanged(() => this.SelectedItem);
      }

      return null;
    }

    /// <summary>
    /// Check whether the <paramref name="doubleValue"/> is within the expected
    /// range of <paramref name="unitToConvert"/> and output a corresponding
    /// error message via <paramref name="message"/> parameter if not.
    /// </summary>
    /// <param name="doubleValue"></param>
    /// <param name="unitToConvert"></param>
    /// <param name="message"></param>
    /// <returns>False if range is not acceptable, true otherwise</returns>
    private bool IsDoubleWithinRange(double doubleValue,
                                     Itemkey unitToConvert,
                                     out string message)
    {
      message = this.SetUnitRangeMessage(unitToConvert);

      switch (unitToConvert)
      {
        // Implement a minimum value of 2 in any way (no matter what the unit is)
        case Itemkey.ScreenFontPoints:
          if (doubleValue < MinFontSizeValue)
            return false;
          else
            if (doubleValue > MaxFontSizeValue)
              return false;

          return true;

        // Implement a minimum value of 2 in any way (no matter what the unit is)
        case Itemkey.ScreenPercent:
          if (doubleValue < MinPercentageSizeValue)
            return false;
          else
            if (doubleValue > MaxPercentageSizeValue)
              return false;

          return true;
      }

      return false;
    }

    private string SetUnitRangeMessage(Itemkey unit)
    {
      switch (unit)
      {
        // Implement a minimum value of 2 in any way (no matter what the unit is)
        case Itemkey.ScreenFontPoints:
          return this.FontSizeErrorTip();

          // Implement a minimum value of 2 in any way (no matter what the unit is)
          case Itemkey.ScreenPercent:
          return this.PercentSizeErrorTip();

          default:
          throw new System.NotSupportedException(unit.ToString());
       }
    }

    /// <summary>
    /// Generate a standard font message to indicate the expected range value.
    /// </summary>
    /// <returns></returns>
    private string FontSizeErrorTip()
    {
      return string.Format(Strings.Enter_Font_Size_InRange_Message,
                           string.Format("{0:0}", MinFontSizeValue),
                           string.Format("{0:0}", MaxFontSizeValue));
    }

    /// <summary>
    /// Generate a standard percent message to indicate the expected range value.
    /// </summary>
    /// <returns></returns>
    private string PercentSizeErrorTip()
    {
      return string.Format(Strings.Enter_Percent_Size_InRange_Message,
                            string.Format("{0:0}", MinPercentageSizeValue),
                            string.Format("{0:0}", MaxPercentageSizeValue));
    }

    /// <summary>
    /// Set a tip like string to indicate the expected input format
    /// or input errors (if there are any input errors).
    /// </summary>
    /// <param name="strError"></param>
    /// <returns></returns>
    private string SetToolTip(string strError)
    {
      string standardTip = string.Format(Strings.Enter_Percent_Font_Size_InRange_Message,
                                          string.Format("{0:0}", MinPercentageSizeValue),
                                          string.Format("{0:0}", MaxPercentageSizeValue),
                                          string.Format("{0:0}", MinFontSizeValue),
                                          string.Format("{0:0}", MaxFontSizeValue));

      if (strError == null)
      {
        if (this.SelectedItem != null)
        {
          switch (this.SelectedItem.Key)
          {
            case Itemkey.ScreenFontPoints:
              this.ValueTip = this.FontSizeErrorTip();
              break;

            case Itemkey.ScreenPercent:
              this.ValueTip = this.PercentSizeErrorTip();
              break;

            default:
              this.ValueTip = standardTip;
              break;
          }
        }
        else
          this.ValueTip = standardTip;
      }
      else
        this.ValueTip = strError;

      return strError;
    }

    private double GetMinValue(Itemkey key)
    {
      if (key == Itemkey.ScreenFontPoints)
        return UnitViewModel.MinFontSizeValue;

      return UnitViewModel.MinPercentageSizeValue;
    }

    private double GetMaxValue(Itemkey key)
    {
      if (key == Itemkey.ScreenFontPoints)
        return UnitViewModel.MaxFontSizeValue;

      return UnitViewModel.MaxPercentageSizeValue;
    }

    #endregion methods
  }
}
