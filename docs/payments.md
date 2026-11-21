# Subscriptions and payments

How GolfProgress is paid for, on the web and Android; iOS follows later (M2 iOS onwards).
Settles #303 ("how coaching is priced"). The work: milestone M7 Payments (#308).

## Decisions

| | |
|---|---|
| **Free trial** | Everything, coaching included, for the first **30 days** after signing up. No card needed. |
| **Subscription** | **€2.99 a month** or **€29 a year**: logging rounds, reflections, practice and monthly reviews. |
| **Coaching** | An add-on for **golfers**: **€1.99 a month** or **€19 a year**, on top of the subscription. A coach's side is free. |
| **After the trial, unsubscribed** | **Read-only**: sign in, see everything, change the profile, export, delete the account, subscribe. Nothing new can be logged. |
| **Web payments** | **Stripe** (Checkout and the Customer Portal). We're the seller, so we handle the VAT. |
| **Android payments** | **Google Play Billing**, as Play requires for a subscription sold in the app. |
| **Source of truth** | The API. A subscription bought on the web counts on Android, and the other way round. |

Prices include VAT. They live in Stripe and Play Console, not in code; the API only knows
which product is which.

### Why the golfer pays for coaching

Coaching is something a golfer brings their coach into, so the golfer pays and the coach
never has a reason to say no. A coach with many students isn't charged for each of them.
Coaches who also log their own golf need a subscription for that, like anyone else.

### Read-only, not locked

The terms promise that users keep access to what they've entered (terms → "Price"). An
account whose trial ended keeps every screen for reading, so nothing is held hostage, and
logging a round is one subscription away.

### One trial, from the server

The trial is the API's, counted from the account's creation, not a trial offer in Stripe
or Play: it needs no card, it's the same on every platform, and it can't be restarted by
subscribing on another one. The stores' own free-trial offers stay off.

## What each state allows

| | Trial | Subscribed | Subscribed + Coaching | Read-only |
|---|---|---|---|---|
| See rounds, practice, progress, reviews | ✓ | ✓ | ✓ | ✓ |
| Log, edit and delete rounds, reflections, practice; generate a review | ✓ | ✓ | ✓ | — |
| Profile, preferences, password, export, delete account | ✓ | ✓ | ✓ | ✓ |
| Invite coaches; coaches see their data; write feedback; goals | ✓ | — | ✓ | — |

**When Coaching stops** (the add-on lapses, or the golfer drops to read-only), the links
stay but are **paused**: the coach's routes answer 404 as if the link had ended, and the
coach's Students list shows the golfer as paused, without their last activity or unread
replies; the weekly digest leaves them out. The coach can still end a paused link.
Renewing resumes them, with nothing lost. Feedback and goals already written stay
visible to the golfer, but they can't invite a coach or write to one: those answer 402
with problem type `coaching-required`.

**A coach's own state doesn't matter** for coaching: a coach who never subscribed can
still read their students' data, write feedback and set goals, as long as the golfer has
Coaching.

## Existing accounts

Accounts created before payments launch get a trial that ends **30 days after launch**, or
30 days after they signed up if that's later, so the terms' "30 days' notice" holds. From
launch, the app shows a banner saying when their free period ends and what it costs.

## API

**State on GET /me** (new fields):

| Field | Meaning |
|---|---|
| `plan` | `Trial`, `Subscribed` or `ReadOnly` |
| `trialEndsAt` | When the trial ends (also shown once it has) |
| `coaching` | Whether Coaching is on: in the trial, or as an add-on |
| `subscription` | Null, or `{ source: Stripe \| GooglePlay, period: Month \| Year, renewsAt, cancelsAt, coachingAddOn }` |

**Enforcement:** one filter decides every write: in read-only, the endpoints that log or
change rounds, reflections, practice sessions and reviews answer **402** with problem type
`subscription-required`. Coaching checks join `CoachAccess` (docs/coach-access.md → "API"):
a link whose golfer doesn't have Coaching counts as not active. The clients show the reason
and a Subscribe button; they also hide the actions in read-only, so the 402 is a backstop.

**Stripe (web):**

| Endpoint | Purpose |
|---|---|
| `POST /me/subscription/checkout` | A Stripe Checkout session for `{ period, coaching }`; the web app redirects to its URL |
| `POST /me/subscription/portal` | A Customer Portal session: change period, add or remove Coaching, cancel, invoices |
| `POST /webhooks/stripe` | `checkout.session.completed`, `customer.subscription.*`, `invoice.payment_failed`; checked against the signing secret |

**Google Play (Android):**

