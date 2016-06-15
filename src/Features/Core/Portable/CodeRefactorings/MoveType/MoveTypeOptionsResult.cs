using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.CodeAnalysis.CodeRefactorings.MoveType
{
    internal class MoveTypeOptionsResult
    {
        public static readonly MoveTypeOptionsResult Cancelled = new MoveTypeOptionsResult(isCancelled: true);

        public bool IsCancelled { get; }
        public IList<string> Folders { get; }
        public string NewFileName { get; }

        public MoveTypeOptionsResult(string newFileName, bool isCancelled = false)
        {
            this.NewFileName = NewFileName;
            this.IsCancelled = IsCancelled;
        }

        private MoveTypeOptionsResult(bool isCancelled)
        {
            this.IsCancelled = isCancelled;
        }
    }
}
