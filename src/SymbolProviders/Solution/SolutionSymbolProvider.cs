using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OoLunar.DocBot.MemberDefinitions;
using OoLunar.DocBot.SymbolProviders.Projects;

namespace OoLunar.DocBot.SymbolProviders.Solutions
{
    public sealed class SolutionSymbolProvider : ProjectSymbolProvider
    {
        public static new string ConfigurationSectionName => "Solution";

        public SolutionSymbolProvider(IConfigurationSection configurationSection, ILogger<ProjectSymbolProvider> logger) : base(configurationSection, logger) { }

        public override async ValueTask LoadAsync()
        {
            Solution solution = await MSBuildWorkspace.Create().OpenSolutionAsync(_configuration.Path);

            _logger.LogInformation("Loading solution {SolutionName} with {ProjectCount:N0} projects...", solution.FilePath, solution.ProjectIds.Count);
            foreach (Project project in solution.Projects)
            {
                _logger.LogInformation("Loading project {ProjectName} with {DocumentCount:N0} documents...", project.Name, project.DocumentIds.Count);

                int currentObjectCount = _objectDefinitions.Count;
                foreach (Document document in project.Documents)
                {
                    // We will not support scripting at this time.
                    if (document.SourceCodeKind is not SourceCodeKind.Regular || await document.GetSyntaxRootAsync() is not SyntaxNode syntaxRoot)
                    {
                        continue;
                    }

                    // Find all members inside of namespace declarations.
                    foreach (SyntaxNode syntaxNode in syntaxRoot.DescendantNodes())
                    {
                        if (syntaxNode is BaseNamespaceDeclarationSyntax baseNamespaceDeclarationSyntax)
                        {
                            NamespaceDefinition namespaceInfo = ParseNamespaceNode(baseNamespaceDeclarationSyntax);
                            _objectDefinitions[namespaceInfo.Name] = namespaceInfo;
                        }
                    }
                }

                _logger.LogInformation("Project {ProjectName} loaded with {MemberCount:N0} members.", project.Name, _objectDefinitions.Count - currentObjectCount);
            }

            _logger.LogInformation("Solution {SolutionName} loaded with {MemberCount:N0} members.", solution.FilePath, _objectDefinitions.Count);
        }
    }
}
