import type { Schemas } from "../api";
import { useMe } from "../api/queries";
import { useStudentScope } from "../api/studentScope";

/** The rules of a user's plan on the web (#313, docs/payments.md), apart from the screens. */

type Me = Schemas["UserResponse"];

/** How many days before the trial ends the banner starts saying so. */
export const trialNoticeDays = 30;

export type PlanBanner = { kind: "readOnly" } | { kind: "trialEnding"; endsAt: string } | null;

/**
 * The banner above every page: read-only after the trial; while the trial runs out, when
 * it ends. Nothing in the launch period, for a subscriber, or while it's still far off.
 */
export function planBanner(me: Pick<Me, "plan" | "trialEndsAt">, now: Date = new Date()): PlanBanner {
  if (me.plan === "ReadOnly") return { kind: "readOnly" };
  if (me.plan !== "Trial" || !me.trialEndsAt) return null;
  const daysLeft = (Date.parse(me.trialEndsAt) - now.getTime()) / 86_400_000;
  return daysLeft <= trialNoticeDays ? { kind: "trialEnding", endsAt: me.trialEndsAt } : null;
}

/** Whether the signed-in user can log and change their golf: not after the trial without a subscription. */
export function useCanLog(): boolean {
  // While /me loads, assume yes: the API refuses what isn't allowed anyway.
  return useMe().data?.plan !== "ReadOnly";
}

/**
 * Whether this screen's rounds and practice can be changed: the user's own, while they can
 * log. A coach reading a student's never can (studentScope.ts).
 */
export function useCanChange(): boolean {
  const student = useStudentScope();
  const canLog = useCanLog();
  return !student && canLog;
}

/** "€2.99" from 299 and "eur", in the user's language. */
export function formatPrice(cents: number, currency: string, locale: string): string {
  return new Intl.NumberFormat(locale, { style: "currency", currency: currency.toUpperCase() }).format(cents / 100);
}

/** Where a subscription is managed: Stripe's portal from here, or Google Play. */
export function managedHere(subscription: NonNullable<Me["subscription"]>): boolean {
  return subscription.source === "Stripe";
}
