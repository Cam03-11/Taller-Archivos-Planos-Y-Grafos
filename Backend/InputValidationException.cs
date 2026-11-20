namespace DeliveryRoutes.Backend;

public class InputValidationException : Exception
{
    public InputValidationException(string title, IReadOnlyList<string> errors)
        : base(title + Environment.NewLine + string.Join(Environment.NewLine, errors))
    {
        Title = title;
        Errors = errors;
    }

    public InputValidationException(string title, string error)
        : this(title, new[] { error })
    {
    }

    public string Title { get; }

    public IReadOnlyList<string> Errors { get; }
}
