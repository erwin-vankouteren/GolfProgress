import type { ReactNode } from "react";
import { FormattedMessage, FormattedNumber, useIntl } from "react-intl";
import { Link, useNavigate, useParams } from "react-router";
import { ApiProblem } from "../api";
import { useStudentOverview } from "../api/queries";
import { LoadError, Skeleton } from "../common/states";
import { MonthChange } from "../dashboard/DashboardPage";
import { messages, type PlainMessage } from "../i18n/messages";
import { SavedReflection } from "../rounds/ReflectionSection";
import { ConfidenceBadge, PlayedDate, formatPlayedDate } from "../rounds/score";
import { sharedLabels } from "./CoachesSection";
import { sharedItems } from "./coachForm";
import { StopCoachingButton } from "./StudentsPage";
import { fitnessTrend, reflectionsState, type StudentOverview } from "./studentOverview";
import "../progress/progress.css";
import "./coaching.css";

/**
 * A student's overview (#203, docs/coach-access.md → "Screens"): what a coach needs
 * before a lesson, with what the student shares at the top.
 */
export function StudentOverviewPage() {
  const { golferId = "" } = useParams();
  const overview = useStudentOverview(golferId);
  const navigate = useNavigate();

  const back = (
    <Link to="/students" className="back-link">
      <span aria-hidden="true">‹ </span>
      <FormattedMessage {...messages.backToStudents} />
    </Link>
  );

  if (overview.isPending) {
    return (
      <section className="page">
        {back}
        <Skeleton blocks={4} className="dashboard-grid" />
      </section>
    );
  }

  if (overview.isError) {
    // Removed by the golfer, or never a student: nothing about them to show.
    const gone = overview.error instanceof ApiProblem && overview.error.status === 404;
    return (
      <section className="page">
        {back}
        {gone ? (
          <p>
            <FormattedMessage {...messages.studentGone} />
          </p>
        ) : (
          <LoadError message={messages.studentOverviewLoadError} onRetry={() => void overview.refetch()} />
        )}
      </section>
    );
  }

  const student = overview.data;
  return (
    <section className="page dashboard student-overview">
      {back}
      <div className="student-overview-header">
        <div>
          <h1>{student.displayName}</h1>
          <SharedLine student={student} />
        </div>
        <StopCoachingButton
          golferId={student.golferId}
          name={student.displayName}
          onStopped={() => navigate("/students", { replace: true })}
        />
      </div>
      {student.lastRound ? (
        <OverviewCards student={student} lastRound={student.lastRound} />
      ) : (
        <>
          <p className="empty-state">
            <FormattedMessage {...messages.studentNoRounds} values={{ name: student.displayName }} />
          </p>
          <div className="dashboard-grid">
            <Reflections student={student} />
          </div>
        </>
      )}
    </section>
  );
}

function SharedLine({ student }: { student: StudentOverview }) {
  const intl = useIntl();
  const items = intl.formatList(sharedItems(student).map((item) => intl.formatMessage(sharedLabels[item])));
  return (
    <p className="hint">
      <FormattedMessage {...messages.studentShares} values={{ items }} />
    </p>
  );
}

function OverviewCards({
  student,
  lastRound,
}: {
  student: StudentOverview;
  lastRound: NonNullable<StudentOverview["lastRound"]>;
}) {
  const intl = useIntl();
  const trend = fitnessTrend(student.recentRounds);
  const roundsPath = `/students/${student.golferId}/rounds`;

  return (
    <div className="dashboard-grid">
      <Card title={messages.lastRound} id="last-round">
        <Link to={`${roundsPath}/${lastRound.roundId}`} className="last-round-link">
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
      </Card>

      {student.nextFocus && (
        <Card title={messages.theirNextFocus} id="next-focus">
          <p className="next-focus-title">{student.nextFocus.title}</p>
          <p>{student.nextFocus.recommendation}</p>
          <p className="card-detail">{student.nextFocus.rationale}</p>
        </Card>
      )}

      {student.baseline && student.baseline.avgScore !== null && (
        <Card title={messages.baseline} id="baseline">
          <p className="stat">
            <span className="stat-label">
              <FormattedMessage {...messages.baselineAverage} />
            </span>
            <span className="stat-value">
              <FormattedNumber value={student.baseline.avgScore} maximumFractionDigits={1} />
            </span>
          </p>
          <p className="baseline-confidence">
            <FormattedMessage {...messages.studentBaselineBasedOn} values={{ count: student.baseline.sampleSize }} />{" "}
            <ConfidenceBadge level={student.baseline.confidenceLevel} explain />
          </p>
        </Card>
      )}

      {student.monthlyFitnessAvg !== null && (
        <Card title={messages.thisMonth} id="this-month">
          <p className="stat">
            <span className="stat-label">
              <FormattedMessage {...messages.monthAverage} />
            </span>
            <span className="stat-value">
              <FormattedNumber value={student.monthlyFitnessAvg} maximumFractionDigits={0} />
            </span>
          </p>
          {student.monthlyFitnessChange !== null && <MonthChange change={student.monthlyFitnessChange} />}
        </Card>
      )}

      <Card title={messages.recentRounds} id="recent-rounds" wide>
        {trend && (
          <p>
            <FormattedMessage
              {...messages.studentTrend}
              values={{ first: trend.first, last: trend.last, count: trend.rounds }}
            />
          </p>
        )}
        <RecentRounds rounds={student.recentRounds} roundsPath={roundsPath} />
        <p>
          <Link to={roundsPath}>
            <FormattedMessage {...messages.allRounds} />
          </Link>
        </p>
      </Card>

      {student.sharePractice && (
        <Card title={messages.practice} id="practice">
          <p>
            <Link to={`/students/${student.golferId}/practice`}>
              <FormattedMessage {...messages.practiceSessions} />
            </Link>
          </p>
        </Card>
      )}

      <Reflections student={student} />
    </div>
  );
}

function RecentRounds({ rounds, roundsPath }: { rounds: StudentOverview["recentRounds"]; roundsPath: string }) {
  return (
    <div className="table-scroll">
      <table className="progress-table">
        <caption className="visually-hidden">
          <FormattedMessage {...messages.recentRounds} />
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
          </tr>
        </thead>
        <tbody>
          {/* Newest first, like the golfer's own round list. */}
          {[...rounds].reverse().map((round) => (
            <tr key={round.roundId}>
              <th scope="row">
                <Link to={`${roundsPath}/${round.roundId}`}>
                  <PlayedDate value={round.playedAt} />
                </Link>
              </th>
              <td>{round.holes}</td>
              <td>{round.totalStrokes}</td>
              <td>{round.fitnessScoreTotal ?? "–"}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function Reflections({ student }: { student: StudentOverview }) {
  const intl = useIntl();
  const state = reflectionsState(student);

  return (
    <Card title={messages.latestReflections} id="latest-reflections" wide>
      {state === "notShared" ? (
        <p>
          <FormattedMessage {...messages.reflectionsNotShared} values={{ name: student.displayName }} />
        </p>
      ) : state === "none" ? (
        <p>
          <FormattedMessage {...messages.noReflectionsYet} />
        </p>
      ) : (
        <ul className="student-reflections">
          {student.latestReflections.map((reflection) => (
            <li key={reflection.reflectionId}>
              {reflection.playedAt && (
                <h3>
                  <FormattedMessage
                    {...messages.reflectionOnRound}
                    values={{ date: formatPlayedDate(intl, reflection.playedAt) }}
                  />
                </h3>
              )}
              <SavedReflection reflection={reflection} />
            </li>
          ))}
        </ul>
      )}
    </Card>
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
