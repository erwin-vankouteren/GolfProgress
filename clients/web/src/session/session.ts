import { unwrap, type AccessTokenProvider, type ApiClient, type Schemas } from "../api";

/**
 * How long an /auth/* request may take before the app gives up on it. Generous, because
 * staging's serverless database can take a minute to resume after idling; the screens
 * explain the wait after a few seconds (useSlow).
 */
export const authTimeoutMs = 90_000;

export type SessionState =
  /** Checking whether the refresh cookie still holds a session (at start-up). */
  { status: "restoring" } | { status: "signedOut" } | { status: "signedIn"; userId: string };

/**
 * The web app's sign-in state (#69).
 *
 * - The **access token** lives only in this object's memory: never localStorage or
 *   sessionStorage, where any script on the page could read it.
 * - The **refresh token** is the API's HttpOnly cookie (#68): this code never sees it.
 *   Requests to /auth/* send it with `credentials: "include"` and ask for it with
 *   `X-Refresh-Token-Mode: cookie`; no other request carries credentials.
 *
 * Restoring after a reload is a refresh: if the cookie is still valid the user is
 * signed straight back in. The API client calls `refreshAccessToken` on a 401 and shares
 * one refresh between concurrent requests.
 *
 * No /auth/* request waits longer than `authTimeoutMs`: a refresh that times out settles on
 * signed out, and a sign-in that times out throws as if the API were unreachable.
 */
export class Session implements AccessTokenProvider {
  private token: string | null;
  private state: SessionState;
  private api: ApiClient | null = null;
  private readonly listeners = new Set<() => void>();

  /** Starts restoring; tests can start signed in. */
  constructor(signedIn?: { accessToken: string; userId: string }) {
    this.token = signedIn?.accessToken ?? null;
    this.state = signedIn ? { status: "signedIn", userId: signedIn.userId } : { status: "restoring" };
  }

  /** The client this session signs in through. Set once, after both are created. */
  attach(api: ApiClient): void {
    this.api = api;
  }

  // React subscribes through useSyncExternalStore.
  readonly subscribe = (listener: () => void) => {
    this.listeners.add(listener);
    return () => {
      this.listeners.delete(listener);
    };
  };

  readonly getState = (): SessionState => this.state;

  accessToken(): string | null {
    return this.token;
  }

  async refreshAccessToken(): Promise<string | null> {
    try {
      const result = await this.withTimeout(
        this.client().POST("/auth/refresh", {
          params: { header: { "X-Refresh-Token-Mode": "cookie" } },
          credentials: "include",
        }),
      );
      if (!result.response.ok || !result.data) {
        this.clear();
        return null;
      }
      this.accept(result.data);
      return this.token;
    } catch {
      // Offline, unreachable or timed out: treat as signed out rather than hanging.
      this.clear();
      return null;
    }
  }

  /** At start-up: signs back in from the refresh cookie, or settles on signed out. */
  async restore(): Promise<void> {
    if (this.state.status === "restoring") await this.refreshAccessToken();
  }

  /** Throws an ApiProblem for a wrong password (401), validation errors (400) or 429. */
  async signIn(email: string, password: string): Promise<void> {
    const result = await this.withTimeout(
      this.client().POST("/auth/login", {
        body: { email, password },
        params: { header: { "X-Refresh-Token-Mode": "cookie" } },
        credentials: "include",
      }),
    );
    this.accept(unwrap(result));
  }

  /** Throws an ApiProblem for a taken email (409), validation errors (400) or 429. */
  async register(email: string, displayName: string, password: string): Promise<void> {
    const result = await this.withTimeout(
      this.client().POST("/auth/register", {
        body: { email, displayName, password },
        params: { header: { "X-Refresh-Token-Mode": "cookie" } },
        credentials: "include",
      }),
    );
    this.accept(unwrap(result));
  }

  /** Revokes the refresh cookie on the API, then forgets the session locally either way. */
  async signOut(): Promise<void> {
    try {
      await this.client().POST("/auth/logout", { credentials: "include" });
    } finally {
      this.clear();
    }
  }

  /** Rejects after `authTimeoutMs`, so a request that never answers can't hang a screen. */
  private withTimeout<T>(request: Promise<T>): Promise<T> {
    let timer: ReturnType<typeof setTimeout> | undefined;
    const timeout = new Promise<never>((_, reject) => {
      timer = setTimeout(() => reject(new Error("The API didn't answer in time.")), authTimeoutMs);
    });
    return Promise.race([request, timeout]).finally(() => clearTimeout(timer));
  }

  private accept(auth: Schemas["AuthResponse"]): void {
    this.token = auth.token;
    this.set({ status: "signedIn", userId: auth.userId });
  }

  private clear(): void {
    this.token = null;
    this.set({ status: "signedOut" });
  }

  private set(state: SessionState): void {
    this.state = state;
    this.listeners.forEach((listener) => listener());
  }

  private client(): ApiClient {
    if (!this.api) throw new Error("Session.attach(api) must be called before use.");
    return this.api;
  }
}
