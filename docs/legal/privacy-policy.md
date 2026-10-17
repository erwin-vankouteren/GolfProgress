# GolfProgress privacy policy

> **DRAFT for review (#121).** Not legal advice, and not yet published. Fill in every
> `[bracketed]` item, have it reviewed, then publish it at a stable URL and set that URL
> in the apps (`VITE_PRIVACY_POLICY_URL` for the web, `PRIVACY_POLICY_URL` for Android)
> and in both store listings. Keep it in step with `docs/architecture.md` →
> "Observability and privacy" and "Deleting an account".

_Last updated: [date of publication]_

GolfProgress helps you see your progress at golf beyond the score. This policy explains
what we store, why, how long we keep it, and how you can see, change or delete it.

## Who we are

GolfProgress is provided by [name of person or company], [address], [country]
("we", "us"). Contact us about privacy at [privacy email address].

## What we store, and why

| What | Examples | Why |
|---|---|---|
| Your account | Email address, display name, a hash of your password (never the password itself) | To let you sign in and recognise your data as yours |
| Your profile and preferences | Units, tracking mode, goal focus areas, handicap index and typical 18-hole score if you enter them, time zone | To show the right fields and compare your rounds fairly |
| Your rounds | Date, holes, course and tee names, strokes and the other statistics you choose to track, notes | To work out your fitness score, micro-wins and trends |
| What we work out from them | Fitness scores, micro-wins, your next focus, baselines, monthly reviews | That is the service |
| Your reflections and practice sessions | Ratings, what worked, what to change; practice type, duration, venue, goal, tags, notes | To include them in your Habits score and show them back to you |
| Sign-in sessions | A hash of each device's sign-in token, and when it expires | To keep you signed in, and to sign you out everywhere when you change or reset your password or delete your account |
| Password reset links | A hash of the link's token, valid for 30 minutes | To let you reset a forgotten password |
| An activity log | That a round was logged or a score computed, with dates and ids, never your notes or reflections | To understand and fix problems, and to protect the service |
| Crash and error reports | The app version, device model and operating system, what went wrong, and a request id, never your email, notes or reflections | To find and fix crashes |
| Technical logs | The time and path of requests and, briefly, your IP address to limit repeated sign-in attempts | To keep the service secure and working |

We do **not** show ads, sell or share your data for advertising, or track you across other
apps and websites. The apps use no advertising or analytics SDKs.

**Legal basis** (if you are in the EU or UK): we process your account, rounds,
reflections and practice to provide the service you signed up for (performance of a
contract). We keep logs, the activity log and crash reports because we have a
legitimate interest in keeping the service secure and working.

## Who processes it for us

- **Microsoft Azure** hosts the service and its database in [region, e.g. West Europe],
  sends password reset emails (Azure Communication Services) and keeps the technical logs
  (Application Insights, with IP addresses masked).
- **[Crash reporting provider, e.g. Sentry]** receives crash and error reports
  ([region]).

They process the data only on our behalf. [If data leaves the EU/EEA: state the
safeguard, e.g. the provider's Standard Contractual Clauses.]

## How long we keep it

- Your account and everything in it: until you delete your account.
- The activity log: 365 days, then it's deleted automatically.
- Sign-in sessions: until they expire (14 days without use) or are revoked.
- Password reset links: they stop working after 30 minutes or when used.
- Technical logs: 30 days.
- Crash reports: [the provider's retention, e.g. 90 days].
- Database backups follow the hosting's backup retention ([e.g. 7 days]) and are then
  overwritten; we don't edit backups.

## Deleting your account

You can delete your account at any time in the app: **Profile → Delete account**. We ask
for your password, then delete, in one go, your account, rounds and their scores and
micro-wins, reflections, practice sessions, baselines and reviews, the activity log, your
password reset links, and every sign-in session, which signs out every device. Nothing
that identifies you is kept, except in backups until they are overwritten.

## Cookies and storage

The web app uses one cookie, `gp_refresh`, to keep you signed in. It is strictly
necessary, can't be read by scripts on the page, and is only sent to the sign-in part of
our service. We use no other cookies, and no tracking or analytics cookies.

## Your rights

You can see and change your details in the app, and delete your account there. You can
also ask us, at [privacy email address], for a copy of your data, to correct it, to
restrict or object to how we use it, or to delete it. If you are in the EU or UK you can
complain to a data protection authority, in the Netherlands the Autoriteit
Persoonsgegevens ([or the authority where you are]).

## Children

GolfProgress is not meant for children under [16]. We don't knowingly collect their data.

## Changes

We will update this page when we change how we handle your data, and say so in the app
for significant changes.
