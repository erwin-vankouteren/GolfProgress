import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useEffect, useId, useRef, useState, type FormEvent } from "react";
import { FormattedMessage, useIntl, type MessageDescriptor } from "react-intl";
import { ApiProblem, unwrap } from "../api";
import { queryKeys, useCoaches, useFeedbackThread, useStudentOverview } from "../api/queries";
import { useStudentScope } from "../api/studentScope";
import { useApi } from "../app/ApiContext";
import { ConfirmDialog } from "../common/ConfirmDialog";
import { LoadError, Skeleton, useOnline } from "../common/states";
import { messages } from "../i18n/messages";
import { useSessionState } from "../session/SessionContext";
import {
  checkFeedbackText,
  feedbackTextMax,
  golferThreads,
  newestMessageAt,
  type FeedbackMessage,
  type FeedbackTargetType,
} from "./feedback";
import "./feedback.css";

type HeadingTag = "h2" | "h3" | "h4";
const below: Record<HeadingTag, HeadingTag> = { h2: "h3", h3: "h4", h4: "h4" };

/**
 * The feedback on a round, reflection or practice session (#214, #216,
 * docs/coach-access.md → "Feedback"). In a student's screens, the coach's own thread with
 * them; in the golfer's own, a thread per coach, under "Feedback from your coach". Seeing
 * a thread marks it read.
 */
export function FeedbackSection({
  targetType,
  targetId,
  Heading,
}: {
  targetType: FeedbackTargetType;
  targetId: string;
  Heading: HeadingTag;
}) {
  const studentId = useStudentScope();
  return studentId ? (
    <CoachFeedback golferId={studentId} targetType={targetType} targetId={targetId} Heading={Heading} />
  ) : (
    <GolferFeedback targetType={targetType} targetId={targetId} Heading={Heading} />
  );
}

type SectionProps = { targetType: FeedbackTargetType; targetId: string; Heading: HeadingTag };

function CoachFeedback({ golferId, targetType, targetId, Heading }: SectionProps & { golferId: string }) {
  const thread = useFeedbackThread(golferId, targetType, targetId);
  const student = useStudentOverview(golferId);
  const titleId = useId();
  useMarkRead(golferId, thread.data, { invalidateStudents: true });

  return (
    <section className="card feedback" aria-labelledby={titleId}>
      <Heading id={titleId}>
        <FormattedMessage {...messages.feedback} />
      </Heading>
      {thread.isPending ? (
        <Skeleton blocks={1} />
      ) : thread.isError ? (
        <LoadError message={messages.feedbackLoadError} onRetry={() => void thread.refetch()} />
      ) : (
        <Thread
          golferId={golferId}
          targetType={targetType}
          targetId={targetId}
          messages={thread.data}
          golferName={student.data?.displayName}
          empty={messages.feedbackEmptyCoach}
          composeLabel={messages.writeFeedback}
        />
      )}
    </section>
  );
}

function GolferFeedback({ targetType, targetId, Heading }: SectionProps) {
  const session = useSessionState();
  const golferId = session.status === "signedIn" ? session.userId : "";
  const thread = useFeedbackThread(golferId, targetType, targetId);
  const coaches = useCoaches();
  const titleId = useId();
  const Sub = below[Heading];
  useMarkRead(golferId, thread.data, { invalidateStudents: false });

  if (thread.isPending || coaches.isPending) return null;

  // What can be known: a golfer who never had a coach sees nothing, not even an error,
  // when either request fails; an error shows only where there'd be a thread.
  const threads = golferThreads(thread.data ?? [], coaches.data?.coaches ?? [], targetType);
  // Without a coach who can see it, and nothing from one before, there's nothing to show.
  if (threads.length === 0) return null;

  if (thread.isError || coaches.isError) {
    return (
      <section className="card feedback" aria-labelledby={titleId}>
        <Heading id={titleId}>
          <FormattedMessage {...messages.feedbackFromYourCoach} />
        </Heading>
        <LoadError
          message={messages.feedbackLoadError}
          onRetry={() => void Promise.all([thread.refetch(), coaches.refetch()])}
        />
      </section>
    );
  }

  return (
    <section className="card feedback" aria-labelledby={titleId}>
      <Heading id={titleId}>
        <FormattedMessage {...messages.feedbackFromYourCoach} />
      </Heading>
      {threads.map((t) => {
        const name = t.coachName ?? undefined;
        return (
          <div key={t.coachId ?? "former"} className="feedback-thread">
            {/* With one coach, the heading above says enough. */}
            {threads.length > 1 && <Sub>{name ?? <FormattedMessage {...messages.formerCoach} />}</Sub>}
            <Thread
              golferId={golferId}
              targetType={targetType}
              targetId={targetId}
              messages={t.messages}
              coachId={t.coachId ?? undefined}
              coachName={name}
              canWrite={t.canWrite}
              empty={messages.feedbackEmptyGolfer}
              composeLabel={messages.messageToCoach}
            />
          </div>
        );
      })}
    </section>
  );
}

