import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { FormattedMessage, useIntl } from "react-intl";
import { ApiProblem, unwrap, type Schemas } from "../api";
import { queryKeys, useStudentList } from "../api/queries";
import { useApi } from "../app/ApiContext";
import { ConfirmDialog } from "../common/ConfirmDialog";
import { LoadError, Skeleton, useOnline } from "../common/states";
import { messages } from "../i18n/messages";
import { sharedLabels } from "./CoachesSection";
import { sharedItems } from "./coachForm";
import "./coaching.css";

/**
 * Students (#200, docs/coach-access.md → "Screens"): the golfers you coach, newest
 * activity first. In the navigation once someone's invitation is accepted.
 */
export function StudentsPage() {
  const list = useStudentList();
  const students = list.data?.pages.flatMap((page) => page.students) ?? [];

  return (
    <section className="page">
      <h1>
        <FormattedMessage {...messages.students} />
      </h1>
      {list.isPending ? (
        <Skeleton blocks={3} />
      ) : list.isError ? (
        <LoadError message={messages.studentsLoadError} onRetry={() => void list.refetch()} />
      ) : students.length === 0 ? (
        <p>
          <FormattedMessage {...messages.studentsEmpty} />
        </p>
      ) : (
        <>
          <ul className="coach-list student-list">
            {students.map((student) => (
              <Student key={student.golferId} student={student} />
            ))}
          </ul>
          {list.hasNextPage && (
            <div className="round-list-more">
              <button
                type="button"
                className="button"
                disabled={list.isFetchingNextPage}
                onClick={() => void list.fetchNextPage()}
              >
                <FormattedMessage {...(list.isFetchingNextPage ? messages.loading : messages.showMoreStudents)} />
              </button>
            </div>
          )}
        </>
      )}
    </section>
  );
}

function Student({ student }: { student: Schemas["StudentResponse"] }) {
  const intl = useIntl();
  const api = useApi();
  const online = useOnline();
  const queryClient = useQueryClient();
  const [confirming, setConfirming] = useState(false);
  // Also GET /me: without students, the section leaves the navigation.
  const refresh = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: queryKeys.students }),
      queryClient.invalidateQueries({ queryKey: queryKeys.me }),
    ]);
  const stop = useMutation({
    mutationFn: async () =>
      unwrap(await api.DELETE("/coach/students/{golferId}", { params: { path: { golferId: student.golferId } } })),
    onSuccess: refresh,
    onError: (error) => {
      // Removed by the golfer meanwhile: the list shows what's true now.
      if (error instanceof ApiProblem && error.status === 404) void refresh();
    },
  });
  const items = intl.formatList(sharedItems(student).map((item) => intl.formatMessage(sharedLabels[item])));

  return (
    <li className="card">
      <div>
        <h2>{student.displayName}</h2>
        <p className="hint">
          {student.lastActivityOn ? (
            <FormattedMessage
              {...messages.studentLastActivity}
              values={{
                // A date without a time: read and shown as UTC, so it can't shift a day.
                date: intl.formatDate(new Date(student.lastActivityOn), { dateStyle: "medium", timeZone: "UTC" }),
              }}
            />
          ) : (
            <FormattedMessage {...messages.studentNoActivity} />
          )}
        </p>
        {student.unreadReplies > 0 && (
          <p className="hint">
            <FormattedMessage {...messages.studentUnreadReplies} values={{ count: student.unreadReplies }} />
          </p>
        )}
        <p className="hint">
          <FormattedMessage {...messages.studentShares} values={{ items }} />
        </p>
      </div>
      <button
        type="button"
        className="button"
        disabled={!online}
        aria-label={intl.formatMessage(messages.stopCoachingOf, { name: student.displayName })}
        onClick={() => {
          stop.reset();
          setConfirming(true);
        }}
      >
        <FormattedMessage {...messages.stopCoaching} />
      </button>
      {confirming && (
        <ConfirmDialog
          title={messages.stopCoachingTitle}
          body={messages.stopCoachingBody}
          confirm={messages.stopCoaching}
          busyLabel={messages.working}
          busy={stop.isPending}
          error={
            stop.isError
              ? intl.formatMessage(
                  stop.error instanceof ApiProblem ? messages.stopCoachingFailed : messages.cantConnect,
                )
              : undefined
          }
          onConfirm={() => stop.mutate()}
          onCancel={() => setConfirming(false)}
        />
      )}
    </li>
  );
}
