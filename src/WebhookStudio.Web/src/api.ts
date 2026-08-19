import type {
  DiffItem,
  Endpoint,
  Filters,
  ReplayResult,
  RequestDetail,
  RequestSummary,
} from "./types";
export class ApiError extends Error {
  constructor(
    message: string,
    public code?: string,
  ) {
    super(message);
  }
}
async function request<T>(url: string, init?: RequestInit): Promise<T> {
  const response = await fetch(url, {
    ...init,
    headers: { "Content-Type": "application/json", ...init?.headers },
  });
  if (!response.ok) {
    const p = await response.json().catch(() => ({}));
    throw new ApiError(
      p.detail || p.title || `Request failed (${response.status})`,
      p.code,
    );
  }
  return response.status === 204 ? (undefined as T) : response.json();
}
export const api = {
  endpoints: () => request<Endpoint[]>("/api/endpoints"),
  endpoint: (id: string) => request<Endpoint>(`/api/endpoints/${id}`),
  createEndpoint: (input: { name: string; slug: string }) =>
    request<Endpoint>("/api/endpoints", {
      method: "POST",
      body: JSON.stringify(input),
    }),
  deleteEndpoint: (id: string) =>
    request<void>(`/api/endpoints/${id}`, { method: "DELETE" }),
  requests: (id: string, f: Filters) => {
    const q = new URLSearchParams({ page: String(f.page), pageSize: "25" });
    if (f.method) q.set("method", f.method);
    if (f.statusCategory) q.set("statusCategory", f.statusCategory);
    if (f.from) q.set("from", new Date(f.from).toISOString());
    if (f.to) q.set("to", new Date(f.to).toISOString());
    if (f.search) q.set("search", f.search);
    return request<{
      items: RequestSummary[];
      total: number;
      page: number;
      pageSize: number;
    }>(`/api/endpoints/${id}/requests?${q}`);
  },
  request: (id: string) => request<RequestDetail>(`/api/requests/${id}`),
  deleteRequest: (id: string) =>
    request<void>(`/api/requests/${id}`, { method: "DELETE" }),
  replay: (id: string, targetUrl: string) =>
    request<ReplayResult>(`/api/requests/${id}/replay`, {
      method: "POST",
      body: JSON.stringify({ targetUrl }),
    }),
  settings: (id: string, input: Partial<Endpoint>) =>
    request<Endpoint>(`/api/endpoints/${id}/settings`, {
      method: "PUT",
      body: JSON.stringify(input),
    }),
  clear: (id: string) =>
    request<{ deleted: number }>(`/api/endpoints/${id}/requests`, {
      method: "DELETE",
    }),
  compare: (id: string, leftId: string, rightId: string) =>
    request<{ differences: DiffItem[] }>(`/api/endpoints/${id}/compare`, {
      method: "POST",
      body: JSON.stringify({ leftId, rightId }),
    }),
  importPackage: (id: string, file: File) =>
    fetch(`/api/endpoints/${id}/import`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: file,
    }).then(async (r) => {
      if (!r.ok) {
        const p = await r.json();
        throw new ApiError(p.detail || "Import failed", p.code);
      }
      return r.json() as Promise<{ imported: number; skipped: number }>;
    }),
  runtime: () =>
    request<{ allowPrivateNetworkReplay: boolean; networkExposed: boolean }>(
      "/api/runtime",
    ),
};
