import { ApiProblem } from "../api";

/** The rules of inviting a coach (#185, docs/coach-access.md → "What a coach sees"). */

const emailShape = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export type EmailError = "required" | "invalid";

export function checkCoachEmail(email: string): EmailError | undefined {
  const trimmed = email.trim();
  if (trimmed === "") return "required";
  return emailShape.test(trimmed) ? undefined : "invalid";
}

export type Sharing = { shareReflections: boolean; sharePractice: boolean };
export type SharedItem = "rounds" | "reflections" | "practice";

/** What a coach sees: rounds and progress always, the other two when the golfer shares them. */
export function sharedItems({ shareReflections, sharePractice }: Sharing): SharedItem[] {
  return [
    "rounds",
    ...(shareReflections ? (["reflections"] as const) : []),
    ...(sharePractice ? (["practice"] as const) : []),
  ];
}

export type InvitationFailure = "gone" | "wrongAccount" | "full" | "other" | "connection";

/** Why previewing, accepting or declining failed, from the API's answer. */
export function invitationFailure(error: unknown): InvitationFailure {
  if (!(error instanceof ApiProblem)) return "connection";
  switch (error.status) {
    case 400:
    case 404:
      return "gone"; // 400: a token the API can't even read, as from a cut-off link.
    case 403:
      return "wrongAccount";
    case 409:
      return "full";
    default:
      return "other";
  }
}
