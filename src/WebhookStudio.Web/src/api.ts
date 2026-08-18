import type { Endpoint, ReplayResult, RequestDetail, RequestSummary } from './types';
export class ApiError extends Error {}
async function request<T>(url:string, init?:RequestInit):Promise<T> {
  const response = await fetch(url, { ...init, headers: { 'Content-Type':'application/json', ...init?.headers } });
  if (!response.ok) { const p = await response.json().catch(()=>({})); throw new ApiError(p.detail || p.title || `Request failed (${response.status})`); }
  return response.status === 204 ? undefined as T : response.json();
}
export const api = {
  endpoints: () => request<Endpoint[]>('/api/endpoints'),
  endpoint: (id:string) => request<Endpoint>(`/api/endpoints/${id}`),
  createEndpoint: (input:{name:string;slug:string}) => request<Endpoint>('/api/endpoints',{method:'POST',body:JSON.stringify(input)}),
  deleteEndpoint: (id:string) => request<void>(`/api/endpoints/${id}`,{method:'DELETE'}),
  requests: (id:string) => request<{items:RequestSummary[];total:number}>(`/api/endpoints/${id}/requests?page=1&pageSize=100`),
  request: (id:string) => request<RequestDetail>(`/api/requests/${id}`),
  deleteRequest: (id:string) => request<void>(`/api/requests/${id}`,{method:'DELETE'}),
  replay: (id:string,targetUrl:string) => request<ReplayResult>(`/api/requests/${id}/replay`,{method:'POST',body:JSON.stringify({targetUrl})})
};
