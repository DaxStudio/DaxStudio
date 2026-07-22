lexer grammar PreProcessorLexer;

@lexer::header {
using System.Collections.Generic;
}

@lexer::members {
	public Dictionary<string,bool> Parameters = new Dictionary<string,bool>();

	public void AddParameter(string param,bool isArray) {
		if (!Parameters.ContainsKey(param)) Parameters.Add(param, isArray);
	}
}

channels { COMMENTS_CHANNEL, STRING_CHANNEL }

COMMENT_SCRIPT_CONTINUATION: '-->>'                                                -> pushMode(TABLE_ROW_MODE);
COMMENT_SCRIPT:          '-->'                                                     -> pushMode(COMMENT_SCRIPT_MODE);
FORMAT_COMMENT:          '/*~' .*? '~*/'                                           ;

SINGLE_LINE_COMMENT:     ( '//' | '--' ) CommentStart InputCharacter*              -> channel(COMMENTS_CHANNEL);
DELIMITED_COMMENT:       '/*'  .*? '*/'                                            -> channel(COMMENTS_CHANNEL);
X_PARAMETERS_OPEN:       '<PARAMETERS'                                             -> pushMode(XMLA_PARAMETER_MODE);
WHITESPACES:             (Whitespace | NewLine)+                                   -> channel(HIDDEN);

// These are not DAX functions so we are using a different mode to capture their specific syntax
RSCUSTOMDAXFILTER:       'RSCUSTOMDAXFILTER'                                       -> pushMode(RSCUSTOMDAXFILTER_MODE);
RDLCUSTOMDAXPARAMETER:   'RDLCUSTOMDAXPARAMETER'                                   -> pushMode(RSCUSTOMDAXFILTER_MODE);

K_VAR:                   'VAR';
K_RETURN:                'RETURN';

