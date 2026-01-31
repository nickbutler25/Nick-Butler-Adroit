import React from 'react';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { vi, type Mocked, type Mock } from 'vitest';
import App from './App';

vi.mock('./services/urlService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('./services/urlService')>();
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
vi.mock('./services/signalRService', () => ({
  startConnection: vi.fn(() => Promise.resolve()),
  stopConnection: vi.fn(() => Promise.resolve()),
  subscribeToEvents: vi.fn(),
  unsubscribeFromEvents: vi.fn(),
}));

vi.mock('react-window', async (importOriginal) => {
  const actual = await importOriginal<typeof import('react-window')>();
  return {
    ...actual,
    List: ({ rowComponent: RowComponent, rowCount, rowHeight, rowProps, style }: any) => {
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

vi.mock('react-window-infinite-loader', () => ({
  useInfiniteLoader: () => () => {},
  InfiniteLoader: ({ children }: any) => children({ onRowsRendered: () => {} }),
}));

const mockedService = (await import('./services/urlService')) as Mocked<typeof import('./services/urlService')>;
const mockedSignalR = (await import('./services/signalRService')) as unknown as {
  startConnection: Mock;
  stopConnection: Mock;
  subscribeToEvents: Mock;
  unsubscribeFromEvents: Mock;
};

beforeEach(() => {
  vi.clearAllMocks();
  mockedService.getRecentUrls.mockResolvedValue([]);
  mockedService.getAllUrls.mockResolvedValue([]);
  mockedService.getPagedUrls.mockResolvedValue({ items: [], totalCount: 0 });
  mockedSignalR.startConnection.mockReturnValue(Promise.resolve());
  mockedSignalR.stopConnection.mockReturnValue(Promise.resolve());
});

test('renders HomePage at root route', async () => {
  render(<App />);
  const heading = await screen.findByRole('heading', { level: 1, name: /adroit url shortener/i });
  expect(heading).toBeInTheDocument();
});

test('renders AllLinksPage at /all route', async () => {
  // AllLinksPage is rendered inside App which uses BrowserRouter.
  // We need to render AllLinksPage directly with MemoryRouter for route testing.
  const { default: AllLinksPage } = await import('./pages/AllLinksPage');
  render(
    <MemoryRouter initialEntries={['/all']}>
      <AllLinksPage />
    </MemoryRouter>
  );
  const heading = await screen.findByRole('heading', { level: 1, name: /all links/i });
  expect(heading).toBeInTheDocument();
});
