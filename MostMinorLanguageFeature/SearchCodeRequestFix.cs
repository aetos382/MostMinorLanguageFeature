namespace MostMinorLanguageFeature
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    using Octokit;

    internal class SearchCodeRequestFix : SearchCodeRequest
    {
        public override IReadOnlyList<string> MergedQualifiers()
        {
            var result = base.MergedQualifiers();
            var parameters = new List<string>(result.Where(x => !x.StartsWith("language:")));

            if (this.Language != null)
            {
                parameters.Add(string.Format(CultureInfo.InvariantCulture, "language:{0}", this.Language));
            }

            return new ReadOnlyCollection<string>(parameters);
        }
    }
}
