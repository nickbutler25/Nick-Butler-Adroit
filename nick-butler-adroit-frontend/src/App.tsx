/**
 * Root application component.
 * Sets up client-side routing with two pages:
 *   - "/" (HomePage): URL shortener form + recent links
 *   - "/all" (AllLinksPage): Paginated list of all links with virtual scrolling
 */
import React from 'react';
import { BrowserRouter, Routes, Route } from 'react-router-dom';
import './App.css';
import HomePage from './pages/HomePage';
import AllLinksPage from './pages/AllLinksPage';

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<HomePage />} />
        <Route path="/all" element={<AllLinksPage />} />
      </Routes>
    </BrowserRouter>
  );
}

export default App;
