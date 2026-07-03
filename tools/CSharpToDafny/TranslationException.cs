namespace Wallymathieu.Auctions.Tools.CSharpToDafny;

/// <summary>Raised when a contract expression falls outside the translatable subset.</summary>
internal sealed class TranslationException : Exception
{
    public TranslationException(string message) : base(message)
    {
    }

    public TranslationException()
    {
    }

    public TranslationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
