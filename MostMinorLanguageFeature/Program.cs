using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Octokit;

namespace MostMinorLanguageFeature
{
    class Program
    {
        static void Main(string[] args)
        {
            MainAsync().Wait();
        }

        private static async Task MainAsync()
        {
            var client = new GitHubClient(new ProductHeaderValue("MMLF", "1.0"));

            var searchRepoRequest = new SearchRepositoriesRequest
            {
                Language = Language.CSharp,
                SortField = RepoSearchSort.Stars,
                Order = SortDirection.Descending,
                PerPage = 100
            };

            var repos = await client.Search.SearchRepo(searchRepoRequest);

            foreach (var repo in repos.Items)
            {
                var searchCodeRequest = new SearchCodeRequestFix
                {
                    Language = Language.CSharp,
                    PerPage = 100
                };

                searchCodeRequest.Repos.Add(repo.FullName);

                for (int page = 0;; ++page)
                {
                    searchCodeRequest.Page = page;

                    var codes = await client.Search.SearchCode(searchCodeRequest);

                    foreach (var code in codes.Items)
                    {
                        var contents = await client.Repository.Content.GetAllContents(repo.Id, code.Path);

                        foreach (var content in contents)
                        {
                            var ast = CSharpSyntaxTree.ParseText(content.Content);
                            var root = await ast.GetRootAsync();

                            var counts = root.DescendantTokens()
                                .Where(x => x.IsKeyword())
                                .GroupBy(x => x.Kind())
                                .Select(x => new
                                {
                                    Keyword = x.Key,
                                    Count = x.Count()
                                })
                                .OrderByDescending(x => x.Count)
                                .ToArray();

                            foreach (var count in counts)
                            {
                                Console.WriteLine($"{count.Keyword,-20}\t{count.Count,10}");
                            }
                        }
                    }
                }
            }
        }
    }
}
