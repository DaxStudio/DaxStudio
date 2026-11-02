lexer grammar DAXLexer;

channels { COMMENTS_CHANNEL }

// ===================================================================
// A. Comments and whitespace
// ===================================================================
DOC_COMMENT:                '///' InputCharacter*           -> channel(COMMENTS_CHANNEL);
SINGLE_LINE_COMMENT:        ('//' | '--') InputCharacter*   -> channel(COMMENTS_CHANNEL);
BLOCK_COMMENT:              '/*' .*? '*/'                   -> channel(COMMENTS_CHANNEL);
WHITESPACES:                (Whitespace | NewLine)+         -> channel(HIDDEN);

// ===================================================================
// B. Function tokens (alphabetical, UPPERCASE)
//    All tokens match uppercase because DAXCharStream uppercases input.
// ===================================================================

// --- A ---
ABS:                        'ABS';
ACCRINT:                    'ACCRINT';
ACCRINTM:                   'ACCRINTM';
ACOS:                       'ACOS';
ACOSH:                      'ACOSH';
ACOT:                       'ACOT';
ACOTH:                      'ACOTH';
ADDCOLUMNS:                 'ADDCOLUMNS';
ADDMISSINGITEMS:            'ADDMISSINGITEMS';
ALL:                        'ALL';
ALLCROSSFILTERED:           'ALLCROSSFILTERED';
ALLEXCEPT:                  'ALLEXCEPT';
ALLNOBLANKROW:              'ALLNOBLANKROW';
ALLSELECTED:                'ALLSELECTED';
AMORDEGRC:                  'AMORDEGRC';
AMORLINC:                   'AMORLINC';
AND:                        'AND';
APPROXIMATEDISTINCTCOUNT:   'APPROXIMATEDISTINCTCOUNT';
ASIN:                       'ASIN';
ASINH:                      'ASINH';
ATAN:                       'ATAN';
ATANH:                      'ATANH';
AVERAGE:                    'AVERAGE';
AVERAGEA:                   'AVERAGEA';
AVERAGEX:                   'AVERAGEX';

// --- B ---
BETADIST:                   'BETA.DIST';
BETAINV:                    'BETA.INV';
BLANK:                      'BLANK';

// --- C ---
CALCULATE:                  'CALCULATE';
CALCULATETABLE:             'CALCULATETABLE';
CALENDAR:                   'CALENDAR';
CALENDARAUTO:               'CALENDARAUTO';
CEILING:                    'CEILING';
CHISQDIST:                  'CHISQ.DIST';
CHISQDISTRT:                'CHISQ.DIST.RT';
CHISQINV:                   'CHISQ.INV';
CHISQINVRT:                 'CHISQ.INV.RT';
CLOSINGBALANCEMONTH:        'CLOSINGBALANCEMONTH';
CLOSINGBALANCEQUARTER:      'CLOSINGBALANCEQUARTER';
CLOSINGBALANCEWEEK:         'CLOSINGBALANCEWEEK';
CLOSINGBALANCEYEAR:         'CLOSINGBALANCEYEAR';
COALESCE:                   'COALESCE';
COLUMNSTATISTICS:           'COLUMNSTATISTICS';
COMBIN:                     'COMBIN';
COMBINA:                    'COMBINA';
COMBINEVALUES:              'COMBINEVALUES';
CONCATENATE:                'CONCATENATE';
CONCATENATEX:               'CONCATENATEX';
CONFIDENCENORM:             'CONFIDENCE.NORM';
CONFIDENCET:                'CONFIDENCE.T';
CONTAINS:                   'CONTAINS';
CONTAINSROW:                'CONTAINSROW';
CONTAINSSTRING:             'CONTAINSSTRING';
CONTAINSSTRINGEXACT:        'CONTAINSSTRINGEXACT';
CONVERT:                    'CONVERT';
COS:                        'COS';
COSH:                       'COSH';
COT:                        'COT';
COTH:                       'COTH';
COUNT:                      'COUNT';
COUNTA:                     'COUNTA';
COUNTAX:                    'COUNTAX';
COUNTBLANK:                 'COUNTBLANK';
COUNTROWS:                  'COUNTROWS';
COUNTX:                     'COUNTX';
COUPDAYBS:                  'COUPDAYBS';
COUPDAYS:                   'COUPDAYS';
COUPDAYSNC:                 'COUPDAYSNC';
COUPNCD:                    'COUPNCD';
COUPNUM:                    'COUPNUM';
COUPPCD:                    'COUPPCD';
CROSSFILTER:                'CROSSFILTER';
CROSSJOIN:                  'CROSSJOIN';
CUMIPMT:                    'CUMIPMT';
CUMPRINC:                   'CUMPRINC';
CURRENCY:                   'CURRENCY';
CURRENTGROUP:               'CURRENTGROUP';
CUSTOMDATA:                 'CUSTOMDATA';

