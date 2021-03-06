﻿using System;

namespace SharpILMixins.Processor
{
    [Flags]
    public enum DumpTargetType
    {
        None = 0,
        Methods = 1 << 0,
        Invoke = Methods | (2 << 0),
        Field = 3 << 0,
        All = Field | Invoke
    }

    public static class DumpTargetTypeExtensions
    {
        public static bool HasFlagFast(this DumpTargetType value, DumpTargetType flag)
        {
            return (value & flag) != 0;
        }
    }
}