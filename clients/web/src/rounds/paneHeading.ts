import { createContext, useContext } from "react";

/**
 * The heading level for what's open in the Rounds section (#95): h2 beside the list's
 * "Rounds" heading on desktop, the page's own h1 on phones.
 */
export const PaneHeading = createContext<1 | 2>(1);

export const usePaneHeadingLevel = () => useContext(PaneHeading);
