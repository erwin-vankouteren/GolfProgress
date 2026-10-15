/// <reference types="vite/client" />

/** The app's version from package.json, set at build time (vite.config.ts). */
declare const __APP_VERSION__: string;

interface ImportMetaEnv {
  /** The API's base URL, e.g. https://api.example.com. Defaults to the local API. */
  readonly VITE_API_BASE_URL?: string;
}
