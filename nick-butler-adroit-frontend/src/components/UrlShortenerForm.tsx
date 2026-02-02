/**
 * URL shortening form component.
 * Provides inputs for the long URL and an optional custom alias,
 * validates client-side before submitting, and calls the API to create a short URL.
 * On success, notifies the parent via onUrlCreated and resets the form.
 */
import React, { useState } from 'react';
import { ShortUrlResult } from '../types';
import { createShortUrl, getShortLinkDomain } from '../services/urlService';

interface UrlShortenerFormProps {
  /** Callback invoked when a short URL is successfully created. */
  onUrlCreated: (result: ShortUrlResult) => void;
}

const UrlShortenerForm: React.FC<UrlShortenerFormProps> = ({ onUrlCreated }) => {
  const [longUrl, setLongUrl] = useState<string>('');
  const [customCode, setCustomCode] = useState<string>('');
  const [error, setError] = useState<string>('');

  /**
   * Client-side URL validation — ensures the input is a well-formed URL
   * with an HTTP or HTTPS protocol. Mirrors the backend validation to
   * provide instant feedback without a network round-trip.
   */
  const isValidUrl = (value: string): boolean => {
    try {
      const url = new URL(value);
      return url.protocol === 'http:' || url.protocol === 'https:';
    } catch {
      return false;
    }
  };

  /**
   * Form submission handler.
   * Validates inputs client-side, then calls the API to create the short URL.
   * On success: resets the form and notifies the parent component.
   * On failure: displays the error message from the API or a fallback.
   */
  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');

    // Client-side validation (matches backend rules)
    if (!isValidUrl(longUrl)) {
      setError('Please enter a valid URL starting with http:// or https://');
      return;
    }

    if (customCode && customCode.length < 5) {
      setError('Alias must be at least 5 characters.');
      return;
    }

    if (customCode && !/^[a-zA-Z0-9]+$/.test(customCode)) {
      setError('Alias must contain only letters and numbers.');
      return;
    }

    try {
      const result = await createShortUrl(longUrl, customCode || undefined);
      onUrlCreated(result);
      setLongUrl('');
      setCustomCode('');
    } catch (err) {
      console.error('[UrlShortenerForm] Create failed:', err);
      setError(err instanceof Error ? err.message : 'An error occurred');
    }
  };

  return (
    <form onSubmit={handleSubmit} className="url-form">
      <h2 className="form-title">
        <svg className="form-title-icon" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <path d="M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71" />
          <path d="M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71" />
        </svg>
        Shorten a Link
      </h2>

      <div className="form-group">
        <label htmlFor="longUrl" className="field-label">
          <svg className="field-icon" width="14" height="14" viewBox="0 0 24 24" fill="currentColor">
            <path d="M12 2L4 7v2h16V7L12 2zM5 11v8l7 3 7-3v-8l-7 3-7-3z" />
          </svg>
          Long URL <span className="required">*</span>
        </label>
        <input
          id="longUrl"
          type="text"
          value={longUrl}
          onChange={e => setLongUrl(e.target.value)}
          placeholder="Paste long URL here"
          required
          className="input-long-url"
        />
      </div>

      <div className="domain-alias-row">
        <div className="domain-group">
          <label htmlFor="domain" className="field-label">
            <svg className="field-icon" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <circle cx="12" cy="12" r="10" />
              <line x1="2" y1="12" x2="22" y2="12" />
              <path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z" />
            </svg>
            Domain
          </label>
          <select id="domain" className="input-domain" disabled>
            <option>{getShortLinkDomain()}</option>
          </select>
        </div>

        <span className="domain-alias-separator">/</span>

        <div className="alias-group">
          <label htmlFor="customCode" className="field-label">
            <svg className="field-icon" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M17 3a2.828 2.828 0 1 1 4 4L7.5 20.5 2 22l1.5-5.5L17 3z" />
            </svg>
            Alias (optional)
          </label>
          <input
            id="customCode"
            type="text"
            value={customCode}
            onChange={e => setCustomCode(e.target.value)}
            placeholder="Add alias here"
            className="input-alias"
          />
          <span className="hint-text">Must be at least 5 letters or numbers</span>
        </div>
      </div>

      {error && <p className="error">{error}</p>}

      <button type="submit" className="submit-btn">Shorten Link</button>
    </form>
  );
};

export default UrlShortenerForm;
