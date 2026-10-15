import { useQuery } from "@tanstack/react-query";
import type { ReactNode } from "react";
import { FormattedMessage } from "react-intl";
import { useApi } from "../app/ApiContext";
import { unwrap } from "../api";
import { messages } from "../i18n/messages";
import { compareVersions } from "./compareVersions";

/**
 * Shows "Update required" instead of the app when this build is older than the API's
 * minimum supported web version (GET /client-config, #66).
 *
 * It never blocks start-up: the app renders while the check runs, and if the check fails
 * (offline, API down) the app carries on. Only a definite "too old" stops it.
 */
export function UpdateGate({ appVersion, children }: { appVersion: string; children: ReactNode }) {
  const api = useApi();
  const { data } = useQuery({
    queryKey: ["client-config"],
    queryFn: async () => unwrap(await api.GET("/client-config")),
    staleTime: 5 * 60 * 1000,
    retry: false,
  });

  const minimum = data?.minimumVersions.web;
  if (minimum && compareVersions(appVersion, minimum) < 0) return <UpdateRequired />;

  return <>{children}</>;
}

function UpdateRequired() {
  return (
    <main className="update-required">
      <h1>
        <FormattedMessage {...messages.updateRequiredTitle} />
      </h1>
      <p>
        <FormattedMessage {...messages.updateRequiredBody} />
      </p>
      <button type="button" className="button button--primary" onClick={() => window.location.reload()}>
        <FormattedMessage {...messages.reload} />
      </button>
    </main>
  );
}