// --- D ---
DATATABLE:                  'DATATABLE';
DATE:                       'DATE';
DATEADD:                    'DATEADD';
DATEDIFF:                   'DATEDIFF';
DATESBETWEEN:               'DATESBETWEEN';
DATESINPERIOD:              'DATESINPERIOD';
DATESMTD:                   'DATESMTD';
DATESQTD:                   'DATESQTD';
DATESYTD:                   'DATESYTD';
DATEVALUE:                  'DATEVALUE';
DAY:                        'DAY';
DB:                         'DB';
DDB:                        'DDB';
DEGREES:                    'DEGREES';
DETAILROWS:                 'DETAILROWS';
DISC:                       'DISC';
DISTINCT:                   'DISTINCT';
DISTINCTCOUNT:              'DISTINCTCOUNT';
DISTINCTCOUNTNOBLANK:       'DISTINCTCOUNTNOBLANK';
DIVIDE:                     'DIVIDE';
DOLLARDE:                   'DOLLARDE';
DOLLARFR:                   'DOLLARFR';
DURATION:                   'DURATION';

// --- E ---
EARLIER:                    'EARLIER';
EARLIEST:                   'EARLIEST';
EDATE:                      'EDATE';
EFFECT:                     'EFFECT';
ENDOFMONTH:                 'ENDOFMONTH';
ENDOFQUARTER:               'ENDOFQUARTER';
ENDOFWEEK:                  'ENDOFWEEK';
ENDOFYEAR:                  'ENDOFYEAR';
EOMONTH:                    'EOMONTH';
ERROR:                      'ERROR';
EVALUATEANDLOG:             'EVALUATEANDLOG';
EVEN:                       'EVEN';
EXACT:                      'EXACT';
EXCEPT:                     'EXCEPT';
EXP:                        'EXP';
EXPONDIST:                  'EXPON.DIST';

// --- F ---
FACT:                       'FACT';
FALSE:                      'FALSE';
FILTER:                     'FILTER';
FILTERS:                    'FILTERS';
FIND:                       'FIND';
FIRST:                      'FIRST';
FIRSTDATE:                  'FIRSTDATE';
FIRSTNONBLANK:              'FIRSTNONBLANK';
FIRSTNONBLANKVALUE:         'FIRSTNONBLANKVALUE';
FIXED:                      'FIXED';
FLOOR:                      'FLOOR';
FORMAT:                     'FORMAT';
FV:                         'FV';

// --- G ---
GCD:                        'GCD';
GENERATE:                   'GENERATE';
GENERATEALL:                'GENERATEALL';
GENERATESERIES:             'GENERATESERIES';
GEOMEAN:                    'GEOMEAN';
GEOMEANX:                   'GEOMEANX';
GROUPBY:                    'GROUPBY';

// --- H ---
HASONEFILTER:               'HASONEFILTER';
HASONEVALUE:                'HASONEVALUE';
HOUR:                       'HOUR';

