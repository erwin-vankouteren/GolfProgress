import { useQuery } from "@tanstack/react-query";
import { useApi } from "../app/ApiContext";
import { ApiProblem } from "./problem";
import { unwrap, type Schemas } from "./client";

/**
 * Query keys, in one place so a write can invalidate exactly what it changed. Every key
 * starts with the resource, so invalidating ["rounds"] covers every round query.
 */
export const queryKeys = {
  me: ["me"] as const,
  dashboard: ["dashboard"] as const,
  rounds: ["rounds"] as const,
  round: (roundId: string) => ["rounds", roundId] as const,
  fitnessScore: (roundId: string) => ["rounds", roundId, "fitness-score"] as const,
  microWins: (roundId: string) => ["rounds", roundId, "micro-wins"] as const,
};

/** The signed-in user's profile and preferences, e.g. the tracking mode. */
export function useMe() {
  const api = useApi();
  return useQuery({ queryKey: queryKeys.me, queryFn: async () => unwrap(await api.GET("/me")) });
}

export function useRound(roundId: string) {
  const api = useApi();
  return useQuery({
    queryKey: queryKeys.round(roundId),
    queryFn: async () => unwrap(await api.GET("/rounds/{id}", { params: { path: { id: roundId } } })),
  });
}

/** The round's fitness score, or null when it has none yet. */
export function useFitnessScore(roundId: string | undefined) {
  const api = useApi();
  return useQuery({
    queryKey: queryKeys.fitnessScore(roundId ?? ""),
    enabled: roundId !== undefined,
    queryFn: async (): Promise<Schemas["FitnessScoreResponse"] | null> =>
      nullWhenNotFound(async () =>
        unwrap(await api.GET("/rounds/{roundId}/fitness-score", { params: { path: { roundId: roundId! } } })),
      ),
  });
}

export function useMicroWins(roundId: string) {
  const api = useApi();
  return useQuery({
    queryKey: queryKeys.microWins(roundId),
    queryFn: async () => unwrap(await api.GET("/rounds/{roundId}/micro-wins", { params: { path: { roundId } } })),
  });
}

/**
 * GET /me/dashboard. The spec types lastRound and nextLever as always present, but the
 * API returns null for a user with no rounds, so this says so.
 */
export type Dashboard = Omit<Schemas["DashboardResponse"], "lastRound" | "nextLever"> & {
  lastRound: Schemas["DashboardRoundSummary"] | null;
  nextLever: Schemas["NextLeverResponse"] | null;
};

export function useDashboard() {
  const api = useApi();
  return useQuery({
    queryKey: queryKeys.dashboard,
    queryFn: async (): Promise<Dashboard> => unwrap(await api.GET("/me/dashboard")),
  });
}

async function nullWhenNotFound<T>(load: () => Promise<T>): Promise<T | null> {
  try {
    return await load();
  } catch (error) {
    if (error instanceof ApiProblem && error.status === 404) return null;
    throw error;
  }
}
