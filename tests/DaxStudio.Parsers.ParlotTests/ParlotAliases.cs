// Using aliases take precedence over the types imported by `using DaxStudio.Parsers.StorageEngine;` in the linked
// test files, so these names resolve to the Parlot implementations there.
global using AntlrXmSqlParser = DaxStudio.Parsers.StorageEngine.ParlotXmSqlParser;
global using AntlrXmSqlFormatter = DaxStudio.Parsers.StorageEngine.ParlotXmSqlFormatter;
global using XmSqlQueryGrouper = DaxStudio.Parsers.StorageEngine.ParlotXmSqlQueryGrouper;