// --- I ---
IF:                         'IF';
IFEAGER:                    'IF.EAGER';
IFERROR:                    'IFERROR';
IGNORE:                     'IGNORE';
INDEX:                      'INDEX';
INFO_FUNCTIONS:             'INFO.FUNCTIONS';
INT:                        'INT';
INTRATE:                    'INTRATE';
INTERSECT:                  'INTERSECT';
IPMT:                       'IPMT';
ISBLANK:                    'ISBLANK';
ISBOOLEAN:                  'ISBOOLEAN';
ISCROSSFILTERED:            'ISCROSSFILTERED';
ISCURRENCY:                 'ISCURRENCY';
ISDATETIME:                 'ISDATETIME';
ISDECIMAL:                  'ISDECIMAL';
ISDOUBLE:                   'ISDOUBLE';
ISEMPTY:                    'ISEMPTY';
ISERROR:                    'ISERROR';
ISEVEN:                     'ISEVEN';
ISFILTERED:                 'ISFILTERED';
ISINT64:                    'ISINT64';
ISINTEGER:                  'ISINTEGER';
ISINSCOPE:                  'ISINSCOPE';
ISLOGICAL:                  'ISLOGICAL';
ISNONTEXT:                  'ISNONTEXT';
ISNUMBER:                   'ISNUMBER';
ISNUMERIC:                  'ISNUMERIC';
ISOCEILING:                 'ISO.CEILING';
ISODD:                      'ISODD';
ISONORAFTER:                'ISONORAFTER';
ISPMT:                      'ISPMT';
ISSELECTEDMEASURE:          'ISSELECTEDMEASURE';
ISSTRING:                   'ISSTRING';
ISSUBTOTAL:                 'ISSUBTOTAL';
ISTEXT:                     'ISTEXT';

// --- K ---
KEEPFILTERS:                'KEEPFILTERS';
KEYWORDMATCH:               'KEYWORDMATCH';

// --- L ---
LAST:                       'LAST';
LASTDATE:                   'LASTDATE';
LASTNONBLANK:               'LASTNONBLANK';
LASTNONBLANKVALUE:          'LASTNONBLANKVALUE';
LCM:                        'LCM';
LEFT:                       'LEFT';
LEN:                        'LEN';
LINEST:                     'LINEST';
LINESTX:                    'LINESTX';
LN:                         'LN';
LOG:                        'LOG';
LOG10:                      'LOG10';
LOOKUP:                     'LOOKUP';
LOOKUPWITHTOTALS:           'LOOKUPWITHTOTALS';
LOOKUPVALUE:                'LOOKUPVALUE';
LOWER:                      'LOWER';

// --- M ---
MATCHBY:                    'MATCHBY';
MAX:                        'MAX';
MAXA:                       'MAXA';
MAXX:                       'MAXX';
MDURATION:                  'MDURATION';
MEDIAN:                     'MEDIAN';
MEDIANX:                    'MEDIANX';
MID:                        'MID';
MIN:                        'MIN';
MINA:                       'MINA';
MINUTE:                     'MINUTE';
MINX:                       'MINX';
MOD:                        'MOD';
MONTH:                      'MONTH';
MROUND:                     'MROUND';

// --- N ---
NATURALINNERJOIN:           'NATURALINNERJOIN';
NATURALLEFTOUTERJOIN:       'NATURALLEFTOUTERJOIN';
NEXT:                       'NEXT';
NEXTDAY:                    'NEXTDAY';
NEXTMONTH:                  'NEXTMONTH';
NEXTQUARTER:                'NEXTQUARTER';
NEXTWEEK:                   'NEXTWEEK';
NEXTYEAR:                   'NEXTYEAR';
NOMINAL:                    'NOMINAL';
NONVISUAL:                  'NONVISUAL';
NORMDIST:                   'NORM.DIST';
NORMINV:                    'NORM.INV';
NORMSDIST:                  'NORM.S.DIST';
NORMSINV:                   'NORM.S.INV';
NOT:                        'NOT';
NOW:                        'NOW';
NPER:                       'NPER';

// --- O ---
ODD:                        'ODD';
ODDFPRICE:                  'ODDFPRICE';
ODDFYIELD:                  'ODDFYIELD';
ODDLPRICE:                  'ODDLPRICE';
ODDLYIELD:                  'ODDLYIELD';
OFFSET:                     'OFFSET';
OPENINGBALANCEMONTH:        'OPENINGBALANCEMONTH';
OPENINGBALANCEQUARTER:      'OPENINGBALANCEQUARTER';
OPENINGBALANCEWEEK:         'OPENINGBALANCEWEEK';
OPENINGBALANCEYEAR:         'OPENINGBALANCEYEAR';
OR:                         'OR';
ORDERBY:                    'ORDERBY';

