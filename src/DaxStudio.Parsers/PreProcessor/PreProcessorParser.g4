parser grammar PreProcessorParser;

options { tokenVocab=PreProcessorLexer; }

document
	: block+ EOF
	;

block
	: script_commands+ command_block_tail?
	| table_row* query xmla_parameters? go_command?
	;

// The tail of a command-led block: either the DAX query (and optional xmla params) that the
// commands apply to, or just a batch terminator. When absent the block is commands-only
// (e.g. a lone "--> SHOW LAST_UPDATED" with no query to run).
command_block_tail
	: table_row* query xmla_parameters? go_command?
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
	| assert
	| assert_rowcount
	| assert_table_header
	| clear_cache
	| trace
	| metrics
	| show
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

connect:           CS_CONNECT (CS_SERVER|CS_PBIX|CS_SSDT) (CS_STRING_LITERAL | CS_IDENTIFIER);
use:               CS_USE (CS_STRING_LITERAL | CS_IDENTIFIER);
script_parameter:  CS_SET_PARAMETER (CS_STRING|CS_INTEGER|CS_DATETIME|CS_BOOLEAN|CS_DOUBLE)? CS_PARAMETER CS_EQUALS ( parameter_array_values | parameter_scalar_values );
output:            CS_OUTPUT (CS_CSV | CS_XLSX | CS_JSON) (CS_STRING_LITERAL | CS_IDENTIFIER);
test:              CS_TEST CS_PERFORMANCE CS_STRING_LITERAL;
assert:            CS_ASSERT (CS_DURATION | CS_SE_CPU | CS_SE_QUERIES )  (CS_EQUALS | CS_GREATERTHAN | CS_LESSTHAN | CS_GREATER_OR_EQUAL | CS_LESS_OR_EQUAL ) (CS_INTEGER_LITERAL | CS_REAL_LITERAL);
assert_rowcount:   CS_ASSERT CS_ROWCOUNT (CS_EQUALS | CS_GREATERTHAN | CS_LESSTHAN | CS_GREATER_OR_EQUAL | CS_LESS_OR_EQUAL ) CS_INTEGER_LITERAL;
assert_table_header: CS_ASSERT CS_TABLE (CS_UNORDERED | CS_PARTIAL)?;
clear_cache:       CS_CLEARCACHE;
trace:             CS_TRACE (CS_SERVERTIMINGS | CS_QUERYPLAN | CS_ALLQUERIES) (CS_ON | CS_OFF);
metrics:           CS_METRICS (metrics_export | metrics_view);
metrics_export:    CS_EXPORT (CS_STRING_LITERAL | CS_IDENTIFIER);
metrics_view:      CS_VIEW;
show:              CS_SHOW (CS_DEPENDENCIES | CS_LAST_UPDATED | CS_MAX_UPDATED);

// the go command is special as it terminates a batch
go_command:        COMMENT_SCRIPT CS_GO (CS_NEWLINE | EOF);

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