import { FormattedMessage, useIntl } from "react-intl";
import { Link } from "react-router";
import { useUnreadFeedback } from "../api/queries";
import { messages } from "../i18n/messages";
import { formatPlayedDate } from "../rounds/score";
import { unreadNotListed, unreadThreadPath, type UnreadThread } from "./unread";
import "./feedback.css";

/**
 * "New from your coach" on Home (#221, docs/coach-access.md → "Screens"): while anything
 * from a coach is unread, the newest threads, each linking to what it's about. Opening a
 * thread marks it read, and the card follows.
 *
 * It shows only when there's something new. Loading, an error or nothing unread show
 * nothing: most golfers have no coach, and Home has its own states.
 */
export function NewFromYourCoach() {
  const unread = useUnreadFeedback();
  // Defensive: a card on Home mustn't take the page down over an unexpected answer.
  if (!unread.data?.count || !Array.isArray(unread.data.threads)) return null;

  const more = unreadNotListed(unread.data);
  return (
    <section className="card dashboard-card dashboard-card--wide new-from-coach" aria-labelledby="new-from-coach">
      <h2 id="new-from-coach">
        <FormattedMessage {...messages.newFromYourCoach} />
      </h2>
      <ul className="new-from-coach-list">
        {unread.data.threads.map((thread) => (
          <UnreadItem key={`${thread.targetType}-${thread.targetId}-${thread.coachId ?? "former"}`} thread={thread} />
        ))}
      </ul>
      {more > 0 && (
        <p className="hint">
          <FormattedMessage {...messages.moreUnreadFeedback} values={{ count: more }} />
        </p>
      )}
    </section>
  );
}

function UnreadItem({ thread }: { thread: UnreadThread }) {
  const intl = useIntl();
  const coach = thread.coachName ?? intl.formatMessage(messages.formerCoach);
  const date = thread.onDate ? formatPlayedDate(intl, thread.onDate) : undefined;
  const about =
    thread.targetType === "PracticeSession"
      ? intl.formatMessage(messages.unreadOnSession, { coach, date: date ?? "" })
      : thread.targetType === "Reflection"
        ? date
          ? intl.formatMessage(messages.unreadOnReflection, { coach, date })
          : intl.formatMessage(messages.unreadOnReflectionNoDate, { coach })
        : intl.formatMessage(messages.unreadOnRound, { coach, date: date ?? "" });
  const path = unreadThreadPath(thread);

  return (
    <li>
      <p className="new-from-coach-about">
        {path ? (
          <Link to={path}>
            {about}
            <span aria-hidden="true"> ›</span>
          </Link>
        ) : (
          about
        )}{" "}
        <span className="hint">
          <FormattedMessage {...messages.newMessages} values={{ count: thread.unread }} />
        </span>
      </p>
      <p className="feedback-text new-from-coach-preview">{thread.preview}</p>
    </li>
  );
}
