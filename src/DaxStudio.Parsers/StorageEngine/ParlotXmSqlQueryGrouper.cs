using DaxStudio.Parsers.StorageEngine.XmSql;
using Serilog;
using System;

namespace DaxStudio.Parsers.StorageEngine
{
    /// <summary>
    /// Groups xmSQL queries by structural similarity, fingerprinting them with the Parlot grammar.
    /// Same grouping as <see cref="XmSqlQueryGrouper"/>.
    /// </summary>
    public class ParlotXmSqlQueryGrouper : XmSqlQueryGrouper
    {
        /// <summary>
        /// Computes a fingerprint for a single xmSQL query string.
        /// </summary>
        internal override XmSqlQueryFingerprint ComputeFingerprint(string xmSql)
        {
            try
            {
                var tree = XmSqlGrammar.Parse(xmSql);

                var walker = new XmSqlFingerprintWalker();
                return walker.ComputeFingerprint(tree);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to compute fingerprint for xmSQL query");
                return null;
            }
        }
    }
}