DATE_LITERAL:            'dt' STRING_LITERAL;
STRING_LITERAL:          '"' (~'"' | '""')* '"' ;
MDX_REFERENCE:           COLUMN_OR_MEASURE '.' COLUMN_OR_MEASURE ('.' (COLUMN_OR_MEASURE|TABLE_OR_VARIABLE))? ;
TABLE:                   '\'' (~["'\r\n\u0085\u2028\u2029] | '\'\'')* '\'' ;
PARTIAL_TABLE:           '\'' (~["'\r\n\u0085\u2028\u2029] | '\'\'')*  ;
COLUMN_OR_MEASURE:       '[' (~["\]\r\n\u0085\u2028\u2029] | ']]')* ']' ;
PARTIAL_COLUMN_OR_MEASURE:  '[' (~["\]\r\n\u0085\u2028\u2029] | ']]')* ;
PARAMETER:               '@' IdentifierOrKeyword {this.AddParameter(this.Text, false);};
TABLE_OR_VARIABLE:       IdentifierOrKeyword;

INTEGER_LITERAL:       [0-9]+;
REAL_LITERAL:          INTEGER_LITERAL '.' INTEGER_LITERAL;



OPEN_CURLY:			   '{';
CLOSE_CURLY:		   '}';
OPEN_PARENS:           '(';
CLOSE_PARENS:          ')';
COMMA:                 ',';
PLUS:                  '+';
MINUS:                 '-';
STAR:                  '*';
DIV:                   '/';
CARET:                 '^';
AMP:                   '&';
ASSIGNMENT:            '=';
OP_NE:                 '<>';
OP_LE:                 '<=';
OP_GE:                 '>=';
LT:                    '<';
GT:                    '>';
OP_AND:                '&&';
OP_OR:                 '||';



fragment Punctuation	
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


fragment CommentStart:         ~('>'|'~');                 // anything that is not a > or ~ character

fragment InputCharacter:       ~[\r\n\u0085\u2028\u2029];  // anything that is not a newline

fragment NewLine
	: '\r\n' | '\r' | '\n'
	| '\u0085' // <Next Line CHARACTER (U+0085)>'
	| '\u2028' //'<Line Separator CHARACTER (U+2028)>'
	| '\u2029' //'<Paragraph Separator CHARACTER (U+2029)>'
	;

fragment Whitespace
	: UnicodeClassZS //'<Any Character With Unicode Class Zs>'
	| '\u0009' //'<Horizontal Tab Character (U+0009)>'
	| '\u000B' //'<Vertical Tab Character (U+000B)>'
	| '\u000C' //'<Form Feed Character (U+000C)>'
	;

fragment UnicodeClassZS
	: '\u0020' // SPACE
	| '\u00A0' // NO_BREAK SPACE
	| '\u1680' // OGHAM SPACE MARK
	| '\u180E' // MONGOLIAN VOWEL SEPARATOR
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
	: '\u0041'..'\u005a'
	;

fragment UnicodeClassLL
	: '\u0061'..'\u007A'
	;

fragment UnicodeClassND
	: '\u0030'..'\u0039'
	;

ANY: . ;

// =======================================================================
mode RSCUSTOMDAXFILTER_MODE;
// =======================================================================

RS_WHITESPACES:       (Whitespace | NewLine)+                                        -> channel(HIDDEN);

RS_CLOSE:             ')'                                                            ->  popMode ;
RS_OPEN:              '('                                                            ;

RS_PARAMETER:         '@' IdentifierOrKeyword                                        {this.AddParameter(this.Text, true);}  -> type(PARAMETER) ;
RS_COMMA:             ','                                                            ;
RS_CONDITION:         ( 'EQUALTOCONDITION' | 'NOTEQUALTOCONDITION' )                 ;
RS_QUOTEDNAME:        '[' (~["\]\r\n\u0085\u2028\u2029] | ']]')* ']'                 {Text = Text.Substring(1, Text.Length - 2).Replace("]]","]");} ;
//RS_IDENTIFIER:        RS_QUOTEDNAME RS_PERIOD RS_QUOTEDNAME                        ;
RS_DOT:               '.'                                                            ;

// TODO - add other data types
RS_STRING :           'STRING' ;
RS_INT :              'INT64' ;

fragment RsDataType : 'STRING'
					| 'INT64' 
					;



// =======================================================================
mode COMMENT_SCRIPT_MODE;
// =======================================================================

CS_NEWLINE:               NewLine                                              -> popMode;

CS_WHITESPACE:            Whitespace                                           -> skip;

CS_CONNECT:               'CONNECT';
CS_USE:                   'USE';
CS_SET:                   'SET';
CS_SET_PARAMETER:         'PARAMETER';
CS_GO:					  'GO';
CS_OUTPUT:                'OUTPUT';
CS_TEST:                  'TEST';
CS_ASSERT:                'ASSERT';
CS_CLEARCACHE:            'CLEARCACHE';
CS_SAVEAS:                'SAVEAS';
CS_PARAMETER:             '@' IdentifierOrKeyword;
CS_EQUALS:                 '=' ;
CS_GREATERTHAN:            '>' ;
CS_LESSTHAN:               '<' ;
CS_GREATER_OR_EQUAL:       '>=';
CS_LESS_OR_EQUAL:          '<='; 
CS_COMMA:                  ',';

CS_CSV:                   'CSV';
CS_XLSX:                  'XLSX';
CS_JSON:                  'JSON';
CS_TXT:                   'TXT';
CS_MD:                    'MD';
CS_PARQUET:               'PARQUET';

CS_SERVER:                'SERVER';
CS_DESKTOP:               'DESKTOP' ;
CS_SSDT:                  'SSDT' ;

CS_DURATION:              'DURATION';
CS_SE_QUERIES:            'SE_QUERIES';
CS_SE_CPU:                'SE_CPU';
CS_ROWCOUNT:              'ROWCOUNT';
CS_TABLE:                 'TABLE';
CS_UNORDERED:             'UNORDERED';
CS_PARTIAL:               'PARTIAL';

CS_TRACE:                 'TRACE';
CS_SERVERTIMINGS:         'SERVERTIMINGS';
CS_QUERYPLAN:             'QUERYPLAN';
CS_ALLQUERIES:            'ALLQUERIES';
CS_ON:                    'ON';
CS_OFF:                   'OFF';

CS_RESULTS:               'RESULTS';

CS_METRICS:               'METRICS';
CS_EXPORT:                'EXPORT';
CS_VIEW:                  'VIEW';

CS_SHOW:                  'SHOW';
CS_DEPENDENCIES:          'DEPENDENCIES';
CS_LAST_UPDATED:          'LAST_UPDATED';
CS_MAX_UPDATED:           'MAX_UPDATED';

CS_STRING:                'STRING';
CS_INTEGER:               'INT' | 'INT64';
CS_DATETIME:              'DATETIME';
CS_BOOLEAN:               'BOOL' | 'BOOLEAN';
CS_DOUBLE:                'DOUBLE';

CS_STRING_LITERAL:        '"' (~'"' | '""')* '"'  {Text = Text.Substring(1, Text.Length - 2).Replace("\"\"","\"");};
CS_INTEGER_LITERAL:       [0-9]+;
CS_REAL_LITERAL:          [0-9]* '.' [0-9]+;
CS_IDENTIFIER:            CsIdentifier;
CS_ARRAY_START:           '{';
CS_ARRAY_END:             '}';


fragment CsIdentifier: CsIdentifierCharacter+ ;

fragment CsIdentifierCharacter
	: IdentifierPartCharacter
	| '\\'
	;


// =======================================================================
mode  XMLA_PARAMETER_MODE;
// =======================================================================

X_PARAMETERS_CLOSE:   '</PARAMETERS>' -> popMode ; 

X_WHITESPACE:         (Whitespace | NewLine)+            -> skip;
X_XMLNS:              'XMLNS' (':' IdentifierOrKeyword)? '="' ~('"')* '"' ;
X_PARAMETER_OPEN:     '<PARAMETER>' ;
X_PARAMETER_CLOSE:    '</PARAMETER>' ;
X_NAME_OPEN:          '<NAME>'                           -> pushMode(XMLA_CONTENT_MODE);
X_NAME_CLOSE:         '</NAME>' ;
X_VALUE_OPEN:         '<VALUE' ;
X_VALUE_CLOSE:        '</VALUE>' ;
X_TYPE:               'XSI:TYPE="' ~('"')+ '">' {Text = Text.Substring(14, Text.Length - 16);}  -> pushMode(XMLA_CONTENT_MODE);

X_TAG_CLOSE:          '>' ;

mode XMLA_CONTENT_MODE;

X_LITERAL:            ~('<')+                             -> popMode;


// =======================================================================
mode TABLE_ROW_MODE;
// =======================================================================

TR_NEWLINE:               NewLine                                              -> popMode;
TR_WHITESPACE:            Whitespace                                           -> skip;
TR_PIPE:                  '|';
TR_SEPARATOR:             '-'+ ;
TR_CELL_TEXT:             ~[|\r\n\u0085\u2028\u2029 \t]+;