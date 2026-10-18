import type { Schemas } from "../api";
import { nl } from "./nl";

/**
 * The app's language: the one chosen in Profile, else the browser's. The choice is the
 * API's `UserResponse.language`: "English", "Dutch", or null to follow the device. It
 * is also remembered in this browser, so the next visit starts in that language before
 * the profile has loaded, signed-in or not.
 */

export type AppLocale = "en" | "nl";
/** What Profile offers; null follows the device. */
export type LanguageChoice = NonNullable<Schemas["UpdateMeRequest"]["language"]> | null;

export const languageChoices: readonly LanguageChoice[] = [null, "English", "Dutch"];

const localeOfChoice: Record<NonNullable<LanguageChoice>, AppLocale> = { English: "en", Dutch: "nl" };

/** Translations by locale; English is each message's defaultMessage. */
export const translations: Record<AppLocale, Record<string, string> | undefined> = { en: undefined, nl };

/** The first of the browser's languages the app comes in, else English. */
export function deviceLocale(languages: readonly string[] = globalThis.navigator?.languages ?? []): AppLocale {
  for (const tag of languages) {
    const primary = tag.toLowerCase().split("-")[0];
    if (primary === "nl") return "nl";
    if (primary === "en") return "en";
  }
  return "en";
}

/**
 * The locale for a profile's language. A value this version doesn't know (the API may
 * add languages) follows the device, as if none was chosen.
 */
export function localeFor(choice: string | null | undefined, device: AppLocale = deviceLocale()): AppLocale {
  return choice && choice in localeOfChoice ? localeOfChoice[choice as NonNullable<LanguageChoice>] : device;
}

/** A profile's language as one of the choices Profile shows. */
export function choiceOf(language: string | null | undefined): LanguageChoice {
  return language && language in localeOfChoice ? (language as NonNullable<LanguageChoice>) : null;
}

const storageKey = "golfprogress.language";

/** The choice remembered in this browser: undefined if there's none (or storage is off). */
export function rememberedChoice(): LanguageChoice | undefined {
  try {
    const stored = globalThis.localStorage?.getItem(storageKey);
    if (stored === null || stored === undefined) return undefined;
    return stored === "device" ? null : choiceOf(stored);
  } catch {
    return undefined;
  }
}

export function rememberChoice(choice: LanguageChoice) {
  try {
    globalThis.localStorage?.setItem(storageKey, choice ?? "device");
  } catch {
    // Private mode or storage switched off: the profile still has it.
  }
}

/**
 * The language shown right now, for code outside React: the API client sends it as
 * Accept-Language, so the API's own texts (micro-wins, next focus) match the screen.
 */
export const currentLanguage = {
  locale: localeFor(rememberedChoice()),
};