// --- P ---
PARALLELPERIOD:             'PARALLELPERIOD';
PARTITIONBY:                'PARTITIONBY';
PATH:                       'PATH';
PATHCONTAINS:               'PATHCONTAINS';
PATHITEM:                   'PATHITEM';
PATHITEMREVERSE:            'PATHITEMREVERSE';
PATHLENGTH:                 'PATHLENGTH';
PDURATION:                  'PDURATION';
PERCENTILEEXC:              'PERCENTILE.EXC';
PERCENTILEINC:              'PERCENTILE.INC';
PERCENTILEXEXC:             'PERCENTILEX.EXC';
PERCENTILEXINC:             'PERCENTILEX.INC';
PERMUT:                     'PERMUT';
PI:                         'PI';
PMT:                        'PMT';
POISSONDIST:                'POISSON.DIST';
POWER:                      'POWER';
PPMT:                       'PPMT';
PREVIOUS:                   'PREVIOUS';
PREVIOUSDAY:                'PREVIOUSDAY';
PREVIOUSMONTH:              'PREVIOUSMONTH';
PREVIOUSQUARTER:            'PREVIOUSQUARTER';
PREVIOUSWEEK:               'PREVIOUSWEEK';
PREVIOUSYEAR:               'PREVIOUSYEAR';
PRICE:                      'PRICE';
PRICEDISC:                  'PRICEDISC';
PRICEMAT:                   'PRICEMAT';
PRODUCT:                    'PRODUCT';
PRODUCTX:                   'PRODUCTX';
PV:                         'PV';

// --- Q ---
QUARTER:                    'QUARTER';
QUOTIENT:                   'QUOTIENT';

// --- R ---
RADIANS:                    'RADIANS';
RAND:                       'RAND';
RANDBETWEEN:                'RANDBETWEEN';
RANK:                       'RANK';
RANKEQ:                     'RANK.EQ';
RANKX:                      'RANKX';
RATE:                       'RATE';
RECEIVED:                   'RECEIVED';
RELATED:                    'RELATED';
RELATEDTABLE:               'RELATEDTABLE';
REMOVEFILTERS:              'REMOVEFILTERS';
REPLACE:                    'REPLACE';
REPT:                       'REPT';
RIGHT:                      'RIGHT';
ROLLUP:                     'ROLLUP';
ROLLUPADDISSUBTOTAL:        'ROLLUPADDISSUBTOTAL';
ROLLUPGROUP:                'ROLLUPGROUP';
ROLLUPISSUBTOTAL:           'ROLLUPISSUBTOTAL';
ROUND:                      'ROUND';
ROUNDDOWN:                  'ROUNDDOWN';
ROUNDUP:                    'ROUNDUP';
ROW:                        'ROW';
ROWNUMBER:                  'ROWNUMBER';
RRI:                        'RRI';

// --- S ---
SAMEPERIODLASTYEAR:         'SAMEPERIODLASTYEAR';
SAMPLE:                     'SAMPLE';
SEARCH:                     'SEARCH';
SECOND:                     'SECOND';
SELECTCOLUMNS:              'SELECTCOLUMNS';
SELECTEDMEASURE:            'SELECTEDMEASURE';
SELECTEDMEASUREFORMATSTRING: 'SELECTEDMEASUREFORMATSTRING';
SELECTEDMEASURENAME:        'SELECTEDMEASURENAME';
SELECTEDVALUE:              'SELECTEDVALUE';
SIGN:                       'SIGN';
SIN:                        'SIN';
SINH:                       'SINH';
SLN:                        'SLN';
SQRT:                       'SQRT';
SQRTPI:                     'SQRTPI';
STARTOFMONTH:               'STARTOFMONTH';
STARTOFQUARTER:             'STARTOFQUARTER';
STARTOFWEEK:                'STARTOFWEEK';
STARTOFYEAR:                'STARTOFYEAR';
STDEVP:                     'STDEV.P';
STDEVS:                     'STDEV.S';
STDEVXP:                    'STDEVX.P';
STDEVXS:                    'STDEVX.S';
SUBSTITUTE:                 'SUBSTITUTE';
SUBSTITUTEWITHINDEX:        'SUBSTITUTEWITHINDEX';
SUM:                        'SUM';
SUMMARIZE:                  'SUMMARIZE';
SUMMARIZECOLUMNS:           'SUMMARIZECOLUMNS';
SUMX:                       'SUMX';
SWITCH:                     'SWITCH';
SYD:                        'SYD';

