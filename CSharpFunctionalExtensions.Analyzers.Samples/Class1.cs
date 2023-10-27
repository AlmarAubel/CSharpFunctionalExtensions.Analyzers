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

        if (y.IsFailure) return y.Value;

        return 1;
    }

    public int PatternMatching()
    {
        var result = Result.Success(1);
        var id = result switch
        {
            { IsSuccess: true }=> result.Value,
            { Error: "eror" } => 0,
            _ => throw new ArgumentOutOfRangeException()
        };

        switch (result.IsSuccess)
        {
            case true:
                return result.Value;
            case false:
                break;
        }

        return id;
    }
}
