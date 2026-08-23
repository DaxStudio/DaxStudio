// DirectQuery SQL Grammar for ANTLR4
// Parses the T-SQL subset used in DirectQuery events from Analysis Services
//
// DirectQuery SQL has multiple formats:
// Format 1 - Subselect with [$Table]:
//   (select [$Table].[col] as [col] from [dbo].[TableName] as [$Table]) AS [t4]
// Format 2 - Direct table reference:
//   [dbo].[TableName] AS [t8]
// Format 3 - Bare table in JOIN:
//   INNER JOIN [dbo].[TableName] AS [t7] ON (...)

grammar DirectQuerySql;

// ==================== PARSER RULES ====================

query
    : selectStatement EOF
    ;

selectStatement
    : SELECT selectList
      fromClause
      joinClause*
      whereClause?
      groupByClause?
      havingClause?
      orderByClause?
    ;

selectList
    : selectItem (COMMA selectItem)*
    ;

selectItem
    : expression (AS bracketedName)?
    ;

fromClause
    : FROM tableSource
    ;

tableSource
    : subselectBlock (COMMA subselectBlock)*
    | tableReference (COMMA tableReference)*
    ;

// (select [$Table].[col] as [col], ... from [schema].[table] as [$Table]) AS [alias]
subselectBlock
    : LPAREN LPAREN? SELECT subselectColumnList FROM schemaTable AS DOLLAR_TABLE RPAREN? RPAREN AS bracketedName
    | tableReference
    ;

subselectColumnList
    : subselectColumn (COMMA subselectColumn)*
    ;

subselectColumn
    : DOLLAR_TABLE DOT bracketedName AS bracketedName
    ;

// [schema].[table] AS [alias]  or  ([schema].[table]) AS [alias]
tableReference
    : LPAREN? schemaTable RPAREN? (AS bracketedName)?
    ;

// [schema].[tableName]
schemaTable
    : bracketedName DOT bracketedName
    ;

joinClause
    : joinType tableJoinSource onJoinClause?
    ;

joinType
    : INNER JOIN
    | LEFT (OUTER)? JOIN
    | RIGHT (OUTER)? JOIN
    | FULL (OUTER)? JOIN
    | CROSS JOIN
    ;

tableJoinSource
    : subselectBlock
    | tableReference
    ;

onJoinClause
    : ON LPAREN? joinCondition (AND joinCondition)* RPAREN?
    ;

joinCondition
    : expression EQUALS expression
    ;

whereClause
    : WHERE expression
    ;

groupByClause
    : GROUP BY expressionList
    ;

havingClause
    : HAVING expression
    ;

orderByClause
    : ORDER BY orderByItem (COMMA orderByItem)*
    ;

orderByItem
    : expression (ASC | DESC)?
    ;

// Expressions
expression
    : unaryExpression
    | expression (STAR | SLASH | PERCENT) expression
    | expression (PLUS | MINUS) expression
    | expression comparisonOp expression
    | expression AND expression
    | expression OR expression
    | expression IS NOT? NULL_
    | LPAREN expression RPAREN
    | NOT expression
    ;

unaryExpression
    : functionCall
    | qualifiedColumnRef
    | bracketedName
    | literal
    | STAR
    | LPAREN selectStatement RPAREN     // subquery
    ;

functionCall
    : functionName LPAREN (DISTINCT? expressionList | STAR)? RPAREN
    ;

functionName
    : IDENTIFIER
    | SUM | COUNT | MIN | MAX | AVG
    | COUNT_BIG
    | ISNULL
    | CAST
    | CONVERT
    | COALESCE
    ;

qualifiedColumnRef
    : bracketedName DOT bracketedName
    ;

comparisonOp
    : EQUALS | NOT_EQUALS | GREATER_THAN | LESS_THAN | GREATER_EQUALS | LESS_EQUALS
    ;

expressionList
    : expression (COMMA expression)*
    ;

literal
    : NUMBER
    | STRING_LITERAL
    | NULL_
    ;

bracketedName
    : BRACKETED_NAME
    ;


// ==================== LEXER RULES ====================

