import { useMutation, useQueryClient } from "@tanstack/react-query";
import { lazy, Suspense, useId, useState, type FormEvent, type ReactNode } from "react";
import { FormattedMessage, FormattedNumber, useIntl, type IntlShape } from "react-intl";
import { Link } from "react-router";
import { ApiProblem, microWinCategoryOf, unwrap, type MicroWinCategory, type Schemas } from "../api";
import {
  queryKeys,
  useBaseline,
  useMonthlyReview,
  useMonthlyTimeline,
  useRoundCount,
  useTimeline,
} from "../api/queries";
import { useApi } from "../app/ApiContext";
import { LoadError, Skeleton, useOnline } from "../common/states";
import { MonthChange } from "../dashboard/DashboardPage";
import { messages, type PlainMessage } from "../i18n/messages";
import { LogRoundLink } from "../rounds/LogRound";
import { ConfidenceBadge, PlayedDate } from "../rounds/score";
import {
  checkFocus,
  customRangeIsValid,
  defaultReviewMonth,
  hasMonthlyTrend,
  maxFocusLength,
  monthlySummary,
  rangeKeys,
  rangeQuery,
  roundsShortOfTrend,
  strokesLeaveOutRounds,
  timelineChart,
  timelineSummary,
  timelineTake,
  todayIso,
  trendMonths,
  type CustomRange,
  type MonthPoint,
  type RangeKey,
  type TimelinePoint,
} from "./progress";
import "../rounds/rounds.css";
import "./progress.css";

/**
 * Progress (#107, docs/clients/ux-design.md → "Progress"): rounds over time, the monthly
 * trend, the baseline and monthly reviews. Every chart has a summary sentence and a
 * table, so nothing depends on seeing the chart.
 */

const TimelineChart = lazy(() => import("./ProgressCharts").then((m) => ({ default: m.TimelineChart })));
const MonthlyChart = lazy(() => import("./ProgressCharts").then((m) => ({ default: m.MonthlyChart })));

const rangeLabels: Record<RangeKey, PlainMessage> = {
  "3m": messages.range3m,
  "6m": messages.range6m,
  "12m": messages.range12m,
  all: messages.rangeAll,
  custom: messages.rangeCustom,
};

const utcDate = (iso: string) => new Date(`${iso}T00:00:00Z`);
const longDate = (intl: IntlShape, iso: string) =>
  intl.formatDate(utcDate(iso), { timeZone: "UTC", day: "numeric", month: "short", year: "numeric" });
const monthName = (intl: IntlShape, iso: string) =>
  intl.formatDate(utcDate(iso), { timeZone: "UTC", month: "long", year: "numeric" });

export function ProgressPage() {
  const roundCount = useRoundCount();

  return (
    <section className="page progress">
      <h1>
        <FormattedMessage {...messages.progress} />
      </h1>
      {roundCount.isPending ? (
        <Skeleton blocks={3} className="progress-skeleton" />
      ) : roundCount.isError ? (
        <LoadError message={messages.progressLoadError} onRetry={() => void roundCount.refetch()} />
      ) : roundCount.data === 0 ? (
        <div className="empty-state">
          <p>
            <FormattedMessage {...messages.progressEmpty} />
          </p>
          <LogRoundLink className="button button--primary">
            <FormattedMessage {...messages.logFirstRound} />
          </LogRoundLink>
        </div>
      ) : (
        <>
          <TimelineSection totalRounds={roundCount.data} />
          <MonthlySection />
          <BaselineSection />
          <ReviewSection />
        </>
      )}
    </section>
  );
}

function Section({ title, children }: { title: PlainMessage; children: ReactNode }) {
  const id = useId();
  return (
    <section className="card progress-section" aria-labelledby={id}>
      <h2 id={id}>
        <FormattedMessage {...title} />
      </h2>
      {children}
    </section>
  );
}

/**
 * A chart with its text alternative (accessibility checklist #4): the summary is
 * always there, and "Show as table" swaps the chart for its data.
 */
