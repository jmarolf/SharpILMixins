﻿using dnlib.DotNet;
using dnlib.DotNet.Emit;
using SharpILMixins.Annotations.Inject;

namespace SharpILMixins.Processor.Workspace.Processor.Actions.Impl.Inject.Impl
{
    public class FieldInjectionProcessor : InterestingOperandProcessor<IField>
    {
        public override AtLocation Location => AtLocation.Field;

        public override bool IsInterestingInstruction(Instruction instruction, InjectAttribute injectAttribute)
        {
            return IsFieldOpCode(instruction.OpCode);
        }

        public static bool IsFieldOpCode(OpCode opCode)
        {
            return opCode.Code switch
            {
                Code.Ldfld => true,
                Code.Ldflda => true,
                Code.Ldsfld => true,
                Code.Ldsflda => true,
                Code.Stfld => true,
                Code.Stsfld => true,
                _ => false
            };
        }
    }
}