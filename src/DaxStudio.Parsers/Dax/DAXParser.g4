parser grammar DAXParser;

options { tokenVocab=DAXLexer; }

// ===================================================================
// Top-level query structure
// ===================================================================

daxQuery
    : defineBlock? evaluateBlock+ EOF
    ;

defineBlock
    : DEFINE definition+
    ;

definition
    : measureDefinition
    | variableDefinition
    | tableDefinition
    | columnDefinition
    | functionDefinition
    ;

measureDefinition
    : MEASURE tableRef COLUMN_OR_MEASURE EQUALS expression
    ;

variableDefinition
    : VAR identifierOrKeyword EQUALS expression
    ;

tableDefinition
    : TABLE_KW identifierOrKeyword EQUALS expression
    ;

columnDefinition
    : COLUMN_KW tableRef COLUMN_OR_MEASURE EQUALS expression
    ;

functionDefinition
    : FUNCTION_KW functionName EQUALS OPEN_PARENS parameterDefList? CLOSE_PARENS LAMBDA_ARROW expression
    ;

parameterDefList
    : parameterDef (COMMA parameterDef)*
    ;

parameterDef
    : identifierOrKeyword (COLON typeAnnotation)?
    ;

typeAnnotation
    : typeCategory typeSubtype? parameterMode?
    ;

typeCategory
    : ANYVAL
    | SCALAR
    | TABLE_KW
    | ANYREF
    ;

typeSubtype
    : VARIANT
    | INT64
    | DECIMAL_KW
    | DOUBLE_KW
    | STRING_KW
    | BOOLEAN_KW
    | DATETIME_KW
    | NUMERIC_KW
    | CURRENCY
    ;

parameterMode
    : VAL_KW
    | EXPR_KW
    ;

// ===================================================================
// EVALUATE block
// ===================================================================

evaluateBlock
    : EVALUATE expression orderByClause? startAtClause?
    ;

orderByClause
    : ORDER BY orderByColumn (COMMA orderByColumn)*
    ;

orderByColumn
    : expression (ASC | DESC)? (SKIP_ | DENSE)?
    ;

startAtClause
    : START AT startAtValue (COMMA startAtValue)*
    ;

startAtValue
    : literal
    | PARAMETER
    ;

// ===================================================================
// Expressions (precedence from lowest to highest)
// ===================================================================

expression
    : OPEN_PARENS expression (COMMA expression)* CLOSE_PARENS                        #parenExpr
    | expression OP_OR expression                                                       #orExpr
    | expression OP_AND expression                                                      #andExpr
    | NOT expression                                                                    #notExpr
    | expression IN tableConstructor                                                    #inExpr
    | expression op=(EQUALS | STRICT_EQUALS | OP_NE | LT | GT | OP_LE | OP_GE) expression  #comparisonExpr
    | expression AMP expression                                                         #concatExpr
    | expression op=(PLUS | MINUS) expression                                           #addSubExpr
    | expression op=(STAR | DIV) expression                                             #mulDivExpr
    | <assoc=right> expression CARET expression                                         #powerExpr
    | (PLUS | MINUS) expression                                                         #unaryExpr
    | functionCall                                                                      #functionCallExpr
    | builtInFunction                                                                   #builtInFunctionRefExpr
    | varReturnExpr                                                                     #varReturnExpression
    | tableConstructor                                                                  #tableConstructorExpr
    | columnRef                                                                         #columnRefExpr
    | tableRef                                                                          #tableRefExpr
    | keyword                                                                           #keywordExpr
    | functionName                                                                      #identifierExpr
    | PARAMETER                                                                         #parameterExpr
    | literal                                                                           #literalExpr
    ;

// ===================================================================
// VAR / RETURN
// ===================================================================

varReturnExpr
    : variableDefinition+ RETURN expression
    ;

// ===================================================================
// Function calls
// ===================================================================

functionCall
    : functionCallName OPEN_PARENS argumentList? CLOSE_PARENS
    ;

argumentList
    : argument (COMMA argument)*
    ;

argument
    : expression
    |   // empty argument (trailing comma or placeholder)
    ;

functionCallName
    : builtInFunction
    | dottedIdentifier
    | identifierOrKeyword
    ;

functionName
    : dottedIdentifier
    | identifierOrKeyword
    ;

dottedIdentifier
    : identifierOrKeyword DOT identifierOrKeyword (DOT identifierOrKeyword)*
    ;

