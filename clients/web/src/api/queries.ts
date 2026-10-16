import { useInfiniteQuery, useQuery } from "@tanstack/react-query";
import { useApi } from "../app/ApiContext";
import { ApiProblem } from "./problem";
import { totalCount, unwrap, type Schemas } from "./client";

/**
 * Query keys, in one place so a write can invalidate exactly what it changed. Every key
 * starts with the resource, so invalidating ["rounds"] covers every round query.
 */
export const queryKeys = {
  me: ["me"] as const,
  dashboard: ["dashboard"] as const,
  rounds: ["rounds"] as const,
  roundList: ["rounds", "list"] as const,
  round: (roundId: string) => ["rounds", roundId] as const,
  fitnessScore: (roundId: string) => ["rounds", roundId, "fitness-score"] as const,
  microWins: (roundId: string) => ["rounds", roundId, "micro-wins"] as const,
  nextLever: (roundId: string) => ["rounds", roundId, "next-lever"] as const,
  reflections: (roundId: string) => ["rounds", roundId, "reflections"] as const,
  practice: ["practice"] as const,
  practiceList: ["practice", "list"] as const,
  practiceSession: (id: string) => ["practice", id] as const,
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
    // Every time the user comes back to the tab, not only once it's 30 seconds old: a
    // round logged on the phone meanwhile should show (#88).
    refetchOnWindowFocus: "always",
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

/** Rounds per page of the round list. */
export const roundPageSize = 20;

/**
 * The user's rounds, newest first, a page at a time (#95). The next page starts where
 * the loaded ones end, until X-Total-Count is reached.
 */
export function useRoundList() {
  const api = useApi();
  return useInfiniteQuery({
    queryKey: queryKeys.roundList,
    initialPageParam: 0,
    queryFn: async ({ pageParam }) => {
      const result = await api.GET("/me/rounds", { params: { query: { skip: pageParam, take: roundPageSize } } });
      return { rounds: unwrap(result), total: totalCount(result.response) };
    },
    getNextPageParam: (lastPage, pages) => {
      const loaded = pages.reduce((count, page) => count + page.rounds.length, 0);
      const total = lastPage.total ?? loaded;
      return loaded < total && lastPage.rounds.length > 0 ? loaded : undefined;
    },
  });
}

/** "Your next focus" for a round, read without recording a recommendation. */
export function useNextLever(roundId: string) {
  const api = useApi();
  return useQuery({
    queryKey: queryKeys.nextLever(roundId),
    queryFn: async () => unwrap(await api.GET("/rounds/{roundId}/next-lever", { params: { path: { roundId } } })),
  });
}

export function useReflections(roundId: string) {
  const api = useApi();
  return useQuery({
    queryKey: queryKeys.reflections(roundId),
    queryFn: async () => unwrap(await api.GET("/rounds/{roundId}/reflections", { params: { path: { roundId } } })),
  });
}

/** Practice sessions per page of the practice list. */
export const practicePageSize = 20;

/** The user's practice sessions, newest first, a page at a time (#103). */
export function usePracticeList() {
  const api = useApi();
  return useInfiniteQuery({
    queryKey: queryKeys.practiceList,
    initialPageParam: 0,
    queryFn: async ({ pageParam }) => {
      const result = await api.GET("/me/practice-sessions", {
        params: { query: { skip: pageParam, take: practicePageSize } },
      });
      return { sessions: unwrap(result), total: totalCount(result.response) };
    },
    getNextPageParam: (lastPage, pages) => {
      const loaded = pages.reduce((count, page) => count + page.sessions.length, 0);
      const total = lastPage.total ?? loaded;
      return loaded < total && lastPage.sessions.length > 0 ? loaded : undefined;
    },
  });
}

export function usePracticeSession(id: string) {
  const api = useApi();
  return useQuery({
    queryKey: queryKeys.practiceSession(id),
    queryFn: async () => unwrap(await api.GET("/me/practice-sessions/{id}", { params: { path: { id } } })),
  });
}
