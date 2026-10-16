import { FormattedDate, FormattedMessage, useIntl } from "react-intl";
import type { Schemas } from "../api";
import { messages } from "../i18n/messages";
import "./rounds.css";

/** Pieces that show a round's score, shared by the round detail and the dashboard. */

export type Confidence = "Low" | "Medium" | "High";

const confidenceLabels = {
  Low: messages.confidenceLow,
  Medium: messages.confidenceMedium,
  High: messages.confidenceHigh,
};

/**
 * A confidence level: a coloured dot plus its name, never the colour alone (#78). Low
 * confidence also says why, when `explain` is set.
 */
export function ConfidenceBadge({ level, explain = false }: { level: Confidence; explain?: boolean }) {
  return (
    <span className="confidence">
      <span className={`confidence-badge confidence-badge--${level.toLowerCase()}`}>
        <span aria-hidden="true">● </span>
        <span className="visually-hidden">
          <FormattedMessage {...messages.confidencePrefix} />{" "}
        </span>
        <FormattedMessage {...confidenceLabels[level]} />
      </span>
      {explain && level === "Low" && (
        <span className="confidence-explanation">
          <FormattedMessage {...messages.lowConfidenceExplanation} />
        </span>
      )}
    </span>
  );
}

/** The most a single pillar can score (ProgressComputationService.PillarMax). */
const pillarMax = 25;

/**
 * The four pillars, each with its name and "18 / 25" as text. The bar repeats the number
 * for sighted users and is hidden from screen readers.
 */
export function PillarBars({ score }: { score: Schemas["FitnessScoreResponse"] }) {
  const pillars = [
    { key: "execution", label: messages.execution, value: score.execution },
    { key: "damage-control", label: messages.damageControl, value: score.damageControl },
    { key: "consistency", label: messages.consistency, value: score.consistency },
    { key: "habits", label: messages.habits, value: score.habits },
  ];

  return (
    <dl className="pillars">
      {pillars.map((pillar) => (
        <div key={pillar.key} className="pillar">
          <dt>
            <FormattedMessage {...pillar.label} />
          </dt>
          <dd>
            <span className="pillar-value">
              <FormattedMessage {...messages.pillarScore} values={{ score: pillar.value, max: pillarMax }} />
            </span>
            <span className="pillar-track" aria-hidden="true">
              <span
                className={`pillar-fill pillar-fill--${pillar.key}`}
                style={{ width: `${(Math.min(pillar.value, pillarMax) / pillarMax) * 100}%` }}
              />
            </span>
          </dd>
        </div>
      ))}
    </dl>
  );
}

/**
 * A date the API sends as yyyy-MM-dd, e.g. "3 May 2026" in the user's locale. Read as UTC
 * and shown in UTC, so it never shifts a day in a time zone behind or ahead of UTC.
 */
export function PlayedDate({ value, withYear = true }: { value: string; withYear?: boolean }) {
  return (
    <FormattedDate
      value={new Date(`${value}T00:00:00Z`)}
      timeZone="UTC"
      day="numeric"
      month="short"
      year={withYear ? "numeric" : undefined}
    />
  );
}

/** "Hilversum · 18 holes · 88 strokes", or without the course when there's none. */
export function useRoundSummary() {
  const intl = useIntl();
  return (round: { courseName?: string | null; holes: number; totalStrokes: number }) =>
    round.courseName
      ? intl.formatMessage(messages.roundSummaryWithCourse, {
          course: round.courseName,
          holes: round.holes,
          strokes: round.totalStrokes,
        })
      : intl.formatMessage(messages.roundSummary, { holes: round.holes, strokes: round.totalStrokes });
}
