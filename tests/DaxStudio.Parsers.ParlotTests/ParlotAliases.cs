// Using aliases take precedence over the types imported by `using DaxStudio.Parsers;` in the linked
// test files, so these names resolve to the Parlot implementations there.
global using AntlrXmSqlParser = DaxStudio.Parsers.ParlotXmSqlParser;
global using AntlrXmSqlFormatter = DaxStudio.Parsers.ParlotXmSqlFormatter;
global using XmSqlQueryGrouper = DaxStudio.Parsers.ParlotXmSqlQueryGrouper;
