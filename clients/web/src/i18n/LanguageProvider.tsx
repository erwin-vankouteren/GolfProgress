import { useQueryClient } from "@tanstack/react-query";
import { useEffect, useLayoutEffect, useRef, useSyncExternalStore, type ReactNode } from "react";
import { IntlProvider } from "react-intl";
import type { Schemas } from "../api";
import { queryKeys } from "../api/queries";
import { choiceOf, currentLanguage, localeFor, rememberChoice, rememberedChoice, translations } from "./language";

/**
 * Shows the app in the user's language. It follows the profile wherever it's loaded,
 * without fetching it itself; until then, and when signed out, the choice remembered
 * in this browser or the browser's own language.
 */
export function LanguageProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const me = useSyncExternalStore(
    (onChange) => queryClient.getQueryCache().subscribe(onChange),
    () => queryClient.getQueryData<Schemas["UserResponse"]>(queryKeys.me),
  );

  const choice = me ? choiceOf(me.language) : rememberedChoice();
  const locale = localeFor(choice);
  // Read by the API client for Accept-Language. A layout effect runs before the
  // effects in which screens start fetching, so their requests already carry it.
  useLayoutEffect(() => {
    currentLanguage.locale = locale;
  }, [locale]);

  useEffect(() => {
    if (me) rememberChoice(choiceOf(me.language));
  }, [me]);

  useEffect(() => {
    document.documentElement.lang = locale;
  }, [locale]);

  // What's loaded carries the API's texts in the old language: fetch it again.
  const shown = useRef(locale);
  useEffect(() => {
    if (shown.current === locale) return;
    shown.current = locale;
    void queryClient.invalidateQueries({ predicate: (query) => query.queryKey[0] !== queryKeys.me[0] });
  }, [locale, queryClient]);

  return (
    <IntlProvider locale={locale} defaultLocale="en" messages={translations[locale]}>
      {children}
    </IntlProvider>
  );
}
