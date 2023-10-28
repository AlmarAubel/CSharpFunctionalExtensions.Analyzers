# CSharpFunctionalExtensions.Analyzers

Unofficial code analyzers for CSharpFunctionalExtensions. This project aims to provide code analyzers to identify potential issues or misuse of the `Result` object from the CSharpFunctionalExtensions library.

## Features

- Identifies scenarios where the `IsSuccess` property is not checked before accessing the `Value`.
- Verifies that logic is terminated  when `IsFailure` is true before before accessing the `Value`.
- Supports a variety of control flow structures, including `if` statements, ternary operators, and now C# switch expressions.

## Getting Started

To install the analyzers, you can add the NuGet package to your project:

```bash
Install-Package CSharpFunctionalExtensions.Analyzers
```

## Examples

Here are some examples to show what this analyzer can catch.

### Example 1: Accessing `Value` without checking `IsSuccess`

```csharp
public void DoSomething(Result<int> result)
{
    var x = result.Value;  // Analyzer will report a warning here
}
```

### Example 2: Using `IsSuccess` in Switch Expressions

```csharp
public IActionResult ProcessResult(Result<int> result)
{
    return result switch
    {
        { Error: var err } when err == Error.NotFound => NotFound(),
        _ => Ok(result.Value)  // Analyzer will report a warning here
    };
}
```

### Example 3: Not breaking out when IsFailure is true
```csharp
public void DoSomething(Result<int> result)
{
    if (result.IsFailure)
    {
        // logic here
    }
    var x= result.Value; // Analyzer will report a warning here
}
```

Will be fixed if you return ;
 ```csharp
public void DoSomething(Result<int> result)
{
    if (result.IsFailure)
    {
        return
    }
    var x= result.Value; // Analyzer will report a warning here
}
```

## Changelog
See [changelog](changelog.md)

## Contributing

Contributions are welcome! Please feel free to open an issue or submit a pull request.

## License

MIT
