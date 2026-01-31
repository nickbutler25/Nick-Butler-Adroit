/**
 * SignalR real-time connection service.
 * Manages a singleton WebSocket connection to the backend UrlHub (/hubs/urls).
 * Provides subscribe/unsubscribe helpers for the three event types:
 *   - UrlCreated: a new short URL was created
 *   - UrlClicked: a short URL was clicked (includes updated click counts)
 *   - UrlDeleted: a short URL was deleted
 *
 * The connection uses automatic reconnect so it recovers from transient network issues.
 */
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { ShortUrlResult } from '../types';

/** Callback signatures for the three real-time events broadcast by the backend. */
export interface SignalRCallbacks {
  onUrlCreated: (result: ShortUrlResult) => void;
  onUrlDeleted: (shortCode: string) => void;
  onUrlClicked: (shortCode: string, clickCount: number, longUrl: string, longUrlClickCount: number) => void;
}

/** SignalR hub URL, configurable via environment variable for different deployment targets. */
const HUB_URL = import.meta.env.VITE_SIGNALR_URL || 'https://localhost:7055/hubs/urls';

/** Singleton connection instance — shared across all components. */
let connection: HubConnection | null = null;

/** Creates a new SignalR connection with automatic reconnect and info-level logging. */
function buildConnection(): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(HUB_URL)
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();
}

/** Returns the singleton connection, creating it lazily on first access. */
export function getConnection(): HubConnection {
  if (!connection) {
    connection = buildConnection();
  }
  return connection;
}

/** Starts the SignalR connection. Should be called once during component mount. */
export async function startConnection(): Promise<void> {
  const conn = getConnection();
  try {
    await conn.start();
    console.info('[SignalR] Connection started');
  } catch (err) {
    console.warn('[SignalR] Connection failed', err);
    throw err;
  }
}

/**
 * Stops the SignalR connection and clears the singleton reference.
 * Called during component unmount to clean up resources.
 */
export async function stopConnection(): Promise<void> {
  const conn = connection;
  connection = null;
  if (conn) {
    await conn.stop();
    console.info('[SignalR] Connection stopped');
  }
}

/**
 * Registers event handlers on the SignalR connection.
 * Must be called before startConnection() so events aren't missed.
 */
export function subscribeToEvents(callbacks: SignalRCallbacks): void {
  const conn = getConnection();
  conn.on('UrlCreated', callbacks.onUrlCreated);
  conn.on('UrlDeleted', callbacks.onUrlDeleted);
  conn.on('UrlClicked', callbacks.onUrlClicked);
}

/** Removes all event handlers from the connection. Called during component cleanup. */
export function unsubscribeFromEvents(): void {
  if (connection) {
    connection.off('UrlCreated');
    connection.off('UrlDeleted');
    connection.off('UrlClicked');
  }
}