/**
 * Marks the golfer's threads read up to the newest message shown, once per new message.
 * For a coach it also refreshes the Students list, whose unread replies change.
 */
function useMarkRead(
  golferId: string,
  thread: FeedbackMessage[] | undefined,
  { invalidateStudents }: { invalidateStudents: boolean },
) {
  const api = useApi();
  const queryClient = useQueryClient();
  const marked = useRef<string | undefined>(undefined);
  const newest = thread ? newestMessageAt(thread) : undefined;

  useEffect(() => {
    if (!golferId || !newest || (marked.current && newest <= marked.current)) return;
    marked.current = newest;
    void api
      .POST("/feedback/read", { body: { golferId, upTo: newest } })
      .then(() => {
        if (invalidateStudents) void queryClient.invalidateQueries({ queryKey: queryKeys.students });
      })
      // Not being marked read isn't worth an error on screen: it's tried again next time.
      .catch(() => undefined);
  }, [api, queryClient, golferId, newest, invalidateStudents]);
}

function Thread({
  golferId,
  targetType,
  targetId,
  messages: list,
  golferName,
  coachId,
  coachName,
  canWrite = true,
  empty,
  composeLabel,
}: {
  golferId: string;
  targetType: FeedbackTargetType;
  targetId: string;
  messages: FeedbackMessage[];
  golferName?: string;
  coachId?: string;
  coachName?: string;
  canWrite?: boolean;
  empty: MessageDescriptor;
  composeLabel: MessageDescriptor;
}) {
  const intl = useIntl();

  return (
    <>
      {list.length === 0 ? (
        <p className="hint">
          <FormattedMessage {...empty} values={{ name: coachName ?? "" }} />
        </p>
      ) : (
        <ol className="feedback-messages">
          {list.map((message) => (
            <Message
              key={message.feedbackMessageId}
              message={message}
              author={
                message.mine
                  ? intl.formatMessage(messages.you)
                  : message.fromCoach
                    ? (message.coachName ?? intl.formatMessage(messages.formerCoach))
                    : (golferName ?? intl.formatMessage(messages.yourStudent))
              }
              threadKey={queryKeys.feedbackThread(golferId, targetType, targetId)}
            />
          ))}
        </ol>
      )}
      {canWrite && (
        <Composer
          golferId={golferId}
          targetType={targetType}
          targetId={targetId}
          coachId={coachId}
          label={intl.formatMessage(composeLabel, { name: coachName ?? "" })}
        />
      )}
    </>
  );
}

function Message({
  message,
  author,
  threadKey,
}: {
  message: FeedbackMessage;
  author: string;
  threadKey: readonly unknown[];
}) {
  const intl = useIntl();
  const api = useApi();
  const online = useOnline();
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const refresh = () => queryClient.invalidateQueries({ queryKey: threadKey });

  const remove = useMutation({
    mutationFn: async () =>
      unwrap(await api.DELETE("/feedback/{id}", { params: { path: { id: message.feedbackMessageId } } })),
    onSuccess: async () => {
      await refresh();
      setConfirming(false);
    },
  });

  const when = intl.formatDate(message.createdAt, { dateStyle: "medium", timeStyle: "short" });

  return (
    <li className={`feedback-message${message.mine ? " feedback-message--mine" : ""}`}>
      <p className="feedback-meta">
        <span className="feedback-author">{author}</span>
        <span> · {when}</span>
        {message.editedAt && (
          <span>
            {" · "}
            <FormattedMessage {...messages.edited} />
          </span>
        )}
      </p>
      {editing ? (
        <MessageForm
          initial={message.text}
          label={intl.formatMessage(messages.editMessage)}
          submitLabel={messages.save}
          busyLabel={messages.saving}
          failed={messages.feedbackSaveFailed}
          send={async (text) =>
            unwrap(
              await api.PATCH("/feedback/{id}", {
                params: { path: { id: message.feedbackMessageId } },
                body: { text },
              }),
            )
          }
          onSent={async () => {
            await refresh();
            setEditing(false);
          }}
          onCancel={() => setEditing(false)}
        />
      ) : (
        <p className="feedback-text">{message.text}</p>
      )}
      {message.mine && !editing && (
        <div className="feedback-actions">
          <button
            type="button"
            className="button"
            disabled={!online}
            aria-label={intl.formatMessage(messages.editMessageOf, { when })}
            onClick={() => setEditing(true)}
          >
            <FormattedMessage {...messages.edit} />
          </button>
          <button
            type="button"
            className="button"
            disabled={!online}
            aria-label={intl.formatMessage(messages.deleteMessageOf, { when })}
            onClick={() => {
              remove.reset();
              setConfirming(true);
            }}
          >
            <FormattedMessage {...messages.delete} />
          </button>
        </div>
      )}
      {confirming && (
        <ConfirmDialog
          title={messages.deleteMessageTitle}
          body={messages.deleteMessageBody}
          confirm={messages.delete}
          busyLabel={messages.deleting}
          busy={remove.isPending}
          error={
            remove.isError
              ? intl.formatMessage(
                  remove.error instanceof ApiProblem ? messages.feedbackDeleteFailed : messages.cantConnect,
                )
              : undefined
          }
          onConfirm={() => remove.mutate()}
          onCancel={() => setConfirming(false)}
        />
      )}
    </li>
  );
}

