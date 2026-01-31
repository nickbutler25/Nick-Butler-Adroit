import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import { vi } from 'vitest';
import UrlList from './UrlList';
import { ShortUrlResult } from '../types';

describe('UrlList', () => {
  const mockOnDelete = vi.fn();
  const mockOnDismissError = vi.fn();

  const sampleUrls: ShortUrlResult[] = [
    {
      shortCode: 'abc1234',
      longUrl: 'https://example.com',
      clickCount: 5,
      longUrlClickCount: 5,
      createdAt: '2025-01-01T00:00:00Z',
    },
    {
      shortCode: 'xyz5678',
      longUrl: 'https://google.com',
      clickCount: 0,
      longUrlClickCount: 0,
      createdAt: '2025-01-02T00:00:00Z',
    },
  ];

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders empty message when no URLs', () => {
    render(<UrlList urls={[]} onDelete={mockOnDelete} />);

    expect(screen.getByText(/no shortened urls yet/i)).toBeInTheDocument();
  });

  it('renders list of URLs', () => {
    render(<UrlList urls={sampleUrls} onDelete={mockOnDelete} />);

    expect(screen.getByText('https://localhost:7055/abc1234')).toBeInTheDocument();
    expect(screen.getByText('https://localhost:7055/xyz5678')).toBeInTheDocument();
    expect(screen.getByText('https://example.com')).toBeInTheDocument();
    expect(screen.getByText('https://google.com')).toBeInTheDocument();
  });

  it('calls onDelete when delete button clicked', () => {
    render(<UrlList urls={sampleUrls} onDelete={mockOnDelete} />);

    const deleteButtons = screen.getAllByText('Delete');
    fireEvent.click(deleteButtons[0]);

    expect(mockOnDelete).toHaveBeenCalledWith('abc1234');
  });

  it('renders short links as direct anchor tags', () => {
    render(<UrlList urls={sampleUrls} onDelete={mockOnDelete} />);

    const link = screen.getByText('https://localhost:7055/abc1234').closest('a');
    expect(link).toHaveAttribute('href', 'https://localhost:7055/abc1234');
    expect(link).toHaveAttribute('target', '_blank');
    expect(link).toHaveAttribute('rel', 'noopener noreferrer');
  });

  it('renders error banner when error prop is set', () => {
    render(
      <UrlList
        urls={sampleUrls}
        onDelete={mockOnDelete}

        error="Something went wrong"
        onDismissError={mockOnDismissError}
      />
    );

    expect(screen.getByRole('alert')).toBeInTheDocument();
    expect(screen.getByText('Something went wrong')).toBeInTheDocument();
  });

  it('does not render error banner when error is null', () => {
    render(
      <UrlList
        urls={sampleUrls}
        onDelete={mockOnDelete}

        error={null}
      />
    );

    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('calls onDismissError when dismiss button is clicked', () => {
    render(
      <UrlList
        urls={sampleUrls}
        onDelete={mockOnDelete}

        error="Some error"
        onDismissError={mockOnDismissError}
      />
    );

    fireEvent.click(screen.getByLabelText('Dismiss error'));

    expect(mockOnDismissError).toHaveBeenCalledTimes(1);
  });
});
