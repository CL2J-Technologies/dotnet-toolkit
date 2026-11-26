using Xunit;

//Deliberately outside the namespace below: a type with no namespace is a case of its own.
#pragma warning disable CA1050 // Declare types in namespaces
public sealed class GlobalNamespaceType;
#pragma warning restore CA1050

namespace cl2j.Tooling.Tests
{
    /// <summary>
    ///     The names <see cref="TypeUtils"/> gives to types that are not at the top level of a
    ///     namespace: nested types, nested generics, and types with no namespace at all. Callers
    ///     write these names into source they compile, so each must come back as something that
    ///     source can refer to.
    /// </summary>
    public class TypeUtilsNamingTests
    {
        public sealed class Outer
        {
            public sealed class Inner;

            public sealed class InnerGeneric<T>;
        }

        public sealed class OuterGeneric<T>
        {
            public sealed class Inner<TInner>;
        }

        [Fact]
        public void A_nested_type_is_named_after_the_type_it_is_declared_in()
        {
            Assert.Equal("cl2j.Tooling.Tests.TypeUtilsNamingTests+Outer.Inner", TypeUtils.GetTypeName<Outer.Inner>());
        }

        [Fact]
        public void A_generic_type_nested_in_a_plain_one_keeps_its_arguments()
        {
            var name = TypeUtils.GetTypeName<Outer.InnerGeneric<int>>();

            Assert.NotNull(name);
            Assert.EndsWith("InnerGeneric<System.Int32>", name, StringComparison.Ordinal);
        }

        [Fact]
        public void A_generic_type_nested_in_a_plain_one_names_the_declaring_type_once()
        {
            Assert.Equal(
                "cl2j.Tooling.Tests.TypeUtilsNamingTests+Outer.InnerGeneric<System.Int32>",
                TypeUtils.GetTypeName<Outer.InnerGeneric<int>>());
        }

        [Fact]
        public void A_generic_type_nested_in_a_plain_one_keeps_its_declaring_type_as_a_generic_argument()
        {
            Assert.Equal(
                "System.Collections.Generic.List<cl2j.Tooling.Tests.TypeUtilsNamingTests.Outer.InnerGeneric<System.Int32>>",
                TypeUtils.GetTypeName<List<Outer.InnerGeneric<int>>>());
        }

        [Fact]
        public void A_plain_type_nested_in_a_plain_one_keeps_its_declaring_type_as_a_generic_argument()
        {
            Assert.Equal(
                "System.Collections.Generic.List<cl2j.Tooling.Tests.TypeUtilsNamingTests.Outer.Inner>",
                TypeUtils.GetTypeName<List<Outer.Inner>>());
        }

        [Fact]
        public void A_generic_type_nested_in_a_generic_one_lists_the_arguments_of_both()
        {
            var name = TypeUtils.GetTypeName<OuterGeneric<string>.Inner<int>>();

            Assert.NotNull(name);
            Assert.Contains("OuterGeneric<System.String>+Inner<System.Int32>", name, StringComparison.Ordinal);
        }

        [Fact]
        public void A_type_without_a_namespace_is_named_without_one()
        {
            Assert.Null(typeof(GlobalNamespaceType).Namespace);

            Assert.Equal("GlobalNamespaceType", TypeUtils.GetTypeName<GlobalNamespaceType>());
        }

        /// <summary>
        ///     A generic type that reports a name with no arity suffix. The runtime always writes
        ///     Name as Pair`2, so only a type that is not a runtime type can reach the code that
        ///     copes without one.
        /// </summary>
        private sealed class UnsuffixedGenericType : System.Reflection.TypeDelegator
        {
            private readonly Type generic;

            public UnsuffixedGenericType(Type generic) : base(generic) => this.generic = generic;

            public override bool IsGenericType => true;

            public override Type[] GetGenericArguments() => generic.GetGenericArguments();

            public override string Name => "Pair";
        }

        /// <summary>
        ///     A type that says it is generic the first time it is asked and not the second. The
        ///     naming code asks twice, and answers from t.Name the second time.
        /// </summary>
        private sealed class ChangesItsMindType : System.Reflection.TypeDelegator
        {
            private readonly Type generic;
            private int asked;

            public ChangesItsMindType(Type generic) : base(generic) => this.generic = generic;

            public override bool IsGenericType => asked++ == 0;

            public override Type[] GetGenericArguments() => generic.GetGenericArguments();

            public override string Name => "Changed";
        }

        [Fact]
        public void A_generic_type_whose_name_has_no_arity_suffix_keeps_its_name()
        {
            Assert.Equal("Pair<Int32, String>", new UnsuffixedGenericType(typeof(Dictionary<int, string>)).GetPrettyName());
        }

        [Fact]
        public void A_type_that_stops_being_generic_while_it_is_named_falls_back_to_its_name()
        {
            var name = TypeUtils.GetTypeName(new ChangesItsMindType(typeof(Dictionary<int, string>)));

            Assert.Equal("System.Collections.Generic.Changed", name);
        }
    }
}
