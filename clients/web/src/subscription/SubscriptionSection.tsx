import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useId, useState } from "react";
import { FormattedMessage, useIntl } from "react-intl";
import { Link, useSearchParams } from "react-router";
import { ApiProblem, unwrap, type Schemas } from "../api";
import { queryKeys, useMe } from "../api/queries";
import { useApi } from "../app/ApiContext";
import { useOnline } from "../common/states";
import { messages } from "../i18n/messages";
import { formatPrice, managedHere, planBanner } from "./plan";
import "./subscription.css";

type Me = Schemas["UserResponse"];
type Period = Schemas["CheckoutRequest"]["period"];

/** Leaving the app for Stripe's pages; a function so tests can see where it would go. */
export const goTo = {
  url: (url: string) => window.location.assign(url),
};

const longDate = (iso: string, intl: ReturnType<typeof useIntl>) =>
  intl.formatDate(new Date(iso), { dateStyle: "long" });

/**
 * Profile → Subscription (#313, docs/payments.md): the plan, and Subscribe through Stripe
 * Checkout, or Manage in Stripe's Customer Portal. A subscription from the Android app is
 * managed in Google Play. Back from checkout, it asks for the plan again a few times, as
 * Stripe's webhook can arrive a moment later.
 */
export function SubscriptionSection({ me }: { me: Me }) {
  const intl = useIntl();
  const id = useId();
  const [params] = useSearchParams();
  const queryClient = useQueryClient();
  const returned = params.get("subscription") === "done";

  useEffect(() => {
    if (!returned || me.plan === "Subscribed") return;
    let tries = 0;
    const timer = setInterval(() => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.me });
      if (++tries >= 5) clearInterval(timer);
    }, 2000);
    return () => clearInterval(timer);
  }, [returned, me.plan, queryClient]);

  const subscription = me.subscription;
  return (
    <section className="card profile-section subscription" id="subscription" aria-labelledby={id}>
      <h2 id={id}>
        <FormattedMessage {...messages.subscription} />
      </h2>
      {returned && me.plan !== "Subscribed" && (
        <p className="hint" role="status">
          <FormattedMessage {...messages.subscriptionDone} />
        </p>
      )}
      {subscription ? (
        <>
          <p className="subscription-plan">
            <FormattedMessage
              {...(subscription.period === "Year" ? messages.subscribedYearly : messages.subscribedMonthly)}
            />
            {subscription.coachingAddOn && (
              <>
                {" · "}
                <FormattedMessage {...messages.coachingIncluded} />
              </>
            )}
          </p>
          <p>
            {subscription.cancelsAt ? (
              <FormattedMessage
                {...messages.subscriptionCancels}
                values={{ date: longDate(subscription.cancelsAt, intl) }}
              />
            ) : subscription.renewsAt ? (
              <FormattedMessage
                {...messages.subscriptionRenews}
                values={{ date: longDate(subscription.renewsAt, intl) }}
              />
            ) : null}
          </p>
          {managedHere(subscription) ? (
            <ManageButton />
          ) : (
            <p className="hint">
              <FormattedMessage {...messages.managedInGooglePlay} />
            </p>
          )}
        </>
      ) : me.plan === "Trial" && !me.trialEndsAt ? (
        // The launch period: nothing to pay for yet.
        <p>
          <FormattedMessage {...messages.planLaunch} />
        </p>
      ) : (
        <>
          <p>
            {me.plan === "ReadOnly" ? (
              <FormattedMessage {...messages.planReadOnly} />
            ) : (
              <FormattedMessage {...messages.trialEndsOn} values={{ date: longDate(me.trialEndsAt!, intl) }} />
            )}
          </p>
          <SubscribeForm />
        </>
      )}
    </section>
  );
}

