import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router-dom";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import styles from "./styles.css?raw";
import App, { EndpointList, RequestDetailPanel } from "./App";
import i18n, { resources } from "./i18n";
let reconnect: undefined | (() => void);
vi.mock("@microsoft/signalr", () => ({
  HubConnectionState: { Disconnected: "Disconnected" },
  HubConnectionBuilder: class {
    withUrl() {
      return this;
    }
    withAutomaticReconnect() {
      return this;
    }
    build() {
      return {
        state: "Connected",
        on: vi.fn(),
        onreconnecting: vi.fn(),
        onreconnected: (x: () => void) => {
          reconnect = x;
        },
        onclose: vi.fn(),
        start: () => Promise.resolve(),
        invoke: () => Promise.resolve(),
        stop: () => Promise.resolve(),
      };
    }
  },
}));
const endpoint = {
  id: "e1",
  name: "Payments",
  slug: "payments",
  createdAtUtc: "2026-01-01T00:00:00Z",
  responseStatusCode: 200,
  responseContentType: "application/json",
  responseBody: '{"received":true}',
  responseDelayMs: 0,
  retentionLimit: 500,
};
const summary = {
  id: "r1",
  method: "POST",
  pathAndQuery: "/orders?id=7",
  contentType: "application/json",
  bodySize: 7,
  receivedAtUtc: "2026-01-01T00:00:00Z",
  responseStatusCode: 200,
};
const detail = {
  ...summary,
  endpointId: "e1",
  headers: { "Content-Type": ["application/json"] },
  bodyBase64: btoa('{"a":1}'),
  remoteIp: "127.0.0.1",
};
function wrapper(ui: React.ReactNode, path = "/") {
  return render(
    <QueryClientProvider
      client={
        new QueryClient({
          defaultOptions: {
            queries: { retry: false },
            mutations: { retry: false },
          },
        })
      }
    >
      <MemoryRouter initialEntries={[path]}>{ui}</MemoryRouter>
    </QueryClientProvider>,
  );
}
function workspaceFetch() {
  vi.mocked(fetch).mockImplementation(async (input) => {
    const u = String(input);
    if (u.includes("/requests/r1")) return new Response(JSON.stringify(detail));
    if (u.includes("/requests?"))
      return new Response(
        JSON.stringify({ items: [summary], total: 1, page: 1, pageSize: 25 }),
      );
    if (u.endsWith("/api/endpoints"))
      return new Response(JSON.stringify([endpoint]));
    return new Response(JSON.stringify(endpoint));
  });
}
beforeEach(() => {
  vi.stubGlobal("fetch", vi.fn());
  localStorage.clear();
  void i18n.changeLanguage("en-US");
  reconnect = undefined;
});
afterEach(() => cleanup());
describe("critical flows", () => {
  it("creates an endpoint", async () => {
    const f = vi.mocked(fetch);
    f.mockResolvedValueOnce(
      new Response(JSON.stringify([])),
    ).mockResolvedValueOnce(
      new Response(JSON.stringify(endpoint), { status: 201 }),
    );
    wrapper(<EndpointList />);
    await userEvent.type(await screen.findByLabelText("Name"), "Payments");
    await userEvent.type(screen.getByLabelText("Slug"), "payments");
    await userEvent.click(
      screen.getByRole("button", { name: "Create endpoint" }),
    );
    expect(f).toHaveBeenCalledWith(
      "/api/endpoints",
      expect.objectContaining({ method: "POST" }),
    );
  });
  it("shows endpoint empty state", async () => {
    vi.mocked(fetch).mockResolvedValue(new Response(JSON.stringify([])));
    wrapper(<EndpointList />);
    expect(await screen.findByText(/No endpoints yet/)).toBeInTheDocument();
  });
  it("selects a request and shows detail", async () => {
    workspaceFetch();
    wrapper(<App />, "/endpoints/e1");
    expect(await screen.findAllByText("/orders?id=7")).not.toHaveLength(0);
  });
  it("shows replay feedback", async () => {
    vi.mocked(fetch)
      .mockResolvedValueOnce(new Response(JSON.stringify(detail)))
      .mockResolvedValueOnce(
        new Response(
          JSON.stringify({
            id: "x",
            statusCode: 204,
            durationMs: 12,
            succeeded: true,
          }),
        ),
      );
    wrapper(<RequestDetailPanel id="r1" />);
    await userEvent.type(
      await screen.findByLabelText("Target URL"),
      "http://localhost:9000/hook",
    );
    await userEvent.click(screen.getByRole("button", { name: "Replay" }));
    expect(
      await screen.findByText("Received HTTP 204 in 12 ms"),
    ).toBeInTheDocument();
  });
  it("renders captured markup as inert text", async () => {
    const malicious = {
      ...detail,
      bodyBase64: btoa('<img src=x onerror="window.pwned=true">'),
    };
    vi.mocked(fetch).mockResolvedValue(new Response(JSON.stringify(malicious)));
    wrapper(<RequestDetailPanel id="r1" />);
    expect(await screen.findByText(/<img src=x/)).toBeInTheDocument();
    expect(document.querySelector(".data-block img")).toBeNull();
    expect(
      (window as typeof window & { pwned?: boolean }).pwned,
    ).toBeUndefined();
  });
  it("switches theme with an accessible control", async () => {
    vi.mocked(fetch).mockResolvedValue(new Response(JSON.stringify([])));
    wrapper(<EndpointList />);
    await userEvent.click(
      screen.getByRole("button", { name: "Use light theme" }),
    );
    expect(document.documentElement.dataset.theme).toBe("light");
  });
  it("traps settings focus, closes with Escape, and restores focus", async () => {
    workspaceFetch();
    wrapper(<App />, "/endpoints/e1");
    const trigger = await screen.findByRole("button", {
      name: "Endpoint settings",
    });
    await userEvent.click(trigger);
    expect(screen.getByLabelText("Status code")).toHaveFocus();
    await userEvent.tab({ shift: true });
    expect(
      screen.getByRole("button", { name: "Close settings" }),
    ).toHaveFocus();
    await userEvent.keyboard("{Escape}");
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    await waitFor(() => expect(trigger).toHaveFocus());
  });
  it("refetches missed requests after SignalR reconnect", async () => {
    workspaceFetch();
    wrapper(<App />, "/endpoints/e1");
    await screen.findByText("Requests");
    const before = vi.mocked(fetch).mock.calls.length;
    await reconnect?.();
    expect(vi.mocked(fetch).mock.calls.length).toBeGreaterThan(before);
  });
  it("defines a reduced-motion fallback", () => {
    expect(styles).toMatch(/@media\s*\(prefers-reduced-motion:\s*reduce\)/);
  });
  it("keeps English and Chinese translation keys identical", () => {
    const flatten = (value: unknown, prefix = ""): string[] =>
      Object.entries(value as Record<string, unknown>).flatMap(([key, item]) =>
        typeof item === "object"
          ? flatten(item, `${prefix}${key}.`)
          : `${prefix}${key}`,
      );
    expect(flatten(resources["zh-CN"]).sort()).toEqual(
      flatten(resources["en-US"]).sort(),
    );
  });
  it("switches language without losing unsaved form state and persists it", async () => {
    vi.mocked(fetch).mockResolvedValue(new Response(JSON.stringify([])));
    wrapper(<EndpointList />);
    const name = await screen.findByLabelText("Name");
    await userEvent.type(name, "Draft");
    await userEvent.click(
      screen.getByRole("button", { name: "Switch language" }),
    );
    expect(document.documentElement.lang).toBe("zh-CN");
    expect(localStorage.getItem("language")).toBe("zh-CN");
    expect(screen.getByLabelText("名称")).toHaveValue("Draft");
    expect(
      screen.getByRole("button", { name: "切换语言" }),
    ).toBeInTheDocument();
  });
});
