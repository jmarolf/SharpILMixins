namespace SampleProjectCore.Mixins
{
    [System.ComponentModel.DescriptionAttribute("Generated by SharpILMixins")]
    public static class ProgramTargets
    {
        public static class Methods
        {
            public const string Main = "System.Void SampleProject.Program::Main(System.String[])";
            public static class MainInjects
            {
                public const string String_Format_String_Object = "System.String System.String::Format(System.String,System.Object)";
                public const string Console_WriteLine_String = "System.Void System.Console::WriteLine(System.String)";
                public const string BooleanOverload = "System.Boolean SampleProject.Program::BooleanOverload(System.Boolean)";
                public const string _coolNumber = "System.Int32 SampleProject.Program::_coolNumber";
            }

            public const string BooleanOverload = "System.Boolean SampleProject.Program::BooleanOverload(System.Boolean)";
            public static class BooleanOverloadInjects
            {
                public const string Boolean_Equals_Boolean = "System.Boolean System.Boolean::Equals(System.Boolean)";
            }

            public const string RandomNumber = "System.Int32 SampleProject.Program::RandomNumber()";
        }
    }

    [System.ComponentModel.DescriptionAttribute("Generated by SharpILMixins")]
    public static class SomeGameClassTargets
    {
        public static class Methods
        {
            public const string DoSomething = "System.Int32 SampleProjectCore.SomeGameClass::DoSomething()";
            public static class DoSomethingInjects
            {
                public const string isRunning = "System.Boolean SampleProjectCore.SomeGameClass::isRunning";
                public const string counter = "System.Int32 SampleProjectCore.SomeGameClass::counter";
            }
        }
    }

    [System.ComponentModel.DescriptionAttribute("Generated by SharpILMixins")]
    public static class InjectClassTargets
    {
        public static class Methods
        {
            public const string Example = "System.Void SampleProjectCore.InjectClass::Example()";
            public static class ExampleInjects
            {
                public const string Console_WriteLine_String = "System.Void System.Console::WriteLine(System.String)";
                public const string Random_Next = "System.Int32 System.Random::Next()";
                public const string Math_Abs_Double = "System.Double System.Math::Abs(System.Double)";
                public const string _d = "System.Double SampleProjectCore.InjectClass::_d";
            }
        }
    }
}