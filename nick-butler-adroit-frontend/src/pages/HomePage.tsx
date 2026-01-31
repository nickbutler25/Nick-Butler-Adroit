/**
 * Home page — primary user interface for the URL shortener.
 * Contains the shortening form and a list of the 5 most recently created links.
 * Subscribes to SignalR events for real-time updates (new URLs, clicks, deletions).
 * Shows a warning banner if the backend API is unreachable.
 */
import React, { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { ShortUrlResult } from '../types';
import { getRecentUrls, deleteShortUrl } from '../services/urlService';
import { ApiError } from '../services/ApiError';
import { subscribeToEvents, unsubscribeFromEvents, startConnection, stopConnection } from '../services/signalRService';
import UrlShortenerForm from '../components/UrlShortenerForm';
import UrlList from '../components/UrlList';

/** Maximum number of recent links to display on the home page. */
const MAX_RECENT = 5;

const HomePage: React.FC = () => {
  const [urls, setUrls] = useState<ShortUrlResult[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [apiAvailable, setApiAvailable] = useState<boolean>(true);

  const clearError = useCallback(() => setError(null), []);

  // Auto-dismiss error messages after 5 seconds
  useEffect(() => {
    if (error === null) return;
    const timer = setTimeout(clearError, 5000);
    return () => clearTimeout(timer);
  }, [error, clearError]);

  // Fetch recent URLs on mount; set apiAvailable=false if the backend is down
  useEffect(() => {
    const loadUrls = async () => {
      try {
        const results = await getRecentUrls(MAX_RECENT);
        setUrls(results);
        setApiAvailable(true);
      } catch (err) {
        console.warn('[HomePage] Initial URL fetch failed — API may be unavailable', err);
        setApiAvailable(false);
      }
    };
    loadUrls();
  }, []);

  // Set up SignalR real-time event subscriptions.
  // Events are subscribed before the connection starts to avoid missing events.
  useEffect(() => {
    subscribeToEvents({
      // When another client creates a URL, prepend it to our list (if not already present)
      onUrlCreated: (result: ShortUrlResult) => {
        setUrls(prev => {
          if (prev.some(u => u.shortCode === result.shortCode)) return prev;
          const updated = [result, ...prev];
          return updated.slice(0, MAX_RECENT);
        });
      },
      // When a URL is deleted (by any client), remove it from the list
      onUrlDeleted: (shortCode: string) => {
        setUrls(prev => prev.filter(u => u.shortCode !== shortCode));
      },
      // When a URL is clicked, update its click counts (and sibling long-URL counts)
      onUrlClicked: (shortCode: string, clickCount: number, longUrl: string, longUrlClickCount: number) => {
        setUrls(prev =>
          prev.map(u => {
            if (u.shortCode === shortCode) {
              return { ...u, clickCount, longUrlClickCount };
            }
            // Update aggregate count for other short codes sharing the same long URL
            if (u.longUrl === longUrl) {
              return { ...u, longUrlClickCount };
            }
            return u;
          })
        );
      },
    });

    // Start the WebSocket connection (deferred to next tick to avoid blocking render)
    const timer = setTimeout(() => {
      startConnection().catch((err) => console.error('[HomePage] SignalR connection failed', err));
    }, 0);

    // Cleanup: unsubscribe and disconnect on unmount
    return () => {
      clearTimeout(timer);
      unsubscribeFromEvents();
      stopConnection().catch(() => {});
    };
  }, []);

  /**
   * Handles successful URL creation from the form.
   * Adds the new URL to the top of the recent list (deduped, capped at MAX_RECENT).
   */
  const handleUrlCreated = (result: ShortUrlResult) => {
    setError(null);
    setApiAvailable(true);
    setUrls(prev => {
      if (prev.some(u => u.shortCode === result.shortCode)) return prev;
      const updated = [result, ...prev];
      return updated.slice(0, MAX_RECENT);
    });
  };

  /**
   * Handles URL deletion.
   * Optimistically removes the URL from the list. If the API returns 404,
   * the URL was already deleted (possibly by another client), so we still remove it.
   */
  const handleDelete = async (shortCode: string) => {
    setError(null);
    try {
      await deleteShortUrl(shortCode);
      setUrls(prev => prev.filter(u => u.shortCode !== shortCode));
    } catch (err) {
      if (err instanceof ApiError && err.status === 404) {
        // Already deleted — remove from UI silently
        setUrls(prev => prev.filter(u => u.shortCode !== shortCode));
      } else {
        console.error('[HomePage] Delete failed for', shortCode, err);
        setError(err instanceof Error ? err.message : 'Failed to delete URL.');
      }
    }
  };

  return (
    <div className="App home-page">
      <h1>Adroit URL Shortener</h1>
      {!apiAvailable && (
        <div className="api-unavailable-banner" role="alert">
          Unable to connect to the API. Please ensure the backend server is running.
        </div>
      )}
      <UrlShortenerForm onUrlCreated={handleUrlCreated} />
      <h2>Your Recent Links</h2>
      <UrlList
        urls={urls}
        onDelete={handleDelete}
        error={error}
        onDismissError={clearError}
      />
      <div className="view-all-link">
        <Link to="/all">View All Links</Link>
      </div>
    </div>
  );
};

export default HomePage;
