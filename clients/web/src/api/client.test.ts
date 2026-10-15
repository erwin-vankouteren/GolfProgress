import { describe, expect, expectTypeOf, it, vi } from "vitest";
import { ApiProblem, createApiClient, totalCount, unwrap, type AccessTokenProvider, type Schemas } from "./index";

const baseUrl = "https://api.example.test";

const user: Schemas["UserResponse"] = {
  userId: "3f2b8c1e-5d4a-4b6f-9e2d-1a7c8b9d0e1f",
  email: "erwin@example.test",
  displayName: "Erwin",
  timezone: null,
  units: "Metric",
  trackingMode: "Minimal",
  handicapIndex: null,
  typical18Score: null,
  goalFocus: ["putting"],
  createdAt: "2026-05-01T09:30:00.1234567+00:00",
  updatedAt: "2026-05-01T09:30:00+00:00",
};

const json = (status: number, body: unknown, headers: Record<string, string> = {}) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": status >= 400 ? "application/problem+json" : "application/json", ...headers },
  });

function tokens(current: string | null = "token-1", refreshed: string | null = "token-2") {
  let token = current;
  const provider: AccessTokenProvider & { refreshes: number } = {
    refreshes: 0,
    accessToken: () => token,
    refreshAccessToken: async () => {
      provider.refreshes++;
      token = refreshed;
      return refreshed;
    },
  };
  return provider;
}

/** A fake `fetch` that records requests and answers with `respond`. */
function fakeFetch(respond: (request: Request, attempt: number) => Response | Promise<Response>) {
  const requests: Request[] = [];
  const fetch = vi.fn(async (request: Request) => {
    requests.push(request);
    return respond(request, requests.length);
  });
  return { fetch, requests };
}

describe("generated client", () => {
  it("calls GET /me with the access token and returns a typed user", async () => {
    const { fetch, requests } = fakeFetch(() => json(200, user));
    const api = createApiClient({ baseUrl, tokens: tokens(), fetch });

    const me = unwrap(await api.GET("/me"));

    expect(me.displayName).toBe("Erwin");
    expect(requests[0]?.url).toBe(`${baseUrl}/me`);
    expect(requests[0]?.headers.get("Authorization")).toBe("Bearer token-1");

    // The types come from the spec: an enum field is a union, not a free string.
    expectTypeOf(me).toEqualTypeOf<Schemas["UserResponse"]>();
    expectTypeOf(me.units).toEqualTypeOf<"Metric" | "Imperial">();
  });

  it("sends typed query parameters and exposes the paging total", async () => {
    const { fetch, requests } = fakeFetch(() => json(200, [], { "X-Total-Count": "25" }));
    const api = createApiClient({ baseUrl, tokens: tokens(), fetch });

    const result = await api.GET("/me/rounds", { params: { query: { skip: 20, take: 10 } } });

    expect(unwrap(result)).toEqual([]);
    expect(totalCount(result.response)).toBe(25);
    expect(new URL(requests[0]!.url).search).toBe("?skip=20&take=10");
  });

  it("sends a typed JSON body", async () => {
    const { fetch, requests } = fakeFetch(() => json(201, {}, { Location: `${baseUrl}/rounds/1` }));
    const api = createApiClient({ baseUrl, tokens: tokens(), fetch });

    await api.POST("/me/rounds", { body: { playedAt: "2026-05-01", holes: 18, totalStrokes: 88 } });

    expect(requests[0]?.method).toBe("POST");
    expect(await requests[0]?.json()).toEqual({ playedAt: "2026-05-01", holes: 18, totalStrokes: 88 });
  });
});

