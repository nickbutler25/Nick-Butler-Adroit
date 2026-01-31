namespace NickButlerAdroit.Api.Exceptions;

/// <summary>
/// Thrown when attempting to create a short URL with a code that already exists.
/// This can happen when a user-provided custom alias collides with an existing one,
/// or (rarely) when the random code generator produces a duplicate after all retry attempts.
/// Caught by the controller and returned as HTTP 409 Conflict.
/// </summary>
public class DuplicateShortCodeException : Exception
{
    /// <summary>The short code that caused the collision.</summary>
    public string ShortCode { get; }

    public DuplicateShortCodeException(string shortCode)
        : base($"Short code '{shortCode}' already exists.")
    {
        ShortCode = shortCode;
    }
}