// Allows function names and non-structural keywords to be used as identifiers.
// DAX prohibits structural keywords (VAR, RETURN, DEFINE, EVALUATE, MEASURE,
// ORDER, BY, START, AT, TABLE, COLUMN, FUNCTION, IN) as variable/measure names.
// Type keywords and enum keywords used in DATATABLE/WINDOW/UDF syntax ARE allowed.
identifierOrKeyword
    : IDENTIFIER
    | builtInFunction
    | ASC | DESC | SKIP_ | DENSE
    | WEEK | BOTH | NONE | ONEWAY | REL
    | INTEGER_KW | DOUBLE_KW | STRING_KW | BOOLEAN_KW | DATETIME_KW
    | ANYVAL | SCALAR | ANYREF | VARIANT | INT64 | DECIMAL_KW | NUMERIC_KW | VAL_KW | EXPR_KW
    ;

// ===================================================================
// References
// ===================================================================

columnRef
    : tableRef COLUMN_OR_MEASURE    // 'Table'[Column]
    | identifierOrKeyword COLUMN_OR_MEASURE  // Table[Column]  (unquoted table)
    | builtInFunction COLUMN_OR_MEASURE      // e.g., for tables named like functions
    | COLUMN_OR_MEASURE             // [Column]
    ;

tableRef
    : TABLE_REF                     // 'Table Name'
    ;

// ===================================================================
// Table and row constructors
// ===================================================================

tableConstructor
    : OPEN_CURLY rowConstructorList? CLOSE_CURLY
    ;

rowConstructorList
    : rowConstructor (COMMA rowConstructor)*
    | expression (COMMA expression)*
    ;

rowConstructor
    : OPEN_PARENS expression (COMMA expression)* CLOSE_PARENS
    ;

// ===================================================================
// Literals
// ===================================================================

literal
    : INTEGER_LITERAL
    | REAL_LITERAL
    | STRING_LITERAL
    | DATE_LITERAL
    | TRUE
    | FALSE
    ;

// ===================================================================
// Keyword arguments for specific functions
// ===================================================================

keyword
    : ASC | DESC
    | SKIP_ | DENSE
    | BOTH | NONE | ONEWAY | REL
    | WEEK
    | YEAR | QUARTER | MONTH | DAY
    | INTEGER_KW | DOUBLE_KW | STRING_KW | BOOLEAN_KW | DATETIME_KW
    | CURRENCY
    ;

// ===================================================================
// Built-in function names (all tokens that can appear as function names)
// ===================================================================