function ChartOrTable({ summary, chart, table }: { summary: ReactNode; chart: ReactNode; table: ReactNode }) {
  const [asTable, setAsTable] = useState(false);
  return (
    <>
      <div className="chart-summary">{summary}</div>
      <button type="button" className="button chart-toggle" onClick={() => setAsTable((t) => !t)}>
        <FormattedMessage {...(asTable ? messages.showAsChart : messages.showAsTable)} />
      </button>
      {asTable ? table : <Suspense fallback={<Skeleton blocks={1} className="chart-skeleton" />}>{chart}</Suspense>}
    </>
  );
}

// ---------------------------------------------------------------- rounds over time

function TimelineSection({ totalRounds }: { totalRounds: number }) {
  const intl = useIntl();
  const id = useId();
  const [range, setRange] = useState<RangeKey>("12m");
  const [custom, setCustom] = useState<CustomRange>({ from: "", to: "" });
  const valid = range !== "custom" || customRangeIsValid(custom);
  const [query, setQuery] = useState(() => rangeQuery("12m", todayIso(), custom));
  const timeline = useTimeline(query, timelineTake);

  const choose = (nextRange: RangeKey, nextCustom: CustomRange) => {
    setRange(nextRange);
    setCustom(nextCustom);
    // An impossible custom range keeps the last one on screen until it's fixed.
    if (nextRange !== "custom" || customRangeIsValid(nextCustom))
      setQuery(rangeQuery(nextRange, todayIso(), nextCustom));
  };

  return (
    <Section title={messages.timelineTitle}>
      <div className="range-controls">
        <div className="field">
          <label htmlFor={`${id}-range`}>
            <FormattedMessage {...messages.period} />
          </label>
          <select id={`${id}-range`} value={range} onChange={(e) => choose(e.target.value as RangeKey, custom)}>
            {rangeKeys.map((key) => (
              <option key={key} value={key}>
                {intl.formatMessage(rangeLabels[key])}
              </option>
            ))}
          </select>
        </div>
        {range === "custom" && (
          <>
            {(["from", "to"] as const).map((end) => (
              <div className="field" key={end}>
                <label htmlFor={`${id}-${end}`}>
                  <FormattedMessage {...(end === "from" ? messages.rangeFrom : messages.rangeTo)} />
                </label>
                <input
                  id={`${id}-${end}`}
                  type="date"
                  value={custom[end]}
                  aria-invalid={valid ? undefined : true}
                  aria-describedby={valid ? undefined : `${id}-range-error`}
                  onChange={(e) => choose("custom", { ...custom, [end]: e.target.value })}
                />
              </div>
            ))}
            {!valid && (
              <p id={`${id}-range-error`} className="field-error range-error" role="alert">
                <FormattedMessage {...messages.rangeOrder} />
              </p>
            )}
          </>
        )}
      </div>

      {timeline.isPending ? (
        <Skeleton blocks={1} className="chart-skeleton" />
      ) : timeline.isError ? (
        <LoadError message={messages.timelineLoadError} onRetry={() => void timeline.refetch()} />
      ) : (
        <TimelineContent
          points={timeline.data.items}
          allRoundsShown={range === "all" || timeline.data.items.length >= totalRounds}
        />
      )}
    </Section>
  );
}

