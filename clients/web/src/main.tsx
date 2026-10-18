import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { createBrowserRouter } from "react-router";
import { RouterProvider } from "react-router/dom";
import { createApiClient } from "./api";
import { App } from "./app/App";
import { routes } from "./app/routes";
import { currentLanguage } from "./i18n/language";
import { Session } from "./session/session";
import "./theme/tokens.css";
import "./theme/global.css";

const session = new Session();
const api = createApiClient({
  baseUrl: import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5068",
  tokens: session,
  language: () => currentLanguage.locale,
});
session.attach(api);

// Signs back in from the refresh cookie after a reload (#69). The app shows
// "Signing you in…" until this settles.
void session.restore();

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <App api={api} session={session} appVersion={__APP_VERSION__}>
      <RouterProvider router={createBrowserRouter(routes)} />
    </App>
  </StrictMode>,
);
