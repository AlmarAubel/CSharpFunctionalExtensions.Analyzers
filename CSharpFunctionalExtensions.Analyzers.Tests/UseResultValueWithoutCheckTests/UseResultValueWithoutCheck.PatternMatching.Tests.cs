using Microsoft.CodeAnalysis;
using Roslynator.Testing;
using Roslynator.Testing.CSharp;

namespace CSharpFunctionalExtensions.Analyzers.Tests.UseResultValueWithoutCheckTests;

public class UseResultValueWithoutCheckPatternMatchingTests
    : AbstractCSharpDiagnosticVerifier<UseResultValueWithoutCheck.UseResultValueWithoutCheck, DummyCodeFixProvider>
{
    public override DiagnosticDescriptor Descriptor => UseResultValueWithoutCheck.UseResultValueWithoutCheck.Rule;
    
    [Theory]
    [InlineData(" { IsSuccess: true } => result.Value")]
    [InlineData(" { IsFailure: false } => result.Value")]
    public async Task TestNoDiagnostics_AccessValueAfterCheck(string source)
    {
        await VerifyNoDiagnosticAsync(AddPatternMatchingContext(source), options: CSharpTestOptions());
    }
    
    [Theory]
    [InlineData(" { IsSuccess: false } => [|result.Value|]")]
    [InlineData(" { IsFailure: true } => [|result.Value|]")]
    public async Task Test_AccessValueAfterIncorrectCheck(string source)
    {
        await VerifyDiagnosticAsync(AddPatternMatchingContext(source), options: CSharpTestOptions());
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

    
    private string AddPatternMatchingContext(string testString, string result = "Result.Success(1)") =>
        $$"""
          using System;
          using CSharpFunctionalExtensions;

          public class FunctionsWithResultObject
          {
              public int GetId(int a)
              {
                var result = {{result}};
                return result switch
                 {
                     {{testString}},
                     { Error: var err } when err == "error" => 0,
                     _ => 0
                 };
              }
          }
          """;
}
