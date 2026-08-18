export type Endpoint = { id:string; name:string; slug:string; createdAtUtc:string };
export type RequestSummary = { id:string; method:string; pathAndQuery:string; contentType?:string; bodySize:number; receivedAtUtc:string };
export type RequestDetail = RequestSummary & { endpointId:string; headers:Record<string,string[]>; bodyBase64:string; remoteIp?:string };
export type ReplayResult = { id:string; statusCode?:number; durationMs:number; succeeded:boolean; error?:string };
