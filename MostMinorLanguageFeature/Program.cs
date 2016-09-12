namespace MostMinorLanguageFeature
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.CodeAnalysis.CSharp;

    using Octokit;

    class Program
    {
        private static ConcurrentDictionary<SyntaxKind, int> _keywords =
            new ConcurrentDictionary<SyntaxKind, int>();

        private static int _sources = 0;

        private static int _lines = 0;

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
                PerPage = 100,
                Page = 1
            };

            var repos = await client.Search.SearchRepo(searchRepoRequest);

            var tasks = new List<Task>();

            using (var semaphore = new SemaphoreSlim(4, 4))
            {
                foreach (var repo in repos.Items)
                {
                    await semaphore.WaitAsync();

                    var task = Task.Run(async () =>
                    {
                        string workDir = await CloneRepository(repo);

                        try
                        {
                            await ProcessRepository(workDir);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });

                    tasks.Add(task);
                }
            }

            Task.WaitAll(tasks.ToArray());
        }

        private static async Task ProcessRepository(string repositoryDir)
        {
            string lastComponent = Path.GetFileName(repositoryDir);
            if (lastComponent.Equals(".git", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var files = Directory.GetFiles(repositoryDir, "*.cs");
            foreach (var file in files)
            {
                await ProcessSource(file);
            }

            var directories = Directory.GetDirectories(repositoryDir);
            foreach (var dir in directories)
            {
                await ProcessRepository(dir);
            }
        }

        private static Task<string> CloneRepository(Repository repo)
        {
            var task = Task.Run(() =>
            {
                string workDir = Path.Combine(
                    Environment.CurrentDirectory,
                    "repos",
                    repo.Id.ToString(CultureInfo.InvariantCulture));
                
                var args = new[]
                {
                    "clone",
                    "-c core.eol=crlf",
                    '"' + repo.CloneUrl.Trim('"') + '"',
                    '"' + workDir.Trim('"') + '"'
                };

                string comandLine = string.Join(" ", args);

                using (var process = Process.Start("git.exe", comandLine))
                {
                    process.WaitForExit();
                }

                return workDir;
            });

            return task;
        }

        private static async Task ProcessSource(string path)
        {
            string code = File.ReadAllText(path);

            var ast = CSharpSyntaxTree.ParseText(code);
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

            Interlocked.Increment(ref _sources);

            int lines = code.Split(
                new[] { "\r\n" }, StringSplitOptions.None).Length;

            Interlocked.Add(ref _lines, lines);

            foreach (var count in counts)
            {
                _keywords.AddOrUpdate(
                    count.Keyword,
                    count.Count,
                    (k, c) => c + count.Count);
            }
        }
    }
}
