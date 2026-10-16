import { FormattedMessage } from "react-intl";
import { Link, useParams } from "react-router";
import { ApiProblem } from "../api";
import { useFitnessScore, useMicroWins, useRound } from "../api/queries";
import { LoadError, Skeleton } from "../common/states";
import { messages } from "../i18n/messages";
import { ConfidenceBadge, PillarBars, PlayedDate, useRoundSummary } from "./score";
import "./rounds.css";

/**
 * A round and its score: where Log round lands after saving (#92), and where the
 * dashboard's last round leads (#88). The full detail, with pillar explanations, the
 * next focus, the reflection and editing, follows in #95.
 */
export function RoundDetailPage() {
  const { roundId } = useParams<{ roundId: string }>();
  const round = useRound(roundId!);
  const score = useFitnessScore(roundId);
  const wins = useMicroWins(roundId!);
  const summary = useRoundSummary();

  const back = (
    <Link to="/rounds" className="back-link">
      <span aria-hidden="true">‹ </span>
      <FormattedMessage {...messages.backToRounds} />
    </Link>
  );

  if (round.isPending) {
    return (
      <section className="page">
        {back}
        <Skeleton blocks={2} />
      </section>
    );
  }

  if (round.isError) {
    const notFound = round.error instanceof ApiProblem && round.error.status === 404;
    return (
      <section className="page">
        {back}
        {notFound ? (
          <div className="empty-state">
            <p>
              <FormattedMessage {...messages.roundNotFound} />
            </p>
          </div>
        ) : (
          <LoadError message={messages.roundLoadError} onRetry={() => void round.refetch()} />
        )}
      </section>
    );
  }

  return (
    <section className="page round-detail">
      {back}
      <h1>
        <PlayedDate value={round.data.playedAt} />
      </h1>
      <p className="round-summary">{summary(round.data)}</p>

      <section className="card" aria-labelledby="score-heading">
        <h2 id="score-heading">
          <FormattedMessage {...messages.fitnessScore} />
        </h2>
        {score.isPending ? (
          <Skeleton blocks={1} />
        ) : score.isError ? (
          <LoadError message={messages.roundLoadError} onRetry={() => void score.refetch()} />
        ) : score.data === null ? (
          <p>
            <FormattedMessage {...messages.notScoredYet} />
          </p>
        ) : (
          <>
            <p className="score-line">
              <span className="score-total">{score.data.scoreTotal}</span>
              <ConfidenceBadge level={score.data.confidenceLevel} explain />
            </p>
            <PillarBars score={score.data} />
          </>
        )}
      </section>

      {wins.data && wins.data.length > 0 && (
        <section className="card" aria-labelledby="wins-heading">
          <h2 id="wins-heading">
            <FormattedMessage {...messages.microWins} />
          </h2>
          <ul className="micro-wins">
            {wins.data.map((win) => (
              <li key={win.microWinId}>
                <span aria-hidden="true">✓ </span>
                <strong>{win.title}</strong>
                <span className="micro-win-message">{win.message}</span>
              </li>
            ))}
          </ul>
        </section>
      )}
    </section>
  );
}