// Keywords (case-insensitive)
SELECT      : [sS][eE][lL][eE][cC][tT] ;
FROM        : [fF][rR][oO][mM] ;
WHERE       : [wW][hH][eE][rR][eE] ;
AS          : [aA][sS] ;
ON          : [oO][nN] ;
AND         : [aA][nN][dD] ;
OR          : [oO][rR] ;
NOT         : [nN][oO][tT] ;
IN          : [iI][nN] ;
IS          : [iI][sS] ;
NULL_       : [nN][uU][lL][lL] ;
INNER       : [iI][nN][nN][eE][rR] ;
LEFT        : [lL][eE][fF][tT] ;
RIGHT       : [rR][iI][gG][hH][tT] ;
FULL        : [fF][uU][lL][lL] ;
OUTER       : [oO][uU][tT][eE][rR] ;
CROSS       : [cC][rR][oO][sS][sS] ;
JOIN        : [jJ][oO][iI][nN] ;
GROUP       : [gG][rR][oO][uU][pP] ;
BY          : [bB][yY] ;
HAVING      : [hH][aA][vV][iI][nN][gG] ;
ORDER       : [oO][rR][dD][eE][rR] ;
ASC         : [aA][sS][cC] ;
DESC        : [dD][eE][sS][cC] ;
DISTINCT    : [dD][iI][sS][tT][iI][nN][cC][tT] ;
BETWEEN     : [bB][eE][tT][wW][eE][eE][nN] ;
LIKE        : [lL][iI][kK][eE] ;
CASE        : [cC][aA][sS][eE] ;
WHEN        : [wW][hH][eE][nN] ;
THEN        : [tT][hH][eE][nN] ;
ELSE        : [eE][lL][sS][eE] ;
END         : [eE][nN][dD] ;
TOP         : [tT][oO][pP] ;
UNION       : [uU][nN][iI][oO][nN] ;
ALL         : [aA][lL][lL] ;

// Aggregate functions
SUM         : [sS][uU][mM] ;
COUNT       : [cC][oO][uU][nN][tT] ;
MIN         : [mM][iI][nN] ;
MAX         : [mM][aA][xX] ;
AVG         : [aA][vV][gG] ;
COUNT_BIG   : [cC][oO][uU][nN][tT] '_' [bB][iI][gG] ;

// Other functions
ISNULL      : [iI][sS][nN][uU][lL][lL] ;
CAST        : [cC][aA][sS][tT] ;
CONVERT     : [cC][oO][nN][vV][eE][rR][tT] ;
COALESCE    : [cC][oO][aA][lL][eE][sS][cC][eE] ;

// [$Table] reference in subselects
DOLLAR_TABLE : '[$Table]' ;

// Bracketed names: [dbo], [TableName], [ColumnName], [t4], etc.
BRACKETED_NAME
    : '[' ~[\]\r\n]+ ']'
    ;

// String literal
STRING_LITERAL
    : '\'' (~['\r\n] | '\'\'')* '\''
    ;

// Number
NUMBER
    : '-'? [0-9]+ ('.' [0-9]+)? ([eE] [+-]? [0-9]+)?
    ;

// Identifiers
IDENTIFIER
    : [a-zA-Z_@#] [a-zA-Z0-9_@#$]*
    ;

// Operators
EQUALS          : '=' ;
NOT_EQUALS      : '<>' | '!=' ;
GREATER_THAN    : '>' ;
LESS_THAN       : '<' ;
GREATER_EQUALS  : '>=' ;
LESS_EQUALS     : '<=' ;

// Punctuation
LPAREN      : '(' ;
RPAREN      : ')' ;
COMMA       : ',' ;
DOT         : '.' ;
SEMICOLON   : ';' ;
PLUS        : '+' ;
MINUS       : '-' ;
STAR        : '*' ;
SLASH       : '/' ;
PERCENT     : '%' ;

// Whitespace
WS          : [ \t\r\n]+ -> skip ;

// Line comments
LINE_COMMENT
    : '--' ~[\r\n]* -> skip
    ;

// Block comments
BLOCK_COMMENT
    : '/*' .*? '*/' -> skip
    ;
