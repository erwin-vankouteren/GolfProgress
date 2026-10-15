/**
 * Supplies access tokens to the API client (#63).
 *
 * The session layer (#69) implements this: it keeps the access token in memory only,
 * and refreshes through `/auth/refresh` using the HttpOnly refresh-token cookie (#68).
 */
export interface AccessTokenProvider {
  /** The current access token, or null when signed out. */
  accessToken(): string | null | Promise<string | null>;
  /**
   * Gets a new access token after a 401. Resolves to null when the session can't be
   * refreshed and the user must sign in again.
   */
  refreshAccessToken(): Promise<string | null>;
}

/**
 * Paths reached before signing in. They never carry a token, and a 401 from them (such
 * as a wrong password on login) must not trigger a refresh.
 */
export const anonymousPaths = [
  "/auth/register",
  "/auth/login",
  "/auth/refresh",
  "/auth/logout",
  "/client-config",
] as const;

function isAnonymous(request: Request): boolean {
  const path = new URL(request.url).pathname;
  return anonymousPaths.some((p) => path.endsWith(p));
}

function withToken(request: Request, token: string): Request {
  const headers = new Headers(request.headers);
  headers.set("Authorization", `Bearer ${token}`);
  return new Request(request, { headers });
}

/**
 * Wraps `fetch` so authenticated requests carry the access token, and a 401 is
 * refreshed once and retried (#63).
 *
 * Concurrent requests that all hit a 401 share a single refresh, rather than each
 * rotating the refresh token in turn (which would invalidate the others').
 */
export function createAuthFetch(
  tokens: AccessTokenProvider,
  baseFetch: (request: Request) => Promise<Response> = (request) => globalThis.fetch(request),
): (request: Request) => Promise<Response> {
  let refreshing: Promise<string | null> | null = null;

  const refreshOnce = (): Promise<string | null> => {
    refreshing ??= tokens.refreshAccessToken().finally(() => {
      refreshing = null;
    });
    return refreshing;
  };

  return async (request) => {
    if (isAnonymous(request)) return baseFetch(request);

    const token = await tokens.accessToken();
    if (!token) return baseFetch(request);

    // Kept untouched, so the request (and its body) can be sent again after a refresh.
    const retry = request.clone();

    const response = await baseFetch(withToken(request, token));
    if (response.status !== 401) return response;

    // Expired token: refresh once and retry. If refreshing fails, the original 401
    // stands and the app returns to sign-in.
    const refreshed = await refreshOnce();
    return refreshed ? baseFetch(withToken(retry, refreshed)) : response;
  };
}
