// xmSQL Grammar for ANTLR4
// Parses VertiPaq Storage Engine xmSQL queries emitted by Analysis Services
// 
// xmSQL is not a formally documented language. This grammar was reverse-engineered
// from real-world Server Timing events and test cases.

grammar xmSQL;

// ==================== PARSER RULES ====================

// A complete xmSQL query may contain multiple statements
query
    : statement+ EOF
    ;

statement
    : defineTableStatement
    | createShallowRelationStatement
    | reducedByStatement
    | selectQueryStatement
    | setDirective
    ;

// SET DC_KIND="AUTO";
setDirective
    : SET IDENTIFIER EQUALS QUOTED_STRING SEMICOLON
    ;

// DEFINE TABLE '$TTable1' := SELECT ... FROM ... WHERE ...
defineTableStatement
    : DEFINE TABLE tableRef ASSIGN selectBody (COMMA | SEMICOLON)?
    ;

// REDUCED BY '$TTable1' := SELECT ... FROM ... WHERE ...
reducedByStatement
    : REDUCED BY tableRef ASSIGN selectBody (COMMA | SEMICOLON)?
    ;

// CREATE SHALLOW RELATION 'RelName' [MANYTOMANY] [BOTH] FROM 'T1'[C1] TO 'T2'[C2];
createShallowRelationStatement
    : CREATE SHALLOW RELATION tableRef
      relationModifier*
      FROM tableColumnRef
      TO tableColumnRef
      (COMMA | SEMICOLON)?
    ;

relationModifier
    : MANYTOMANY
    | BOTH
    ;

// Full select query: [WITH ...] SELECT ... FROM ... [JOIN ...] [WHERE ...] ;
selectQueryStatement
    : withClause? selectBody (COMMA | SEMICOLON)?
    ;

// WITH $Expr0 := (expr) [$Expr1 := (expr) ...]
withClause
    : WITH exprDefinition+
    ;

exprDefinition
    : EXPR_REF ASSIGN LPAREN expression RPAREN
    ;

// The core select body (shared by top-level and DEFINE TABLE)
selectBody
    : SELECT selectList fromClause joinClause* whereClause?
    ;

// SELECT column list
selectList
    : selectItem (COMMA selectItem)*
    ;

selectItem
    : aggregationExpr alias?
    | callbackExpr alias?
    | tableColumnRef CALLBACKDATAID? alias?
    | EXPR_AT_REF     // @$Expr0 reference
    | expression       // catch-all for complex expressions
    ;

// Optional select item alias: AS [name] or AS 'name'
alias
    : AS (BRACKETED_NAME | QUOTED_TABLE_NAME)
    ;

aggregationExpr
    : aggFunction LPAREN tableColumnRef? RPAREN
    | aggFunction LPAREN EXPR_AT_REF RPAREN
    ;

callbackExpr
    : ENCODECALLBACK LPAREN tableColumnRef RPAREN
    | CALLBACKDATAID LPAREN tableColumnRef RPAREN
    | CALLBACK expression? tableColumnRef
    ;

aggFunction
    : SUM | COUNT | DCOUNT | MIN | MAX | AVG | SUMSQR | COUNT_BIG
    ;

// FROM 'TableName'
fromClause
    : FROM tableRef
    ;

// LEFT OUTER JOIN 'Table' [ON 'T1'[C1]='T2'[C2]]
// INNER JOIN 'Table' [ON ...]
joinClause
    : joinType tableRef onClause?
    | joinType tableColumnRef onClause?
    | reverseBitmapJoin
    ;

joinType
    : LEFT OUTER JOIN
    | INNER JOIN
    ;

reverseBitmapJoin
    : REVERSE BITMAP JOIN tableRef ON tableColumnRef EQUALS tableColumnRef
    ;

onClause
    : ON tableColumnRef EQUALS tableColumnRef
    ;

// WHERE clause with filter predicates
whereClause
    : WHERE filterPredicate (logicalOp filterPredicate)*
    ;

filterPredicate
    : tableColumnRef comparisonOp filterValue                        // 'T'[C] = value
    | tableColumnRef IN LPAREN valueList RPAREN                     // 'T'[C] IN (v1, v2)
    | tableColumnRef NIN LPAREN valueList RPAREN                    // 'T'[C] NIN (v1, v2)
    | tableColumnRef BETWEEN filterValue AND filterValue            // 'T'[C] BETWEEN v1 AND v2
    | tableColumnRef ININDEX tableColumnRef                         // 'T1'[C1] ININDEX '$T'[C2]
    | coalesceFilter                                                 // PFCASTCOALESCE/COALESCE
    | tableColumnRef CALLBACKDATAID                                  // callback in filter
    | expression                                                     // catch-all for unknown filter patterns
    ;

coalesceFilter
    : (PFCASTCOALESCE | COALESCE) LPAREN tableColumnRef (AS IDENTIFIER)? RPAREN 
      comparisonOp 
      (COALESCE LPAREN)? filterValue RPAREN?
    ;

logicalOp
    : VAND | VOR | COMMA
    ;

comparisonOp
    : EQUALS | NOT_EQUALS | GREATER_THAN | LESS_THAN | GREATER_EQUALS | LESS_EQUALS
    ;

filterValue
    : QUOTED_STRING
    | QUOTED_TABLE_NAME   // single-quoted string values also appear in filters
    | NUMBER
    | IDENTIFIER
    ;

valueList
    : filterValue (COMMA filterValue)* truncationIndicator?
    ;

truncationIndicator
    : DOTDOT DOT? BRACKETED_NAME
    ;

// 'TableName'[ColumnName] or [TableName].[ColumnName]
tableColumnRef
    : tableRef BRACKETED_NAME
    | tableRef DOT BRACKETED_NAME
    ;

