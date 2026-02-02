import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { vi, type MockedFunction } from 'vitest';
import UrlShortenerForm from './UrlShortenerForm';
import * as urlService from '../services/urlService';

vi.mock('../services/urlService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../services/urlService')>();
  return {
    ...actual,
    createShortUrl: vi.fn(),
  };
});

const mockCreateShortUrl = urlService.createShortUrl as MockedFunction<typeof urlService.createShortUrl>;

describe('UrlShortenerForm', () => {
  const mockOnUrlCreated = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders form inputs and button', () => {
    render(<UrlShortenerForm onUrlCreated={mockOnUrlCreated} />);

    expect(screen.getByLabelText(/long url/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/alias/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /shorten link/i })).toBeInTheDocument();
  });

  it('calls onUrlCreated on successful submit', async () => {
    const result = {
      shortCode: 'abc1234',
      longUrl: 'https://example.com',
      clickCount: 0,
      longUrlClickCount: 0,
      createdAt: '2025-01-01T00:00:00Z',
    };
    mockCreateShortUrl.mockResolvedValue(result);

    render(<UrlShortenerForm onUrlCreated={mockOnUrlCreated} />);

    fireEvent.change(screen.getByLabelText(/long url/i), {
      target: { value: 'https://example.com' },
    });
    fireEvent.click(screen.getByRole('button', { name: /shorten link/i }));

    await waitFor(() => {
      expect(mockOnUrlCreated).toHaveBeenCalledWith(result);
    });
  });

  it('shows client-side error for invalid URL', async () => {
    render(<UrlShortenerForm onUrlCreated={mockOnUrlCreated} />);

    fireEvent.change(screen.getByLabelText(/long url/i), {
      target: { value: 'bad-url' },
    });
    fireEvent.click(screen.getByRole('button', { name: /shorten link/i }));

    await waitFor(() => {
      expect(screen.getByText(/please enter a valid url/i)).toBeInTheDocument();
    });
    expect(mockCreateShortUrl).not.toHaveBeenCalled();
  });

  it('shows client-side error for non-http URL', async () => {
    render(<UrlShortenerForm onUrlCreated={mockOnUrlCreated} />);

    fireEvent.change(screen.getByLabelText(/long url/i), {
      target: { value: 'ftp://example.com' },
    });
    fireEvent.click(screen.getByRole('button', { name: /shorten link/i }));

    await waitFor(() => {
      expect(screen.getByText(/please enter a valid url/i)).toBeInTheDocument();
    });
    expect(mockCreateShortUrl).not.toHaveBeenCalled();
  });

  it('shows error when alias is shorter than 5 characters', async () => {
    render(<UrlShortenerForm onUrlCreated={mockOnUrlCreated} />);

    fireEvent.change(screen.getByLabelText(/long url/i), {
      target: { value: 'https://example.com' },
    });
    fireEvent.change(screen.getByLabelText(/alias/i), {
      target: { value: 'ab' },
    });
    fireEvent.click(screen.getByRole('button', { name: /shorten link/i }));

    await waitFor(() => {
      expect(screen.getByText(/alias must be at least 5 characters/i)).toBeInTheDocument();
    });
    expect(mockCreateShortUrl).not.toHaveBeenCalled();
  });

  it('allows submit when alias is exactly 5 characters', async () => {
    const result = {
      shortCode: 'abcde',
      longUrl: 'https://example.com',
      clickCount: 0,
      longUrlClickCount: 0,
      createdAt: '2025-01-01T00:00:00Z',
    };
    mockCreateShortUrl.mockResolvedValue(result);

    render(<UrlShortenerForm onUrlCreated={mockOnUrlCreated} />);

    fireEvent.change(screen.getByLabelText(/long url/i), {
      target: { value: 'https://example.com' },
    });
    fireEvent.change(screen.getByLabelText(/alias/i), {
      target: { value: 'abcde' },
    });
    fireEvent.click(screen.getByRole('button', { name: /shorten link/i }));

    await waitFor(() => {
      expect(mockCreateShortUrl).toHaveBeenCalledWith('https://example.com', 'abcde');
    });
  });

  it('allows submit when alias is empty', async () => {
    const result = {
      shortCode: 'auto123',
      longUrl: 'https://example.com',
      clickCount: 0,
      longUrlClickCount: 0,
      createdAt: '2025-01-01T00:00:00Z',
    };
    mockCreateShortUrl.mockResolvedValue(result);

    render(<UrlShortenerForm onUrlCreated={mockOnUrlCreated} />);

    fireEvent.change(screen.getByLabelText(/long url/i), {
      target: { value: 'https://example.com' },
    });
    fireEvent.click(screen.getByRole('button', { name: /shorten link/i }));

    await waitFor(() => {
      expect(mockCreateShortUrl).toHaveBeenCalledWith('https://example.com', undefined);
    });
  });

  it('shows error when alias contains non-alphanumeric characters', async () => {
    render(<UrlShortenerForm onUrlCreated={mockOnUrlCreated} />);

    fireEvent.change(screen.getByLabelText(/long url/i), {
      target: { value: 'https://example.com' },
    });
    fireEvent.change(screen.getByLabelText(/alias/i), {
      target: { value: 'abcd%' },
    });
    fireEvent.click(screen.getByRole('button', { name: /shorten link/i }));

    await waitFor(() => {
      expect(screen.getByText(/alias must contain only letters and numbers/i)).toBeInTheDocument();
    });
    expect(mockCreateShortUrl).not.toHaveBeenCalled();
  });

  it('shows error when alias contains hyphens', async () => {
    render(<UrlShortenerForm onUrlCreated={mockOnUrlCreated} />);

    fireEvent.change(screen.getByLabelText(/long url/i), {
      target: { value: 'https://example.com' },
    });
    fireEvent.change(screen.getByLabelText(/alias/i), {
      target: { value: 'my-code' },
    });
    fireEvent.click(screen.getByRole('button', { name: /shorten link/i }));

    await waitFor(() => {
      expect(screen.getByText(/alias must contain only letters and numbers/i)).toBeInTheDocument();
    });
    expect(mockCreateShortUrl).not.toHaveBeenCalled();
  });

  it('shows error when alias contains spaces', async () => {
    render(<UrlShortenerForm onUrlCreated={mockOnUrlCreated} />);

    fireEvent.change(screen.getByLabelText(/long url/i), {
      target: { value: 'https://example.com' },
    });
    fireEvent.change(screen.getByLabelText(/alias/i), {
      target: { value: 'my code' },
    });
    fireEvent.click(screen.getByRole('button', { name: /shorten link/i }));

    await waitFor(() => {
      expect(screen.getByText(/alias must contain only letters and numbers/i)).toBeInTheDocument();
    });
    expect(mockCreateShortUrl).not.toHaveBeenCalled();
  });

  it.each([
    ['ab@de', '@'],
    ['ab#de', '#'],
    ['ab$de', '$'],
    ['ab&de', '&'],
    ['ab+de', '+'],
    ['ab=de', '='],
    ['ab.de', '.'],
    ['ab_de', '_'],
    ['ab~de', '~'],
    ['ab!de', '!'],
  ])('shows error when alias contains %s', async (alias, _char) => {
    render(<UrlShortenerForm onUrlCreated={mockOnUrlCreated} />);

    fireEvent.change(screen.getByLabelText(/long url/i), {
      target: { value: 'https://example.com' },
    });
    fireEvent.change(screen.getByLabelText(/alias/i), {
      target: { value: alias },
    });
    fireEvent.click(screen.getByRole('button', { name: /shorten link/i }));

    await waitFor(() => {
      expect(screen.getByText(/alias must contain only letters and numbers/i)).toBeInTheDocument();
    });
    expect(mockCreateShortUrl).not.toHaveBeenCalled();
  });

  it('allows uppercase alias and submits it to the API', async () => {
    const result = {
      shortCode: 'abcde',
      longUrl: 'https://example.com',
      clickCount: 0,
      longUrlClickCount: 0,
      createdAt: '2025-01-01T00:00:00Z',
    };
    mockCreateShortUrl.mockResolvedValue(result);

    render(<UrlShortenerForm onUrlCreated={mockOnUrlCreated} />);

    fireEvent.change(screen.getByLabelText(/long url/i), {
      target: { value: 'https://example.com' },
    });
    fireEvent.change(screen.getByLabelText(/alias/i), {
      target: { value: 'ABCDE' },
    });
    fireEvent.click(screen.getByRole('button', { name: /shorten link/i }));

    await waitFor(() => {
      expect(mockCreateShortUrl).toHaveBeenCalledWith('https://example.com', 'ABCDE');
    });
  });

  it('displays server error on API failure', async () => {
    mockCreateShortUrl.mockRejectedValue(new Error('Duplicate short code.'));

    render(<UrlShortenerForm onUrlCreated={mockOnUrlCreated} />);

    fireEvent.change(screen.getByLabelText(/long url/i), {
      target: { value: 'https://example.com' },
    });
    fireEvent.click(screen.getByRole('button', { name: /shorten link/i }));

    await waitFor(() => {
      expect(screen.getByText('Duplicate short code.')).toBeInTheDocument();
    });
  });
});
