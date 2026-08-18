import { expect, test } from '@playwright/test';
test('create endpoint, capture a webhook, and see it without refresh', async ({ page }) => {
  const slug = `smoke-${Date.now()}`;
  await page.goto('/');
  await page.getByLabel('Name').fill('Smoke test');
  await page.getByLabel('Slug').fill(slug);
  await page.getByRole('button', { name: 'Create endpoint' }).click();
  await page.getByRole('link', { name: new RegExp(`Smoke test.*${slug}`) }).click();
  await expect(page.getByText('Waiting for a webhook')).toBeVisible();
  await page.waitForTimeout(500);
  const response = await page.request.post(`/hooks/${slug}/orders?source=playwright`, { data: { orderId: 42 } });
  expect(response.ok()).toBeTruthy();
  await expect(page.getByRole('button', { name: /POST.*orders\?source=playwright/ })).toBeVisible();
  await expect(page.getByText('"orderId": 42')).toBeVisible();
});
