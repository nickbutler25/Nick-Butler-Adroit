using Microsoft.AspNetCore.SignalR;

namespace NickButlerAdroit.Api.Hubs;

/// <summary>
/// SignalR hub for real-time URL event notifications.
/// Mapped to /hubs/urls in the middleware pipeline.
///
/// The hub itself has no server-side methods — it only broadcasts events from the service layer
/// via IHubContext&lt;UrlHub&gt;. Connected clients receive three event types:
///   - UrlCreated: fired when a new short URL is created
///   - UrlClicked: fired when a short URL is resolved (click counter updated)
///   - UrlDeleted: fired when a short URL is deleted
///
/// This enables the React frontend to update the URL list and click counts in real time
/// without polling the API.
/// </summary>
public class UrlHub : Hub;
