import { useEffect, useRef, useState } from "react";
import { FormattedMessage, useIntl } from "react-intl";
import { Link, NavLink, Outlet, useLocation } from "react-router";
import { messages } from "../i18n/messages";
import "./AppLayout.css";

/** The five top-level sections (docs/clients/ux-design.md → "Navigation"). */
export const sections = [
  { path: "/", label: messages.home, end: true },
  { path: "/rounds", label: messages.rounds, end: false },
  { path: "/practice", label: messages.practice, end: false },
  { path: "/progress", label: messages.progress, end: false },
  { path: "/profile", label: messages.profile, end: false },
] as const;

/**
 * The signed-in app frame (#75): header with "Log round", sidebar navigation that
 * collapses into a menu on narrow screens, and the current section.
 */
export function AppLayout() {
  const intl = useIntl();
  const location = useLocation();
  // The menu remembers the page it was opened on, so navigating closes it without an
  // extra state update.
  const [menuOpenOn, setMenuOpenOn] = useState<string | null>(null);
  const menuOpen = menuOpenOn === location.pathname;
  const setMenuOpen = (toggle: (open: boolean) => boolean) =>
    setMenuOpenOn(toggle(menuOpen) ? location.pathname : null);
  const main = useRef<HTMLElement>(null);
  const firstRender = useRef(true);

  // After navigating, move focus to the new page, so keyboard and screen-reader users
  // land on its content rather than staying in the menu.
  useEffect(() => {
    if (firstRender.current) {
      firstRender.current = false;
      return;
    }
    main.current?.focus();
  }, [location.pathname]);

  return (
    <div className="app-layout">
      <a className="skip-link" href="#main">
        <FormattedMessage {...messages.skipToContent} />
      </a>

      <header className="app-header">
        <button
          type="button"
          className="menu-button"
          aria-expanded={menuOpen}
          aria-controls="main-navigation"
          aria-label={intl.formatMessage(menuOpen ? messages.closeMenu : messages.openMenu)}
          onClick={() => setMenuOpen((open) => !open)}
        >
          <span aria-hidden="true">☰</span>
        </button>
        <Link to="/" className="app-name">
          <FormattedMessage {...messages.appName} />
        </Link>
        <Link to="/rounds/new" className="button button--primary">
          <FormattedMessage {...messages.logRound} />
        </Link>
      </header>

      <nav
        id="main-navigation"
        className="app-nav"
        data-open={menuOpen}
        aria-label={intl.formatMessage(messages.mainNavigation)}
      >
        <ul>
          {sections.map((section) => (
            <li key={section.path}>
              <NavLink to={section.path} end={section.end}>
                <FormattedMessage {...section.label} />
              </NavLink>
            </li>
          ))}
        </ul>
      </nav>

      <main id="main" ref={main} tabIndex={-1} className="app-main">
        <Outlet />
      </main>
    </div>
  );
}