// 'TableName' or [TableName]  (single-quoted or bracketed table identifier)
tableRef
    : QUOTED_TABLE_NAME
    | BRACKETED_NAME
    ;

// Generic expression (catch-all for complex expressions like PFCAST, arithmetic, etc.)
expression
    : expressionAtom (expressionOp expressionAtom)*
    ;

expressionAtom
    : tableColumnRef
    | EXPR_AT_REF
    | EXPR_REF
    | functionCall
    | LPAREN expression RPAREN
    | filterValue
    | AS IDENTIFIER
    ;

functionCall
    : IDENTIFIER LPAREN expressionList? RPAREN
    | PFCAST LPAREN expression AS IDENTIFIER RPAREN
    ;

expressionList
    : expression (COMMA expression)*
    ;

expressionOp
    : PLUS | MINUS | STAR | SLASH | EQUALS | comparisonOp
    ;


// ==================== LEXER RULES ====================

// Keywords (case-insensitive)
SET         : [sS][eE][tT] ;
DEFINE      : [dD][eE][fF][iI][nN][eE] ;
TABLE       : [tT][aA][bB][lL][eE] ;
CREATE      : [cC][rR][eE][aA][tT][eE] ;
SHALLOW     : [sS][hH][aA][lL][lL][oO][wW] ;
RELATION    : [rR][eE][lL][aA][tT][iI][oO][nN] ;
MANYTOMANY  : [mM][aA][nN][yY][tT][oO][mM][aA][nN][yY] ;
BOTH        : [bB][oO][tT][hH] ;
REDUCED     : [rR][eE][dD][uU][cC][eE][dD] ;
BY          : [bB][yY] ;
WITH        : [wW][iI][tT][hH] ;
SELECT      : [sS][eE][lL][eE][cC][tT] ;
FROM        : [fF][rR][oO][mM] ;
LEFT        : [lL][eE][fF][tT] ;
OUTER       : [oO][uU][tT][eE][rR] ;
INNER       : [iI][nN][nN][eE][rR] ;
JOIN        : [jJ][oO][iI][nN] ;
ON          : [oO][nN] ;
WHERE       : [wW][hH][eE][rR][eE] ;
IN          : [iI][nN] ;
NIN         : [nN][iI][nN] ;
BETWEEN     : [bB][eE][tT][wW][eE][eE][nN] ;
AND         : [aA][nN][dD] ;
AS          : [aA][sS] ;
TO          : [tT][oO] ;
ININDEX     : [iI][nN][iI][nN][dD][eE][xX] ;
REVERSE     : [rR][eE][vV][eE][rR][sS][eE] ;
BITMAP      : [bB][iI][tT][mM][aA][pP] ;

// Aggregation keywords
SUM         : [sS][uU][mM] ;
COUNT       : [cC][oO][uU][nN][tT] ;
DCOUNT      : [dD][cC][oO][uU][nN][tT] ;
MIN         : [mM][iI][nN] ;
MAX         : [mM][aA][xX] ;
AVG         : [aA][vV][gG] ;
SUMSQR      : [sS][uU][mM][sS][qQ][rR] ;
COUNT_BIG   : [cC][oO][uU][nN][tT] '_' [bB][iI][gG] ;

// Callback keywords
CALLBACKDATAID  : [cC][aA][lL][lL][bB][aA][cC][kK][dD][aA][tT][aA][iI][dD] ;
ENCODECALLBACK  : [eE][nN][cC][oO][dD][eE][cC][aA][lL][lL][bB][aA][cC][kK] ;
CALLBACK        : [cC][aA][lL][lL][bB][aA][cC][kK] ;

// Filter/cast functions
PFCASTCOALESCE  : [pP][fF][cC][aA][sS][tT][cC][oO][aA][lL][eE][sS][cC][eE] ;
COALESCE        : [cC][oO][aA][lL][eE][sS][cC][eE] ;
PFCAST          : [pP][fF][cC][aA][sS][tT] ;

// Logical operators
VAND        : [vV][aA][nN][dD] ;
VOR         : [vV][oO][rR] ;

// Expression references: $Expr0, @$Expr0
EXPR_AT_REF : '@$Expr' [0-9]+ ;
EXPR_REF    : '$Expr' [0-9]+ ;

// Table name in single quotes: 'Table Name' or '$TTable1'
QUOTED_TABLE_NAME
    : '\'' (~['\r\n])+ '\''
    ;

// Bracketed name: [ColumnName], [$SemijoinProjection], etc.
// Contains everything between [ and ]
BRACKETED_NAME
    : '[' (~[\]\r\n])+ ']'
    ;

// String literals (double-quoted)
QUOTED_STRING
    : '"' (~["\r\n])* '"'
    ;

// Numbers (integer and decimal, with optional sign and scientific notation)
NUMBER
    : '-'? [0-9]+ ('.' [0-9]+)? ([eE] [+-]? [0-9]+)?
    ;

// Operators
ASSIGN          : ':=' ;
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
SEMICOLON   : ';' ;
PLUS        : '+' ;
MINUS       : '-' ;
STAR        : '*' ;
SLASH       : '/' ;
DOTDOT      : '..' ;

// Identifiers (for types like INT, CURRENCY, keywords not otherwise matched)
IDENTIFIER
    : [a-zA-Z_] [a-zA-Z0-9_]*
    ;

// Whitespace (skip)
WS          : [ \t\r\n]+ -> skip ;

// Single dot (for [Table].[Column] references)
DOT         : '.' ;

// Catch-all for any unrecognized characters (allows partial parsing)
ANY_CHAR    : . ;
