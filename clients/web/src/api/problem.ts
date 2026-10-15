/**
 * An error response from the API, from its ProblemDetails body (#63).
 *
 * Every API error is `application/problem+json`. `unwrap` throws this for any non-2xx
 * response, so a call either returns its data or throws: components never inspect
 * status codes themselves.
 */
export class ApiProblem extends Error {
  /** HTTP status code, e.g. 400, 404, 409, 429, 500. */
  readonly status: number;
  readonly title?: string;
  readonly detail?: string;
  /** Matches the API's log entry for this request. Show it in development builds only. */
  readonly traceId?: string;
  /** For 400s: validation messages keyed by field, to show next to that field. */
  readonly errors: Record<string, string[]>;

  constructor(status: number, body?: unknown) {
    const problem = (typeof body === "object" && body !== null ? body : {}) as Record<string, unknown>;
    const title = typeof problem.title === "string" ? problem.title : undefined;
    const detail = typeof problem.detail === "string" ? problem.detail : undefined;

    super(detail ?? title ?? `Request failed with status ${status}`);
    this.name = "ApiProblem";
    this.status = typeof problem.status === "number" ? problem.status : status;
    this.title = title;
    this.detail = detail;
    this.traceId = typeof problem.traceId === "string" ? problem.traceId : undefined;
    this.errors = isErrorMap(problem.errors) ? problem.errors : {};
  }

  /**
   * The validation messages for one field, matched case-insensitively: the API keys
   * body fields as `PlayedAt` and query parameters as `from`.
   */
  messagesFor(field: string): string[] {
    const key = Object.keys(this.errors).find((k) => k.toLowerCase() === field.toLowerCase());
    return (key && this.errors[key]) || [];
  }
}

function isErrorMap(value: unknown): value is Record<string, string[]> {
  return (
    typeof value === "object" &&
    value !== null &&
    Object.values(value).every((v) => Array.isArray(v) && v.every((m) => typeof m === "string"))
  );
}
