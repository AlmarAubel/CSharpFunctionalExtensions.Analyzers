using Microsoft.CodeAnalysis;
using Roslynator.Testing.CSharp;

namespace CSharpFunctionalExtensions.Analyzers.Tests.PreferImplicitCastingOfResult;

public class PreferImplicitCastingOfResultTests
    : AbstractCSharpDiagnosticVerifier<
        Analyzers.PreferImplicitCastingOfResult,
        PreferImplicitCastingOfResultCodeFixProvider
    >
{
    public override DiagnosticDescriptor Descriptor => Analyzers.PreferImplicitCastingOfResult.Rule;

    [Fact(Skip = "Work in progees")]
    public async Task Test_ReturnOfExplicitResult()
    {
        await VerifyDiagnosticAndFixAsync(
            AddContext("""return [|Result.Failure<int>("Could not find any id")|];"""),
            AddContext("""return "Could not find any id";"""),
            options: CSharpTestOptions()
        );
    }

    private CSharpTestOptions CSharpTestOptions()
    {
        var cSharpFunctionalExtensions = MetadataReference.CreateFromFile(
            typeof(CSharpFunctionalExtensions.Result).Assembly.Location
        );
        var cSharpTestOptions = Options.WithMetadataReferences(
            Options.MetadataReferences.Add(cSharpFunctionalExtensions)
        );
        return cSharpTestOptions;
    }

    private string AddContext(string testString) =>
        $$"""
          namespace CSharpFunctionalExtensions.Analyzers.Samples;

          public class Class1
          {
              public Result<int> Foo()
              {
                     {{testString}}
              }
          }

          """;
}
