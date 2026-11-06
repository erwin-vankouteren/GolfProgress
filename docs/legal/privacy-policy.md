# GolfProgress privacy policy

<!--
  Published at https://golfprogress.app/privacy by the web app (#121), and linked from the
  apps and both store listings. Keep it in step with docs/architecture.md → "Observability
  and privacy" and "Deleting an account", and with store-declarations.md; update "Last
  updated" with every change.
-->

_Last updated: 6 November 2026_

GolfProgress helps you see your progress at golf beyond the score. This policy explains
what we store, why, how long we keep it, and how you can see, change or delete it.

## Who we are

GolfProgress is provided by Resolit B.V., Gedempte Zalmhaven 257, 3011 BT Rotterdam, the
Netherlands ("we", "us"). Contact us about privacy at service@golfprogress.app.

## What we store, and why

| What | Examples | Why |
|---|---|---|
| Your account | Email address, display name, a hash of your password (never the password itself) | To let you sign in and recognise your data as yours |
| Your profile and preferences | Units, tracking mode, goal focus areas, handicap index and typical 18-hole score if you enter them, time zone | To show the right fields and compare your rounds fairly |
| Your rounds | Date, holes, course and tee names, strokes and the other statistics you choose to track, notes | To work out your fitness score, micro-wins and trends |
| What we work out from them | Fitness scores, micro-wins, your next focus, baselines, monthly reviews | That is the service |
| Your reflections and practice sessions | Ratings, what worked, what to change; practice type, duration, venue, goal, tags, notes | To include them in your Habits score and show them back to you |
| Coach invitations | The email address you enter for your coach, what you chose to share, when it was sent and answered, and a hash of the link's token | To send the invitation, and let the invited person accept or decline it |
| Your coaches and students | Who coaches whom, what is shared, and when that started and ended | To show a coach only what the golfer chose to share |
| Sign-in sessions | A hash of each device's sign-in token, and when it expires | To keep you signed in, and to sign you out everywhere when you change or reset your password or delete your account |
| Password reset links | A hash of the link's token, valid for 30 minutes | To let you reset a forgotten password |
| An activity log | That a round was logged or a score computed, with dates and ids, never your notes or reflections | To understand and fix problems, and to protect the service |
| Technical logs | The time and path of requests and, briefly, your IP address to limit repeated sign-in attempts | To keep the service secure and working |
| Crash and error reports | When the app crashes or the service fails: what went wrong and where in the code, the app version, device model and operating system, the screens just before it, and a request id. No name, email address, account or device id, IP address, notes or reflections | To find and fix bugs |

We do **not** show ads, sell or share your data for advertising, or track you across other
apps and websites. The apps use no advertising or analytics SDKs.

**Legal basis** (if you are in the EU or UK): we process your account, rounds,
reflections and practice, and share them with a coach you invite, to provide the service
you signed up for (performance of a contract). We keep logs and the activity log because
we have a legitimate interest in keeping the service secure and working. We process the
email address of someone you invite as a coach because you and we have a legitimate
interest in delivering your invitation.

## Sharing with your coach

Nobody else sees your data unless you invite them. You can invite up to three coaches by
email. A coach sees your data once they accept, with an account that has the invited
address, and that includes what you logged before they accepted.

| What your coach sees | Contents | Your choice |
|---|---|---|
| Your name and progress | Your display name, rounds and their statistics, fitness scores, micro-wins, next focus, baseline, timeline, monthly reviews, and the date of your latest activity | Always shared: it's what coaching is about |
| Reflections and notes | Your round reflections, and the notes on your rounds and practice sessions | Shared unless you switch it off |
| Practice | Your practice sessions and practice goals | Shared unless you switch it off |

Coaches never see your email address, password, preferences, handicap index, typical
score, or your other coaches.

You can remove a coach at any time, and a coach can stop coaching you. Their access ends
straight away. What a coach wrote for you, such as feedback and practice goals, stays
with you. We can't undo what a coach has already seen or noted down: a coach is an
independent person you choose, not someone working for us, and is responsible for how
they use what they see. Our [terms of use](terms-of-use.md) tell them what they may do
with it.

**If you are a coach**, the golfers you coach see your display name. They already know
the address they invited.

**If someone invited you** and you have no account, we use your address only to send you
their invitation, which names them and what they would share. Its link stops working
after 7 days. The address stays stored with their invitation until they delete their
account; ask us at service@golfprogress.app if you want it removed sooner.

## Who processes it for us

- **Microsoft Azure** hosts the service and its database in the Netherlands (West
  Europe), with backup copies in Ireland (North Europe), sends password reset emails
  and coach invitations (Azure Communication Services, in Europe) and keeps the technical logs (Application
  Insights, with IP addresses masked).
- **Sentry** (Functional Software, Inc.) receives the crash and error reports and stores
  them in its EU region, in Germany.

They process the data only on our behalf, and store it in the EU.

## How long we keep it

- Your account and everything in it: until you delete your account.
- The activity log: 365 days, then it's deleted automatically.
- Sign-in sessions: until they expire (14 days without use) or are revoked.
- Password reset links: they stop working after 30 minutes or when used.
- Coach invitations: their links stop working after 7 days or when used; the invitation
  itself stays until the golfer's account is deleted, or the invited person's.
- Technical logs: 90 days.
- Crash and error reports: up to 90 days.
- Database backups: 35 days, and a few longer-term copies (weekly for 5 weeks, monthly for
  12 months) to recover from mistakes nobody noticed sooner. They are then deleted; we
  don't edit backups.

## Deleting your account

You can delete your account at any time in the app: **Profile → Delete account**. We ask
for your password, then delete, in one go, your account, rounds and their scores and
micro-wins, reflections, practice sessions, baselines and reviews, the activity log, your
password reset links, your coach invitations, invitations sent to your address, your links
to coaches and students, and every sign-in session, which signs out every device. Your
coaches lose access at once. Nothing that identifies you is kept, except in backups until
they are overwritten.

If you coach someone, what you wrote for them, such as feedback and practice goals, stays
with them without your name, as it's part of their record.

## Cookies and storage

The web app uses one cookie, `gp_refresh`, to keep you signed in. It is strictly
necessary, can't be read by scripts on the page, and is only sent to the sign-in part of
our service. We use no other cookies, and no tracking or analytics cookies.

## Your rights

You can see and change your details in the app, and delete your account there. You can
also ask us, at service@golfprogress.app, for a copy of your data, to correct it, to
restrict or object to how we use it, or to delete it. If you are in the EU or UK you can
complain to a data protection authority, in the Netherlands the Autoriteit
Persoonsgegevens, or the authority where you live.

## Children

GolfProgress is not meant for children under 16. We don't knowingly collect their data.

## Changes

We will update this page when we change how we handle your data, and say so in the app
for significant changes.
