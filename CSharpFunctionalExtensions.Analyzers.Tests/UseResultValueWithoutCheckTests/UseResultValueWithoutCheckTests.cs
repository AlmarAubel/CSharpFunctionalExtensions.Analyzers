using Microsoft.CodeAnalysis;
using Roslynator.Testing;
using Roslynator.Testing.CSharp;


namespace CSharpFunctionalExtensions.Analyzers.Tests.UseResultValueWithoutCheckTests;

public class UseResultValueWithoutCheckTests :  AbstractCSharpDiagnosticVerifier<UseResultValueWithoutCheck, DummyCodeFixProvider>
{
    public override DiagnosticDescriptor Descriptor => UseResultValueWithoutCheck.Rule;

    [Theory]
    [InlineData("if(!result.IsSuccess) Console.WriteLine( [|result.Value|]);")]
    [InlineData("if(result.IsSuccess == false) Console.WriteLine([|result.Value|]);")]
    [InlineData("if(result.IsSuccess == false || new Random().Next() > 1) Console.WriteLine([|result.Value|]);")]
    [InlineData("var x=  a > 0 ? [|result.Value|]: 0;")]
    public async Task Test_AccessValueWithinConditionalStament(string source)
    {
        await VerifyDiagnosticAsync(AddContext(source), options: CSharpTestOptions());
    }

    [Fact]
    public async Task Test_AccesValueWithoutCheck()
    {
        await VerifyDiagnosticAsync(AddContext("Console.WriteLine([|result.Value|]);"), options: CSharpTestOptions());
    }

    [Fact]
    public async Task TestNoDiagnostic_WhenCheckedResultAndReturned()
    {
        await VerifyNoDiagnosticAsync(
            AddContext($"""
                              if(!result.IsSuccess) return;
                              Console.WriteLine(result.Value);
                        """), options: CSharpTestOptions());
    }

    [Theory]
    [InlineData("if(result.IsSuccess) Console.WriteLine(result.Value);")]
    [InlineData("if(result.IsSuccess == true) Console.WriteLine(result.Value);")]
    [InlineData("if(result.IsSuccess && new Random().Next() > 1) Console.WriteLine(result.Value);")]
    [InlineData("var x=  result.IsSuccess ? result.Value: 0;")]
    public async Task TestNoDiagnostic_AccesValueOnResultObject_WithCheckIsSuccess(string source)
    {
        await VerifyNoDiagnosticAsync(
            AddContext(source), options: CSharpTestOptions());
    }

    [Theory]
    [InlineData("if(!result.IsFailure) Console.WriteLine(result.Value);")]
    [InlineData("if(result.IsFailure == false) Console.WriteLine(result.Value);")]
    [InlineData("if(!result.IsFailure && new Random().Next() > 1) Console.WriteLine(result.Value);")]
    [InlineData("""if(result.IsFailure || result.Value > 1) Console.WriteLine("foo");""")]
    [InlineData("var x = !result.IsFailure ? result.Value: 0;")]
    public async Task TestNoDiagnostic_AccesValueOnResultObject_WithCheckIsFailure(string source)
    {
        await VerifyNoDiagnosticAsync(AddContext(source), options: CSharpTestOptions());
    }
    
    [Fact]
    public async Task AccesValueOnResultObject_WithCComplexIsSuccess_ShouldFail()
    {
        await VerifyDiagnosticAsync(
            AddContext("""
                       if(result.IsSuccess || new Random().Next() > 1) Console.WriteLine([|result.Value|]);
                       if(result.IsFailure || new Random().Next() > 1) Console.WriteLine([|result.Value|]);
                       if(result.IsFailure && new Random().Next() > 1) Console.WriteLine([|result.Value|]);
                       """), options: CSharpTestOptions());
    }

    [Theory]
    [InlineData("if(result.IsFailure) Console.WriteLine([|result.Value|]);")]
    [InlineData(" var x=  result.IsFailure ? [|result.Value|]: 0;")]
    public async Task Test_AccessValueAfterCheckForFailure(string source)
    {
        await VerifyDiagnosticAsync(AddContext(source), options: CSharpTestOptions());
    }

    [Fact]
    public async Task AccesValueOnResultObject_WithcheckingIsFailure_ShouldPass()
    {
        await VerifyNoDiagnosticAsync(
            AddContext("""
                       if(!result.IsFailure) Console.WriteLine(result.Value);
                       var x =  !result.IsFailure ? result.Value: 0;
                       if (result.IsFailure) return;
                       var y = result.Value;
                       """),
            options: CSharpTestOptions());
    }


    private CSharpTestOptions CSharpTestOptions()
    {
        var cSharpFunctionalExtensions = MetadataReference.CreateFromFile(typeof(CSharpFunctionalExtensions.Result).Assembly.Location);
        var cSharpTestOptions = Options.WithMetadataReferences(Options.MetadataReferences.Add(cSharpFunctionalExtensions));
        return cSharpTestOptions;
    }

    private string AddContext(string testString) =>
        $$"""
          using System;
          using CSharpFunctionalExtensions;

          public class FunctionsWithResultObject
          {
              public void GetId(int a)
              {
                 var result = Result.Success(1);
                 {{testString}}
              }
          }
          """;
}
