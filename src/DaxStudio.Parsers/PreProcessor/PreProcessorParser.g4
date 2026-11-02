parser grammar PreProcessorParser;

options { tokenVocab=PreProcessorLexer; }

document
	: block+ EOF
	;

block
	: (script_commands | table_row)+ command_block_tail?
	| table_row* query xmla_parameters? go_command?
	;

// The tail of a command-led block: either the DAX query (and optional xmla params) that the
// commands apply to, or just a batch terminator. When absent the block is commands-only
// (e.g. a lone "--> SHOW LAST_UPDATED" with no query to run). Table rows (from an ASSERT TABLE)
// are matched as part of the leading command group above so that further "-->" commands may
// appear after the "-->>" rows.
command_block_tail
	: query xmla_parameters? go_command?
	| go_command
	;

query
	: query_parts+ 
	;

query_parts
	: PARAMETER                 #DaxParameter
	| rscustomdaxfilter         #RSCustomDaxFilter
	| rdlcustomdaxparameter     #RdlCustomDaxParameter
	| STRING_LITERAL            #Other
    | TABLE                     #Other
	| PARTIAL_TABLE             #Other
    | PARTIAL_COLUMN_OR_MEASURE #Other
    | COLUMN_OR_MEASURE         #Other
	| MDX_REFERENCE             #Other
    | TABLE_OR_VARIABLE         #Other
	| INTEGER_LITERAL           #Other
	| REAL_LITERAL              #Other
	| punctuation               #Other
	| FORMAT_COMMENT            #Other
	| WHITESPACES               #Other
	| SINGLE_LINE_COMMENT       #Other
    | DELIMITED_COMMENT         #Other
	| K_VAR                     #Other
	| K_RETURN                  #Other
	| ANY                       #Other
	;

punctuation	
   : OPEN_CURLY	
   | CLOSE_CURLY	
   | OPEN_PARENS   
   | CLOSE_PARENS  
   | COMMA     
   | PLUS          
   | MINUS         
   | STAR          
   | DIV           
   | CARET
   | AMP          
   | ASSIGNMENT
   | LT     
   | GT            
   | OP_AND
   | OP_OR         
   | OP_NE        
   | OP_LE         
   | OP_GE         
   | COMMA
   ;

// Adding support for RSCustomDaxFilter function for Paginated Reports
rscustomdaxfilter:   RSCUSTOMDAXFILTER RS_OPEN PARAMETER RS_COMMA RS_CONDITION RS_COMMA table=RS_QUOTEDNAME RS_DOT column=RS_QUOTEDNAME RS_COMMA (RS_STRING | RS_INT) RS_CLOSE;

rdlcustomdaxparameter:   RDLCUSTOMDAXPARAMETER RS_OPEN PARAMETER RS_COMMA (RS_STRING | RS_INT) RS_CLOSE;

//
// TODO check all possible data type for RSCustomDaxFilter
//




// Comment Script commands
script_commands: COMMENT_SCRIPT command (CS_NEWLINE | EOF);


command
	: connect
	| use
	| script_parameter
	| test
	| baseline
	| assert
	| assert_rowcount
	| assert_table_header
	| clear_cache
	| trace
	| export
	| show
	| results
	| set_variable
	| saveas
	;

parameter_scalar_values
	: CS_STRING_LITERAL
	| CS_INTEGER_LITERAL
	| CS_REAL_LITERAL
	| CS_IDENTIFIER
	;

parameter_array_values
	: CS_ARRAY_START parameter_scalar_values (CS_COMMA parameter_scalar_values)* CS_ARRAY_END
	;

connect:           CS_CONNECT (CS_SERVER|CS_DESKTOP|CS_SSDT) (CS_STRING_LITERAL | unquoted_value);
use:               CS_USE (CS_STRING_LITERAL | unquoted_value);
script_parameter:  CS_SET_PARAMETER (CS_STRING|CS_INTEGER|CS_DATETIME|CS_BOOLEAN|CS_DOUBLE)? CS_PARAMETER CS_EQUALS ( parameter_array_values | parameter_scalar_values );
output:            CS_OUTPUT (CS_CSV | CS_XLSX | CS_JSON) (CS_STRING_LITERAL | CS_IDENTIFIER);
test:              CS_TEST (CS_STRING_LITERAL | unquoted_value);

