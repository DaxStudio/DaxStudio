using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using ICSharpCode.AvalonEdit.Document;
using System.Text.RegularExpressions;
using DaxStudio.UI.Utils;
using DaxStudio.UI.Extensions;

namespace DaxStudio.UI.Utils
{
    public enum LineState
    {
        String,
        Table,
        TableClosed,
        Column,
        ColumnClosed,
        Measure,
        MeasureClosed,
        LetterOrDigit,
        Other,
        NotSet,
        TableDelimiter,
        Dmv
    }

    public class DaxLineState
    {
        private int _endOffset;
        private int _startOffset;
        private int _caretOffset;
        private int _startOfLineOffset;
        private LineState _state;
        private LineState _endState;
        private string _tableName;
        //private object _columnName;

        public DaxLineState(LineState lineState,int caretOffset, int startOffset, int endOffset, int startOfLineOffset)
        {
            _state = lineState;
            _caretOffset = caretOffset;
            _startOffset = startOffset;
            _endOffset = endOffset;
            _endState = LineState.NotSet;
            _startOfLineOffset = startOfLineOffset;
        }
        public LineState LineState { get { return _state; } }
        public int StartOffset { get { return _startOffset + _startOfLineOffset; } }
        public int EndOffset { get { return (_endOffset==0?_caretOffset:_endOffset) + _startOfLineOffset; } }
        public string TableName { get { return _tableName; } set { _tableName = value; } }

        //public string ColumnName { get { return string.Format("{0}[{1}", _tableName, _columnName); }  internal set { _columnName = value; } }
        public string ColumnName { get; internal set; }
        public void SetState(LineState newState, int pos)
        {
            if (newState != _state)
            {
                if (pos < _caretOffset && _endOffset == 0)
                {
                    _state = newState;
                    if ((_state == LineState.Column && newState == LineState.ColumnClosed)
                        || (_state == LineState.Table && newState == LineState.TableClosed)
                        || (_state == LineState.Measure && newState == LineState.MeasureClosed))
                    {
                        // don't reset startOffset
                    }
                    else
                    {
                        _startOffset = pos;
                    }
                }
                else
                {
                    if (_endState == Utils.LineState.NotSet)
                    {
                        //todo - only set state if we are in one of the closed states
                       // _state = newState;
                        _endState = newState;
                        _endOffset = pos;
                        if (newState == LineState.MeasureClosed) {
                            _endState = LineState.Measure;
                            _state = newState;
                            _endOffset++;
                        }
                        if (newState == LineState.TableClosed) {
                            _endState = LineState.Table;
                            _state = newState;
                            _endOffset++;
                        }
                        if (newState == LineState.ColumnClosed) {
                            _endState = LineState.Column;
                            _state = newState;
                            _endOffset++;
                        }
                        
                    }
                }
            }
            
        }

    }
    public static class DaxLineParser
    {
        //                                 " ,;(+-*^%=<>\\"
        //                                 " ,;(+-*^%=<>\\%&|.\""
        private const string Punctuation = " ,;(+-*^%=<>\\%&|.";
 

