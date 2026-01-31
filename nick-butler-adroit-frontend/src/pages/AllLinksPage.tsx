/**
 * All Links page — displays every shortened URL with virtual scrolling and search.
 * Uses react-window for efficient rendering of large lists and react-window-infinite-loader
 * to fetch pages of data on demand as the user scrolls. Supports debounced search filtering
 * and real-time updates via SignalR.
 */
import React, { useCallback, useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { List, RowComponentProps } from 'react-window';
import { useInfiniteLoader } from 'react-window-infinite-loader';
import { ShortUrlResult } from '../types';
import { getPagedUrls, deleteShortUrl, formatShortLink } from '../services/urlService';
import { ApiError } from '../services/ApiError';
import { subscribeToEvents, unsubscribeFromEvents, startConnection, stopConnection } from '../services/signalRService';
import UrlStatsDisplay from '../components/UrlStatsDisplay';

/** Number of items to fetch per API page request. */
const PAGE_SIZE = 50;

/** Fixed row height (px) for the virtual list — required by react-window for layout. */
const ROW_HEIGHT = 90;

/** Props passed to each Row component via react-window's rowProps. */
interface RowData {
  items: ShortUrlResult[];
  onDelete: (shortCode: string) => void;
}

/**
 * Virtual list row component.
 * Renders a single URL entry with its short link, long URL, stats, and delete button.
 * Shows a loading placeholder for rows that haven't been fetched yet.
 */
function Row({ index, style, items, onDelete }: RowComponentProps<RowData>) {
  const url = items[index];
  if (!url) {
    return (
      <div style={style} className="url-item all-links-row">
        <span className="loading-placeholder">Loading...</span>
      </div>
    );
  }

  return (
    <div style={style} className="url-item all-links-row">
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
    </div>
  );
}

const AllLinksPage: React.FC = () => {
  const [items, setItems] = useState<ShortUrlResult[]>([]);
  const [totalCount, setTotalCount] = useState<number>(0);
  const [error, setError] = useState<string | null>(null);
  const [listHeight, setListHeight] = useState<number>(0);
  const [search, setSearch] = useState<string>('');          // Raw input value
  const [activeSearch, setActiveSearch] = useState<string>(''); // Debounced search term
  const searchTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  /** Tracks which page chunks have been loaded to avoid duplicate API calls. */
  const loadedRangesRef = useRef<Set<number>>(new Set());

  /**
   * Callback ref that measures the available height for the virtual list container.
   * Recalculates on window resize so the list always fills the viewport.
   */
  const listContainerRef = useCallback((node: HTMLDivElement | null) => {
    if (!node) return;

    const measure = () => {
      const top = node.getBoundingClientRect().top;
      setListHeight(Math.max(200, window.innerHeight - top - 16));
    };

    measure();

    const onResize = () => measure();
    window.addEventListener('resize', onResize);

    const cleanup = () => window.removeEventListener('resize', onResize);
    (node as any).__cleanupResize = cleanup;
  }, []);

  /** Returns true if the row at the given index has already been loaded. */
  const isRowLoaded = useCallback((index: number) => index < items.length && !!items[index], [items]);

  /**
   * Fetches a range of rows from the API when the user scrolls to unloaded rows.
   * Uses page-key deduplication to prevent redundant requests for the same chunk.
   * On failure, removes the page key so the chunk can be retried.
   */
  const loadMoreRows = useCallback(async (startIndex: number, stopIndex: number) => {
    const offset = startIndex;
    const limit = stopIndex - startIndex + 1;

    // Deduplicate: skip if this page chunk has already been fetched
    const pageKey = Math.floor(offset / PAGE_SIZE);
    if (loadedRangesRef.current.has(pageKey)) return;
    loadedRangesRef.current.add(pageKey);

    try {
      const result = await getPagedUrls(offset, limit, activeSearch || undefined);
      setTotalCount(result.totalCount);
      // Merge new items into the sparse array at the correct offsets
      setItems(prev => {
        const newItems = [...prev];
        result.items.forEach((item, i) => {
          newItems[offset + i] = item;
        });
        return newItems;
      });
    } catch (err) {
      console.error('[AllLinksPage] Failed to fetch paged URLs', err);
      loadedRangesRef.current.delete(pageKey);
      setError('Failed to load URLs.');
    }
  }, [activeSearch]);

  // Reset and reload when the search term changes (activeSearch is debounced)
  useEffect(() => {
    setItems([]);
    setTotalCount(0);
    loadedRangesRef.current.clear();
    loadMoreRows(0, PAGE_SIZE - 1);
  }, [loadMoreRows]);

  /**
   * Debounced search handler — waits 300ms after the user stops typing
   * before triggering a new API search, reducing unnecessary requests.
   */
  const handleSearchChange = (value: string) => {
    setSearch(value);
    if (searchTimerRef.current) clearTimeout(searchTimerRef.current);
    searchTimerRef.current = setTimeout(() => {
      setActiveSearch(value.trim());
    }, 300);
  };

  // SignalR real-time event subscriptions (same pattern as HomePage)
  useEffect(() => {
    subscribeToEvents({
      // Increment total count so the virtual list knows a new item exists
      onUrlCreated: () => {
        setTotalCount(prev => prev + 1);
      },
      // Remove deleted item from the sparse array
      onUrlDeleted: (shortCode: string) => {
        setItems(prev => prev.filter(u => u?.shortCode !== shortCode));
        setTotalCount(prev => Math.max(0, prev - 1));
      },
      // Update click counts for the clicked short code and its long-URL siblings
      onUrlClicked: (shortCode: string, clickCount: number, longUrl: string, longUrlClickCount: number) => {
        setItems(prev =>
          prev.map(u => {
            if (!u) return u;
            if (u.shortCode === shortCode) {
              return { ...u, clickCount, longUrlClickCount };
            }
            if (u.longUrl === longUrl) {
              return { ...u, longUrlClickCount };
            }
            return u;
          })
        );
      },
    });

    const timer = setTimeout(() => {
      startConnection().catch((err) => console.error('[AllLinksPage] SignalR connection failed', err));
    }, 0);

    return () => {
      clearTimeout(timer);
      unsubscribeFromEvents();
      stopConnection().catch(() => {});
    };
  }, []);

  /**
   * Handles URL deletion. Removes the item from the sparse array and decrements the total count.
   * If the API returns 404, the URL was already deleted elsewhere — still remove it from the UI.
   */
  const handleDelete = async (shortCode: string) => {
    setError(null);
    try {
      await deleteShortUrl(shortCode);
      setItems(prev => prev.filter(u => u?.shortCode !== shortCode));
      setTotalCount(prev => Math.max(0, prev - 1));
    } catch (err) {
      if (err instanceof ApiError && err.status === 404) {
        setItems(prev => prev.filter(u => u?.shortCode !== shortCode));
        setTotalCount(prev => Math.max(0, prev - 1));
      } else {
        console.error('[AllLinksPage] Delete failed for', shortCode, err);
        setError(err instanceof Error ? err.message : 'Failed to delete URL.');
      }
    }
  };

  // Total item count drives virtual list height; use loaded count as fallback
  const itemCount = totalCount > 0 ? totalCount : items.length;

  // Infinite loader callback — triggers loadMoreRows when unloaded rows come into view
  const onRowsRendered = useInfiniteLoader({
    isRowLoaded,
    loadMoreRows,
    rowCount: itemCount,
  });

  return (
    <div className="App all-links-page">
      <div className="all-links-header">
        <Link to="/" className="back-link">Back to Home</Link>
        <h1>All Links</h1>
        <div className="search-bar">
          <input
            type="text"
            value={search}
            onChange={e => handleSearchChange(e.target.value)}
            placeholder="Search by long URL..."
            className="search-input"
            aria-label="Search by long URL"
          />
        </div>
        <p className="total-count">{totalCount} total links</p>
      </div>
      {error && (
        <div className="error-banner" role="alert">
          <span>{error}</span>
          <button className="dismiss-btn" onClick={() => setError(null)} aria-label="Dismiss error">
            x
          </button>
        </div>
      )}
      {itemCount === 0 ? (
        <p className="empty-message">{activeSearch ? 'No links match your search.' : 'No shortened URLs yet.'}</p>
      ) : (
        <div ref={listContainerRef} className="all-links-list-container">
          {listHeight > 0 && (
            <List
              rowComponent={Row}
              rowCount={itemCount}
              rowHeight={ROW_HEIGHT}
              rowProps={{ items, onDelete: handleDelete }}
              onRowsRendered={onRowsRendered}
              style={{ height: listHeight, width: '100%' }}
            />
          )}
        </div>
      )}
    </div>
  );
};

export default AllLinksPage;
