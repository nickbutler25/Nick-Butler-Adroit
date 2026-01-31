import React from 'react';
import { render, screen, fireEvent, waitFor, act } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { vi, type Mocked, type Mock } from 'vitest';
import HomePage from './HomePage';
import * as urlService from '../services/urlService';
import { ApiError } from '../services/ApiError';
import type { SignalRCallbacks } from '../services/signalRService';

let capturedCallbacks: SignalRCallbacks | null = null;

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
  subscribeToEvents: vi.fn((callbacks: any) => {
    capturedCallbacks = callbacks;
  }),
  unsubscribeFromEvents: vi.fn(),
}));

const mockedService = urlService as Mocked<typeof urlService>;
const mockedSignalR = (await import('../services/signalRService')) as unknown as {
  startConnection: Mock;
  stopConnection: Mock;
  subscribeToEvents: Mock;
  unsubscribeFromEvents: Mock;
};

beforeEach(() => {
  vi.clearAllMocks();
  capturedCallbacks = null;
  mockedService.getRecentUrls.mockResolvedValue([]);
  mockedSignalR.startConnection.mockReturnValue(Promise.resolve());
  mockedSignalR.stopConnection.mockReturnValue(Promise.resolve());
  mockedSignalR.subscribeToEvents.mockImplementation((callbacks: any) => {
    capturedCallbacks = callbacks;
  });
});

function renderHomePage() {
  return render(
    <MemoryRouter>
      <HomePage />
    </MemoryRouter>
  );
}

test('renders heading and form', async () => {
  renderHomePage();
  expect(screen.getByRole('heading', { level: 1, name: /adroit url shortener/i })).toBeInTheDocument();
  await waitFor(() => expect(mockedService.getRecentUrls).toHaveBeenCalledWith(5));
});

test('shows API unavailable banner when backend is down', async () => {
  mockedService.getRecentUrls.mockRejectedValue(new ApiError(0, 'Network error'));

  renderHomePage();

  await waitFor(() => {
    expect(screen.getByText(/unable to connect to the api/i)).toBeInTheDocument();
  });
});

test('loads recent URLs on mount', async () => {
  mockedService.getRecentUrls.mockResolvedValue([
    {
      shortCode: 'recent1',
      longUrl: 'https://example.com',
      clickCount: 3,
      longUrlClickCount: 3,
      createdAt: '2025-01-01T00:00:00Z',
    },
  ]);

  renderHomePage();

  await waitFor(() => {
    expect(screen.getByText('https://localhost:7055/recent1')).toBeInTheDocument();
  });
});

test('shows View All Links link', async () => {
  renderHomePage();
  await waitFor(() => expect(mockedService.getRecentUrls).toHaveBeenCalled());
  expect(screen.getByText('View All Links')).toBeInTheDocument();
});

test('adds URL from SignalR and caps at 5 items', async () => {
  const existing = Array.from({ length: 5 }, (_, i) => ({
    shortCode: `code${i}`,
    longUrl: `https://example${i}.com`,
    clickCount: 0,
    longUrlClickCount: 0,
    createdAt: '2025-01-01T00:00:00Z',
  }));
  mockedService.getRecentUrls.mockResolvedValue(existing);

  renderHomePage();

  await waitFor(() => expect(screen.getByText('https://localhost:7055/code0')).toBeInTheDocument());

  act(() => {
    capturedCallbacks!.onUrlCreated({
      shortCode: 'new123',
      longUrl: 'https://new.com',
      clickCount: 0,
      longUrlClickCount: 0,
      createdAt: '2025-06-01T00:00:00Z',
    });
  });

  expect(screen.getByText('https://localhost:7055/new123')).toBeInTheDocument();
  // Should still only show 5 items (new one prepended, last one removed)
  const items = screen.getAllByRole('listitem');
  expect(items.length).toBeLessThanOrEqual(5);
});

test('shows error when delete fails with non-404', async () => {
  mockedService.deleteShortUrl.mockRejectedValue(new ApiError(500, 'Server error'));
  mockedService.createShortUrl.mockResolvedValue({
    shortCode: 'del500',
    longUrl: 'https://example.com',
    clickCount: 0,
    longUrlClickCount: 0,
    createdAt: '2025-01-01T00:00:00Z',
  });

  renderHomePage();

  const longUrlInput = screen.getByLabelText(/long url/i);
  fireEvent.change(longUrlInput, { target: { value: 'https://example.com' } });
  fireEvent.click(screen.getByRole('button', { name: /shorten link/i }));

  await waitFor(() => {
    expect(screen.getByText('https://localhost:7055/del500')).toBeInTheDocument();
  });

  fireEvent.click(screen.getByText('Delete'));

  await waitFor(() => {
    expect(screen.getByRole('alert')).toBeInTheDocument();
    expect(screen.getByText('Server error')).toBeInTheDocument();
  });
});