| Endpoint | Purpose |
|---|---|
| `POST /me/subscription/google-play` | The app sends a purchase token after buying; the API checks it with the Play Developer API (`purchases.subscriptionsv2`), acknowledges it and records it |
| `POST /webhooks/google-play` | Real-time developer notifications, pushed by a Pub/Sub subscription: renewals, cancellations, holds, revocations |

Products in Play Console: subscription `premium` (base plans `monthly`, `yearly`) and
subscription `coaching` (base plans `monthly`, `yearly`). The app passes the user's id as
`obfuscatedAccountId`, so a notification can be matched to an account.

**One subscription at a time:** someone subscribed in one place sees "Managed in Google
Play" or "Managed on the web" in the other, with a link there to manage it, and can't buy a
second. The Android app never points to the web to pay (Play's payments policy).

## Data model

| Table | Columns |
|---|---|
| `Subscriptions` | Id, UserId, Source (Stripe, GooglePlay), ExternalId (Stripe subscription id, or Play purchase token), Product (Premium, Coaching), Period, Status (Active, InGracePeriod, OnHold, Cancelled, Expired), CurrentPeriodEnd, CancelAt, CreatedAt, UpdatedAt |
| `Users` | Gains `StripeCustomerId` (with Stripe, #310) |

A user is **Subscribed** while a Premium row is Active or InGracePeriod, and has the
add-on while a Coaching row is. Webhooks only update rows; the state is derived on read.

The trial isn't stored either: it ends `Payments:TrialDays` (30) after the account was
created, or after `Payments:LaunchDate`, whichever is later. Until `LaunchDate` is set,
everyone is in the launch period: `plan` is `Trial` and `trialEndsAt` is null.

## Privacy, tax and legal

- **Payment details** never reach us: Stripe and Google take them. We keep the customer and
  subscription ids, status and dates. Account deletion cancels a Stripe subscription and
  deletes the rows; a Play subscription is cancelled in Play by the user (the app says so
  before deleting).
- **VAT:** as the seller on the web, we charge Dutch VAT, or the customer's country's
  through the EU One-Stop Shop. The prices are what customers pay, VAT included, and the VAT
  is accounted for outside Stripe (Stripe Tax is off: `stripeAutomaticTax`); registering
  for OSS is yours to do. On Google Play, Google is the seller and handles VAT.
- **Right of withdrawal (EU):** 14 days, unless the customer agrees to start straight away
  and gives up that right. Checkout asks for that consent.
- **Documents to update before launch:** terms → "Price" (plans, renewal, cancelling,
  withdrawal), privacy policy (Stripe as processor, what we keep), store declarations
  (Financial info → Purchase history), Play listing (in-app purchases: yes).

## Setup outside the code (yours)

1. **Stripe**, in test mode for staging and live mode for production:
   - Products **GolfProgress** (€2.99 a month, €29 a year) and **Coaching** (€1.99, €19),
     the amounts customers pay (set up with tax behaviour "exclusive", so Stripe adds
     nothing to them). Their four `price_…` ids go into `stripePrices` in
     `infra/<environment>.bicepparam`.
   - **Customer Portal** set up (switching period, adding and removing
     Coaching, cancelling, invoices), and the terms' URL `https://<web>/terms` under
     Settings → Public details: checkout asks to accept it.
   - A **webhook** to `https://<api>/webhooks/stripe` for `checkout.session.completed` and
     `customer.subscription.created`, `.updated` and `.deleted`.
   - Into the environment's Key Vault, **before** setting `stripePrices`: the secret key as
     `stripe-secret-key`, the webhook's signing secret as `stripe-webhook-secret`.
2. **Google Play:** the two subscriptions and their base plans in Play Console; a Pub/Sub
   topic for real-time notifications with a push subscription to
   `https://api.golfprogress.app/webhooks/google-play`; the release service account gets
   "View financial data" and "Manage orders and subscriptions".
3. **OSS VAT registration** with the Belastingdienst, before the first EU sale outside the
   Netherlands.

## Phasing

1. **Plans and read-only** (API, web, Android): the trial, `plan` on `/me`, read-only
   enforcement and screens, the banner for existing accounts. Shippable on its own:
   nobody pays yet, and the trial covers everyone.
2. **Stripe on the web.**
3. **Google Play Billing on Android.**
4. **Coaching as an add-on:** pausing links, the add-on in both checkouts.
5. **Legal and listings**, then **launch**: setting `Payments__LaunchDate` on the API's App Service, the date that starts the existing accounts'
   30 days.

## Open questions

- **Launch date**, which starts the existing accounts' 30 days.
- **Juniors** (M6): whether a parent pays, and how that fits the add-on.
