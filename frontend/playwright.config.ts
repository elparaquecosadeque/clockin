import { defineConfig, devices } from '@playwright/test';

// One dev server per language, as each locale is its own build (ADR 0001).
export default defineConfig({
  testDir: 'e2e',
  forbidOnly: !!process.env['CI'],
  retries: process.env['CI'] ? 1 : 0,
  reporter: process.env['CI'] ? 'github' : 'list',
  projects: [
    { name: 'en', use: { ...devices['Desktop Chrome'], baseURL: 'http://localhost:4200' } },
    { name: 'es', use: { ...devices['Pixel 7'], baseURL: 'http://localhost:4201' } },
  ],
  webServer: [
    {
      command: 'npx ng serve --port 4200',
      url: 'http://localhost:4200',
      reuseExistingServer: !process.env['CI'],
    },
    {
      command: 'npx ng serve --configuration es --port 4201',
      url: 'http://localhost:4201',
      reuseExistingServer: !process.env['CI'],
    },
  ],
});
