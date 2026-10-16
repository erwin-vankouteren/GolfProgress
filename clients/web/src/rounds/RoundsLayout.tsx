import { useEffect, useRef } from "react";
import { FormattedMessage, useIntl } from "react-intl";
import { NavLink, Outlet, useMatch } from "react-router";
import { useRoundList } from "../api/queries";
import { useMediaQuery, wideScreen } from "../common/media";
import { LoadError, Skeleton } from "../common/states";
import { messages } from "../i18n/messages";
import { LogRoundLink } from "./LogRound";
import { formatPlayedDate } from "./score";
import { PaneHeading } from "./paneHeading";
import "./rounds.css";

/**
 * The Rounds section (#95): the list beside the selected round on desktop, and one at a
 * time on phones, where /rounds is the list and /rounds/:id the round. Every round has
 * its own URL either way.
 */

export function RoundsLayout() {
  const wide = useMediaQuery(wideScreen);
  const paneOpen = useMatch("/rounds/:segment/*") !== null;

  if (!wide) {
    return paneOpen ? (
      <Outlet />
    ) : (
      <section className="page">
        <RoundList headingLevel={1} />
      </section>
    );
  }

  return (
    <div className="rounds-layout">
      <section className="rounds-list-pane">
        <RoundList headingLevel={1} />
      </section>
      <div className="rounds-detail-pane">
        <PaneHeading.Provider value={2}>
          {paneOpen ? (
            <Outlet />
          ) : (
            <p className="rounds-placeholder">
              <FormattedMessage {...messages.selectRound} />
            </p>
          )}
        </PaneHeading.Provider>
      </div>
    </div>
  );
}

/** The user's rounds, newest first, loading more on scroll or with "Show more rounds". */
function RoundList({ headingLevel }: { headingLevel: 1 | 2 }) {
  const list = useRoundList();
  const intl = useIntl();
  const Heading = headingLevel === 1 ? "h1" : "h2";
  const sentinel = useRef<HTMLDivElement>(null);
  const { hasNextPage, isFetchingNextPage, fetchNextPage } = list;

  // Load the next page as the end of the list scrolls into view. The button stays for
  // keyboards, and for browsers without IntersectionObserver.
  useEffect(() => {
    const element = sentinel.current;
    if (!element || !hasNextPage || typeof IntersectionObserver === "undefined") return;
    const observer = new IntersectionObserver((entries) => {
      if (entries.some((e) => e.isIntersecting) && !isFetchingNextPage) void fetchNextPage();
    });
    observer.observe(element);
    return () => observer.disconnect();
  }, [hasNextPage, isFetchingNextPage, fetchNextPage]);

  const heading = (
    <Heading>
      <FormattedMessage {...messages.rounds} />
    </Heading>
  );

  if (list.isPending) {
    return (
      <>
        {heading}
        <Skeleton blocks={4} className="round-list-skeleton" />
      </>
    );
  }

  if (list.isError) {
    return (
      <>
        {heading}
        <LoadError message={messages.roundsLoadError} onRetry={() => void list.refetch()} />
      </>
    );
  }

  const rounds = list.data.pages.flatMap((page) => page.rounds);

  if (rounds.length === 0) {
    return (
      <>
        {heading}
        <div className="empty-state">
          <p>
            <FormattedMessage {...messages.roundsEmpty} />
          </p>
          <LogRoundLink className="button button--primary">
            <FormattedMessage {...messages.logRound} />
          </LogRoundLink>
        </div>
      </>
    );
  }

  return (
    <>
      {heading}
      <ul className="round-list">
        {rounds.map((round) => (
          <li key={round.roundId}>
            <NavLink to={`/rounds/${round.roundId}`} className="round-row">
              <span className="round-row-main">
                <span className="round-row-date">{formatPlayedDate(intl, round.playedAt)}</span>
                <span className="round-row-course">
                  {round.courseName ?? ""}
                  {round.holes === 9 && (
                    <>
                      {round.courseName ? " · " : ""}
                      <FormattedMessage {...messages.holesOption} values={{ count: 9 }} />
                    </>
                  )}
                </span>
              </span>
              <span className="round-row-figure">
                {round.totalStrokes}
                <span className="round-row-unit">
                  {" "}
                  <FormattedMessage {...messages.strokesUnit} />
                </span>
              </span>
              <span className="round-row-figure round-row-score">
                {round.fitnessScore ?? "–"}
                <span className="round-row-unit">
                  {" "}
                  <FormattedMessage {...(round.fitnessScore === null ? messages.notScored : messages.scoreUnit)} />
                </span>
              </span>
            </NavLink>
          </li>
        ))}
      </ul>
      {hasNextPage && (
        <div ref={sentinel} className="round-list-more">
          <button type="button" className="button" disabled={isFetchingNextPage} onClick={() => void fetchNextPage()}>
            <FormattedMessage {...(isFetchingNextPage ? messages.loading : messages.showMoreRounds)} />
          </button>
        </div>
      )}
    </>
  );
}
