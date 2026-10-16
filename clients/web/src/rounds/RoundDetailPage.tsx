import type { ReactNode } from "react";
import { FormattedMessage, useIntl, type IntlShape } from "react-intl";
import { Link, useNavigate, useParams } from "react-router";
import { ApiProblem, type Schemas } from "../api";
import { useFitnessScore, useMicroWins, useNextLever, useReflections, useRound } from "../api/queries";
import { LoadError, Skeleton } from "../common/states";
import { useMediaQuery, wideScreen } from "../common/media";
import { messages, type PlainMessage } from "../i18n/messages";
import { LogRoundForm } from "./LogRoundForm";
import { explainPillars, trackedStats, type Sentence } from "./roundDetail";
import type { StatField } from "./roundForm";
import { usePaneHeadingLevel } from "./paneHeading";
import { ConfidenceBadge, PlayedDate, useRoundSummary } from "./score";
import "./rounds.css";

/**
 * A round (#95): its score with each pillar's explanation, micro-wins, the next focus,
 * its stats and its reflection. Where Log round lands after saving (#92), and where the
 * list and the dashboard (#88) lead.
 */
export function RoundDetailPage() {
  const { roundId } = useParams<{ roundId: string }>();
  const round = useRound(roundId!);
  const summary = useRoundSummary();
  const level = usePaneHeadingLevel();
  const Title = level === 1 ? "h1" : "h2";
  const Section = level === 1 ? "h2" : "h3";

  if (round.isPending) {
    return (
      <section className="page">
        <BackLink />
        <Skeleton blocks={3} />
      </section>
    );
  }

  if (round.isError) {
    return (
      <section className="page">
        <BackLink />
        {round.error instanceof ApiProblem && round.error.status === 404 ? (
          <div className="empty-state" role="status">
            <p>
              <FormattedMessage {...messages.roundNoLongerExists} />
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
      <BackLink />
      <div className="round-detail-header">
        <Title>
          <PlayedDate value={round.data.playedAt} />
        </Title>
        <Link to={`/rounds/${round.data.roundId}/edit`} className="button">
          <FormattedMessage {...messages.edit} />
        </Link>
      </div>
      <p className="round-summary">{summary(round.data)}</p>

      <ScoreCard round={round.data} Heading={Section} />
      <MicroWinsCard roundId={round.data.roundId} Heading={Section} />
      <NextFocusCard roundId={round.data.roundId} Heading={Section} />
      <StatsCard round={round.data} Heading={Section} />
      <ReflectionCard roundId={round.data.roundId} Heading={Section} />
    </section>
  );
}

/** Back to the list, where the list isn't already beside the round. */
function BackLink() {
  const wide = useMediaQuery(wideScreen);
  return wide ? null : (
    <Link to="/rounds" className="back-link">
      <span aria-hidden="true">‹ </span>
      <FormattedMessage {...messages.backToRounds} />
    </Link>
  );
}

type HeadingTag = "h2" | "h3";

function Card({ title, Heading, children }: { title: PlainMessage; Heading: HeadingTag; children: ReactNode }) {
  const intl = useIntl();
  return (
    <section className="card" aria-label={intl.formatMessage(title)}>
      <Heading>
        <FormattedMessage {...title} />
      </Heading>
      {children}
    </section>
  );
}

const pillarMax = 25;

function ScoreCard({ round, Heading }: { round: Schemas["RoundResponse"]; Heading: HeadingTag }) {
  const score = useFitnessScore(round.roundId);
  const intl = useIntl();

  return (
    <Card title={messages.fitnessScore} Heading={Heading}>
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
          <ExplainedPillars round={round} score={score.data} intl={intl} />
        </>
      )}
    </Card>
  );
}

/**
 * Each pillar as a disclosure: its name and "18 / 25", with a bar that repeats the number,
 * opening to why it scored that. <details> is keyboard- and screen-reader-ready as is.
 */
function ExplainedPillars({
  round,
  score,
  intl,
}: {
  round: Schemas["RoundResponse"];
  score: Schemas["FitnessScoreResponse"];
  intl: IntlShape;
}) {
  const explanations = explainPillars(round, score);
  const pillars = [
    { key: "execution", label: messages.execution, value: score.execution, why: explanations.execution },
    {
      key: "damage-control",
      label: messages.damageControl,
      value: score.damageControl,
      why: explanations.damageControl,
    },
    { key: "consistency", label: messages.consistency, value: score.consistency, why: explanations.consistency },
    { key: "habits", label: messages.habits, value: score.habits, why: explanations.habits },
  ];

  return (
    <div className="pillars pillars--explained">
      {pillars.map((pillar) => (
        <details key={pillar.key} className="pillar-details">
          <summary className="pillar">
            <span className="pillar-name">
              <FormattedMessage {...pillar.label} />
            </span>
            <span className="pillar-value">
              <FormattedMessage {...messages.pillarScore} values={{ score: pillar.value, max: pillarMax }} />
            </span>
            <span className="pillar-track" aria-hidden="true">
              <span
                className={`pillar-fill pillar-fill--${pillar.key}`}
                style={{ width: `${(Math.min(pillar.value, pillarMax) / pillarMax) * 100}%` }}
              />
            </span>
          </summary>
          <p className="pillar-explanation">{pillar.why.map((s) => sentenceText(s, intl)).join(" ")}</p>
        </details>
      ))}
    </div>
  );
}

/** A Sentence from roundDetail.ts, in the user's language. */
export function sentenceText(sentence: Sentence, intl: IntlShape): string {
  const n = (value: number) => intl.formatNumber(value, { maximumFractionDigits: 1 });
  switch (sentence.key) {
    case "firstRound":
      return intl.formatMessage(messages.firstRound);
    case "comparedWithAverage": {
      const values = { strokes: sentence.strokes, average: n(sentence.average) } as const;
      if (sentence.difference < 0)
        return intl.formatMessage(messages.betterThanAverage, { ...values, amount: n(-sentence.difference) });
      if (sentence.difference > 0)
        return intl.formatMessage(messages.worseThanAverage, { ...values, amount: n(sentence.difference) });
      return intl.formatMessage(messages.levelWithAverage, values);
    }
    case "nineHoleEquivalent":
      return intl.formatMessage(messages.nineHoleEquivalent, { strokes: sentence.strokes, per18: sentence.per18 });
    case "damageNotTracked":
      return intl.formatMessage(messages.damageNotTracked);
    case "damage":
      return intl.formatMessage(messages.damage, {
        penalties:
          sentence.penalties === null
            ? intl.formatMessage(messages.penaltiesNotTracked)
            : intl.formatMessage(messages.penaltiesCount, { count: sentence.penalties }),
        blowUps:
          sentence.blowUps === null
            ? intl.formatMessage(messages.blowUpsNotTracked)
            : intl.formatMessage(messages.blowUpsCount, { count: sentence.blowUps }),
      });
    case "consistencyNeedsRounds":
      return intl.formatMessage(messages.consistencyNeedsRounds);
    case "consistencyAtOrBetter":
      return intl.formatMessage(messages.consistencyAtOrBetter);
    case "consistencyWorse":
      return intl.formatMessage(messages.consistencyWorse, {
        worseBy: n(sentence.worseBy),
        spread: n(sentence.spread),
      });
    case "habitsUnknown":
      return intl.formatMessage(messages.habitsUnknown);
    case "habits":
      return intl.formatMessage(messages.habitsExplanation, { rounds: sentence.rounds, practice: sentence.practice });
    case "reflected":
      return intl.formatMessage(messages.reflected);
    case "notReflected":
      return intl.formatMessage(messages.notReflected);
  }
}

function MicroWinsCard({ roundId, Heading }: { roundId: string; Heading: HeadingTag }) {
  const wins = useMicroWins(roundId);
  if (!wins.data || wins.data.length === 0) return null;

  return (
    <Card title={messages.microWins} Heading={Heading}>
      <ul className="micro-wins">
        {wins.data.map((win) => (
          <li key={win.microWinId}>
            <span aria-hidden="true">✓ </span>
            <strong>{win.title}</strong>
            <span className="micro-win-message">{win.message}</span>
          </li>
        ))}
      </ul>
    </Card>
  );
}

function NextFocusCard({ roundId, Heading }: { roundId: string; Heading: HeadingTag }) {
  const lever = useNextLever(roundId);
  if (!lever.data) return null;

  return (
    <Card title={messages.nextFocus} Heading={Heading}>
      <p className="next-focus-title">{lever.data.title}</p>
      <p>{lever.data.recommendation}</p>
      <p className="card-detail">{lever.data.rationale}</p>
    </Card>
  );
}

const statLabels: Record<StatField, PlainMessage> = {
  totalPutts: messages.putts,
  penalties: messages.penalties,
  fairwaysHit: messages.fairways,
  gir: messages.greens,
  threePutts: messages.threePutts,
  blowUpHoles: messages.blowUpHoles,
};

function StatsCard({ round, Heading }: { round: Schemas["RoundResponse"]; Heading: HeadingTag }) {
  const tracked = trackedStats(round);

  return (
    <Card title={messages.stats} Heading={Heading}>
      {tracked.length === 0 ? (
        <p>
          <FormattedMessage {...messages.noStatsTracked} />
        </p>
      ) : (
        <dl className="stat-list">
          {tracked.map((field) => (
            <div key={field}>
              <dt>
                <FormattedMessage {...statLabels[field]} />
              </dt>
              <dd>{round[field]}</dd>
            </div>
          ))}
        </dl>
      )}
      {(round.teeName || round.notes) && (
        <dl className="stat-list">
          {round.teeName && (
            <div>
              <dt>
                <FormattedMessage {...messages.tee} />
              </dt>
              <dd>{round.teeName}</dd>
            </div>
          )}
          {round.notes && (
            <div className="stat-list-wide">
              <dt>
                <FormattedMessage {...messages.notes} />
              </dt>
              <dd>{round.notes}</dd>
            </div>
          )}
        </dl>
      )}
    </Card>
  );
}

function ReflectionCard({ roundId, Heading }: { roundId: string; Heading: HeadingTag }) {
  const reflections = useReflections(roundId);
  // The latest one; adding reflections arrives with #99.
  const latest = reflections.data?.at(-1);

  return (
    <Card title={messages.reflection} Heading={Heading}>
      {reflections.isPending ? (
        <Skeleton blocks={1} />
      ) : !latest ? (
        <p>
          <FormattedMessage {...messages.noReflection} />
        </p>
      ) : (
        <dl className="stat-list">
          {latest.focusRating !== null && (
            <Entry label={messages.focus}>
              <FormattedMessage {...messages.outOfFive} values={{ value: latest.focusRating }} />
            </Entry>
          )}
          {latest.moodRating !== null && (
            <Entry label={messages.mood}>
              <FormattedMessage {...messages.outOfFive} values={{ value: latest.moodRating }} />
            </Entry>
          )}
          {latest.frustrationSpillover !== null && (
            <Entry label={messages.frustrationCarriedOver}>
              <FormattedMessage {...(latest.frustrationSpillover ? messages.yes : messages.no)} />
            </Entry>
          )}
          {latest.whatWorked && <Entry label={messages.whatWorked}>{latest.whatWorked}</Entry>}
          {latest.whatToRepeat && <Entry label={messages.whatToRepeat}>{latest.whatToRepeat}</Entry>}
          {latest.oneChangeNext && <Entry label={messages.oneChangeNext}>{latest.oneChangeNext}</Entry>}
        </dl>
      )}
    </Card>
  );
}

function Entry({ label, children }: { label: PlainMessage; children: ReactNode }) {
  return (
    <div className="stat-list-wide">
      <dt>
        <FormattedMessage {...label} />
      </dt>
      <dd>{children}</dd>
    </div>
  );
}

/** /rounds/:roundId/edit: the Log round form, prefilled, with Delete (#95). */
export function EditRoundPage() {
  const { roundId } = useParams<{ roundId: string }>();
  const round = useRound(roundId!);
  const navigate = useNavigate();
  const level = usePaneHeadingLevel();

  if (round.isPending) return <Skeleton blocks={3} />;
  if (round.isError) {
    return round.error instanceof ApiProblem && round.error.status === 404 ? (
      <div className="empty-state" role="status">
        <p>
          <FormattedMessage {...messages.roundNoLongerExists} />
        </p>
      </div>
    ) : (
      <LoadError message={messages.roundLoadError} onRetry={() => void round.refetch()} />
    );
  }

  return (
    <section className="page log-round-page">
      <LogRoundForm
        // A fresh form for each round, so switching rounds doesn't keep the old values.
        key={round.data.roundId}
        headingLevel={level}
        round={round.data}
        // Replace the form in history: Back from the round shouldn't reopen the edit.
        onSaved={(id) => navigate(`/rounds/${id}`, { replace: true })}
        onCancel={() => navigate(`/rounds/${round.data.roundId}`, { replace: true })}
        onDeleted={() => navigate("/rounds", { replace: true })}
      />
    </section>
  );
}