        private static Regex MeasureDefRegex = new Regex(@"\bmeasure\s*(?:'.*'|[^\s]*)\[?[^\]]*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static DaxLineState ParseLine(string line, int offset, int startOfLineOffset )
        {
            //System.Diagnostics.Debug.WriteLine(">>> ParseLine");

            // todo - can we get away with 1 string builder instance?
           StringBuilder sbTableName = new StringBuilder();
            StringBuilder sbColumnName = new StringBuilder();
            StringBuilder sbMeasureName = new StringBuilder();

            //LineState daxState.LineState = LineState.Other;
            DaxLineState daxState = new DaxLineState(LineState.NotSet, offset, 0, 0, startOfLineOffset);
            if (offset == 0) { return daxState; }

            for (var i = 0; i < line.Length; i++)
            {
                switch (line[i])
                {
                    case '\"':
                        if (daxState.LineState == LineState.String)
                            daxState.SetState(LineState.Other, i);
                        else
                            daxState.SetState(LineState.String, i);
                        break;
                    case '[':
                        if (daxState.LineState != LineState.String)
                        {
                            if (daxState.LineState == LineState.LetterOrDigit
                                || daxState.LineState == LineState.Table
                                || daxState.LineState == LineState.TableClosed)
                            {
                                daxState.SetState(LineState.Column, i);
                                sbColumnName.Clear();
                                if (i > 1 && line[i - 1] != '\'')
                                {
                                    sbTableName.Clear();
                                    sbTableName.Append(GetPreceedingTableName(line.Substring(0, i)));
                                }
                            }
                            else daxState.SetState(LineState.Measure, i);
                        }
                        break;
                    case ']':
                        switch (daxState.LineState) {
                            case LineState.Column:
                                daxState.SetState(LineState.ColumnClosed, i);
                                break;
                            case LineState.Measure:
                                daxState.SetState(LineState.MeasureClosed, i);
                                break;
                            case LineState.String:
                                // do nothing, stay in string state
                                break;
                            default:
                                daxState.SetState(LineState.Other, i);
                                break;
                        }

                        break;
                    case '\'':
                        if (daxState.LineState != LineState.String && daxState.LineState != LineState.Table)
                        {
                            //daxState.SetState(LineState.Table,i+1);
                            daxState.SetState(LineState.Table, i);
                            sbTableName.Clear();
                            break;
                        }
                        if (daxState.LineState == LineState.Table)
                            daxState.SetState(LineState.TableClosed, i);
                        break;
                    case '(':
                    case '=':
                    case '-':
                    case '\\':
                    case '*':
                    case '>':
                    case '<':
                    case '^':
                    case '%':
                    case '&':
                    case '|':
                    case ',':
                    case ';':
                    case ' ':
                    case '\t':
                        if (daxState.LineState != LineState.String
                            && daxState.LineState != LineState.Table
                            && daxState.LineState != LineState.Column
                            && daxState.LineState != LineState.Measure
                            )
                        {

                            daxState.SetState(line[i].IsDaxLetterOrDigit() ? LineState.LetterOrDigit : LineState.Other, i);
                        }
                        if (daxState.LineState == LineState.Table) sbTableName.Append(line[i]);
                        if (daxState.LineState == LineState.Column) sbColumnName.Append(line[i]);
                        if (daxState.LineState == LineState.Measure) sbMeasureName.Append(line[i]);
                        break;
                    case '.':
                        if (GetPreceedingWord(line.Substring(0,i)).ToUpper() == "$SYSTEM") {
                            if (daxState.LineState != LineState.String && daxState.LineState != LineState.Table)
                            {
                                daxState.SetState(LineState.Dmv, i+1);
                            }
                        }
                        // TODO can tables have . in them??
                        break;
                    default:
                        if (daxState.LineState != LineState.String 
                            && daxState.LineState != LineState.Table 
                            //&& daxState.LineState != LineState.TableDelimiter
                            && daxState.LineState != LineState.Column 
                            && daxState.LineState != LineState.Dmv
                            && daxState.LineState != LineState.Measure)
                        {
                            daxState.SetState( line[i].IsDaxLetterOrDigit() || line[i] == '$'?LineState.LetterOrDigit:LineState.Other ,i);
                        }
                        if (daxState.LineState == LineState.Table) sbTableName.Append(line[i]);
                        if (daxState.LineState == LineState.Column) sbColumnName.Append(line[i]);
                        if (daxState.LineState == LineState.Measure) sbMeasureName.Append(line[i]);
                        break;
                }

            }

            if (daxState.LineState != LineState.String
                && daxState.LineState != LineState.Table
                && daxState.LineState != LineState.Column
                && daxState.LineState != LineState.Measure
                )
            {

                daxState.SetState( LineState.Other, line.Length);
            }

            daxState.TableName = sbTableName.ToString();
            daxState.ColumnName = sbColumnName.ToString();
            return daxState;
        }

        public static string GetPreceedingTableName(string line)
        {
            
            string tableName = "";
            var isQuotedName = false;
            if (line.EndsWith("["))
            {
                line = line.TrimEnd('[');
            }
            try
            {
                Func<char, bool> isSeparator = c => { return char.IsWhiteSpace(c); };
            
                if (line.Length == 0) return " "; // return a space if we are at the start of a line

                var lastChar = line[line.Length - 1];
                if (Punctuation.Contains(lastChar)) return " "; //

                if (line.EndsWith("\'"))
                {
                    isQuotedName = true;
                    isSeparator = c => c == '\'';
                    line = line.TrimEnd('\'');
                }
                for (var i = line.Length - 1; i >= 0; i--)
                {
                    if (isSeparator(line[i] )) break;
                    if (!isQuotedName && Punctuation.Contains(line[i])) break;
                    tableName = line[i] + tableName;
                }
            }
            catch (Exception ex)
            {
                Log.Error("{class} {method} {error}", "DaxLineParser", "GetPreceedingTableName", ex.Message);
            }

            return tableName.TrimStart('\'').TrimEnd('\'');
        }
        
        public static LinePosition GetPreceedingWordSegment( int startOfLineOffset, int column, string line, DaxLineState daxState)
        {
            LinePosition segment = new LinePosition();

            if (daxState != null)
            {
                switch (daxState.LineState)
                {
                    case LineState.TableClosed:
                    case LineState.ColumnClosed:
                    case LineState.MeasureClosed:
                        // for these states we want to replace the entire current "word"
                        segment = new LinePosition() { Offset = startOfLineOffset + daxState.StartOffset, Length = daxState.EndOffset - daxState.StartOffset };
                        break;
                    default:
                        // for other types we want to just replace unto the current cursor position (which is the incoming "column" parameter)
                        segment = new LinePosition() { Offset = startOfLineOffset + daxState.StartOffset, Length = column - daxState.StartOffset };
                        break;
                }
            }

            Log.Debug("{class} {method} {state} {endOffset} offset: {offset} length: {length}", "DaxLineParser", "GetPreceedingWordSegment",daxState.LineState.ToString(), column, segment.Offset, segment.Length);
            return segment;
        }

        public static string GetPreceedingWord(string line)
        {
            line = line.TrimEnd();
            string word = "";
            int pos = 0;
            char c;
            bool inStr = false;
            bool inCol = false;
            bool inTab = false;
            for (var i = 0; i < line.Length; i++)
            {
                c = line[i];
                switch (c)
                {
                    case '(':
                    case '=':
                    case '-':
                    case '\\':
                    case '*':
                    case '>':
                    case '<':
                    case '^':
                    case '%':
                    case '&':
                    case '|':
                    case ',':
                    case ';':
                    case '.':
                    case ' ':
                    case '\t':
                        if (!inStr && !inTab && !inCol) { word = ""; pos = i + 1; }
                        else word += c;
                        break;
                    case '[':
                        if (!inStr) inCol = true;
                        word += c;
                        break;
                    case ']':
                        if (!inStr) inCol = false;
                        word += c;
                        break;
                    case '\"':
                        inStr = !inStr;
                        word += c;
                        break;
                    case '\'':
                        if (!inStr) inTab = !inTab;
                        word += c;
                        break;
                    default:
                        word += c;
                        break;
                }
            }
            System.Diagnostics.Debug.Assert((line.Length - pos) == word.Length);
            return word;
        }

        public static bool IsLineMeasureDefinition(string line)
        {
            return MeasureDefRegex.IsMatch(line);    
        }

        public static bool IsSeparatorChar(char c)
        {
            return Punctuation.Contains(c);
        }

    }
}
