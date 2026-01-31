/**
 * Displays a list of shortened URLs with click statistics and delete buttons.
 * Shows an optional error banner (auto-dismissible) and an empty-state message.
 * Used on the HomePage to show recent links; the AllLinksPage uses its own
 * virtualized list for performance with large datasets.
 */
import React from 'react';
import { ShortUrlResult } from '../types';
import { formatShortLink } from '../services/urlService';
import UrlStatsDisplay from './UrlStatsDisplay';

interface UrlListProps {
  /** Array of URL results to display. */
  urls: ShortUrlResult[];
  /** Callback when the user clicks "Delete" on a URL. */
  onDelete: (shortCode: string) => void;
  /** Optional error message to display in a banner above the list. */
  error?: string | null;
  /** Callback to dismiss the error banner. */
  onDismissError?: () => void;
}

const UrlList: React.FC<UrlListProps> = ({ urls, onDelete, error, onDismissError }) => {
  return (
    <>
      {error && (
        <div className="error-banner" role="alert">
          <span>{error}</span>
          {onDismissError && (
            <button className="dismiss-btn" onClick={onDismissError} aria-label="Dismiss error">
              ×
            </button>
          )}
        </div>
      )}
      {urls.length === 0 ? (
        <p className="empty-message">No shortened URLs yet.</p>
      ) : (
        <ul className="url-list">
          {urls.map(url => (
            <li key={url.shortCode} className="url-item">
              <div className="url-info">
                <a
                  href={formatShortLink(url.shortCode)}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="short-link"
                >
                  <strong>{formatShortLink(url.shortCode)}</strong>
                </a>
                <span className="long-url">{url.longUrl}</span>
                <UrlStatsDisplay clickCount={url.clickCount} longUrlClickCount={url.longUrlClickCount} createdAt={url.createdAt} />
              </div>
              <div className="url-actions">
                <button onClick={() => onDelete(url.shortCode)} className="delete-btn">Delete</button>
              </div>
            </li>
          ))}
        </ul>
      )}
    </>
  );
};

export default UrlList;
