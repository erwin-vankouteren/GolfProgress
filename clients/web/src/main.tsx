import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { createBrowserRouter } from "react-router";
import { RouterProvider } from "react-router/dom";
import { createApiClient } from "./api";
import { App } from "./app/App";
import { routes } from "./app/routes";
import "./theme/tokens.css";
import "./theme/global.css";

const api = createApiClient({
  baseUrl: import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5068",
  // Signed out until the session layer arrives in #69.
  tokens: { accessToken: () => null, refreshAccessToken: async () => null },
});

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <App api={api} appVersion={__APP_VERSION__}>
      <RouterProvider router={createBrowserRouter(routes)} />
    </App>
  </StrictMode>,
);