function Composer({
  golferId,
  targetType,
  targetId,
  coachId,
  label,
}: {
  golferId: string;
  targetType: FeedbackTargetType;
  targetId: string;
  coachId?: string;
  label: string;
}) {
  const api = useApi();
  const queryClient = useQueryClient();
  // A form per message, so a sent one starts empty again.
  const [round, setRound] = useState(0);

  return (
    <MessageForm
      key={round}
      initial=""
      label={label}
      submitLabel={messages.send}
      busyLabel={messages.sending}
      failed={messages.feedbackSendFailed}
      send={async (text) =>
        unwrap(
          await api.POST("/feedback", {
            body: { golferId, targetType, targetId, coachId: coachId ?? null, text },
          }),
        )
      }
      onSent={async () => {
        await queryClient.invalidateQueries({ queryKey: queryKeys.feedbackThread(golferId, targetType, targetId) });
        setRound((r) => r + 1);
      }}
    />
  );
}

function MessageForm({
  initial,
  label,
  submitLabel,
  busyLabel,
  failed,
  send,
  onSent,
  onCancel,
}: {
  initial: string;
  label: string;
  submitLabel: MessageDescriptor;
  busyLabel: MessageDescriptor;
  failed: MessageDescriptor;
  send: (text: string) => Promise<unknown>;
  onSent: () => Promise<void>;
  onCancel?: () => void;
}) {
  const intl = useIntl();
  const online = useOnline();
  const id = useId();
  const [text, setText] = useState(initial);
  const [error, setError] = useState<string | undefined>();
  const sending = useRef(false);

  const mutation = useMutation({
    mutationFn: send,
    onSuccess: onSent,
    onError: (problem) => {
      if (!(problem instanceof ApiProblem)) setError(intl.formatMessage(messages.cantConnect));
      else if (problem.status === 429) setError(intl.formatMessage(messages.tooManyMessages));
      // The API's own words for the text, when it turned it down.
      else setError(problem.messagesFor("text")[0] ?? intl.formatMessage(failed));
    },
    onSettled: () => {
      sending.current = false;
    },
  });

  const submit = (event: FormEvent) => {
    event.preventDefault();
    if (sending.current || !online) return;
    const problem = checkFeedbackText(text);
    if (problem) {
      setError(
        problem === "tooLong"
          ? intl.formatMessage(messages.atMostCharacters, { max: feedbackTextMax })
          : intl.formatMessage(messages.writeSomethingFirst),
      );
      return;
    }
    sending.current = true;
    mutation.mutate(text.trim());
  };

  return (
    <form className="feedback-form" onSubmit={submit} noValidate>
      <div className="field">
        <label htmlFor={id}>{label}</label>
        <textarea
          id={id}
          rows={3}
          value={text}
          maxLength={feedbackTextMax}
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? `${id}-error` : undefined}
          onChange={(e) => {
            setText(e.target.value);
            setError(undefined);
          }}
        />
        {error && (
          <p id={`${id}-error`} className="field-error" role="alert">
            {error}
          </p>
        )}
      </div>
      {!online && (
        <p className="hint" role="status">
          <FormattedMessage {...messages.offlineCantSendMessage} />
        </p>
      )}
      <div className="feedback-actions">
        <button
          type="submit"
          className="button button--primary"
          disabled={mutation.isPending || !online}
          aria-busy={mutation.isPending}
        >
          <FormattedMessage {...(mutation.isPending ? busyLabel : submitLabel)} />
        </button>
        {onCancel && (
          <button type="button" className="button" onClick={onCancel} disabled={mutation.isPending}>
            <FormattedMessage {...messages.cancel} />
          </button>
        )}
      </div>
    </form>
  );
}