builtInFunction
    : ABS | ACCRINT | ACCRINTM | ACOS | ACOSH | ACOT | ACOTH
    | ADDCOLUMNS | ADDMISSINGITEMS | ALL | ALLCROSSFILTERED | ALLEXCEPT
    | ALLNOBLANKROW | ALLSELECTED | AMORDEGRC | AMORLINC | AND
    | APPROXIMATEDISTINCTCOUNT | ASIN | ASINH | ATAN | ATANH
    | AVERAGE | AVERAGEA | AVERAGEX
    | BETADIST | BETAINV | BLANK
    | CALCULATE | CALCULATETABLE | CALENDAR | CALENDARAUTO | CEILING
    | CHISQDIST | CHISQDISTRT | CHISQINV | CHISQINVRT
    | CLOSINGBALANCEMONTH | CLOSINGBALANCEQUARTER | CLOSINGBALANCEYEAR | CLOSINGBALANCEWEEK
    | COALESCE | COLUMNSTATISTICS | COMBIN | COMBINA | COMBINEVALUES
    | CONCATENATE | CONCATENATEX
    | CONFIDENCENORM | CONFIDENCET | CONTAINS | CONTAINSROW
    | CONTAINSSTRING | CONTAINSSTRINGEXACT | CONVERT | COS | COSH | COT | COTH
    | COUNT | COUNTA | COUNTAX | COUNTBLANK | COUNTROWS | COUNTX
    | CROSSFILTER | CROSSJOIN | CUMIPMT | CUMPRINC | CURRENCY | CURRENTGROUP | CUSTOMDATA
    | DATATABLE | DATE | DATEADD | DATEDIFF | DATESBETWEEN | DATESINPERIOD
    | DATESMTD | DATESQTD | DATESYTD | DATEVALUE | DAY | DB | DDB | DEGREES
    | DETAILROWS | DISC | DISTINCT | DISTINCTCOUNT | DISTINCTCOUNTNOBLANK
    | DIVIDE | DOLLARDE | DOLLARFR | DURATION
    | EARLIER | EARLIEST | EDATE | EFFECT | ENDOFMONTH | ENDOFQUARTER
    | ENDOFYEAR | ENDOFWEEK | EOMONTH | ERROR | EVALUATEANDLOG | EVEN
    | EXACT | EXCEPT | EXP | EXPONDIST
    | FACT | FALSE | FILTER | FILTERS | FIND | FIRST | FIRSTDATE
    | FIRSTNONBLANK | FIRSTNONBLANKVALUE | FIXED | FLOOR | FORMAT | FV
    | GCD | GENERATE | GENERATEALL | GENERATESERIES | GEOMEAN | GEOMEANX | GROUPBY
    | HASONEFILTER | HASONEVALUE | HOUR
    | IF | IFEAGER | IFERROR | IGNORE | INDEX | INT | INTERSECT | INTRATE | IPMT | ISPMT
    | ISBLANK | ISBOOLEAN | ISCROSSFILTERED | ISCURRENCY | ISDATETIME | ISDECIMAL
    | ISDOUBLE | ISEMPTY | ISERROR | ISEVEN | ISFILTERED | ISINT64 | ISINTEGER
    | ISINSCOPE | ISLOGICAL | ISNONTEXT | ISNUMBER | ISNUMERIC
    | ISOCEILING | ISODD | ISONORAFTER | ISSELECTEDMEASURE | ISSTRING | ISSUBTOTAL | ISTEXT
    | KEEPFILTERS | KEYWORDMATCH
    | LAST | LASTDATE | LASTNONBLANK | LASTNONBLANKVALUE | LCM | LEFT | LEN
    | LINEST | LINESTX | LN | LOG | LOG10 | LOOKUP | LOOKUPWITHTOTALS | LOOKUPVALUE | LOWER
    | MATCHBY | MAX | MAXA | MAXX | MDURATION | MEDIAN | MEDIANX
    | MID | MIN | MINA | MINUTE | MINX | MOD | MONTH | MROUND
    | NATURALINNERJOIN | NATURALLEFTOUTERJOIN | NEXT | NEXTDAY | NEXTMONTH
    | NEXTQUARTER | NEXTWEEK | NEXTYEAR | NOMINAL | NONVISUAL | NORMDIST
    | NORMINV | NORMSDIST | NORMSINV | NOT | NOW | NPER
    | ODD | ODDFPRICE | ODDFYIELD | ODDLPRICE | ODDLYIELD | OFFSET
    | OPENINGBALANCEMONTH | OPENINGBALANCEQUARTER | OPENINGBALANCEYEAR | OPENINGBALANCEWEEK
    | OR | ORDERBY
    | PARALLELPERIOD | PARTITIONBY | PATH | PATHCONTAINS | PATHITEM
    | PATHITEMREVERSE | PATHLENGTH | PDURATION
    | PERCENTILEEXC | PERCENTILEINC | PERCENTILEXEXC | PERCENTILEXINC
    | PERMUT | PI | PMT | POISSONDIST | POWER | PPMT | PRICE | PRICEDISC | PRICEMAT
    | PREVIOUS | PREVIOUSDAY | PREVIOUSMONTH | PREVIOUSQUARTER | PREVIOUSWEEK | PREVIOUSYEAR
    | PRODUCT | PRODUCTX | PV
    | QUARTER | QUOTIENT
    | RADIANS | RAND | RANDBETWEEN | RANK | RANKEQ | RANKX | RATE
    | RECEIVED | RELATED | RELATEDTABLE | REMOVEFILTERS | REPLACE | REPT
    | RIGHT | ROLLUP | ROLLUPADDISSUBTOTAL | ROLLUPGROUP | ROLLUPISSUBTOTAL
    | ROUND | ROUNDDOWN | ROUNDUP | ROW | ROWNUMBER | RRI
    | SAMEPERIODLASTYEAR | SAMPLE | SEARCH | SECOND | SELECTCOLUMNS
    | SELECTEDMEASURE | SELECTEDMEASUREFORMATSTRING | SELECTEDMEASURENAME | SELECTEDVALUE
    | SIGN | SIN | SINH | SLN | SQRT | SQRTPI
    | STARTOFMONTH | STARTOFQUARTER | STARTOFYEAR | STARTOFWEEK
    | STDEVP | STDEVS | STDEVXP | STDEVXS
    | SUBSTITUTE | SUBSTITUTEWITHINDEX | SUM | SUMMARIZE | SUMMARIZECOLUMNS | SUMX
    | SWITCH | SYD
    | TABLEOF | TAN | TANH | TBILLEQ | TBILLPRICE | TBILLYIELD
    | TDIST | TDIST2T | TDISTRT | TIME | TIMEVALUE | TINV | TINV2T
    | TOCSV | TODAY | TOJSON | TOPN | TOPNPERLEVEL | TOPNSKIP
    | TOTALMTD | TOTALQTD | TOTALWTD | TOTALYTD
    | TREATAS | TRIM | TRUE | TRUNC
    | UNICHAR | UNICODE | UNION | UPPER | USERELATIONSHIP
    | USERNAME | USEROBJECTID | USERPRINCIPALNAME | UTCNOW | UTCTODAY
    | VALUE | VALUES | VARP | VARS | VARXP | VARXS | VDB
    | WEEKDAY | WEEKNUM | WINDOW
    | XIRR | XNPV
    | YEAR | YEARFRAC | YIELD | YIELDDISC | YIELDMAT
    | INFO_FUNCTIONS
    ;
