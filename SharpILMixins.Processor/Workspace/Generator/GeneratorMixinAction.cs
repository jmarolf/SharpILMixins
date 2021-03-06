﻿using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SharpILMixins.Processor.Utils;
using SharpILMixins.Processor.Workspace.Processor.Actions;
using SharpILMixins.Processor.Workspace.Processor.Actions.Impl.Inject.Impl;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SharpILMixins.Processor.Workspace.Generator
{
    public class GeneratorMixinAction
    {
        public MethodDef TargetMethod { get; }

        private readonly SyntaxTokenList _publicStaticModifiers =
            TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword));

        public GeneratorMixinAction(MethodDef targetMethod)
        {
            TargetMethod = targetMethod;
            SimpleTargetMethodName = ComputeSimpleMethodName(TargetMethod);
        }

        public string SimpleTargetMethodName { get; set; }

        private static string ComputeSimpleMethodName(IMethodDefOrRef targetMethod,
            bool includeDeclaringTypeName = false)
        {
            var count = targetMethod.DeclaringType.ResolveTypeDef().Methods
                .Count(m => m.Name.ToString().Equals(targetMethod.Name));
            if (count > 1)
            {
                var strings = new List<string> {targetMethod.Name.ToString()};
                if (includeDeclaringTypeName) strings.Insert(0, targetMethod.DeclaringType.ReflectionName);

                return string.Join("_",
                    strings.Concat(targetMethod.GetParams()
                        .Select(c => c.ReflectionName)));
            }

            return targetMethod.Name;
        }

        public IEnumerable<MemberDeclarationSyntax> ToSyntax()
        {
            var memberDeclarationSyntax = GetStringLiteralField(SimpleTargetMethodName,
                TargetMethod.FullName);
            if (memberDeclarationSyntax != null)
            {
                yield return memberDeclarationSyntax;
            }

            var injectMembers = new List<MemberDeclarationSyntax>();

            AddInvokeMembers(injectMembers);
            AddFieldMembers(injectMembers);

            if (injectMembers.Count != 0)
                yield return ClassDeclaration($"{SimpleTargetMethodName}Injects")
                    .AddMembers(injectMembers.ToArray())
                    .WithModifiers(_publicStaticModifiers);
        }

        private void AddFieldMembers(List<MemberDeclarationSyntax> injectMembers)
        {
            injectMembers.AddRange(
                TargetMethod.Body.Instructions
                    .Where(i => FieldInjectionProcessor.IsFieldOpCode(i.OpCode))
                    .Select(i => i.Operand)
                    .OfType<IField>()
                    .DistinctBy(i => i.Name.ToString())
                    .Select(i => GetStringLiteralField(i.Name, i.FullName))
                    .Where(i => i is not null)
                    .Select(i => i!).ToList()
            );
        }

        private void AddInvokeMembers(List<MemberDeclarationSyntax> injectMembers)
        {
            injectMembers.AddRange(TargetMethod.Body.Instructions
                .Where(i => InvokeInjectionProcessor.IsCallOpCode(i.OpCode))
                .Select(i => i.Operand)
                .OfType<IMethodDefOrRef>()
                .DistinctBy(i => ComputeSimpleMethodName(i, true))
                .Select(i => GetStringLiteralField(ComputeSimpleMethodName(i, true), i.FullName))
                .Where(i => i is not null)
                .Select(i => i!)
                .Distinct().ToList());
        }

        private MemberDeclarationSyntax? GetStringLiteralField(string name, string stringLiteral)
        {
            if (!SyntaxFacts.IsValidIdentifier(name)) return null;
            return FieldDeclaration(VariableDeclaration(
                        PredefinedType(
                            Token(SyntaxKind.StringKeyword)))
                    .WithVariables(
                        SingletonSeparatedList(
                            VariableDeclarator(
                                    Identifier(name))
                                .WithInitializer(
                                    EqualsValueClause(
                                        LiteralExpression(
                                            SyntaxKind.StringLiteralExpression,
                                            Literal(stringLiteral)))))))
                .WithModifiers(
                    TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.ConstKeyword)));
        }
    }
}