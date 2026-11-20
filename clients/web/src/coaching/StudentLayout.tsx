import { FormattedMessage, useIntl } from "react-intl";
import { Link, Outlet, useMatch, useParams } from "react-router";
import { useStudentOverview } from "../api/queries";
import { StudentScope } from "../api/studentScope";
import { messages } from "../i18n/messages";
import "./coaching.css";

/**
 * A student's screens (#206): the overview, and their rounds and practice on the golfer's
 * own screens, read-only. Everything below reads the coach's routes for this student.
 */
export function StudentLayout() {
  const { golferId = "" } = useParams();
  const onOverview = useMatch("/students/:golferId") !== null;

  return (
    <StudentScope.Provider value={golferId}>
      {/* The overview has its own way back; the screens below it lead back to it. */}
      {!onOverview && <StudentBreadcrumb golferId={golferId} />}
      <Outlet />
    </StudentScope.Provider>
  );
}

function StudentBreadcrumb({ golferId }: { golferId: string }) {
  const intl = useIntl();
  // From the cache when coming from the overview; until then, just Students.
  const overview = useStudentOverview(golferId);

  return (
    <nav className="student-breadcrumb" aria-label={intl.formatMessage(messages.breadcrumb)}>
      <ol>
        <li>
          <Link to="/students">
            <FormattedMessage {...messages.students} />
          </Link>
        </li>
        {overview.data && (
          <li>
            <Link to={`/students/${golferId}`}>{overview.data.displayName}</Link>
          </li>
        )}
      </ol>
    </nav>
  );
}
