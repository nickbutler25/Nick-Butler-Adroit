import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { vi, type Mocked, type Mock } from 'vitest';
import AllLinksPage from './AllLinksPage';
import * as urlService from '../services/urlService';

vi.mock('../services/urlService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../services/urlService')>();
  return {
    ...actual,
    getAllUrls: vi.fn(),
    getRecentUrls: vi.fn(),
    getPagedUrls: vi.fn(),
    createShortUrl: vi.fn(),
    deleteShortUrl: vi.fn(),
    resolveShortUrl: vi.fn(),
    getUrlStats: vi.fn(),
  };
});
vi.mock('../services/signalRService', () => ({
  startConnection: vi.fn(() => Promise.resolve()),
  stopConnection: vi.fn(() => Promise.resolve()),
  subscribeToEvents: vi.fn(),
  unsubscribeFromEvents: vi.fn(),
}));

vi.mock('react-window', async (importOriginal) => {
  const actual = await importOriginal<typeof import('react-window')>();
  return {
    ...actual,
    List: ({ rowComponent: RowComponent, rowCount, rowHeight, rowProps, onRowsRendered, style }: any) => {
      const items = [];
      for (let i = 0; i < Math.min(rowCount, 20); i++) {
        items.push(
          <div key={i} style={{ height: rowHeight }}>
            <RowComponent index={i} style={{}} ariaAttributes={{}} {...rowProps} />
          </div>
        );
      }
      return <div data-testid="virtual-list" style={style}>{items}</div>;
    },
  };
});

vi.mock('react-window-infinite-loader', async (importOriginal) => {
  const actual = await importOriginal<typeof import('react-window-infinite-loader')>();
  return {
    ...actual,
    useInfiniteLoader: () => () => {},
    InfiniteLoader: ({ children }: any) => children({ onRowsRendered: () => {} }),
  };
});

const mockedService = urlService as Mocked<typeof urlService>;
const mockedSignalR = (await import('../services/signalRService')) as unknown as {
  startConnection: Mock;
  stopConnection: Mock;
  subscribeToEvents: Mock;
  unsubscribeFromEvents: Mock;
};

beforeEach(() => {
  vi.clearAllMocks();
  mockedService.getPagedUrls.mockResolvedValue({ items: [], totalCount: 0 });
  mockedSignalR.startConnection.mockReturnValue(Promise.resolve());
  mockedSignalR.stopConnection.mockReturnValue(Promise.resolve());
});

function renderAllLinksPage() {
  return render(
    <MemoryRouter>
      <AllLinksPage />
    </MemoryRouter>
  );
}

test('renders All Links heading', async () => {
  renderAllLinksPage();
  expect(screen.getByRole('heading', { level: 1, name: /all links/i })).toBeInTheDocument();
  await waitFor(() => expect(mockedService.getPagedUrls).toHaveBeenCalled());
});

test('shows Back to Home link', async () => {
  renderAllLinksPage();
  expect(screen.getByText('Back to Home')).toBeInTheDocument();
  await waitFor(() => expect(mockedService.getPagedUrls).toHaveBeenCalled());
});

test('shows empty message when no URLs', async () => {
  renderAllLinksPage();

  await waitFor(() => {
    expect(screen.getByText('No shortened URLs yet.')).toBeInTheDocument();
  });
});

test('shows total count', async () => {
  mockedService.getPagedUrls.mockResolvedValue({
    items: [
      {
        shortCode: 'all001',
        longUrl: 'https://example.com',
        clickCount: 0,
        longUrlClickCount: 0,
        createdAt: '2025-01-01T00:00:00Z',
      },
    ],
    totalCount: 1,
  });

  renderAllLinksPage();

  await waitFor(() => {
    expect(screen.getByText('1 total links')).toBeInTheDocument();
  });
});

test('loads initial page of URLs', async () => {
  mockedService.getPagedUrls.mockResolvedValue({
    items: [
      {
        shortCode: 'loaded1',
        longUrl: 'https://loaded.com',
        clickCount: 5,
        longUrlClickCount: 5,
        createdAt: '2025-01-01T00:00:00Z',
      },
    ],
    totalCount: 1,
  });

  renderAllLinksPage();

  await waitFor(() => {
    expect(screen.getByText('https://localhost:7055/loaded1')).toBeInTheDocument();
  });
});
