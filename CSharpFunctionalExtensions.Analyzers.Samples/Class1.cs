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
    }

    public void Bar()
    {
        Result<int> failure = Result.Failure<int>("Could not find any id");
        Result<int> success = Result.Success<int>(1);
    }
}
