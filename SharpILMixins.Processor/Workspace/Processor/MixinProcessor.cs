﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using dnlib.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NLog;
using SharpILMixins.Processor.Utils;
using SharpILMixins.Processor.Workspace.Generator;
using SharpILMixins.Processor.Workspace.Processor.Actions.Impl;
using SharpILMixins.Processor.Workspace.Processor.Actions.Impl.Inject.Impl;
using SharpILMixins.Processor.Workspace.Processor.Scaffolding;
using SharpILMixins.Processor.Workspace.Processor.Scaffolding.Redirects;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SharpILMixins.Processor.Workspace.Processor
{
    public class MixinProcessor
    {
        public MixinProcessor(MixinWorkspace workspace)
        {
            Workspace = workspace;
            CopyScaffoldingHandler = new CopyScaffoldingHandler(workspace);
        }

        public Logger Logger { get; } = LoggerUtils.LogFactory.GetLogger(nameof(MixinProcessor));

        public MixinWorkspace Workspace { get; }

        public CopyScaffoldingHandler CopyScaffoldingHandler { get; set; }

        public RedirectManager RedirectManager => CopyScaffoldingHandler.RedirectManager;

        public void Process(List<MixinRelation> mixinRelations, MixinTargetModule targetModule)
        {
            DumpRequestedTargets(mixinRelations, Workspace.Settings.DumpTargets);

            if (Workspace.Settings.IsGenerateOnly)
            {
                GenerateHelperCode(mixinRelations, targetModule);
                return;
            }

            CopyScaffoldingHandler.CopyNonMixinClasses(Workspace.MixinModule, targetModule.ModuleDef);
            foreach (var mixinRelation in mixinRelations)
            {
                Logger.Info($"Starting to process mixin {mixinRelation.MixinType.Name}");

                if (mixinRelation.IsAccessor)
                {
                    Logger.Info(
                        $"Mixin {mixinRelation.MixinType.Name} is an accessor for {mixinRelation.TargetType.Name}.");
                    RedirectManager.RegisterTypeRedirect(mixinRelation.MixinType, mixinRelation.TargetType);
                    continue;
                }

                CopyScaffoldingHandler.ProcessType(mixinRelation.TargetType, mixinRelation.MixinType);

                foreach (var action in mixinRelation.MixinActions.OrderBy(a => a.Priority))
                {
                    action.LocateTargetMethod();
                    Logger.Debug($"Starting to proccess action for \"{action.MixinMethod.FullName}\"");

                    try
                    {
                        action.CheckIsValid();
                    }
                    catch (Exception e)
                    {
                        throw new MixinApplyException(
                            $"Method \"{action.TargetMethod}\" is not a valid target for \"{action.MixinMethod}\"",
                            e);
                    }

                    var processor =
                        BaseMixinActionProcessorManager.GetProcessor(action.MixinAttribute.GetType(), Workspace);
                    processor.ProcessAction(action, action.MixinAttribute);
                    if (action.TargetMethod.Body != null)
                    {
                        action.TargetMethod.Body.UpdateInstructionOffsets();
                        FixPdbStateIfNeeded(action.TargetMethod);
                        RedirectManager.ProcessRedirects(action.TargetMethod, action.TargetMethod.Body);
                    }

                    Logger.Debug($"Finished to proccess action for \"{action.MixinMethod.FullName}\"");
                }

                Logger.Info($"Finished to process mixin {mixinRelation.MixinType.Name}");
            }
        }

        private static void FixPdbStateIfNeeded(MethodDef method)
        {
            var body = method.Body;
            if (body == null || !body.HasPdbMethod)
                return;
            var pdbMethod = body.PdbMethod;
            var pdbMethodScope = pdbMethod.Scope;
            if (pdbMethodScope == null)
                return;
            
            //Fix start
            if (!body.Instructions.Contains(pdbMethodScope.Start))
                pdbMethodScope.Start = body.Instructions.FirstOrDefault(i => i.SequencePoint != null);
            //Fix start
            if (!body.Instructions.Contains(pdbMethodScope.End))
                pdbMethodScope.End = body.Instructions.FirstOrDefault(i => i.SequencePoint != null);
        }

        private void GenerateHelperCode(List<MixinRelation> mixinRelations, MixinTargetModule targetModule)
        {
            List<ClassDeclarationSyntax> declarationSyntaxes = new List<ClassDeclarationSyntax>();

            foreach (var mixinRelation in mixinRelations)
            {
                Logger.Info($"Starting to process mixin {mixinRelation.MixinType.Name}");

                var classDeclarationSyntax = new GeneratorMixinRelation(mixinRelation).ToSyntax();
                if (classDeclarationSyntax != null) declarationSyntaxes.Add(classDeclarationSyntax);

                Logger.Info($"Finished to process mixin {mixinRelation.MixinType.Name}");
            }

            var namespaceDeclarationSyntax = NamespaceDeclaration(
                    IdentifierName(Workspace.Configuration.BaseNamespace ?? "SharpILMixins"))
                .WithNamespaceKeyword(
                    Token(SyntaxKind.NamespaceKeyword))
                .WithMembers(new SyntaxList<MemberDeclarationSyntax>(declarationSyntaxes));

            var code = namespaceDeclarationSyntax.NormalizeWhitespace().ToFullString();

            File.WriteAllText(Path.Combine(Workspace.Settings.OutputPath, "GeneratedTargets.cs"), code);
        }

        private void DumpRequestedTargets(List<MixinRelation> mixinRelations, DumpTargetType dumpTargets)
        {
            foreach (var relation in mixinRelations.DistinctBy(r => r.MixinType.FullName))
            {
                if (!ShouldDump(relation, dumpTargets)) continue;

                var targetType = relation.TargetType;
                Logger.Info($"Target dump for \"{targetType.FullName}\":");

                Logger.Info("Methods:");
                foreach (var method in targetType.Methods)
                {
                    Logger.Info($">> {method.FullName}");

                    if (method.Body != null)
                    {
                        DumpInvokeTargets(method, dumpTargets);
                        DumpFieldTargets(method, dumpTargets);
                    }
                }

                Logger.Info("");
            }
        }

        private void DumpInvokeTargets(MethodDef method, DumpTargetType dumpTargets)
        {
            if (dumpTargets.HasFlagFast(DumpTargetType.Invoke))
            {
                Logger.Info("");
                Logger.Info("Invoke targets:");

                var invokeCalls = method.Body.Instructions
                    .Where(i => InvokeInjectionProcessor.IsCallOpCode(i.OpCode))
                    .Select(i => i.Operand)
                    .OfType<IMethodDefOrRef>()
                    .Select(i => i.FullName).Distinct().ToList();
                invokeCalls.ForEach(c => Logger.Info($">>> {c}"));
            }
        }

        private void DumpFieldTargets(MethodDef method, DumpTargetType dumpTargets)
        {
            if (dumpTargets.HasFlagFast(DumpTargetType.Invoke))
            {
                Logger.Info("");
                Logger.Info("Field targets:");

                var fieldCalls = method.Body.Instructions
                    .Where(i => FieldInjectionProcessor.IsFieldOpCode(i.OpCode))
                    .Select(i => i.Operand)
                    .OfType<IField>()
                    .Select(i => i.FullName).Distinct().ToList();
                fieldCalls.ForEach(c => Logger.Info($">>> {c}"));
            }
        }

        private bool ShouldDump(MixinRelation relation, DumpTargetType dumpTargets)
        {
            return dumpTargets != DumpTargetType.None;
        }
    }
}