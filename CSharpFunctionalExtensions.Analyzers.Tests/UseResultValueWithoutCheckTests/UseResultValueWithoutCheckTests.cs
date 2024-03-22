using Microsoft.CodeAnalysis;
using Roslynator.Testing;
using Roslynator.Testing.CSharp;

namespace CSharpFunctionalExtensions.Analyzers.Tests.UseResultValueWithoutCheckTests;

public class UseResultValueWithoutCheckTests
    : AbstractCSharpDiagnosticVerifier<UseResultValueWithoutCheck.UseResultValueWithoutCheck, DummyCodeFixProvider>
{
    public override DiagnosticDescriptor Descriptor => UseResultValueWithoutCheck.UseResultValueWithoutCheck.Rule;

    [Theory]
    [InlineData("if(!result.IsSuccess) Console.WriteLine( [|result.Value|]);")]
    [InlineData("if(result.IsSuccess == false) Console.WriteLine([|result.Value|]);")]
    [InlineData("if(result.IsSuccess == false || new Random().Next() > 1) Console.WriteLine([|result.Value|]);")]
    [InlineData("if(result.IsFailure && [|result.Value|] > 0) Console.WriteLine(0);")]
    [InlineData("var x=  a > 0 ? [|result.Value|]: 0;")]
    public async Task Test_AccessValueWithinConditionalStament(string source)
    {
        await VerifyDiagnosticAsync(AddContext(source), options: CSharpTestOptions());
    }

    [Fact]
    public async Task Test_AccesValueWithoutCheck()
    {
        await VerifyDiagnosticAsync(
            AddContext("Console.WriteLine([|result.Value|]);", "Result.Success<int>(1)"),
            options: CSharpTestOptions()
        );
    }

    [Fact]
    public async Task TestNoDiagnostic_WhenCheckedResultAndReturned()
    {
        await VerifyNoDiagnosticAsync(
            AddContext(
                $"""
                              if(!result.IsSuccess) return;
                              Console.WriteLine(result.Value);
                        """,
                "Result.Success<int>(1)"
            ),
            options: CSharpTestOptions()
        );
    }

    [Theory]
    [InlineData("if(result.IsSuccess) Console.WriteLine(result.Value);")]
    [InlineData("if(result.IsSuccess == true) Console.WriteLine(result.Value);")]
    [InlineData("if(result.IsSuccess && new Random().Next() > 1) Console.WriteLine(result.Value);")]
    [InlineData("if(result is { IsSuccess: true, Value: > 1 }) Console.WriteLine(result.Value);")]
    [InlineData("var x = result.IsSuccess ? result.Value: 0;")]
    public async Task TestNoDiagnostic_AccesValueOnResultObject_WithCheckIsSuccess(string source)
    {
        await VerifyNoDiagnosticAsync(AddContext(source), options: CSharpTestOptions());
    }

    [Theory]
    [InlineData("if(!result.IsFailure) Console.WriteLine(result.Value);")]
    [InlineData("if(result.IsFailure == false) Console.WriteLine(result.Value);")]
    [InlineData("if(!result.IsFailure && new Random().Next() > 1) Console.WriteLine(result.Value);")]
    [InlineData("""if(result.IsFailure || result.Value > 1) Console.WriteLine("foo");""")]
    [InlineData("""if(result.IsFailure || result.Value == 1) Console.WriteLine("foo");""")]
    [InlineData("""if(!result.IsSuccess || result.Value > 1) Console.WriteLine("foo");""")]
    [InlineData("var x = !result.IsFailure ? result.Value: 0;")]
    public async Task TestNoDiagnostic_AccesValueOnResultObject_WithCheckIsFailure(string source)
    {
        await VerifyNoDiagnosticAsync(AddContext(source), options: CSharpTestOptions());
    }

    [Fact]
    public async Task AccesValueOnResultObject_WithCComplexIsSuccess_ShouldFail()
    {
        await VerifyDiagnosticAsync(
            AddContext(
                """
                       if(result.IsSuccess || new Random().Next() > 1) Console.WriteLine([|result.Value|]);
                       if(result.IsFailure || new Random().Next() > 1) Console.WriteLine([|result.Value|]);
                       if(result.IsFailure && new Random().Next() > 1) Console.WriteLine([|result.Value|]);
                       """
            ),
            options: CSharpTestOptions()
        );
    }

    [Theory]
    [InlineData("if(result.IsFailure) Console.WriteLine([|result.Value|]);")]
    [InlineData(" var x = result.IsFailure ? [|result.Value|]: 0;")]
    [InlineData(" var x = result.IsSuccess ? 0: [|result.Value|];")]
    public async Task Test_AccessValueAfterCheckForFailure(string source)
    {
        await VerifyDiagnosticAsync(AddContext(source), options: CSharpTestOptions());
    }

    [Fact]
    public async Task Test_AccessWithinReturnStatement()
    {
        await VerifyDiagnosticAsync(
            $$"""
                                        using System;
                                        using CSharpFunctionalExtensions;

                                        public class Class2
                                        {
                                            public int Test()
                                            {
                                                var y = Result.Success(1);
                                                if (y.IsFailure)
                                                    return [|y.Value|];
                                                
                                                return 1;
                                            }
                                        }
                                        """,
            options: CSharpTestOptions()
        );
    }
    
    [Fact]
    public async Task TestNoDiagnostic_AccessWithinUsingStatement()
    {
        await VerifyNoDiagnosticAsync(
            $$"""
                                        using System;
                                        using System.IO;
                                        using CSharpFunctionalExtensions;

                                        public class Class2
                                        {
                                            public void UsingStatementExample()
                                            {
                                                var result = Result.Success(1);
                                                if (result.IsFailure) return;
                                               
                                                using (var streamWriter = new StreamWriter("filePath"))
                                                {
                                                    streamWriter.Write(result.Value);
                                                }
                                            }
                                        }
                                        """,
            options: CSharpTestOptions()
        );
    }

    [Fact]
    public async Task Test_AccessWithinUsingStatement()
    {
        await VerifyDiagnosticAsync(
            $$"""
              using System;
              using System.IO;
              using CSharpFunctionalExtensions;

              public class Class2
              {
                  public void UsingStatementExample()
                  {
                      var result = Result.Success(1);
                      if (result.IsSuccess) return;
                     
                      using (var streamWriter = new StreamWriter("filePath"))
                      {
                          streamWriter.Write([|result.Value|]);
                      }
                  }
              }
              """,
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

    private string AddContext(string testString, string result = "Result.Success(1)") =>
        $$"""
          using System;
          using CSharpFunctionalExtensions;

          public class FunctionsWithResultObject
          {
              public void GetId(int a)
              {
                 var result = {{result}};
                 {{testString}}
              }
          }
          """;
}
