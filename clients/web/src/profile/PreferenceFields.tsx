import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useId } from "react";
import { FormattedMessage, useIntl } from "react-intl";
import { unwrap, type Schemas } from "../api";
import { queryKeys } from "../api/queries";
import { useApi } from "../app/ApiContext";
import { deviceLocale, languageChoices, type AppLocale, type LanguageChoice } from "../i18n/language";
import { messages, type PlainMessage } from "../i18n/messages";
import {
  goalFocusAreas,
  maxGoalFocus,
  toggleGoalFocus,
  trackingModes,
  unitOptions,
  type GoalFocusArea,
  type TrackingMode,
  type Units,
} from "./profileForm";
import "./profile.css";

/**
 * The preference controls (#111), shared by onboarding and Profile: radio groups for
 * units and tracking mode, and up to three goal focus areas as checkboxes.
 */

const unitLabels: Record<Units, PlainMessage> = { Metric: messages.unitsMetric, Imperial: messages.unitsImperial };

const trackingLabels: Record<TrackingMode, { label: PlainMessage; hint: PlainMessage }> = {
  Minimal: { label: messages.trackingMinimal, hint: messages.trackingMinimalHint },
  Balanced: { label: messages.trackingBalanced, hint: messages.trackingBalancedHint },
  Detailed: { label: messages.trackingDetailed, hint: messages.trackingDetailedHint },
};

export const goalLabels: Record<GoalFocusArea, PlainMessage> = {
  Putting: messages.goalPutting,
  Penalties: messages.goalPenalties,
  TeeShots: messages.goalTeeShots,
  Consistency: messages.goalConsistency,
};

export const trackingLabel = (mode: TrackingMode) => trackingLabels[mode].label;
export const unitLabel = (units: Units) => unitLabels[units];

function Legend({ label, hidden }: { label: PlainMessage; hidden?: boolean }) {
  return (
    <legend className={hidden ? "visually-hidden" : "choice-legend"}>
      <FormattedMessage {...label} />
    </legend>
  );
}

/** `hideLegend`: where a heading already asks the question, as in onboarding. */
export function UnitsField({
  value,
  onChange,
  legend = messages.units,
  hideLegend,
}: {
  value: Units;
  onChange: (units: Units) => void;
  legend?: PlainMessage;
  hideLegend?: boolean;
}) {
  const name = useId();
  return (
    <fieldset className="choice-group">
      <Legend label={legend} hidden={hideLegend} />
      {unitOptions.map((units) => (
        <label key={units} className="choice">
          <input type="radio" name={name} checked={value === units} onChange={() => onChange(units)} />
          <span className="choice-label">
            <FormattedMessage {...unitLabels[units]} />
          </span>
        </label>
      ))}
    </fieldset>
  );
}

export function TrackingModeField({
  value,
  onChange,
  legend = messages.trackingMode,
  hideLegend,
}: {
  value: TrackingMode;
  onChange: (mode: TrackingMode) => void;
  legend?: PlainMessage;
  hideLegend?: boolean;
}) {
  const name = useId();
  return (
    <fieldset className="choice-group">
      <Legend label={legend} hidden={hideLegend} />
      {trackingModes.map((mode) => (
        <label key={mode} className="choice">
          <input type="radio" name={name} checked={value === mode} onChange={() => onChange(mode)} />
          <span className="choice-label">
            <FormattedMessage {...trackingLabels[mode].label} />
            <span className="hint">
              <FormattedMessage {...trackingLabels[mode].hint} />
            </span>
          </span>
        </label>
      ))}
      <p className="hint">
        <FormattedMessage {...messages.canChangeAnyTime} />
      </p>
    </fieldset>
  );
}

/**
 * Each language by its own name, as language pickers do, so it can be found whatever
 * the app is showing; `lang` lets screen readers say it in that language.
 */
const languageNames: Record<AppLocale, string> = { en: "English", nl: "Nederlands" };
const localeOfChoice: Record<NonNullable<LanguageChoice>, AppLocale> = { English: "en", Dutch: "nl" };

function LanguageName({ locale }: { locale: AppLocale }) {
  return <span lang={locale}>{languageNames[locale]}</span>;
}

/** The device's language is named next to its option, so it's clear what it means here. */
export function LanguageField({
  value,
  onChange,
}: {
  value: LanguageChoice;
  onChange: (choice: LanguageChoice) => void;
}) {
  const name = useId();
  const hintId = useId();
  return (
    <fieldset className="choice-group" aria-describedby={hintId}>
      <Legend label={messages.language} />
      <p id={hintId} className="hint">
        <FormattedMessage {...messages.languageHint} />
      </p>
      {languageChoices.map((choice) => (
        <label key={choice ?? "device"} className="choice">
          <input type="radio" name={name} checked={value === choice} onChange={() => onChange(choice)} />
          <span className="choice-label">
            {choice === null ? (
              <>
                <FormattedMessage {...messages.languageDevice} />
                <span className="hint">
                  <LanguageName locale={deviceLocale()} />
                </span>
              </>
            ) : (
              <LanguageName locale={localeOfChoice[choice]} />
            )}
          </span>
        </label>
      ))}
    </fieldset>
  );
}

/** Once three are ticked, the others are disabled until one is unticked. */
export function GoalFocusField({
  value,
  onChange,
  legend = messages.goalFocus,
  hideLegend,
}: {
  value: GoalFocusArea[];
  onChange: (areas: GoalFocusArea[]) => void;
  legend?: PlainMessage;
  hideLegend?: boolean;
}) {
  const hintId = useId();
  const full = value.length >= maxGoalFocus;
  return (
    <fieldset className="choice-group" aria-describedby={hintId}>
      <Legend label={legend} hidden={hideLegend} />
      <p id={hintId} className="hint">
        <FormattedMessage {...messages.goalFocusHint} values={{ max: maxGoalFocus }} />
      </p>
      {goalFocusAreas.map((area) => {
        const checked = value.includes(area);
        return (
          <label key={area} className="choice">
            <input
              type="checkbox"
              checked={checked}
              disabled={full && !checked}
              onChange={() => onChange(toggleGoalFocus(value, area))}
            />
            <span className="choice-label">
              <FormattedMessage {...goalLabels[area]} />
            </span>
          </label>
        );
      })}
    </fieldset>
  );
}

/**
 * PATCH /me. The saved profile replaces the cached one at once, so a new tracking mode
 * changes Log round's fields straight away.
 */
export function useUpdateMe() {
  const api = useApi();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (body: Schemas["UpdateMeRequest"]) => unwrap(await api.PATCH("/me", { body })),
    onSuccess: (me) => {
      queryClient.setQueryData(queryKeys.me, me);
    },
  });
}

/** "Changes saved." or the error, announced to screen readers. */
export function SaveStatus({ saved, error }: { saved: boolean; error?: string }) {
  const intl = useIntl();
  return (
    <>
      <p className="visually-hidden" role="status">
        {saved ? intl.formatMessage(messages.changesSaved) : ""}
      </p>
      {saved && (
        <p className="save-confirmation" aria-hidden="true">
          <FormattedMessage {...messages.changesSaved} />
        </p>
      )}
      {error && (
        <p className="form-error" role="alert">
          {error}
        </p>
      )}
    </>
  );
}
