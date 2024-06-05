using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DSharpPlus;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OoLunar.DocBot.MemberDefinitions;

namespace OoLunar.DocBot.SymbolProviders.Projects
{
    public class ProjectSymbolProvider : ISymbolProvider
    {
        public string ConfigurationSectionName => "Project";

        protected readonly ProjectSymbolProviderConfiguration _configuration;
        protected readonly Dictionary<string, MemberDefinition> _objectDefinitions = [];
        protected readonly ILogger<ProjectSymbolProvider> _logger;

        public ProjectSymbolProvider(IConfigurationSection configurationSection, ILogger<ProjectSymbolProvider> logger)
        {
            _logger = logger;
            _configuration = configurationSection.Get<ProjectSymbolProviderConfiguration>()!;
            if (_configuration is null)
            {
                throw new InvalidOperationException("Failed to load configuration.");
            }
            else if (string.IsNullOrWhiteSpace(_configuration.Path))
            {
                throw new InvalidOperationException("ProjectPath must be set in the configuration.");
            }
            else if (!File.Exists(_configuration.Path))
            {
                throw new InvalidOperationException($"Project file {_configuration.Path} does not exist.");
            }
        }

        public string? GetDocumentation(string query) => _objectDefinitions.TryGetValue(query, out MemberDefinition? memberDefinition) ? Formatter.BlockCode(memberDefinition.Declaration, "cs") : null;

        public virtual async ValueTask LoadAsync()
        {
            Project project = await MSBuildWorkspace.Create().OpenProjectAsync(_configuration.Path);

            _logger.LogInformation("Loading project {ProjectName} with {DocumentCount:N0} documents...", project.Name, project.DocumentIds.Count);
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
                    if (syntaxNode is BaseNamespaceDeclarationSyntax)
                    {
                        NamespaceDefinition namespaceInfo = ParseNamespaceNode(syntaxNode);
                        _objectDefinitions[namespaceInfo.Name] = namespaceInfo;
                    }
                }
            }

            _logger.LogInformation("Loaded {ObjectCount:N0} objects from project {ProjectName}.", _objectDefinitions.Count, project.Name);
        }

        protected NamespaceDefinition ParseNamespaceNode(SyntaxNode namespaceDeclaration)
        {
            NamespaceDefinition namespaceInfo;

            string namespaceName = namespaceDeclaration.ToString();
            if (_objectDefinitions.TryGetValue(namespaceName, out MemberDefinition? memberDefinition))
            {
                if (memberDefinition is not NamespaceDefinition parsedNamespaceInfo)
                {
                    throw new InvalidOperationException($"Expected {namespaceName} to be a {nameof(NamespaceDefinition)}, instead found {memberDefinition.GetType().Name}");
                }

                namespaceInfo = parsedNamespaceInfo;
            }
            else
            {
                namespaceInfo = new NamespaceDefinition(namespaceName, namespaceDeclaration.ToFullString());
            }

            foreach (SyntaxNode subNode in namespaceDeclaration.DescendantNodes())
            {
                MemberDefinition? memberInfo = subNode switch
                {
                    ClassDeclarationSyntax classDeclaration => ParseClassNode(classDeclaration),
                    EnumDeclarationSyntax enumDeclaration => ParseEnumNode(enumDeclaration),
                    //InterfaceDeclarationSyntax interfaceDeclaration => ParseInterfaceNode(interfaceDeclaration),
                    //NamespaceDeclarationSyntax subNamespaceDeclaration => ParseNamespaceNode(subNamespaceDeclaration),
                    //RecordDeclarationSyntax recordDeclaration => ParseRecordNode(recordDeclaration),
                    //StructDeclarationSyntax structDeclaration => ParseStructNode(structDeclaration),
                    //DelegateDeclarationSyntax delegateDeclaration => ParseDelegateNode(delegateDeclaration),
                    _ => null
                };

                if (memberInfo is not null)
                {
                    namespaceInfo.SetMember(memberInfo);
                }
            }

            return namespaceInfo;
        }

        protected ClassDefinition ParseClassNode(ClassDeclarationSyntax classDeclaration)
        {
            ClassDefinition classInfo;
            if (_objectDefinitions.TryGetValue(classDeclaration.Identifier.Text, out MemberDefinition? memberInfo))
            {
                if (memberInfo is not ClassDefinition parsedClassInfo)
                {
                    throw new InvalidOperationException($"Expected {classDeclaration.Identifier.Text} to be a {nameof(ClassDefinition)}, instead found {memberInfo.GetType().Name}");
                }

                classInfo = parsedClassInfo;
                classInfo.UpdateMetadata(classDeclaration.GetText().ToString());
            }
            else
            {
                classInfo = new ClassDefinition(classDeclaration.Identifier.Text, classDeclaration.WithMembers(default).WithOpenBraceToken(default).WithCloseBraceToken(default).WithSemicolonToken(default).ToString());
                _objectDefinitions.Add(classDeclaration.Identifier.Text, classInfo);
            }

            return classInfo;
        }

        protected EnumDefinition ParseEnumNode(EnumDeclarationSyntax enumDeclaration)
        {
            EnumDefinition enumInfo;
            if (_objectDefinitions.TryGetValue(enumDeclaration.Identifier.Text, out MemberDefinition? memberInfo))
            {
                if (memberInfo is not EnumDefinition parsedEnumInfo)
                {
                    throw new InvalidOperationException($"Expected {enumDeclaration.Identifier.Text} to be a {nameof(EnumDefinition)}, instead found {memberInfo.GetType().Name}");
                }

                enumInfo = parsedEnumInfo;
            }
            else
            {
                enumInfo = new EnumDefinition(enumDeclaration.Identifier.Text, enumDeclaration.WithMembers(default).WithOpenBraceToken(default).WithCloseBraceToken(default).WithSemicolonToken(default).ToString());
                _objectDefinitions.Add(enumDeclaration.Identifier.Text, enumInfo);
            }

            return enumInfo;
        }

        protected StructDefinition ParseStructNode(StructDeclarationSyntax structDeclaration)
        {
            StructDefinition structInfo;
            if (_objectDefinitions.TryGetValue(structDeclaration.Identifier.Text, out MemberDefinition? memberInfo))
            {
                if (memberInfo is not StructDefinition parsedStructInfo)
                {
                    throw new InvalidOperationException($"Expected {structDeclaration.Identifier.Text} to be a {nameof(StructDefinition)}, instead found {memberInfo.GetType().Name}");
                }

                structInfo = parsedStructInfo;
            }
            else
            {
                structInfo = new StructDefinition(structDeclaration.Identifier.Text, structDeclaration.WithMembers(default).WithOpenBraceToken(default).WithCloseBraceToken(default).WithSemicolonToken(default).ToString());
                _objectDefinitions.Add(structDeclaration.Identifier.Text, structInfo);
            }

            return structInfo;
        }
    }
}