// --- T ---
TABLEOF:                    'TABLEOF';
TAN:                        'TAN';
TANH:                       'TANH';
TBILLEQ:                    'TBILLEQ';
TBILLPRICE:                 'TBILLPRICE';
TBILLYIELD:                 'TBILLYIELD';
TDIST:                      'T.DIST';
TDIST2T:                    'T.DIST.2T';
TDISTRT:                    'T.DIST.RT';
TIME:                       'TIME';
TIMEVALUE:                  'TIMEVALUE';
TINV:                       'T.INV';
TINV2T:                     'T.INV.2T';
TOCSV:                      'TOCSV';
TODAY:                      'TODAY';
TOJSON:                     'TOJSON';
TOPN:                       'TOPN';
TOPNPERLEVEL:               'TOPNPERLEVEL';
TOPNSKIP:                   'TOPNSKIP';
TOTALMTD:                   'TOTALMTD';
TOTALQTD:                   'TOTALQTD';
TOTALWTD:                   'TOTALWTD';
TOTALYTD:                   'TOTALYTD';
TREATAS:                    'TREATAS';
TRIM:                       'TRIM';
TRUE:                       'TRUE';
TRUNC:                      'TRUNC';

// --- U ---
UNICHAR:                    'UNICHAR';
UNICODE:                    'UNICODE';
UNION:                      'UNION';
UPPER:                      'UPPER';
USERELATIONSHIP:            'USERELATIONSHIP';
USERNAME:                   'USERNAME';
USEROBJECTID:               'USEROBJECTID';
USERPRINCIPALNAME:          'USERPRINCIPALNAME';
UTCNOW:                     'UTCNOW';
UTCTODAY:                   'UTCTODAY';

// --- V ---
VALUE:                      'VALUE';
VALUES:                     'VALUES';
VARP:                       'VAR.P';
VARS:                       'VAR.S';
VARXP:                      'VARX.P';
VARXS:                      'VARX.S';
VDB:                        'VDB';

// --- W ---
WEEKDAY:                    'WEEKDAY';
WEEKNUM:                    'WEEKNUM';
WINDOW:                     'WINDOW';

// --- X ---
XIRR:                       'XIRR';
XNPV:                       'XNPV';

// --- Y ---
YEAR:                       'YEAR';
YEARFRAC:                   'YEARFRAC';
YIELD:                      'YIELD';
YIELDDISC:                  'YIELDDISC';
YIELDMAT:                   'YIELDMAT';

// ===================================================================
// C. Statement keywords (must come AFTER function tokens)
// ===================================================================
DEFINE:                     'DEFINE';
EVALUATE:                   'EVALUATE';
FUNCTION_KW:                'FUNCTION';
ORDER:                      'ORDER';
BY:                         'BY';
START:                      'START';
AT:                         'AT';
MEASURE:                    'MEASURE';
RETURN:                     'RETURN';
VAR:                        'VAR';
IN:                         'IN';
ASC:                        'ASC';
DESC:                       'DESC';
SKIP_:                      'SKIP';
DENSE:                      'DENSE';
TABLE_KW:                   'TABLE';
COLUMN_KW:                  'COLUMN';

// ===================================================================
// D. UDF type keywords
// ===================================================================
ANYVAL:                     'ANYVAL';
SCALAR:                     'SCALAR';
ANYREF:                     'ANYREF';
VARIANT:                    'VARIANT';
INT64:                      'INT64';
DECIMAL_KW:                 'DECIMAL';
NUMERIC_KW:                 'NUMERIC';
VAL_KW:                     'VAL';
EXPR_KW:                    'EXPR';

// ===================================================================
// E. Enum keywords
// ===================================================================
WEEK:                       'WEEK';
BOTH:                       'BOTH';
NONE:                       'NONE';
ONEWAY:                     'ONEWAY';
REL:                        'REL';
INTEGER_KW:                 'INTEGER';
DOUBLE_KW:                  'DOUBLE';
STRING_KW:                  'STRING';
BOOLEAN_KW:                 'BOOLEAN';
DATETIME_KW:                'DATETIME';

