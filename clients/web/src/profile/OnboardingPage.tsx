import { useEffect, useRef, useState, type FormEvent } from "react";
import { FormattedMessage, useIntl } from "react-intl";
import { useNavigate } from "react-router";
import { ApiProblem } from "../api";
import { useOnline } from "../common/states";
import { messages, type PlainMessage } from "../i18n/messages";
import { GoalFocusField, TrackingModeField, UnitsField, useUpdateMe } from "./PreferenceFields";
import { browserTimeZone, type Preferences } from "./profileForm";
import "../auth/AuthPages.css";
import "./profile.css";

/**
 * Onboarding after Create account (#111, docs/clients/wireframes.md → "Onboarding"):
 * units, tracking mode and up to three goal focus areas, saved together with
 * PATCH /me, then straight to Log round. Each step moves focus to its question.
 */

const steps: PlainMessage[] = [
  messages.onboardingUnitsTitle,
  messages.onboardingTrackingTitle,
  messages.onboardingGoalsTitle,
];

export function OnboardingPage() {
  const intl = useIntl();
  const navigate = useNavigate();
  const online = useOnline();
  const update = useUpdateMe();
  const heading = useRef<HTMLHeadingElement>(null);
  const [step, setStep] = useState(0);
  const [values, setValues] = useState<Preferences>({ units: "Metric", trackingMode: "Minimal", goalFocus: [] });
  const [error, setError] = useState<string>();
  const first = useRef(true);

  useEffect(() => {
    // Not on arrival: the router already focuses the page then.
    if (first.current) {
      first.current = false;
      return;
    }
    heading.current?.focus();
  }, [step]);

  const last = step === steps.length - 1;

  const submit = (event: FormEvent) => {
    event.preventDefault();
    if (!last) return setStep((s) => s + 1);
    if (update.isPending || !online) return;
    setError(undefined);
    const timezone = browserTimeZone();
    update.mutate(
      { ...values, ...(timezone ? { timezone } : {}) },
      {
        onSuccess: () => navigate("/rounds/new", { replace: true }),
        onError: (failure) => {
          // An unknown time zone shouldn't block onboarding: save the rest without it.
          if (failure instanceof ApiProblem && failure.status === 400 && failure.messagesFor("timezone").length) {
            update.mutate(values, {
              onSuccess: () => navigate("/rounds/new", { replace: true }),
              onError: () => setError(intl.formatMessage(messages.profileSaveFailed)),
            });
            return;
          }
          setError(
            intl.formatMessage(failure instanceof ApiProblem ? messages.profileSaveFailed : messages.cantConnect),
          );
        },
      },
    );
  };

  return (
    <main className="auth-frame onboarding" id="main">
      <form className="auth-card" onSubmit={submit} noValidate>
        <p className="hint">
          <FormattedMessage {...messages.onboardingStep} values={{ step: step + 1, total: steps.length }} />
        </p>
        <h1 ref={heading} tabIndex={-1}>
          <FormattedMessage {...steps[step]!} />
        </h1>

        {step === 0 && (
          <UnitsField
            value={values.units}
            legend={steps[0]}
            hideLegend
            onChange={(units) => setValues((v) => ({ ...v, units }))}
          />
        )}
        {step === 1 && (
          <TrackingModeField
            value={values.trackingMode}
            legend={steps[1]}
            hideLegend
            onChange={(trackingMode) => setValues((v) => ({ ...v, trackingMode }))}
          />
        )}
        {step === 2 && (
          <GoalFocusField
            value={values.goalFocus}
            legend={steps[2]}
            hideLegend
            onChange={(goalFocus) => setValues((v) => ({ ...v, goalFocus }))}
          />
        )}

        {error && (
          <p className="form-error" role="alert">
            {error}
          </p>
        )}
        {last && !online && (
          <p className="hint">
            <FormattedMessage {...messages.offlineCantSaveProfile} />
          </p>
        )}

        <div className="onboarding-actions">
          {step > 0 && (
            <button type="button" className="button" onClick={() => setStep((s) => s - 1)}>
              <FormattedMessage {...messages.back} />
            </button>
          )}
          <button type="submit" className="button button--primary" disabled={last && (update.isPending || !online)}>
            <FormattedMessage
              {...(last ? (update.isPending ? messages.saving : messages.startLogging) : messages.next)}
            />
          </button>
        </div>
        <button type="button" className="button onboarding-skip" onClick={() => navigate("/", { replace: true })}>
          <FormattedMessage {...messages.skipForNow} />
        </button>
      </form>
    </main>
  );
}