describe("errors", () => {
  it("throws an ApiProblem carrying the ProblemDetails", async () => {
    const { fetch } = fakeFetch(() =>
      json(404, { title: "Not found", status: 404, detail: "User not found.", traceId: "00-abc" }),
    );
    const api = createApiClient({ baseUrl, tokens: tokens(), fetch });

    const result = await api.GET("/me");

    const problem = (() => {
      try {
        unwrap(result);
      } catch (error) {
        return error;
      }
    })();

    expect(problem).toBeInstanceOf(ApiProblem);
    expect(problem).toMatchObject({
      status: 404,
      detail: "User not found.",
      traceId: "00-abc",
      message: "User not found.",
    });
  });

  it("keys validation messages by field, case-insensitively", () => {
    const problem = new ApiProblem(400, {
      status: 400,
      errors: { PlayedAt: ["PlayedAt must be an ISO date, yyyy-MM-dd."] },
    });

    expect(problem.messagesFor("playedAt")).toEqual(["PlayedAt must be an ISO date, yyyy-MM-dd."]);
    expect(problem.messagesFor("holes")).toEqual([]);
  });

  it("still gives a status when the body isn't ProblemDetails", () => {
    const problem = new ApiProblem(502, "Bad gateway");

    expect(problem.status).toBe(502);
    expect(problem.message).toBe("Request failed with status 502");
  });
});

describe("access token refresh", () => {
  it("refreshes once on a 401 and retries with the new token and the same body", async () => {
    const session = tokens();
    const { fetch, requests } = fakeFetch((request, attempt) =>
      attempt === 1 ? json(401, { status: 401 }) : json(201, {}),
    );
    const api = createApiClient({ baseUrl, tokens: session, fetch });

    const result = await api.POST("/me/rounds", { body: { playedAt: "2026-05-01", holes: 18, totalStrokes: 88 } });

    expect(result.response.status).toBe(201);
    expect(session.refreshes).toBe(1);
    expect(requests[1]?.headers.get("Authorization")).toBe("Bearer token-2");
    expect(await requests[1]?.json()).toEqual({ playedAt: "2026-05-01", holes: 18, totalStrokes: 88 });
  });

  it("returns the original 401 when refreshing fails", async () => {
    const session = tokens("token-1", null);
    const { fetch, requests } = fakeFetch(() => json(401, { status: 401, title: "Unauthorized" }));
    const api = createApiClient({ baseUrl, tokens: session, fetch });

    const result = await api.GET("/me");

    expect(result.response.status).toBe(401);
    expect(requests).toHaveLength(1);
    expect(() => unwrap(result)).toThrow(ApiProblem);
  });

  it("shares one refresh between concurrent requests", async () => {
    const session = tokens();
    let release!: () => void;
    const gate = new Promise<void>((resolve) => (release = resolve));
    session.refreshAccessToken = vi.fn(async () => {
      await gate;
      session.refreshes++;
      return "token-2";
    });
    const { fetch } = fakeFetch((request) =>
      request.headers.get("Authorization") === "Bearer token-2" ? json(200, user) : json(401, {}),
    );
    const api = createApiClient({ baseUrl, tokens: session, fetch });

    const calls = Promise.all([api.GET("/me"), api.GET("/me/baseline"), api.GET("/me/dashboard")]);
    await vi.waitFor(() => expect(session.refreshAccessToken).toHaveBeenCalledTimes(1));
    release();
    const results = await calls;

    expect(session.refreshAccessToken).toHaveBeenCalledTimes(1);
    expect(results.map((r) => r.response.status)).toEqual([200, 200, 200]);
  });

  it("never sends a token on sign-in, and never refreshes a failed login", async () => {
    const session = tokens();
    const { fetch, requests } = fakeFetch(() => json(401, { status: 401 }));
    const api = createApiClient({ baseUrl, tokens: session, fetch });

    const result = await api.POST("/auth/login", { body: { email: "a@example.test", password: "wrong" } });

    expect(result.response.status).toBe(401);
    expect(requests[0]?.headers.get("Authorization")).toBeNull();
    expect(session.refreshes).toBe(0);
  });

  it("sends no token when signed out", async () => {
    const { fetch, requests } = fakeFetch(() => json(401, {}));
    const api = createApiClient({ baseUrl, tokens: tokens(null), fetch });

    await api.GET("/me");

    expect(requests[0]?.headers.get("Authorization")).toBeNull();
  });
});
