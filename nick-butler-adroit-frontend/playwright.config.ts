import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  retries: 0,
  workers: 1,
  reporter: 'list',
  use: {
    baseURL: 'http://localhost:3000',
    ignoreHTTPSErrors: true,
  },
  projects: [
    {
      name: 'chromium',
      use: { browserName: 'chromium' },
    },
  ],
  webServer: [
    {
      command: 'dotnet run --project ../Nick-Butler-Adroit.Api --launch-profile https',
      url: 'https://localhost:7055/swagger',
      reuseExistingServer: true,
      ignoreHTTPSErrors: true,
      timeout: 30000,
      env: {
        ...process.env,
        DISABLE_RATE_LIMITING: 'true',
      },
    },
    {
      command: 'npm run start',
      url: 'http://localhost:3000',
      reuseExistingServer: true,
      timeout: 15000,
    },
  ],
});
