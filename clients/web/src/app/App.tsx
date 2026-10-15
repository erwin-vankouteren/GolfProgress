import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { useState, type ReactNode } from "react";
import { IntlProvider } from "react-intl";
import type { ApiClient } from "../api";
import { UpdateGate } from "../version/UpdateGate";
import { ApiProvider } from "./ApiContext";

/**
 * The providers every screen relies on (#75): the API client, server-state caching,
 * localised messages, and the minimum-version check. `children` is the router, so tests
 * can render the same app around a memory router.
 */
export function App({ api, appVersion, children }: { api: ApiClient; appVersion: string; children: ReactNode }) {
  const [queryClient] = useState(
    () => new QueryClient({ defaultOptions: { queries: { staleTime: 30_000, refetchOnWindowFocus: true } } }),
  );

  return (
    <ApiProvider client={api}>
      <QueryClientProvider client={queryClient}>
        {/* English only for now; messages fall back to their defaultMessage (#78). */}
        <IntlProvider locale="en" defaultLocale="en">
          <UpdateGate appVersion={appVersion}>{children}</UpdateGate>
        </IntlProvider>
      </QueryClientProvider>
    </ApiProvider>
  );
}
