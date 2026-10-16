import type { ReactNode } from "react";
import { FormattedMessage, FormattedNumber, useIntl } from "react-intl";
import { Link } from "react-router";
import { useDashboard, useFitnessScore, type Dashboard } from "../api/queries";
import { LoadError, Skeleton } from "../common/states";
import { messages, type PlainMessage } from "../i18n/messages";
import { LogRoundLink } from "../rounds/LogRound";
import { ConfidenceBadge, PillarBars, formatPlayedDate } from "../rounds/score";
import "./dashboard.css";

/**
 * Home: where the user stands after their last round (#88, docs/clients/ux-design.md →
 * "Home: dashboard"). It refreshes when the window regains focus (the QueryClient's
 * refetchOnWindowFocus), so it's current when the user comes back to the tab.
 */
export function DashboardPage() {
  const dashboard = useDashboard();

  return (
    <section className="page dashboard">
      <h1>
        <FormattedMessage {...messages.home} />
      </h1>
      {dashboard.isPending ? (
        <Skeleton blocks={4} className="dashboard-grid" />
      ) : dashboard.isError ? (
        <LoadError message={messages.dashboardLoadError} onRetry={() => void dashboard.refetch()} />
      ) : dashboard.data.lastRound ? (
        <DashboardCards dashboard={dashboard.data} lastRound={dashboard.data.lastRound} />
      ) : (
        <div className="empty-state">
          <p>
            <FormattedMessage {...messages.homeEmpty} />
          </p>
          <LogRoundLink className="button button--primary">
            <FormattedMessage {...messages.logFirstRound} />
          </LogRoundLink>
        </div>
      )}
    </section>
  );
}

function DashboardCards({
  dashboard,
  lastRound,
}: {
  dashboard: Dashboard;
  lastRound: NonNullable<Dashboard["lastRound"]>;
}) {
  const intl = useIntl();
  // The pillars aren't in the dashboard response; the round's own score has them.
  const score = useFitnessScore(lastRound.roundId);

  return (
    <div className="dashboard-grid">
      <Card title={messages.lastRound} id="last-round" wide>
        <Link to={`/rounds/${lastRound.roundId}`} className="last-round-link">
          <FormattedMessage
            {...messages.lastRoundLink}
            values={{ date: formatPlayedDate(intl, lastRound.playedAt), strokes: lastRound.totalStrokes }}
          />
          <span aria-hidden="true"> ›</span>
        </Link>
        {lastRound.fitnessScore !== null && lastRound.fitnessConfidence !== null ? (
          <p className="score-line">
            <span className="visually-hidden">
              <FormattedMessage {...messages.fitnessScore} />{" "}
            </span>
            <span className="score-total">{lastRound.fitnessScore}</span>
            <ConfidenceBadge level={lastRound.fitnessConfidence} explain />
          </p>
        ) : (
          <p>
            <FormattedMessage {...messages.notScoredYet} />
          </p>
        )}
        {score.data && <PillarBars score={score.data} />}
      </Card>

      {dashboard.nextLever && (
        <Card title={messages.nextFocus} id="next-focus">
          <p className="next-focus-title">{dashboard.nextLever.title}</p>
          <p>{dashboard.nextLever.recommendation}</p>
          <p className="card-detail">{dashboard.nextLever.rationale}</p>
        </Card>
      )}

      {dashboard.baselineAvgScore !== null && (
        <Card title={messages.baseline} id="baseline">
          <p className="stat">
            <span className="stat-label">
              <FormattedMessage {...messages.baselineAverage} />
            </span>
            <span className="stat-value">
              <FormattedNumber value={dashboard.baselineAvgScore} maximumFractionDigits={1} />
            </span>
          </p>
          {dashboard.baselineConfidence && <ConfidenceBadge level={dashboard.baselineConfidence} explain />}
        </Card>
      )}

      {dashboard.microWins.length > 0 && (
        <Card title={messages.microWins} id="micro-wins" wide>
          <ul className="micro-wins">
            {dashboard.microWins.slice(0, 3).map((win) => (
              <li key={win.microWinId}>
                <span aria-hidden="true">✓ </span>
                <strong>{win.title}</strong>
              </li>
            ))}
          </ul>
        </Card>
      )}

      {dashboard.monthlyFitnessAvg !== null && (
        <Card title={messages.thisMonth} id="this-month">
          <p className="stat">
            <span className="stat-label">
              <FormattedMessage {...messages.monthAverage} />
            </span>
            <span className="stat-value">
              <FormattedNumber value={dashboard.monthlyFitnessAvg} maximumFractionDigits={0} />
            </span>
          </p>
          {dashboard.monthlyFitnessChange !== null && <MonthChange change={dashboard.monthlyFitnessChange} />}
        </Card>
      )}
    </div>
  );
}

function Card({
  title,
  id,
  wide = false,
  children,
}: {
  title: PlainMessage;
  id: string;
  wide?: boolean;
  children: ReactNode;
}) {
  return (
    <section className={`card dashboard-card${wide ? " dashboard-card--wide" : ""}`} aria-labelledby={id}>
      <h2 id={id}>
        <FormattedMessage {...title} />
      </h2>
      {children}
    </section>
  );
}

/** "▲ Up 4 on last month": the arrow is decoration, the words carry the direction. */
export function MonthChange({ change }: { change: number }) {
  const intl = useIntl();
  const rounded = Math.round(change);
  if (rounded === 0) {
    return (
      <p className="month-change">
        <FormattedMessage {...messages.sameAsLastMonth} />
      </p>
    );
  }

  const amount = intl.formatNumber(Math.abs(rounded));
  return (
    <p className={`month-change month-change--${rounded > 0 ? "up" : "down"}`}>
      <span aria-hidden="true">{rounded > 0 ? "▲ " : "▼ "}</span>
      <FormattedMessage
        {...(rounded > 0 ? messages.upOnLastMonth : messages.downOnLastMonth)}
        values={{ change: amount }}
      />
    </p>
  );
}
