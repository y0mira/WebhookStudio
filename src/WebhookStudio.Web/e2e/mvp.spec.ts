import { expect, test } from "@playwright/test";
const cases = [
  {
    language: "en-US",
    name: "Name",
    slug: "Slug",
    create: "Create endpoint",
    connected: "Connected",
    method: "HTTP method",
    add: "Add to comparison",
    comparison: "Request comparison",
    changed: "Changed",
    close: "Close comparison",
    target: "Target URL",
    replay: "Replay",
    received: /Received HTTP 200/,
    blocked: /without credentials/,
    warning: /Private-network replay is enabled/,
    settings: "Endpoint settings",
    body: "Response body",
    switchLanguage: "Switch language",
    switchedBody: "响应 Body",
    switchBack: "切换语言",
    closeSettings: "Close settings",
  },
  {
    language: "zh-CN",
    name: "名称",
    slug: "Slug",
    create: "创建 Endpoint",
    connected: "已连接",
    method: "HTTP 方法",
    add: "加入比较",
    comparison: "请求比较",
    changed: "变化",
    close: "关闭比较",
    target: "目标 URL",
    replay: "重放",
    received: /收到 HTTP 200/,
    blocked: /不含用户名密码/,
    warning: /已启用私网重放/,
    settings: "Endpoint 设置",
    body: "响应 Body",
    switchLanguage: "切换语言",
    switchedBody: "Response body",
    switchBack: "Switch language",
    closeSettings: "关闭设置",
  },
] as const;
for (const labels of cases)
  test(`${labels.language}: complete localized flow and responsive layout`, async ({
    page,
  }) => {
    await page.addInitScript(
      (language) => localStorage.setItem("language", language),
      labels.language,
    );
    const slug = `release-${labels.language.toLowerCase()}-${Date.now()}`;
    await page.goto("/");
    await expect(page.locator("html")).toHaveAttribute("lang", labels.language);
    await page.getByLabel(labels.name).fill(`Release ${labels.language}`);
    await page.getByLabel(labels.slug).fill(slug);
    await page.getByRole("button", { name: labels.create }).click();
    await expect(page.getByText(labels.connected)).toBeVisible();
    await expect(page.getByText(labels.warning)).toBeVisible();
    await page.request.post(`/hooks/${slug}/orders?case=one`, {
      data: { orderId: 1, state: "new" },
    });
    await page.request.post(`/hooks/${slug}/orders?case=two`, {
      data: { orderId: 2, state: "paid" },
    });
    await expect(page.getByText("/orders?case=two")).toBeVisible();
    await page.getByLabel(labels.method).selectOption("POST");
    await expect(page).toHaveURL(/method=POST/);
    await page.getByRole("button", { name: labels.settings }).click();
    await page.getByLabel(labels.body).fill("unsaved release draft");
    const statefulUrl = page.url();
    await page
      .getByRole("button", { name: labels.switchLanguage })
      .last()
      .click();
    await expect(page).toHaveURL(statefulUrl);
    await expect(page.getByLabel(labels.switchedBody)).toHaveValue(
      "unsaved release draft",
    );
    await page.getByRole("button", { name: labels.switchBack }).last().click();
    await page.getByRole("button", { name: labels.closeSettings }).click();
    const compare = page.getByRole("button", { name: labels.add });
    await compare.first().click();
    await compare.first().click();
    await expect(
      page.getByRole("heading", { name: labels.comparison }),
    ).toBeVisible();
    await expect(page.getByText(labels.changed).first()).toBeVisible();
    await page.getByRole("button", { name: labels.close }).click();
    await page.getByText("/orders?case=two").click();
    const origin = new URL(page.url()).origin;
    await page
      .getByLabel(labels.target)
      .fill(`${origin}/hooks/${slug}/replayed`);
    await page
      .getByRole("button", { name: labels.replay, exact: true })
      .click();
    await expect(page.getByText(labels.received)).toBeVisible();
    await page
      .getByLabel(labels.target)
      .fill(`${origin.replace("://", "://user:pass@")}/`);
    await page
      .getByRole("button", { name: labels.replay, exact: true })
      .click();
    await expect(page.getByText(labels.blocked)).toBeVisible();
    for (const width of [375, 768, 1024, 1440]) {
      await page.setViewportSize({ width, height: 850 });
      await page.waitForTimeout(350);
      await page.screenshot({
        path: `test-results/${labels.language}-${width}.png`,
        fullPage: true,
      });
      expect(
        await page.evaluate(() => [
          document.documentElement.scrollWidth,
          document.documentElement.clientWidth,
        ]),
        `overflow ${labels.language} ${width}`,
      ).toEqual([width, width]);
    }
  });
