# DAX Language ANTLR Grammar & Parser Specification for DAX Studio Intellisense

## Executive Summary

This document is a comprehensive specification for building an ANTLR4-based grammar and parser for the DAX (Data Analysis Expressions) language, designed to power intellisense in DAX Studio. The specification covers the complete DAX language surface area including: query statements (DEFINE, EVALUATE, ORDER BY, START AT), all expression types and operators with correct precedence, 250+ built-in functions, the new User-Defined Functions (UDF) system with typed parameters, custom calendar-based time intelligence, and runtime-injected metadata (tables, columns, measures, calendars, UDFs). The spec also covers the existing DaxParser codebase gaps and proposes a concrete grammar architecture to address them.

The existing `DAXParser2.g4` grammar in the DaxParser repo covers ~70% of the language but is missing: UDF definitions (`FUNCTION` keyword with typed lambda parameters), the strict equality operator (`==`), `COLUMN`/`TABLE` definitions in DEFINE blocks, multiple EVALUATE statements, calendar references in time intelligence functions, several newer functions (TOTALWTD, RANK, ROWNUMBER, etc.), and robust error recovery for partial input needed by intellisense.

---

## Table of Contents

1. [DAX Language Overview](#1-dax-language-overview)
2. [Lexical Specification](#2-lexical-specification)
3. [Parser Grammar Specification](#3-parser-grammar-specification)
4. [User-Defined Functions (UDFs)](#4-user-defined-functions-udfs)
5. [Custom Calendar-Based Time Intelligence](#5-custom-calendar-based-time-intelligence)
6. [Runtime Reference Data Integration](#6-runtime-reference-data-integration)
7. [Intellisense Architecture](#7-intellisense-architecture)
8. [Gap Analysis: Current vs. Required](#8-gap-analysis-current-vs-required)
9. [Implementation Roadmap](#9-implementation-roadmap)
10. [Confidence Assessment](#10-confidence-assessment)
11. [Footnotes](#11-footnotes)

---

## 1. DAX Language Overview

DAX is a formula language used across Power BI, Analysis Services, and Power Pivot. It operates on tabular data models and has two primary usage contexts[^1]:

1. **Measure/Calculated Column Expressions** – scalar formulas starting with `=` that define calculations within a model
2. **DAX Queries** – standalone queries using `EVALUATE` statements, optionally wrapped in `DEFINE` blocks

The language is **case-insensitive**[^2], uses **single-quoted table names** (`'Sales'`), **bracket-delimited column/measure names** (`[Amount]`), and supports **250+ built-in functions**[^3].

### Language Contexts for the Parser

The parser must handle multiple input contexts:

| Context | Entry Point | Example |
|---------|-------------|---------|
| DAX Query | `DEFINE...EVALUATE...` | Full query in DAX Studio editor |
| Measure Expression | `=` followed by scalar expression | Measure definition |
| Calculated Column | `=` followed by row-context expression | Column definition |
| Comment Script | `-->` commands + DAX | DAX Studio's preprocessor |

---

## 2. Lexical Specification

### 2.1 Token Categories

The lexer must handle these token types (using ANTLR4 with `DAXCharStream` for case-insensitive matching)[^4]:

#### 2.1.1 Comments
```antlr
SINGLE_LINE_COMMENT:  ( '//' | '--' ) InputCharacter*  -> channel(COMMENTS_CHANNEL);
BLOCK_COMMENT:        '/*' .*? '*/'                    -> channel(COMMENTS_CHANNEL);
DOC_COMMENT:          '///' InputCharacter*             -> channel(COMMENTS_CHANNEL);
```
Note: `///` (triple-slash) comments are used for UDF descriptions and should be captured in a DOC_COMMENT channel for intellisense tooltips[^5].

#### 2.1.2 Whitespace
```antlr
WHITESPACES: (Whitespace | NewLine)+ -> channel(HIDDEN);
```
Full Unicode whitespace support (Zs class) is required[^6].

#### 2.1.3 Keywords (Statement-Level)
```antlr
DEFINE:    'DEFINE';
EVALUATE:  'EVALUATE';
ORDER:     'ORDER';
BY:        'BY';
START:     'START';
AT:        'AT';
MEASURE:   'MEASURE';
VAR:       'VAR';
RETURN:    'RETURN';
FUNCTION:  'FUNCTION';    // NEW: UDF support
COLUMN:    'COLUMN';      // Virtual column definitions
TABLE:     'TABLE';       // Virtual table definitions (keyword, not token)
IN:        'IN';
ASC:       'ASC';
DESC:      'DESC';
NOT:       'NOT';
TRUE:      'TRUE';
FALSE:     'FALSE';
SKIP_:     'SKIP';
DENSE:     'DENSE';
```

#### 2.1.4 UDF Parameter Type Keywords (NEW)
```antlr
// Type keywords for UDF parameters
K_ANYVAL:    'ANYVAL';
K_SCALAR:    'SCALAR';
K_TABLE:     'TABLE';
K_ANYREF:    'ANYREF';

// Subtype keywords
K_VARIANT:   'VARIANT';
K_INT64:     'INT64';
K_DECIMAL:   'DECIMAL';
K_NUMERIC:   'NUMERIC';

// Parameter mode keywords
K_VAL:       'VAL';
K_EXPR:      'EXPR';
```
These are context-sensitive and only meaningful within UDF parameter declarations[^7].

#### 2.1.5 Built-in Functions (~260 tokens)

All 250+ DAX functions should be individual lexer tokens for unambiguous parsing. The current grammar has ~200 tokens[^8]. Functions added since the last grammar update include:

| Function | Added | Category |
|----------|-------|----------|
| TOTALWTD | Sep 2025 | Time Intelligence (week) |
| CLOSINGBALANCEWEEK | Sep 2025 | Time Intelligence (week) |
| ENDOFWEEK | Sep 2025 | Time Intelligence (week) |
| NEXTWEEK | Sep 2025 | Time Intelligence (week) |
| OPENINGBALANCEWEEK | Sep 2025 | Time Intelligence (week) |
| PREVIOUSWEEK | Sep 2025 | Time Intelligence (week) |
| STARTOFWEEK | Sep 2025 | Time Intelligence (week) |
| TABLEOF | Feb 2026 | Other |
| LOOKUPWITHTOTALS | Jun 2025 | Visual Calculations |
| LOOKUP | Jun 2025 | Visual Calculations |
| FIRST | Jan 2024 | Visual Calculations |
| LAST | Jan 2024 | Visual Calculations |
| NEXT | Jan 2024 | Visual Calculations |
| PREVIOUS | Jan 2024 | Visual Calculations |
| MATCHBY | May 2023 | Window |
| RANK | Apr 2023 | Window |
| ROWNUMBER | Apr 2023 | Window |
| LINEST | Feb 2023 | Statistical |
| LINESTX | Feb 2023 | Statistical |
| EVALUATEANDLOG | Recent | Debug |
| TOCSV | Recent | Text |
| TOJSON | Recent | Text |
| COLUMNSTATISTICS | Recent | Info |
| ISBOOLEAN | Recent | Information/UDF |
| ISCURRENCY | Recent | Information/UDF |
| ISDATETIME | Recent | Information/UDF |
| ISDECIMAL | Recent | Information/UDF |
| ISDOUBLE | Recent | Information/UDF |
| ISINT64 | Recent | Information/UDF |
| ISINTEGER | Recent | Information/UDF |
| ISNUMERIC | Recent | Information/UDF |
| ISSTRING | Recent | Information/UDF |
| WINDOW | Recent | Window |
| OFFSET | Recent | Window |
| INDEX | Recent | Window |
| ORDERBY | Recent | Window |
| PARTITIONBY | Recent | Window |

These need to be added to `DAXLexer2.g4`[^9].

#### 2.1.6 Operators
```antlr
OPEN_PARENS:  '(';
CLOSE_PARENS: ')';
OPEN_CURLY:   '{';
CLOSE_CURLY:  '}';
COMMA:        ',';
PLUS:         '+';
MINUS:        '-';
STAR:         '*';
DIV:          '/';
CARET:        '^';
AMP:          '&';
EQUALS:       '=';
STRICT_EQUALS:'==';    // NEW: strict equality
LT:           '<';
GT:           '>';
OP_AND:       '&&';
OP_OR:        '||';
OP_NE:        '<>';
OP_LE:        '<=';
OP_GE:        '>=';
LAMBDA_ARROW: '=>';    // NEW: UDF lambda syntax
COLON:        ':';     // NEW: UDF parameter type annotation
```

The `==` (strict equality) operator differs from `=` in BLANK handling: `==` does NOT treat BLANK as equal to 0 or empty string[^10].

#### 2.1.7 Literals
```antlr
INTEGER_LITERAL:  (MINUS)? [0-9]+;
REAL_LITERAL:     (MINUS)? [0-9]* '.' [0-9]+;
STRING_LITERAL:   '"' (~'"' | '""')* '"';
DATE_LITERAL:     'dt' STRING_LITERAL;
BOOLEAN_LITERAL:  TRUE | FALSE;
```

#### 2.1.8 Identifiers and References
```antlr
// Table name in single quotes: 'Table Name'
TABLE_REF:           '\'' (~["'\r\n] | '\'\'')* '\'';

// Column or measure in brackets: [Column Name]
COLUMN_OR_MEASURE:   '[' (~["\]\r\n] | ']]')* ']';

// Unquoted identifier (table name, variable name, UDF name)
IDENTIFIER:          IdentifierStartChar IdentifierPartChar*;

// Dotted identifier for namespaced UDFs: MyNamespace.MyFunction
DOTTED_IDENTIFIER:   IDENTIFIER ('.' IDENTIFIER)+;

// Parameter reference: @ParameterName
PARAMETER:           '@' IdentifierOrKeyword;
```

**UDF Name Rules**: UDF names can contain dots for namespacing (e.g., `Microsoft.PowerBI.MyFunc`) but cannot start/end with a dot or have consecutive dots[^11].

#### 2.1.9 Enum-Like Arguments
Certain functions accept keyword arguments that are not general identifiers:

```antlr
// DATEADD/DATEDIFF interval arguments:
WEEK:      'WEEK';
// (DAY, MONTH, QUARTER, YEAR are already function tokens)

// CROSSFILTER direction:
BOTH:      'BOTH';
NONE:      'NONE';
ONEWAY:    'ONEWAY';
ONEWAYRIGHTFILTERSLEFT:  'ONEWAYRIGHTFILTERSLEFT';
ONEWAYLEFTFILTERSRIGHT:  'ONEWAYLEFTFILTERSRIGHT';

// DATATABLE type arguments:
INTEGER:   'INTEGER';
DOUBLE:    'DOUBLE';
STRING:    'STRING';
BOOLEAN:   'BOOLEAN';
DATETIME:  'DATETIME';
```

---

## 3. Parser Grammar Specification

### 3.1 Top-Level Query Structure

A DAX query consists of an optional `DEFINE` block followed by one or more `EVALUATE` statements[^12]:

```antlr
parser grammar DAXParser;
options { tokenVocab=DAXLexer; }

// Entry point for DAX queries
daxQuery
    : defineBlock? evaluateBlock+ EOF
    ;

// Entry point for measure/calculated column expressions  
measureExpression
    : expression EOF
    ;
```

### 3.2 DEFINE Block

The DEFINE block can contain variables, measures, functions (UDFs), and virtual tables/columns[^13]:

```antlr
defineBlock
    : DEFINE definition+
    ;

definition
    : variableDefinition
    | measureDefinition
    | functionDefinition      // NEW: UDF support
    | virtualTableDefinition  // Virtual tables (query-scoped)
    | virtualColumnDefinition // Virtual columns (query-scoped)
    ;

variableDefinition
    : VAR variableName '=' expression
    ;

measureDefinition
    : MEASURE tableRef columnOrMeasureRef '=' expression
    ;

functionDefinition
    : docComment?
      FUNCTION functionName '=' '(' parameterList? ')' '=>' expression
    ;

virtualTableDefinition
    : TABLE tableName '=' tableExpression
    ;

virtualColumnDefinition
    : COLUMN tableRef columnOrMeasureRef '=' expression
    ;
```

### 3.3 EVALUATE Block

```antlr
evaluateBlock
    : EVALUATE tableExpression orderByClause? startAtClause?
    ;

orderByClause
    : ORDER BY orderByItem (',' orderByItem)*
    ;

orderByItem
    : expression (ASC | DESC)?
    ;

startAtClause
    : START AT startAtValue (',' startAtValue)*
    ;

startAtValue
    : literal
    | PARAMETER
    ;
```

### 3.4 Expression Grammar

The expression grammar must correctly implement DAX operator precedence[^14]:

```
Precedence (highest to lowest):
1. ^ (exponentiation, right-associative)
2. - (unary sign)
3. *, / (multiplication, division)
4. +, - (addition, subtraction)
5. & (text concatenation)
6. =, ==, <>, <, >, <=, >= , IN (comparison)
7. NOT (logical negation)
8. &&, || (logical AND, OR)
```

```antlr
expression
    : '(' expression ')'                                            #parenExpr
    | <assoc=right> expression '^' expression                       #powerExpr
    | '-' expression                                                #unaryMinusExpr
    | '+' expression                                                #unaryPlusExpr
    | expression ('*' | '/') expression                             #mulDivExpr
    | expression ('+' | '-') expression                             #addSubExpr
    | expression '&' expression                                     #concatExpr
    | expression ('=' | '==' | '<>' | '<' | '>' | '<=' | '>=')
      expression                                                    #comparisonExpr
    | expression IN tableConstructor                                #inExpr
    | NOT expression                                                #notExpr
    | expression ('&&' | '||') expression                           #logicalExpr
    | functionCall                                                  #funcCallExpr
    | varReturnBlock                                                #varReturnExpr
    | primaryExpression                                             #primaryExpr
    ;

primaryExpression
    : literal
    | columnReference
    | tableRef
    | IDENTIFIER                // Variable or unqualified table
    | PARAMETER
    | tableConstructor
    ;

literal
    : INTEGER_LITERAL
    | REAL_LITERAL
    | STRING_LITERAL
    | DATE_LITERAL
    | TRUE ('(' ')')?
    | FALSE ('(' ')')?
    ;
```

### 3.5 References

```antlr
// Fully qualified column: 'Table'[Column]
columnReference
    : tableRef columnOrMeasureRef
    ;

// Table reference (quoted or unquoted)
tableRef
    : TABLE_REF              // 'Quoted Table Name'
    | IDENTIFIER             // UnquotedTable
    ;

// Column or measure name  
columnOrMeasureRef
    : COLUMN_OR_MEASURE      // [Column Name]
    ;

// Variable name (unquoted, optionally prefixed with __)
variableName
    : IDENTIFIER
    ;
```

### 3.6 Function Calls

```antlr
functionCall
    : functionName '(' argumentList? ')'
    ;

functionName
    : builtInFunction
    | IDENTIFIER              // UDF name (unqualified)
    | DOTTED_IDENTIFIER       // UDF name (namespaced: Namespace.FuncName)
    ;

argumentList
    : argument (',' argument)*
    ;

argument
    : expression
    ;

builtInFunction
    : ABS | ACOS | ... | YEAR   // All 260+ function tokens
    ;
```

### 3.7 VAR / RETURN Blocks

VAR blocks can appear in DEFINE or inline within expressions[^15]:

```antlr
varReturnBlock
    : variableDefinition+ RETURN expression
    ;
```

### 3.8 Table Constructors

```antlr
tableConstructor
    : '{' rowConstructor (',' rowConstructor)* '}'
    | '{' expressionList '}'
    | '{' '}'                   // Empty table constructor
    ;

rowConstructor
    : '(' expressionList ')'
    ;

expressionList
    : expression (',' expression)*
    ;
```

### 3.9 IN Operator

The `IN` operator checks if a row value is in a table[^16]:

```antlr
// Already handled in expression rule as:
// expression IN tableConstructor
// Example: 'Product'[Color] IN { "Red", "Blue", "Black" }
```

---

## 4. User-Defined Functions (UDFs)

UDFs are a preview feature that introduces a `FUNCTION` keyword allowing users to define reusable DAX logic[^17][^18].

### 4.1 UDF Syntax

```antlr
functionDefinition
    : docComment?
      FUNCTION functionName '='
      '(' parameterList? ')' '=>' functionBody
    ;

parameterList
    : parameterDecl (',' parameterDecl)*
    ;

parameterDecl
    : parameterName (':' parameterTypeSpec)?
    ;

parameterTypeSpec
    : parameterType? parameterSubtype? parameterMode?
    ;

parameterType
    : K_ANYVAL | K_SCALAR | K_TABLE | K_ANYREF
    ;

parameterSubtype
    : K_VARIANT | K_INT64 | K_DECIMAL | K_DOUBLE
    | K_STRING  | K_DATETIME | K_BOOLEAN | K_NUMERIC
    ;

parameterMode
    : K_VAL | K_EXPR
    ;

functionBody
    : expression
    ;

functionName
    : IDENTIFIER
    | DOTTED_IDENTIFIER    // Namespaced: MyNamespace.MyFunc
    ;

parameterName
    : IDENTIFIER
    ;

docComment
    : DOC_COMMENT+
    ;
```

### 4.2 UDF Examples in Grammar

```dax
// Simple scalar function
DEFINE
    /// AddTax takes in amount and returns amount including tax
    FUNCTION AddTax = (amount : NUMERIC) => amount * 1.1
EVALUATE { AddTax(10) }

// Function with typed parameters and expression mode
DEFINE
    FUNCTION CountRowsLater = (t : TABLE EXPR) =>
        COUNTROWS(CALCULATETABLE(t, ALL('Date')))
EVALUATE { CountRowsLater('Sales') }

// Namespaced function
DEFINE
    FUNCTION Finance.NetMargin = (revenue : DECIMAL, cost : DECIMAL) =>
        DIVIDE(revenue - cost, revenue)
EVALUATE { Finance.NetMargin(1000, 750) }
```

### 4.3 UDF Intellisense Considerations

- **Function names** are model objects: at runtime, the set of available UDFs comes from the connected model via `INFO.FUNCTIONS("ORIGIN", "2")`[^19]
- **Parameter types** provide hints for argument completion
- **Doc comments** (`///`) provide tooltip descriptions
- **Recursion is NOT supported**[^20]
- **No function overloading**
- **No optional parameters**

### 4.4 Parser Handling Strategy

UDF function names should be treated as dynamic identifiers, not as lexer tokens. The parser must:

1. Accept any `IDENTIFIER` or `DOTTED_IDENTIFIER` in function call position
2. At the semantic/intellisense layer, resolve against a runtime-provided catalog of UDFs + built-in functions
3. Provide signature help using parameter metadata from the model

---

## 5. Custom Calendar-Based Time Intelligence

Custom calendars are a new Power BI feature that allows any table to define named calendar structures for time intelligence[^21].

### 5.1 Calendar References in DAX

Calendars are referenced as **string literals** (quoted calendar names) in time intelligence functions:

```dax
// Using a calendar name in PARALLELPERIOD
CALCULATE([Total Quantity], PARALLELPERIOD('Gregorian', -1, YEAR))

// Week-to-date with a custom calendar
TOTALWTD([Total Sales], 'ISO-454')

// Week-based period
CALCULATE([Total Quantity], PARALLELPERIOD('Gregorian', -1, WEEK))
```

### 5.2 Grammar Impact

Calendar references appear as the **first argument** to time intelligence functions where previously a date column was expected[^22]:

```antlr
// Time intelligence functions now accept either:
// 1. A column reference (traditional): TOTALYTD([Sales], 'Date'[Date])
// 2. A calendar reference (new): TOTALWTD([Sales], 'ISO-454')

// No grammar change needed at the syntax level - calendar names are
// single-quoted identifiers (TABLE_REF tokens) which are already valid.
// The semantic layer must distinguish calendar refs from table refs.
```

### 5.3 New Week-Based Functions

These functions only work with calendar references[^22]:

| Function | Signature |
|----------|-----------|
| `TOTALWTD` | `TOTALWTD(<expression>, <calendar> [, <filter>])` |
| `CLOSINGBALANCEWEEK` | `CLOSINGBALANCEWEEK(<expression>, <calendar>)` |
| `OPENINGBALANCEWEEK` | `OPENINGBALANCEWEEK(<expression>, <calendar>)` |
| `STARTOFWEEK` | `STARTOFWEEK(<calendar>)` |
| `ENDOFWEEK` | `ENDOFWEEK(<calendar>)` |
| `NEXTWEEK` | `NEXTWEEK(<calendar>)` |
| `PREVIOUSWEEK` | `PREVIOUSWEEK(<calendar>)` |

### 5.4 WEEK Enum Value

The `WEEK` keyword is now valid as a period argument to functions like `PARALLELPERIOD`, `DATEADD`, and `DATEDIFF`[^23]. This is already present in `DAXLexer2.g4`[^8].

### 5.5 Intellisense for Calendars

The intellisense system needs a runtime-provided list of calendars defined on the connected model. When the cursor is inside a time intelligence function:

1. Detect the function name (TOTALWTD, PARALLELPERIOD, etc.)
2. Determine the argument position
3. If position expects a calendar: suggest calendar names from the model metadata
4. If position expects a period: suggest YEAR, QUARTER, MONTH, WEEK, DAY

---

## 6. Runtime Reference Data Integration

The parser and intellisense system must accept external metadata that varies based on the connected data model[^24][^25].

### 6.1 Reference Data Types

```csharp
/// <summary>
/// Interface for providing model metadata to the parser/intellisense system.
/// Implementations vary based on the connected data source.
/// </summary>
public interface IModelMetadataProvider
{
    // Tables in the model
    IReadOnlyList<TableMetadata> GetTables();
    
    // Columns for a specific table
    IReadOnlyList<ColumnMetadata> GetColumns(string tableName);
    
    // Measures (can be on any table)
    IReadOnlyList<MeasureMetadata> GetMeasures();
    IReadOnlyList<MeasureMetadata> GetMeasures(string tableName);
    
    // User-Defined Functions (NEW)
    IReadOnlyList<UdfMetadata> GetUserDefinedFunctions();
    
    // Custom Calendars (NEW)
    IReadOnlyList<CalendarMetadata> GetCalendars();
    IReadOnlyList<CalendarMetadata> GetCalendars(string tableName);
    
    // Built-in function signatures (for signature help)
    IReadOnlyList<FunctionSignature> GetBuiltInFunctions();
}

public class TableMetadata
{
    public string Name { get; set; }
    public string Description { get; set; }
    public bool IsHidden { get; set; }
}

public class ColumnMetadata
{
    public string TableName { get; set; }
    public string ColumnName { get; set; }
    public string DataType { get; set; }     // String, Int64, Double, DateTime, etc.
    public string Description { get; set; }
    public bool IsHidden { get; set; }
    public string SortByColumn { get; set; }
}

public class MeasureMetadata
{
    public string TableName { get; set; }
    public string MeasureName { get; set; }
    public string Expression { get; set; }     // DAX formula
    public string DataType { get; set; }
    public string Description { get; set; }
    public string FormatString { get; set; }
    public bool IsHidden { get; set; }
}

public class UdfMetadata
{
    public string Name { get; set; }             // e.g., "AddTax" or "Finance.NetMargin"
    public string Description { get; set; }      // From /// doc comments
    public string Expression { get; set; }       // UDF body
    public List<UdfParameter> Parameters { get; set; }
}

public class UdfParameter
{
    public string Name { get; set; }
    public string Type { get; set; }       // AnyVal, Scalar, Table, AnyRef
    public string Subtype { get; set; }    // Int64, Decimal, String, etc.
    public string Mode { get; set; }       // Val, Expr
}

public class CalendarMetadata
{
    public string CalendarName { get; set; }     // e.g., "Gregorian", "ISO-454"
    public string TableName { get; set; }        // Table the calendar is defined on
    public List<CalendarCategory> Categories { get; set; }
}

public class CalendarCategory
{
    public string Name { get; set; }             // Year, Quarter, Month, Week, Date, etc.
    public string PrimaryColumn { get; set; }
    public List<string> AssociatedColumns { get; set; }
    public bool IsComplete { get; set; }         // Complete vs. Partial category
}

public class FunctionSignature
{
    public string Name { get; set; }
    public string Description { get; set; }
    public string Category { get; set; }
    public List<FunctionParameter> Parameters { get; set; }
    public string ReturnType { get; set; }       // Scalar or Table
}

public class FunctionParameter
{
    public string Name { get; set; }
    public string Description { get; set; }
    public string DataType { get; set; }
    public bool IsOptional { get; set; }
    public bool IsRepeatable { get; set; }       // For variadic functions
}
```

### 6.2 Integration with DAX Studio

DAX Studio's current intellisense uses `ADOTabularModel` for metadata[^25]:

```
┌─────────────────┐     ┌──────────────────┐     ┌───────────────────┐
│  DAX Editor     │────▶│ DaxIntellisense  │────▶│ ADOTabularModel   │
│  (AvalonEdit)   │     │ Provider         │     │ (Runtime Metadata)│
│                 │     │                  │     │  .Tables          │
│  ProcessText    │     │  ParseLine()     │     │  .Functions       │
│  Entered()      │     │  DaxLineParser   │     │  .DMVs            │
│                 │     │  PopulateData()  │     │                   │
└─────────────────┘     └──────────────────┘     └───────────────────┘
```

The new parser-based intellisense should maintain this architecture but replace the character-level `DaxLineParser` with ANTLR4-based parsing:

```
┌─────────────────┐     ┌──────────────────┐     ┌───────────────────┐
│  DAX Editor     │────▶│ DaxIntellisense  │────▶│ IModelMetadata    │
│  (AvalonEdit)   │     │ Provider (new)   │     │ Provider          │
│                 │     │                  │     │  .GetTables()     │
│  OnTextChanged  │     │  ANTLR4 Parser   │     │  .GetColumns()    │
│                 │     │  + Error Recovery│     │  .GetMeasures()   │
│                 │     │  DaxState        │     │  .GetUDFs()       │
│                 │     │  EditState       │     │  .GetCalendars()  │
└─────────────────┘     └──────────────────┘     └───────────────────┘
```

### 6.3 Metadata Sources in DAX Studio

In the current DAX Studio codebase, metadata is loaded from the connected model:

| Metadata | Source | DAX Studio Class |
|----------|--------|-----------------|
| Tables | SSAS/PBI model | `ADOTabularTableCollection` |
| Columns | Per-table from model | `ADOTabularColumnCollection` |
| Measures | Per-table from model | `ADOTabularMeasureCollection` |
| Functions | MDSCHEMA_FUNCTIONS | `ADOTabularFunctionGroupCollection` |
| DMVs | Dynamic Management Views | `ADOTabularDynamicManagementViewCollection` |
| UDFs | `INFO.FUNCTIONS("ORIGIN", "2")` | NEW – needs implementation |
| Calendars | TMDL / model metadata | NEW – needs implementation |

---

## 7. Intellisense Architecture

### 7.1 Edit States

The parser must determine the editing context at the cursor position:

```csharp
public enum EditState
{
    // Table contexts
    PartialTable,            // Cursor inside incomplete table name: 'Prod|
    CompleteTable,           // Cursor after complete table: 'Product'|
    
    // Column/Measure contexts
    PartialColumn,           // Inside column name: 'Product'[Col|
    PartialMeasure,          // Inside measure name: [Sal|
    
    // Expression contexts
    FunctionArgument,        // Inside function parens: CALCULATE(|
    ExpressionStart,         // Start of a new expression
    AfterOperator,           // After an operator: [Amount] + |
    
    // Statement contexts
    DefineContext,           // Inside DEFINE block, expecting definition type
    EvaluateContext,         // After EVALUATE keyword
    OrderByContext,          // Inside ORDER BY clause
    
    // Comment Script contexts
    CommentScriptCommand,    // After --> prefix
    
    // UDF contexts (NEW)
    FunctionDefinition,      // Inside FUNCTION definition
    ParameterType,           // Typing parameter type annotation
    
    // Calendar contexts (NEW)  
    CalendarArgument,        // Inside time intelligence function expecting calendar
    PeriodArgument,          // Expecting YEAR/QUARTER/MONTH/WEEK/DAY
    
    // General
    Identifier,              // General identifier context
    Unknown                  // Cannot determine context
}
```

### 7.2 Completion Triggers

| Trigger | Context | Completions |
|---------|---------|-------------|
| `'` (single quote) | Any expression position | Table names |
| `[` (open bracket) | After table ref or standalone | Columns/Measures of that table, or all measures |
| Space after keyword | After `EVALUATE` | Table names, function names |
| Space after keyword | After `DEFINE` | `VAR`, `MEASURE`, `FUNCTION`, `TABLE`, `COLUMN` |
| `(` after function | After function name | First argument hint |
| `,` in function | Inside function call | Next argument hint |
| `@` | Any expression position | Parameter names |
| `-->` | Comment Script prefix | Command keywords |
| `:` in UDF param | After parameter name | Type keywords (SCALAR, TABLE, etc.) |
| Any letter | Various | Context-dependent: functions, variables, tables |

### 7.3 Error Recovery for Partial Input

The intellisense parser must handle incomplete input gracefully. ANTLR4 strategies:

1. **DefaultErrorStrategy** with custom recovery – skip to next known sync point
2. **BailErrorStrategy** for fast-fail when we just need the partial parse tree
3. **Two-pass parsing**: 
   - First pass with `SLL` prediction mode (fast)
   - Fall back to `LL` on ambiguity (accurate)
4. **Partial token handling**: The lexer produces `PARTIAL_TABLE` and `PARTIAL_COLUMN_OR_MEASURE` tokens for incomplete references[^6]

```antlr
// Lexer rules for partial input (intellisense)
PARTIAL_TABLE:             '\'' (~["'\r\n])*;        // No closing quote
PARTIAL_COLUMN_OR_MEASURE: '[' (~["\]\r\n])*;        // No closing bracket
```

### 7.4 Signature Help

For function signature help (parameter info popup):

```csharp
public class SignatureHelpResult
{
    public string FunctionName { get; set; }
    public int ActiveParameter { get; set; }    // 0-based index
    public List<FunctionSignature> Signatures { get; set; }
    public bool IsUdf { get; set; }
}
```

The parser walks backward from the cursor to find the enclosing function call, counting commas to determine the active parameter index.

---

## 8. Gap Analysis: Current vs. Required

### 8.1 `DAXParser2.g4` Gaps

| Feature | Current State | Required |
|---------|--------------|----------|
| Multiple EVALUATE statements | ❌ Single only | ✅ `evaluateBlock+` |
| FUNCTION definitions (UDFs) | ❌ Missing | ✅ Full UDF grammar |
| Virtual TABLE definitions | ❌ Missing | ✅ `TABLE name = expr` in DEFINE |
| Virtual COLUMN definitions | ❌ Missing | ✅ `COLUMN table[col] = expr` in DEFINE |
| Strict equality `==` | ❌ Missing | ✅ New operator token |
| Lambda arrow `=>` | ❌ Missing | ✅ New operator token |
| Parameter type colon `:` | ❌ Missing | ✅ For UDF params |
| Dotted identifiers | ❌ Missing | ✅ For namespaced UDFs |
| `///` doc comments | ❌ Missing | ✅ Separate channel |
| Expression precedence | ⚠️ Partially correct | ✅ Full precedence chain |
| `NOT` as unary operator | ❌ Missing from expression | ✅ Add to expression |
| Unary `+` | ❌ Missing | ✅ Add to expression |
| Newer functions (~40) | ❌ Missing | ✅ Add tokens + parser entries |
| Calendar references | ⚠️ Works syntactically (TABLE_REF) | ✅ Semantic layer needed |
| Error recovery | ❌ None | ✅ Custom error strategy |

### 8.2 `DAXLexer2.g4` Gaps

| Feature | Current State | Required |
|---------|--------------|----------|
| `==` operator | ❌ Missing | ✅ STRICT_EQUALS token |
| `=>` operator | ❌ Missing | ✅ LAMBDA_ARROW token |
| `:` operator | ❌ Missing | ✅ COLON token |
| `///` doc comment | ❌ Not distinguished | ✅ DOC_COMMENT token |
| UDF type keywords | ❌ Missing | ✅ ANYVAL, SCALAR, ANYREF, etc. |
| UDF subtype keywords | ❌ Missing | ✅ INT64, DECIMAL, VARIANT, etc. |
| VAL/EXPR keywords | ❌ Missing | ✅ Parameter mode keywords |
| Missing function tokens | ❌ ~40 missing | ✅ Add all new functions |
| PARTIAL_TABLE token | ⚠️ In PreProcessorLexer only | ✅ Add to DAXLexer2 |
| Dotted identifier | ❌ Missing | ✅ DOTTED_IDENTIFIER token |

### 8.3 `PreProcessorParser.g4` Gaps

| Feature | Current State | Required |
|---------|--------------|----------|
| ASSERT TABLE command | ❌ Not implemented | ✅ Per spec in CommandScriptSpecs.md |
| TRACE commands | ❌ Not implemented | ✅ SERVERTIMINGS, QUERYPLAN, ALLQUERIES |
| SAVEAS command | ❌ Not implemented | ✅ With filename/timestamp |
| METRICS command | ❌ Not implemented | ✅ EXPORT, VIEW |
| OUTPUT configuration | ⚠️ Partial | ✅ Full spec (CSV/XLSX/JSON + folder/file) |
| LOOP command | ❌ Not implemented | ✅ TOPN ISONORAFTER pattern |
| OPEN (folded into CONNECT PBIX with a file path) | ✅ Implemented | ✅ CONNECT PBIX "<path>" auto-opens the file and connects |

---

## 9. Implementation Roadmap

### Phase 1: Core Grammar Upgrades

**Goal**: Bring `DAXLexer2.g4` and `DAXParser2.g4` to full DAX language coverage.

1. Add missing lexer tokens: `==`, `=>`, `:`, doc comments, all new functions
2. Fix parser for multiple EVALUATE statements
3. Add `COLUMN`, `TABLE`, `FUNCTION` to DEFINE block
4. Correct expression precedence chain
5. Add `NOT` and unary `+` to expressions
6. Add UDF parameter type grammar

### Phase 2: UDF Support

**Goal**: Full user-defined function parsing and intellisense.

1. Implement `functionDefinition` parser rule with parameter type annotations
2. Add dotted identifier support for namespaced UDFs
3. Implement UDF metadata provider (`INFO.FUNCTIONS` DMV)
4. Add UDF signature help to intellisense
5. Handle doc comments for tooltip descriptions

### Phase 3: Calendar Integration

**Goal**: Support calendar-based time intelligence in intellisense.

1. Add TOTALWTD and other week-based function tokens
2. Implement calendar metadata provider
3. Add context-aware calendar name completion in TI functions
4. Add WEEK as a period argument option

### Phase 4: Intellisense Error Recovery

**Goal**: Robust partial-input parsing for intellisense.

1. Implement custom ANTLR4 error recovery strategy
2. Add PARTIAL_TABLE and PARTIAL_COLUMN tokens to DAXLexer2
3. Create intellisense-specific parser variant (like existing `CommentScriptIntellisenseParser.g4`)
4. Implement two-pass SLL/LL parsing strategy
5. Build cursor-position-aware state determination

### Phase 5: DAX Studio Integration

**Goal**: Replace the character-level `DaxLineParser` with ANTLR4-based parsing.

1. Implement `IModelMetadataProvider` backed by `ADOTabularModel`
2. Wire ANTLR4 parser into `DaxIntellisenseProvider`
3. Add UDF and calendar metadata loading
4. Migrate `DaxLineState` to parser-derived `EditState`
5. Integration testing with NSubstitute mocks

---

## 10. Confidence Assessment

| Area | Confidence | Notes |
|------|-----------|-------|
| DAX operator precedence | **High** | Directly from Microsoft documentation[^14] |
| DAX query structure (DEFINE/EVALUATE) | **High** | Well-documented with formal syntax[^12][^13] |
| Built-in function list | **High** | Sourced from official function reference[^3][^9] |
| UDF syntax and semantics | **High** | Detailed Microsoft documentation[^17][^18] |
| UDF parameter types | **High** | Formal syntax in FUNCTION statement docs[^7] |
| Custom calendar syntax | **Medium-High** | Blog post + docs, but feature is in preview[^21][^22] |
| Calendar metadata structure | **Medium** | Inferred from TMDL examples; no formal API docs yet |
| DAX Studio integration points | **High** | Examined actual source code[^24][^25] |
| Intellisense edit states | **Medium** | Based on existing DAX Studio patterns + inference |
| Expression grammar completeness | **Medium-High** | No formal EBNF from Microsoft; reconstructed from docs and examples |
| Missing function count (~40) | **Medium** | Based on diff between lexer tokens and current function reference; may be higher |

### Assumptions Made
- The UDF preview feature will be GA'd with the documented syntax (no breaking changes expected)
- Custom calendars will maintain the quoted-name-as-first-argument pattern in TI functions
- DAX Studio will continue using AvalonEdit + Caliburn.Micro architecture
- The parser will target .NET Framework 4.7.1 (matching current DaxParser project)

---

## 11. Footnotes

[^1]: [DAX Overview](https://learn.microsoft.com/en-us/dax/) – Microsoft Learn
[^2]: [DAX Syntax Reference – Naming Requirements](https://learn.microsoft.com/en-us/dax/dax-syntax-reference) – "All object names are case-insensitive"
[^3]: [DAX Function Reference](https://learn.microsoft.com/en-us/dax/dax-function-reference) – "over 250 functions"
[^4]: `DaxParser/DAXLexer2.g4:1-10` – Case-insensitive lexing via DAXCharStream
[^5]: [UDF Best Practices](https://learn.microsoft.com/en-us/dax/best-practices/dax-user-defined-functions) – "Use `///` for function descriptions"
[^6]: `DaxParser/DAXLexer2.g4:806-840` – Whitespace and Unicode handling
[^7]: [FUNCTION Statement](https://learn.microsoft.com/en-us/dax/function-statement-dax) – Parameter type/subtype/mode syntax
[^8]: `DaxParser/DAXLexer2.g4:12-400` – Current function token definitions
[^9]: [New DAX Functions](https://learn.microsoft.com/en-us/dax/new-dax-functions) – Functions added 2023-2026
[^10]: [DAX Operator Reference](https://learn.microsoft.com/en-us/dax/dax-operator-reference) – "==" strict equality, BLANK handling
[^11]: [UDF Best Practices – Naming Requirements](https://learn.microsoft.com/en-us/dax/best-practices/dax-user-defined-functions) – "Can include periods (dots) for namespacing"
[^12]: [DAX Queries](https://learn.microsoft.com/en-us/dax/dax-queries) – EVALUATE, ORDER BY, START AT syntax
[^13]: [DEFINE Statement](https://learn.microsoft.com/en-us/dax/define-statement-dax) – Full DEFINE syntax including FUNCTION
[^14]: [DAX Operator Reference – Precedence](https://learn.microsoft.com/en-us/dax/dax-operator-reference) – Operator precedence table
[^15]: [VAR Statement](https://learn.microsoft.com/en-us/dax/var-dax) – VAR/RETURN syntax and scoping rules
[^16]: [DAX Operator Reference](https://learn.microsoft.com/en-us/dax/dax-operator-reference) – IN operator with table constructor
[^17]: [DAX User-Defined Functions](https://learn.microsoft.com/en-us/dax/best-practices/dax-user-defined-functions) – Full UDF documentation
[^18]: [FUNCTION Statement](https://learn.microsoft.com/en-us/dax/function-statement-dax) – Formal FUNCTION syntax
[^19]: [UDF Best Practices – DMVs](https://learn.microsoft.com/en-us/dax/best-practices/dax-user-defined-functions) – `INFO.FUNCTIONS("ORIGIN", "2")`
[^20]: [UDF Limitations](https://learn.microsoft.com/en-us/dax/best-practices/dax-user-defined-functions) – "Recursion or mutual recursion is not supported"
[^21]: [Calendar-Based Time Intelligence Blog](https://powerbi.microsoft.com/en-us/blog/calendar-based-time-intelligence-time-intelligence-tailored-preview/) – Feature announcement
[^22]: [Calendar-Based Time Intelligence Blog](https://powerbi.microsoft.com/en-us/blog/calendar-based-time-intelligence-time-intelligence-tailored-preview/) – Calendar reference syntax in DAX functions
[^23]: `DaxParser/DAXLexer2.g4:376` – WEEK token already defined
[^24]: [`DaxStudio/DaxStudio` repository](https://github.com/DaxStudio/DaxStudio) – DAX Studio source code
[^25]: `src/DaxStudio.UI/Utils/Intellisense/DaxIntellisenseProvider.cs` in [DaxStudio/DaxStudio](https://github.com/DaxStudio/DaxStudio) – Current intellisense implementation using DaxLineParser and ADOTabularModel
