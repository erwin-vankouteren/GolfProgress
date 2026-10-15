import createClient from "openapi-fetch";
import { createAuthFetch, type AccessTokenProvider } from "./authFetch";
import { ApiProblem } from "./problem";
import type { components, paths } from "./schema";

/** The API's request and response models, e.g. `Schemas["RoundResponse"]`. */
export type Schemas = components["schemas"];

export interface ApiClientOptions {
  /** The API's base URL, e.g. https://api.example.com */
  baseUrl: string;
  tokens: AccessTokenProvider;
  /** Replaces `fetch`, for tests. */
  fetch?: (request: Request) => Promise<Response>;
}

/**
 * The API client for the web app (#63): `openapi-fetch` typed by the types generated
 * from api/openapi.json, with the access token added and refreshed automatically.
 *
 * ```ts
 * const api = createApiClient({ baseUrl, tokens: session });
 * const me = unwrap(await api.GET("/me"));
 * const page = unwrap(await api.GET("/me/rounds", { params: { query: { skip: 0, take: 20 } } }));
 * ```
 */
export function createApiClient(options: ApiClientOptions) {
  return createClient<paths>({
    baseUrl: options.baseUrl,
    fetch: createAuthFetch(options.tokens, options.fetch),
  });
}

export type ApiClient = ReturnType<typeof createApiClient>;

/**
 * The data of a successful response, or a thrown `ApiProblem` for an error response.
 * Lets callers (and TanStack Query) treat every failure the same way.
 */
export function unwrap<T>(result: { data?: T; error?: unknown; response: Response }): T {
  if (!result.response.ok) {
    const retryAfter = Number.parseInt(result.response.headers.get("Retry-After") ?? "", 10);
    throw new ApiProblem(result.response.status, result.error, Number.isNaN(retryAfter) ? undefined : retryAfter);
  }
  return result.data as T;
}

/** The total item count of a paged list response (`X-Total-Count`). */
export function totalCount(response: Response): number | undefined {
  const value = response.headers.get("X-Total-Count");
  return value === null ? undefined : Number(value);
}
