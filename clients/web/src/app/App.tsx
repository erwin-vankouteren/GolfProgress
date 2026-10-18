import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { useEffect, useState, type ReactNode } from "react";
import { ApiProblem, type ApiClient } from "../api";
import { LanguageProvider } from "../i18n/LanguageProvider";
import type { Session } from "../session/session";
import { SessionProvider } from "../session/SessionContext";
import { UpdateGate } from "../version/UpdateGate";
import { ApiProvider } from "./ApiContext";

/**
 * The providers every screen relies on (#75, #69): the API client, the session,
 * server-state caching, localised messages, and the minimum-version check. `children` is
 * the router, so tests can render the same app around a memory router.
 */
export function App({
  api,
  session,
  appVersion,
  children,
}: {
  api: ApiClient;
  session: Session;
  appVersion: string;
  children: ReactNode;
}) {
  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            staleTime: 30_000,
            refetchOnWindowFocus: true,
            // A 4xx won't change on retry, so it shows at once. A server or network error
            // gets one retry, so the error state appears in about a second, not seven.
            retry: (failureCount, error) => !(error instanceof ApiProblem && error.status < 500) && failureCount < 1,
          },
        },
      }),
  );

  // Signing out drops everything cached for that user.
  useEffect(
    () =>
      session.subscribe(() => {
        if (session.getState().status === "signedOut") queryClient.clear();
      }),
    [session, queryClient],
  );

  return (
    <ApiProvider client={api}>
      <SessionProvider session={session}>
        <QueryClientProvider client={queryClient}>
          <LanguageProvider>
            <UpdateGate appVersion={appVersion}>{children}</UpdateGate>
          </LanguageProvider>
        </QueryClientProvider>
      </SessionProvider>
    </ApiProvider>
  );
}
