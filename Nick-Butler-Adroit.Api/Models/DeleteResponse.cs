namespace NickButlerAdroit.Api.Models;

/// <summary>
/// Response returned on successful URL deletion.
/// </summary>
/// <param name="Message">A confirmation message indicating which short code was deleted.</param>
public record DeleteResponse(string Message);
