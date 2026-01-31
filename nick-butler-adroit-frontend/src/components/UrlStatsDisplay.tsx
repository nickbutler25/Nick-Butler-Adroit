/**
 * Inline statistics display for a single shortened URL.
 * Shows the per-code click count, aggregate clicks for the destination URL,
 * and the creation date. Used in both UrlList and AllLinksPage row components.
 */
import React from 'react';

interface UrlStatsDisplayProps {
  /** Click count for this specific short code. */
  clickCount: number;
  /** Aggregate clicks across all short codes pointing to the same long URL. */
  longUrlClickCount: number;
  /** ISO 8601 creation timestamp (formatted to locale date string for display). */
  createdAt: string;
}

const UrlStatsDisplay: React.FC<UrlStatsDisplayProps> = ({ clickCount, longUrlClickCount, createdAt }) => {
  return (
    <span className="url-stats">
      Clicks: {clickCount} | Total for URL: {longUrlClickCount} | Created: {new Date(createdAt).toLocaleDateString()}
    </span>
  );
};

export default UrlStatsDisplay;