// "--> BASELINE ["name"] [RUNS n]" captures the batch's result set and Server Timings metrics so a
// later batch can assert against them with "ASSERT <property> <op> BASELINE ["name"]".
// The RUNS clause is reserved for a later repeat-and-aggregate feature; it parses today but the
// listener rejects a value other than 1 so the syntax can be added without a breaking change.
baseline:          CS_BASELINE baseline_name? runs_clause?;
baseline_name:     CS_STRING_LITERAL | CS_IDENTIFIER;
runs_clause:       CS_RUNS CS_INTEGER_LITERAL;

// A reference to a previously captured baseline, used as the right-hand operand of an ASSERT.
// The optional factor multiplies the captured value, so "<= BASELINE "v1" * 1.1" allows a 10%
// regression and "<= BASELINE "v1" * 0.9" demands a 10% improvement.
// PREVIOUS is sugar for "the previous batch that runs a query" - that batch is captured as a
// baseline automatically, so no "--> BASELINE" command (and no name) is needed.
baseline_ref:      (CS_BASELINE baseline_name? | CS_PREVIOUS) baseline_factor?;
baseline_factor:   CS_STAR numeric_literal;

comparison_op:     CS_EQUALS | CS_GREATERTHAN | CS_LESSTHAN | CS_GREATER_OR_EQUAL | CS_LESS_OR_EQUAL;
numeric_literal:   CS_INTEGER_LITERAL | CS_REAL_LITERAL;

assert:            CS_ASSERT (CS_DURATION | CS_SE_CPU | CS_SE_QUERIES ) comparison_op assert_operand;
assert_operand:    numeric_literal | baseline_ref;
assert_rowcount:   CS_ASSERT CS_ROWCOUNT comparison_op assert_rowcount_operand;
assert_rowcount_operand: CS_INTEGER_LITERAL | baseline_ref;
assert_table_header: CS_ASSERT CS_TABLE (CS_UNORDERED | CS_PARTIAL)? (assert_table_file | baseline_ref)? ;
assert_table_file:   (CS_CSV | CS_TXT | CS_MD | CS_PARQUET) CS_STRING_LITERAL ;
clear_cache:       CS_CLEARCACHE;
trace:             CS_TRACE (CS_SERVERTIMINGS | CS_QUERYPLAN | CS_ALLQUERIES) (CS_ON | CS_OFF);
export:            CS_EXPORT CS_METRICS (CS_STRING_LITERAL | CS_IDENTIFIER);
show:              CS_SHOW (CS_DEPENDENCIES | CS_LAST_UPDATED | CS_MAX_UPDATED | CS_DIAGRAM | CS_METRICS | CS_DELTA);
results:           CS_RESULTS (CS_ON | CS_OFF);
set_variable:      CS_SET CS_IDENTIFIER CS_EQUALS ( CS_STRING_LITERAL | CS_INTEGER_LITERAL | CS_REAL_LITERAL | CS_IDENTIFIER );
saveas:            CS_SAVEAS (CS_STRING_LITERAL | CS_IDENTIFIER);

// An unquoted value for CONNECT/USE that may contain spaces (e.g. a database or Power BI report
// name like "AW Internet Sales"). It captures every token up to the end of the command line. The
// first token must not be a string literal so this does not overlap with the quoted alternative;
// the original source text (including the internal spaces the lexer skips) is recovered in the
// PreProcessorListener from the parse-tree char interval.
unquoted_value:    ~(CS_NEWLINE | CS_STRING_LITERAL) (~CS_NEWLINE)* ;

// The GO command is special as it terminates a batch. An optional delay belongs to the
// boundary and runs before the following batch starts. Bare values are milliseconds.
go_command:        COMMENT_SCRIPT CS_GO go_delay? (CS_NEWLINE | EOF);
go_delay:          CS_DELAY (CS_INTEGER_LITERAL CS_IDENTIFIER? | CS_IDENTIFIER);

// XMLA Parameters

xmla_parameters: X_PARAMETERS_OPEN X_XMLNS* X_TAG_CLOSE xmla_parameter+ X_PARAMETERS_CLOSE ;

xmla_parameter: X_PARAMETER_OPEN xmla_name xmla_value X_PARAMETER_CLOSE ;

xmla_name: X_NAME_OPEN X_LITERAL X_NAME_CLOSE ;

xmla_value: X_VALUE_OPEN X_TYPE X_LITERAL X_VALUE_CLOSE ;

// Assert Table continuation rows (-->> | col1 | col2 |)
table_row:           COMMENT_SCRIPT_CONTINUATION (table_data_row | table_separator_row) (TR_NEWLINE | EOF);
table_data_row:      TR_PIPE (table_cell TR_PIPE)+ ;
table_cell:          TR_CELL_TEXT* ;
table_separator_row: (TR_PIPE TR_SEPARATOR)+ TR_PIPE ;