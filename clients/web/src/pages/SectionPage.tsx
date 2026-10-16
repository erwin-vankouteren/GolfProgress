import type { ReactNode } from "react";
import { FormattedMessage, type MessageDescriptor } from "react-intl";
import { Link } from "react-router";
import { messages } from "../i18n/messages";

/**
 * A section's page in the app shell (#75): its heading and the empty state from the
 * wireframes. The features fill these in M2.
 */
function SectionPage({
  title,
  empty,
  action,
}: {
  title: MessageDescriptor;
  empty: MessageDescriptor;
  action?: ReactNode;
}) {
  return (
    <section className="page">
      <h1>
        <FormattedMessage {...title} />
      </h1>
      <div className="empty-state">
        <p>
          <FormattedMessage {...empty} />
        </p>
        {action}
      </div>
    </section>
  );
}

export const ProgressPage = () => <SectionPage title={messages.progress} empty={messages.progressEmpty} />;

export function NotFoundPage() {
  return (
    <SectionPage
      title={messages.notFoundTitle}
      empty={messages.notFoundBody}
      action={
        <Link to="/" className="button">
          <FormattedMessage {...messages.backHome} />
        </Link>
      }
    />
  );
}
