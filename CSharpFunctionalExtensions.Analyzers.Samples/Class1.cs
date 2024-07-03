namespace CSharpFunctionalExtensions.Analyzers.Samples;

public class Class1
{
    public Result<int> Foo()
    {
        return Result.Success<int>(1);
    }

    public Result<int> Foo2()
    {
        return Result.Success(1);
    }

    public Result<int> Foo3()
    {
        return Result.Failure<int>("Could not find any id");
    }

    public void Foo4()
    {
        var a = 1;
        if (1 == 1 && a > 2)
        {
            var b = 2223;
        }

        var y = Result.Success(1);
        int? aa = null;
        if (y.IsFailure)
            Console.Write(y.Value);
    }

    public void Bar()
    {
        Result<int> failure = Result.Failure<int>("Could not find any id");
        Result<int> success = Result.Success<int>(1);
    }
}

public class Class2
{
    public int Test()
    {
        var y = Result.Success(1);

        if (!y.IsSuccess || y.Value > 0 )  return 0;
        
        return 1;
    }

    public int Test2()
    {
        var y = Result.Success(1);
        var x =  y.IsSuccess ? y.Value : 0;
        var foo = y.Value;
        return y.IsFailure ? 0 : y.Value;
    }
    public int PatternMatching()
    {
        var result = Result.Success(1);
        var id = result switch
        {
            { IsSuccess: true, Value: 1 }=> result.Value,
            { Error: "eror", Value: 1  } => 0,
            _ => throw new ArgumentOutOfRangeException()
        };
        
        var x = result.IsFailure? 0: result.Value;
        switch (result.IsSuccess)
        {
            case true:
                return result.Value;
            case false:
                break;
        }

        return id;
    }

    public void UsingStatementExample()
    {
        var result = Result.Success(1);
        if (result.IsFailure) return;
        //var resultMessage = !result.IsSuccess ? $"{result.Value} success." : "Failed.";
        using (var streamWriter = new StreamWriter("filePath"))
        {
            streamWriter.Write(result.Value);
        }
    }
    
    public void CombinedCheckExample()
    {
        var result = Result.Success(1);
        //if (result.IsFailure || result.Value == 1 ) return;
        if (result is { IsSuccess: true, Value: > 1 })
        {
            Console.WriteLine("foo" + result.Value);
        }
        
        // if (result.IsSuccess && result.Value == 1)
        // {
        //     Console.WriteLine("foo" + result.Value);
        // }
       
      
    }
    
    
}