// ===================================================================
// F. Multi-char operators (must come before single-char versions)
// ===================================================================
STRICT_EQUALS:              '==';
LAMBDA_ARROW:               '=>';
OP_AND:                     '&&';
OP_OR:                      '||';
OP_NE:                      '<>';
OP_LE:                      '<=';
OP_GE:                      '>=';

// ===================================================================
// G. Single-char operators and delimiters
// ===================================================================
OPEN_CURLY:                 '{';
CLOSE_CURLY:                '}';
OPEN_PARENS:                '(';
CLOSE_PARENS:               ')';
COMMA:                      ',';
PLUS:                       '+';
MINUS:                      '-';
STAR:                       '*';
DIV:                        '/';
CARET:                      '^';
AMP:                        '&';
EQUALS:                     '=';
LT:                         '<';
GT:                         '>';
COLON:                      ':';
DOT:                        '.';

// ===================================================================
// H. Literals
// ===================================================================
INTEGER_LITERAL:            [0-9]+;
REAL_LITERAL:               [0-9]* '.' [0-9]+;
STRING_LITERAL:             '"' (~'"' | '""')* '"' {Text = Text.Substring(1, Text.Length - 2);};
DATE_LITERAL:               'DT' STRING_LITERAL;

// ===================================================================
// I. References
// ===================================================================
TABLE_REF:                  '\'' (~["'\r\n\u0085\u2028\u2029] | '\'\'')* '\'' {Text = Text.Substring(1, Text.Length - 2).Replace("''","'");};
COLUMN_OR_MEASURE:          '[' (~["\]\r\n\u0085\u2028\u2029] | ']]')* ']' {Text = Text.Substring(1, Text.Length - 2).Replace("]]","]");};
PARTIAL_TABLE:              '\'' (~["'\r\n\u0085\u2028\u2029] | '\'\'')*;
PARTIAL_COLUMN_OR_MEASURE:  '[' (~["\]\r\n\u0085\u2028\u2029] | ']]')*;
PARAMETER:                  '@' IdentifierOrKeyword;

// ===================================================================
// J. Identifiers (catch-all, must be last)
// ===================================================================
IDENTIFIER:                 IdentifierOrKeyword;

// ===================================================================
// K. Fragments
// ===================================================================
fragment InputCharacter
    : ~[\r\n\u0085\u2028\u2029]
    ;

fragment NewLine
    : '\r\n' | '\r' | '\n'
    | '\u0085' // NEXT_LINE
    | '\u2028' // LINE_SEPARATOR
    | '\u2029' // PARAGRAPH_SEPARATOR
    ;

fragment Whitespace
    : ' '
    | '\t'
    | '\u000B' // VERTICAL TAB
    | '\u000C' // FORM FEED
    | '\u00A0' // NO_BREAK SPACE
    | '\u1680' // OGHAM SPACE MARK
    | '\u2000' // EN QUAD
    | '\u2001' // EM QUAD
    | '\u2002' // EN SPACE
    | '\u2003' // EM SPACE
    | '\u2004' // THREE_PER_EM SPACE
    | '\u2005' // FOUR_PER_EM SPACE
    | '\u2006' // SIX_PER_EM SPACE
    | '\u2008' // PUNCTUATION SPACE
    | '\u2009' // THIN SPACE
    | '\u200A' // HAIR SPACE
    | '\u202F' // NARROW NO_BREAK SPACE
    | '\u3000' // IDEOGRAPHIC SPACE
    | '\u205F' // MEDIUM MATHEMATICAL SPACE
    ;

fragment IdentifierOrKeyword
    : IdentifierStartCharacter IdentifierPartCharacter*
    ;

fragment IdentifierStartCharacter
    : UnicodeClassLU
    | UnicodeClassLL
    | '_'
    ;

fragment IdentifierPartCharacter
    : IdentifierStartCharacter
    | UnicodeClassND
    ;

fragment UnicodeClassLU
    : '\u0041'..'\u005A'
    ;

fragment UnicodeClassLL
    : '\u0061'..'\u007A'
    ;

fragment UnicodeClassND
    : '\u0030'..'\u0039'
    ;


