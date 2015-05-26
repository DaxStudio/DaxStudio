using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using ICSharpCode.AvalonEdit.Document;
using System.Text.RegularExpressions;
using DaxStudio.UI.Utils;

namespace DaxStudio.UI.Utils
{
    public enum LineState
    {
        String,
        Table,
        Column,
        Measure,
        LetterOrDigit,
        Other,
        NotSet,
        TableDelimiter
    }

    public class DaxLineState
    {
        private int _endOffset=0;
        private int _startOffset=0;
        private int _caretOffset;
        private int _startOfLineOffset = 0;
        private Utils.LineState _state;
        private LineState _endState;
        private string _tableName;

        public DaxLineState(LineState lineState,int caretOffset, int startOffset, int endOffset, int startOfLineOffset)
        {
            _state = lineState;
            _caretOffset = caretOffset;
            _startOffset = startOffset;
            _endOffset = endOffset;
            _endState = Utils.LineState.NotSet;
            _startOfLineOffset = startOfLineOffset;
        }
        public LineState LineState { get { return _state; } }
        public int StartOffset { get { return _startOffset + _startOfLineOffset; } }
        public int EndOffset { get { return (_endOffset==0?_caretOffset:_endOffset) + _startOfLineOffset; } }
        public string TableName { get { return _tableName; } set { _tableName = value; } }
        public void SetState(LineState newState, int pos)
        {
            if (newState != _state)
            {
                if (pos <= _caretOffset && _endOffset == 0)
                {
                    _state = newState;
                    _startOffset = pos;
                }
                else
                {
                    if ( _endState == Utils.LineState.NotSet)
                    {
                        _endState = newState;
                        _endOffset = pos;
                    }
                }
            }
            
        }

    }
    public static class DaxLineParser
    {
        private static Regex MeasureDefRegex = new Regex(@"\bmeasure\s*(?:'.*'|[^\s]*)\[?[^\]]*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static DaxLineState ParseLine(string line, int offset, int startOfLineOffset )
        {
            StringBuilder sbTableName = new StringBuilder();
            //LineState daxState.LineState = LineState.Other;
            DaxLineState daxState = new DaxLineState(LineState.NotSet, offset, 0, 0, startOfLineOffset);
            for (var i = 0; i < line.Length; i++)
            {
                switch (line[i])
                {
                    case '\"':
                        if (daxState.LineState == LineState.String)
                            daxState.SetState(LineState.Other,i);
                        else
                            daxState.SetState(LineState.String, i);
                        break;
                    case '[':
                        if (daxState.LineState != LineState.String)
                        {
                            if (daxState.LineState == LineState.LetterOrDigit 
                                || daxState.LineState == LineState.TableDelimiter)
                            {
                                daxState.SetState(LineState.Column, i);
                                if (i > 1 && line[i-1] != '\'')
                                {
                                    sbTableName.Clear();
                                    sbTableName.Append(GetPreceedingTableName(line.Substring(0, i)));
                                }
                            }
                            else daxState.SetState(LineState.Measure, i);
                        }
                        break;
                    case ']':
                        if (daxState.LineState != LineState.String)
                            daxState.SetState(LineState.Other,i);
                        break;
                    case '\'':
                        if (daxState.LineState != LineState.String && daxState.LineState != LineState.Table)
                        {
                            daxState.SetState(LineState.Table,i);
                            sbTableName.Clear();
                            break;
                        }
                        if (daxState.LineState == LineState.Table)
                            daxState.SetState( LineState.TableDelimiter,i);
                        break;
                    default:
                        if (daxState.LineState != LineState.String 
                            && daxState.LineState != LineState.Table 
                            && daxState.LineState != LineState.TableDelimiter
                            && daxState.LineState != LineState.Column )
                        {
                            daxState.SetState( char.IsLetterOrDigit(line[i])?LineState.LetterOrDigit:LineState.Other ,i);
                        }
                        if (daxState.LineState == LineState.Table) sbTableName.Append(line[i]);
                        break;
                }

            }
            
            daxState.TableName = sbTableName.ToString();
            return daxState;
        }

        public static string GetPreceedingTableName(string line)
        {
            
            string tableName = "";
            if (line.EndsWith("["))
            {
                line = line.TrimEnd('[');
            }
            try
            {
                var separator = ' ';
            
                if (line.Length == 0) return " "; // return a space if we are at the start of a line

                var lastChar = line[line.Length - 1];
                if ("+\\*(,-><=^ ".Contains(lastChar)) return " "; //

                if (line.EndsWith("\'"))
                {
                    separator = '\'';
                    line = line.TrimEnd('\'');
                }
                for (var i = line.Length - 1; i >= 0; i--)
                {
                    if (line[i] == separator) break;
                    if (separator == ' ' && " ,(+*-^%=<>\\".Contains(line[i])) break;
                    tableName = line[i] + tableName;
                }
            }
            catch (Exception ex)
            {
                Log.Error("{class} {method} {error}", "DaxLineParser", "GetPreceedingTableName", ex.Message);
            }

            return tableName.TrimStart('\'').TrimEnd('\'');
        }
        
        public static ISegment GetPreceedingWordSegment(TextDocument document, int endOffset, string line)
        {
            string word = "";
            int pos=0;
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
                    case ' ':
                        if (!inStr && !inTab && !inCol) { word = ""; pos = i+1; }
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
            var segment = new ICSharpCode.AvalonEdit.Document.AnchorSegment(document,endOffset - word.Length, word.Length);
            return segment;
        }

        public static bool IsLineMeasureDefinition(string line)
        {
            return MeasureDefRegex.IsMatch(line);    
        }

        public static bool IsSeparatorChar(char c)
        {
            return "(=-\\*><^%&|, .\"".Contains(c);
        }

    }
}