function TimelineContent({ points, allRoundsShown }: { points: TimelinePoint[]; allRoundsShown: boolean }) {
  const intl = useIntl();
  const summary = timelineSummary(points);
  if (!summary)
    return (
      <p className="hint">
        <FormattedMessage {...messages.noRoundsInPeriod} />
      </p>
    );

  const { rows, strokesHoles } = timelineChart(points);
  const short = roundsShortOfTrend(points);
  const scored = points.filter((p) => p.fitnessScoreTotal !== null).length;

  const sentence = (
    <p>
      <FormattedMessage
        {...messages.timelineSummary}
        values={{
          rounds: summary.rounds,
          from: longDate(intl, summary.firstDate),
          to: longDate(intl, summary.lastDate),
        }}
      />
      {summary.score && (
        <>
          {" "}
          <FormattedMessage {...messages.timelineScoreSummary} values={summary.score} />
        </>
      )}
    </p>
  );
  const capped = points.length >= timelineTake && (
    <p className="hint">
      <FormattedMessage {...messages.timelineCapped} values={{ count: timelineTake }} />
    </p>
  );
  const table = <TimelineTable points={points} />;

  // Too few for a line: say how many more, and show what there is as a table.
  if (short > 0)
    return (
      <>
        <div className="chart-summary">{sentence}</div>
        <p className="low-data">
          {allRoundsShown ? (
            <FormattedMessage {...messages.moreRoundsForTrend} values={{ count: short }} />
          ) : (
            <FormattedMessage {...messages.fewRoundsInPeriod} values={{ count: scored }} />
          )}
        </p>
        {table}
      </>
    );

  return (
    <ChartOrTable
      summary={
        <>
          {sentence}
          {capped}
          {strokesHoles && strokesLeaveOutRounds(points) && (
            <p className="hint">
              <FormattedMessage {...messages.strokesLeaveOut} values={{ holes: strokesHoles }} />
            </p>
          )}
        </>
      }
      chart={<TimelineChart rows={rows} strokesHoles={strokesHoles} />}
      table={table}
    />
  );
}