function SubscribeForm() {
  const intl = useIntl();
  const api = useApi();
  const online = useOnline();
  const coachingId = useId();
  const [period, setPeriod] = useState<Period>("Month");
  const [coaching, setCoaching] = useState(false);
  const prices = useQuery({
    queryKey: ["subscription-prices"],
    queryFn: async () => unwrap(await api.GET("/subscription/prices")),
    staleTime: 60 * 60_000,
    // 503 means subscribing isn't set up: asking again won't change that.
    retry: (failures, error) => !(error instanceof ApiProblem) && failures < 1,
  });
  const checkout = useMutation({
    mutationFn: async () => unwrap(await api.POST("/me/subscription/checkout", { body: { period, coaching } })),
    onSuccess: (redirect) => goTo.url(redirect.url),
  });

  if (prices.isPending) return null;
  if (prices.isError) {
    const unavailable = prices.error instanceof ApiProblem && prices.error.status === 503;
    return (
      <p className="hint">
        <FormattedMessage {...(unavailable ? messages.subscriptionUnavailable : messages.subscriptionPricesError)} />
      </p>
    );
  }

  const p = prices.data;
  const price = (cents: number) => formatPrice(cents, p.currency, intl.locale);
  const per = (cents: number, of: Period) =>
    intl.formatMessage(of === "Year" ? messages.pricePerYear : messages.pricePerMonth, { price: price(cents) });

  return (
    <form
      className="subscribe-form"
      noValidate
      onSubmit={(event) => {
        event.preventDefault();
        if (online && !checkout.isPending) checkout.mutate();
      }}
    >
      <fieldset className="choice-group">
        <legend className="choice-legend">
          <FormattedMessage {...messages.billingPeriod} />
        </legend>
        {(["Month", "Year"] as const).map((option) => (
          <label key={option} className="choice">
            <input
              type="radio"
              name="period"
              value={option}
              checked={period === option}
              onChange={() => setPeriod(option)}
            />
            {per(option === "Year" ? p.premiumYear : p.premiumMonth, option)}
          </label>
        ))}
      </fieldset>
      <div className="choice-with-hint">
        <label className="choice" htmlFor={coachingId}>
          <input id={coachingId} type="checkbox" checked={coaching} onChange={(e) => setCoaching(e.target.checked)} />
          <FormattedMessage
            {...messages.coachingFor}
            values={{ price: per(period === "Year" ? p.coachingYear : p.coachingMonth, period) }}
          />
        </label>
        <p className="hint">
          <FormattedMessage {...messages.coachingHint} />
        </p>
      </div>
      {!online && (
        <p className="hint">
          <FormattedMessage {...messages.offlineCantSubscribe} />
        </p>
      )}
      {checkout.isError && (
        <p className="field-error" role="alert">
          <FormattedMessage
            {...(checkout.error instanceof ApiProblem ? messages.subscribeFailed : messages.cantConnect)}
          />
        </p>
      )}
      <button type="submit" className="button button--primary" disabled={!online || checkout.isPending}>
        <FormattedMessage {...(checkout.isPending ? messages.working : messages.subscribe)} />
      </button>
    </form>
  );
}

function ManageButton() {
  const api = useApi();
  const online = useOnline();
  const portal = useMutation({
    mutationFn: async () => unwrap(await api.POST("/me/subscription/portal")),
    onSuccess: (redirect) => goTo.url(redirect.url),
  });

  return (
    <>
      {portal.isError && (
        <p className="field-error" role="alert">
          <FormattedMessage
            {...(portal.error instanceof ApiProblem ? messages.subscribeFailed : messages.cantConnect)}
          />
        </p>
      )}
      <button type="button" className="button" disabled={!online || portal.isPending} onClick={() => portal.mutate()}>
        <FormattedMessage {...(portal.isPending ? messages.working : messages.manageSubscription)} />
      </button>
    </>
  );
}

/**
 * Above every page (#313): read-only after the trial, or when the trial ends, with a way to
 * Subscribe. Nothing in the launch period or for a subscriber.
 */
export function PlanBanner() {
  const intl = useIntl();
  const me = useMe();
  const banner = me.data ? planBanner(me.data) : null;
  if (!banner) return null;

  return (
    <div className="plan-banner" role="status">
      <p>
        {banner.kind === "readOnly" ? (
          <FormattedMessage {...messages.readOnlyBanner} />
        ) : (
          <FormattedMessage {...messages.trialEndingBanner} values={{ date: longDate(banner.endsAt, intl) }} />
        )}
      </p>
      <Link to="/profile#subscription" className="button button--primary">
        <FormattedMessage {...messages.subscribe} />
      </Link>
    </div>
  );
}
