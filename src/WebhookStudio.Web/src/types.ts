export type Endpoint = { id:string; name:string; slug:string; createdAtUtc:string; responseStatusCode:number; responseContentType:string; responseBody:string; responseDelayMs:number; retentionLimit:number };
export type RequestSummary = { id:string; method:string; pathAndQuery:string; contentType?:string; bodySize:number; receivedAtUtc:string; responseStatusCode:number };
export type RequestDetail = RequestSummary & { endpointId:string; headers:Record<string,string[]>; bodyBase64:string; remoteIp?:string };
export type ReplayResult = { id:string; statusCode?:number; durationMs:number; succeeded:boolean; error?:string };
export type Filters={page:number;method:string;statusCategory:string;from:string;to:string;search:string};
export type DiffItem={path:string;kind:'added'|'removed'|'changed';left?:string;right?:string};