function TimelineTable({ points }: { points: TimelinePoint[] }) {
  return (
    <div className="table-scroll">
      <table className="progress-table">
        <caption className="visually-hidden">
          <FormattedMessage {...messages.timelineTitle} />
        </caption>
        <thead>
          <tr>
            <th scope="col">
              <FormattedMessage {...messages.date} />
            </th>
            <th scope="col">
              <FormattedMessage {...messages.holes} />
            </th>
            <th scope="col">
              <FormattedMessage {...messages.strokes} />
            </th>
            <th scope="col">
              <FormattedMessage {...messages.fitnessScore} />
            </th>
            <th scope="col">
              <FormattedMessage {...messages.confidenceColumn} />
            </th>
          </tr>
        </thead>
        <tbody>
          {/* Newest first, like the round list. */}
          {[...points].reverse().map((point) => (
            <tr key={point.roundId}>
              <th scope="row">
                <Link to={`/rounds/${point.roundId}`}>
                  <PlayedDate value={point.playedAt} />
                </Link>
              </th>
              <td>{point.holes}</td>
              <td>{point.totalStrokes}</td>
              <td>{point.fitnessScoreTotal ?? <FormattedMessage {...messages.notScored} />}</td>
              <td>{point.fitnessConfidence && <ConfidenceBadge level={point.fitnessConfidence} />}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

// ---------------------------------------------------------------- monthly trend

function MonthlySection() {
  const monthly = useMonthlyTimeline(trendMonths);
  return (
    <Section title={messages.monthlyTrendTitle}>
      {monthly.isPending ? (
        <Skeleton blocks={1} className="chart-skeleton" />
      ) : monthly.isError ? (
        <LoadError message={messages.monthlyLoadError} onRetry={() => void monthly.refetch()} />
      ) : (
        <MonthlyContent items={monthly.data.items} />
      )}
    </Section>
  );
}

function MonthlyContent({ items }: { items: MonthPoint[] }) {
  const intl = useIntl();
  const summary = monthlySummary(items);
  if (!summary)
    return (
      <p className="hint">
        <FormattedMessage {...messages.monthlyEmpty} />
      </p>
    );

  const table = <MonthlyTable items={items} />;
  if (!hasMonthlyTrend(items))
    return (
      <>
        <div className="chart-summary">
          <p>
            <FormattedMessage
              {...messages.monthlySummaryOne}
              values={{ month: monthName(intl, summary.last.month), average: summary.last.average }}
            />
          </p>
        </div>
        <p className="low-data">
          <FormattedMessage {...messages.monthlyNeedsAnotherMonth} />
        </p>
        {table}
      </>
    );

  return (
    <ChartOrTable
      summary={
        <p>
          <FormattedMessage
            {...messages.monthlySummary}
            values={{
              months: summary.months,
              firstMonth: monthName(intl, summary.first.month),
              first: summary.first.average,
              lastMonth: monthName(intl, summary.last.month),
              last: summary.last.average,
            }}
          />
        </p>
      }
      chart={<MonthlyChart items={items} />}
      table={table}
    />
  );
}

function MonthlyTable({ items }: { items: MonthPoint[] }) {
  const intl = useIntl();
  return (
    <div className="table-scroll">
      <table className="progress-table">
        <caption className="visually-hidden">
          <FormattedMessage {...messages.monthlyTrendTitle} />
        </caption>
        <thead>
          <tr>
            <th scope="col">
              <FormattedMessage {...messages.monthColumn} />
            </th>
            <th scope="col">
              <FormattedMessage {...messages.roundsColumn} />
            </th>
            <th scope="col">
              <FormattedMessage {...messages.monthAverage} />
            </th>
            <th scope="col">
              <FormattedMessage {...messages.changeColumn} />
            </th>
          </tr>
        </thead>
        <tbody>
          {[...items].reverse().map((item) => (
            <tr key={item.monthStart}>
              <th scope="row">{monthName(intl, item.monthStart)}</th>
              <td>{item.roundsLogged}</td>
              <td>
                {item.fitnessAvg === null ? (
                  <FormattedMessage {...messages.noScoredRounds} />
                ) : (
                  <FormattedNumber value={item.fitnessAvg} maximumFractionDigits={0} />
                )}
              </td>
              <td>
                {item.fitnessChange !== null && (
                  <FormattedNumber value={item.fitnessChange} maximumFractionDigits={0} signDisplay="exceptZero" />
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

// ---------------------------------------------------------------- baseline

const baselineStats: { field: keyof Schemas["UserBaselineResponse"]; label: PlainMessage }[] = [
  { field: "avgPutts", label: messages.putts },
  { field: "avgPenalties", label: messages.penalties },
  { field: "avgThreePutts", label: messages.threePutts },
  { field: "avgBlowUpHoles", label: messages.blowUpHoles },
  { field: "avgFairwaysHit", label: messages.fairways },
  { field: "avgGir", label: messages.greens },
];

function BaselineSection() {
  const baseline = useBaseline();
  return (
    <Section title={messages.baseline}>
      {baseline.isPending ? (
        <Skeleton blocks={1} className="stat-skeleton" />
      ) : baseline.isError ? (
        <LoadError message={messages.baselineLoadError} onRetry={() => void baseline.refetch()} />
      ) : baseline.data === null ? (
        <p className="hint">
          <FormattedMessage {...messages.baselineEmpty} />
        </p>
      ) : (
        <BaselineContent baseline={baseline.data} />
      )}
    </Section>
  );
}

function BaselineContent({ baseline }: { baseline: Schemas["UserBaselineResponse"] }) {
  const intl = useIntl();
  const oneDecimal = (value: number) => intl.formatNumber(value, { maximumFractionDigits: 1 });

  return (
    <>
      <p className="hint">
        <FormattedMessage {...messages.baselineIntro} />
      </p>
      <dl className="stat-list">
        {baseline.avgScore !== null && <Stat label={messages.averageStrokes}>{oneDecimal(baseline.avgScore)}</Stat>}
        {baseline.scoreStdDev !== null && (
          <Stat label={messages.spread}>
            <FormattedMessage {...messages.spreadValue} values={{ value: oneDecimal(baseline.scoreStdDev) }} />
          </Stat>
        )}
        {baselineStats.map(({ field, label }) => {
          const value = baseline[field];
          return typeof value === "number" ? (
            <Stat key={field} label={label}>
              {oneDecimal(value)}
            </Stat>
          ) : null;
        })}
        <Stat label={messages.statsTracked}>
          <FormattedNumber value={baseline.completenessRatio} style="percent" maximumFractionDigits={0} />
        </Stat>
      </dl>
      <p className="baseline-confidence">
        <FormattedMessage {...messages.baselineBasedOn} values={{ count: baseline.sampleSize }} />{" "}
        <ConfidenceBadge level={baseline.confidenceLevel} explain />
      </p>
    </>
  );
}

function Stat({ label, children }: { label: PlainMessage; children: ReactNode }) {
  return (
    <div>
      <dt>
        <FormattedMessage {...label} />
      </dt>
      <dd>{children}</dd>
    </div>
  );
}

// ---------------------------------------------------------------- monthly review

function ReviewSection() {
  const intl = useIntl();
  const id = useId();
  const monthly = useMonthlyTimeline(trendMonths);
  const [chosen, setChosen] = useState<string>();
  const items = monthly.data?.items ?? [];
  const month = chosen ?? defaultReviewMonth(items);
  const review = useMonthlyReview(month);
  const point = items.find((i) => i.monthStart === month);
  // A month without rounds has nothing to review (an existing review still shows).
  const nothingToReview = !review.data && point?.roundsLogged === 0;

  return (
    <Section title={messages.monthlyReviewTitle}>
      {monthly.isPending ? (
        <Skeleton blocks={1} className="stat-skeleton" />
      ) : monthly.isError ? (
        <LoadError message={messages.monthlyLoadError} onRetry={() => void monthly.refetch()} />
      ) : (
        <>
          <div className="field review-month">
            <label htmlFor={`${id}-month`}>
              <FormattedMessage {...messages.reviewMonth} />
            </label>
            <select id={`${id}-month`} value={month} onChange={(e) => setChosen(e.target.value)}>
              {[...items].reverse().map((item) => (
                <option key={item.monthStart} value={item.monthStart}>
                  {item.hasReview
                    ? intl.formatMessage(messages.monthReviewed, { month: monthName(intl, item.monthStart) })
                    : monthName(intl, item.monthStart)}
                </option>
              ))}
            </select>
          </div>

          {month === undefined || review.isPending ? (
            <Skeleton blocks={1} className="stat-skeleton" />
          ) : review.isError ? (
            <LoadError message={messages.reviewLoadError} onRetry={() => void review.refetch()} />
          ) : (
            <>
              {review.data ? (
                <ReviewCard review={review.data} monthRounds={point?.roundsLogged} />
              ) : (
                <p className="hint">
                  <FormattedMessage
                    {...(nothingToReview ? messages.reviewNoRounds : messages.reviewNone)}
                    values={{ month: monthName(intl, month) }}
                  />
                </p>
              )}
              {/* In one place whether or not there's a review yet, so creating one keeps
                  the form, and its "Review ready." announcement, mounted. */}
              {!nothingToReview && <ReviewForm key={month} month={month} review={review.data} />}
            </>
          )}
        </>
      )}
    </Section>
  );
}

/** The month's most common micro-win category, as users read it (#162). */
const themeLabels: Record<Exclude<MicroWinCategory, "unknown">, PlainMessage> = {
  StatDelta: messages.themeStatDelta,
  Behavior: messages.themeBehavior,
  Narrative: messages.themeNarrative,
  Habit: messages.themeHabit,
  Recovery: messages.themeRecovery,
  FocusArea: messages.themeFocusArea,
};

/**
 * A review as the API stored it. Its English `highlights` aren't shown: everything they
 * say is in the fields, shown here in the user's language (#162). Reviews from before
 * those fields fall back to the month's current round count.
 */
function ReviewCard({ review, monthRounds }: { review: Schemas["MonthlyReviewResponse"]; monthRounds?: number }) {
  const intl = useIntl();
  const roundsLogged = review.roundsLogged ?? monthRounds;
  const theme = microWinCategoryOf(review.topMicroWinCategory);
  return (
    <div className="review">
      <dl className="stat-list">
        <Stat label={messages.monthAverage}>
          {review.fitnessAvg === null ? (
            <FormattedMessage {...messages.noScoredRounds} />
          ) : (
            <FormattedNumber value={review.fitnessAvg} maximumFractionDigits={0} />
          )}
        </Stat>
        {roundsLogged !== undefined && (
          <Stat label={messages.roundsColumn}>
            <FormattedMessage {...messages.roundsPlayed} values={{ count: roundsLogged }} />
          </Stat>
        )}
        {/* A category added after this build (#134) is left out rather than misnamed. */}
        {theme !== "unknown" && (
          <Stat label={messages.reviewTheme}>
            <FormattedMessage {...themeLabels[theme]} />
          </Stat>
        )}
        {review.focusNextMonth && (
          <div className="stat-list-wide">
            <dt>
              <FormattedMessage {...messages.reviewFocus} />
            </dt>
            <dd>{review.focusNextMonth}</dd>
          </div>
        )}
      </dl>
      {review.fitnessChange !== null && <MonthChange change={review.fitnessChange} comparedWith="previousMonth" />}
      <p className="hint">
        <FormattedMessage
          {...messages.reviewCreatedOn}
          values={{ date: intl.formatDate(review.computedAt, { day: "numeric", month: "short", year: "numeric" }) }}
        />
      </p>
    </div>
  );
}

/**
 * Creates or updates a month's review, with an optional focus for the next month. The
 * button says it's working while the API computes, and "Review ready." is announced.
 */
function ReviewForm({ month, review }: { month: string; review: Schemas["MonthlyReviewResponse"] | null }) {
  const intl = useIntl();
  const api = useApi();
  const queryClient = useQueryClient();
  const online = useOnline();
  const id = useId();
  const [focus, setFocus] = useState(review?.focusNextMonth ?? "");
  const [focusError, setFocusError] = useState<string>();
  const [formError, setFormError] = useState<string>();
  const [announcement, setAnnouncement] = useState<string>();

  const generate = useMutation({
    mutationFn: async (body: Schemas["GenerateMonthlyReviewRequest"]) =>
      unwrap(await api.POST("/me/monthly-reviews/generate", { body })),
    onSuccess: (saved) => {
      queryClient.setQueryData(queryKeys.monthlyReview(month), saved);
      // The month picker marks reviewed months, and Home shows the latest review.
      void queryClient.invalidateQueries({ queryKey: ["progress", "monthly"] });
      void queryClient.invalidateQueries({ queryKey: queryKeys.dashboard });
      setAnnouncement(intl.formatMessage(messages.reviewReady));
    },
    onError: (error) => {
      const fieldMessage = error instanceof ApiProblem ? error.messagesFor("focusNextMonth")[0] : undefined;
      if (fieldMessage) setFocusError(fieldMessage);
      else setFormError(intl.formatMessage(error instanceof ApiProblem ? messages.reviewFailed : messages.cantConnect));
    },
  });

  const submit = (event: FormEvent) => {
    event.preventDefault();
    if (generate.isPending || !online) return;
    setFormError(undefined);
    setAnnouncement(undefined);
    const checked = checkFocus(focus);
    if (!checked.ok) {
      setFocusError(intl.formatMessage(messages.focusTooLong, { max: maxFocusLength }));
      document.getElementById(`${id}-focus`)?.focus();
      return;
    }
    generate.mutate({ monthStart: month, focusNextMonth: checked.value });
  };

  const describedBy = [`${id}-focus-hint`, focusError && `${id}-focus-error`].filter(Boolean).join(" ");

  return (
    <form className="review-form" onSubmit={submit} noValidate aria-busy={generate.isPending}>
      <div className="field">
        <label htmlFor={`${id}-focus`}>
          <FormattedMessage {...messages.focusNextMonth} />
        </label>
        <input
          id={`${id}-focus`}
          type="text"
          value={focus}
          aria-invalid={focusError ? true : undefined}
          aria-describedby={describedBy}
          onChange={(e) => {
            setFocus(e.target.value);
            setFocusError(undefined);
          }}
        />
        <p id={`${id}-focus-hint`} className="hint">
          <FormattedMessage {...messages.focusHint} />
        </p>
        {focusError && (
          <p id={`${id}-focus-error`} className="field-error">
            {focusError}
          </p>
        )}
      </div>
      {formError && (
        <p className="form-error" role="alert">
          {formError}
        </p>
      )}
      {!online && (
        <p className="hint">
          <FormattedMessage {...messages.offlineCantReview} />
        </p>
      )}
      <div>
        <button type="submit" className="button button--primary" disabled={generate.isPending || !online}>
          <FormattedMessage
            {...(generate.isPending ? messages.creatingReview : review ? messages.updateReview : messages.createReview)}
          />
        </button>
      </div>
      <p className="visually-hidden" role="status">
        {announcement}
      </p>
    </form>
  );
}
