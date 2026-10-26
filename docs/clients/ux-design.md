# Client UX design

The screens and navigation shared by the web, iOS and Android apps. Each platform
implements them with its own native controls and conventions, but the structure, content
and wording are the same everywhere. Every screen has a low-fidelity wireframe in
[`wireframes.md`](wireframes.md), and every screen meets the
[accessibility and localisation checklist](accessibility.md). The technical architecture is in
[`../architecture.md`](../architecture.md).

## Principles

- **Progress beyond the score.** A bad round can still show progress: the dashboard leads
  with the fitness score, micro-wins and the next lever, not just strokes.
- **Logging a round takes under a minute.** Only strokes and holes are required, and
  every other stat is optional. The form shows only the fields matching the user's
  tracking mode (Minimal, Balanced, Detailed).
- **Explain every number.** Each score links to how it was calculated: the pillars, the
  baseline it was compared against, and its confidence level.
- **Honest about uncertainty.** With few rounds, the confidence level is shown, and the
  copy says so ("Based on 3 rounds: early days").

## Navigation

Five top-level sections, in a sidebar on the web. On iOS and Android, Home, Rounds,
Practice and Progress are tabs, and **Profile** is the avatar in each screen's top bar:
together with the centre **Log round** button, five sections would need six tab slots, more
than fit comfortably on a phone.

| Section | Purpose | Main screens |
|---|---|---|
| **Home** | Where you stand now | Dashboard |
| **Rounds** | Your rounds | Round list → round detail → reflection; Log round |
| **Practice** | Your practice sessions | Session list → session detail; Log session |
| **Progress** | Trends over time | Timeline, baseline, monthly reviews |
| **Profile** | You and your settings | Profile, preferences, account |

**Log round** is the main action, reachable in one tap from every section: a centre tab
button on iOS and Android, and a primary button in the web header.

Signed-out users only see the authentication screens.

## Screens

Each screen lists the endpoints it uses. Endpoints marked **new** don't exist yet; they're
tracked as API issues.

### Authentication

| Screen | Content | Endpoints |
|---|---|---|
| Welcome | What the app does; Sign in / Create account | – |
| Create account | Email, display name, password | `POST /auth/register` |
| Sign in | Email, password; "Forgot password?" | `POST /auth/login` |
| Reset password | Request a reset email; set a new password from the email link (on the web, for now also from the mobile apps) | `POST /auth/password-reset`, `/confirm` |
| Update required | Shown when the app is older than the minimum supported version | `GET /client-config` |

After creating an account, a short onboarding asks for units, tracking mode and up to
three goal focus areas (`PATCH /me`), then opens Log round.

### Home: dashboard

| Element | Content | Endpoint |
|---|---|---|
| Last round card | Date, strokes, fitness score with confidence | `GET /me/dashboard` |
| Micro-wins | Up to three wins from the last round | ″ |
| Next lever | The one thing to focus on next round, with the reason | ″ |
| Baseline | Average score and confidence | ″ |
| This month | Average fitness score and the change on last month | ″ |

Empty state, before any rounds: an explanation and a "Log your first round" button.

### Rounds

| Screen | Content | Endpoints |
|---|---|---|
| Round list | Newest first, paged on scroll: date, course, strokes, fitness score | `GET /me/rounds` |
| Log round | Date, 9 or 18 holes, course, tee, strokes, notes, plus stats by tracking mode: **Minimal** strokes only; **Balanced** adds putts and penalties; **Detailed** also adds fairways, greens in regulation, three-putts and blow-up holes. Validated as you type, with the same rules as the API | `POST /me/rounds` (scores it) |
| Round detail | Fitness score with its four pillars, each expandable to its explanation; micro-wins; next lever; the round's stats; reflection | `GET /rounds/{id}`, `/fitness-score`, `/micro-wins`, `/reflections` |
| Edit round | As Log round, prefilled; delete with confirmation | `PUT` / `DELETE /rounds/{id}` |
| Add reflection | Focus and mood (1–5), frustration spill-over, what worked, what to repeat, one change for next time | `POST /rounds/{id}/reflections` |

After a round is logged, the app goes straight to its detail screen and shows the new
score: that moment is the reward for logging.

### Practice

| Screen | Content | Endpoints |
|---|---|---|
| Session list | Newest first, paged: date, type, duration | `GET /me/practice-sessions` |
| Log session | Date, type (Range, Putting, Short game, Simulator, Lesson, Other), duration, venue, goal, tags, notes | `POST /me/practice-sessions` |
| Session detail | All fields; delete | `GET` / `DELETE /me/practice-sessions/{id}` |

The list explains that practice counts towards the Habits pillar.

### Progress

| Screen | Content | Endpoints |
|---|---|---|
| Timeline | Chart of fitness score and strokes per round, with a date-range filter | `GET /me/timeline` |
| Monthly trend | Monthly averages over the last 6–12 months | `GET /me/timeline/monthly` |
| Baseline | Current averages per stat, sample size, completeness and confidence | `GET /me/baseline` |
| Monthly review | Highlights for a month and a focus for the next; generated on request | `GET` / `POST /me/monthly-reviews/...` |

Every chart has a text alternative (a summary sentence or a table) for screen readers.

### Profile

| Screen | Content | Endpoints |
|---|---|---|
| Profile | Display name, email, handicap, typical 18-hole score | `GET /me`, `PATCH /me` |
| Preferences | Units, tracking mode, goal focus areas | `PATCH /me` |
| Change password | Current and new password | `POST /me/password` |
| Sign out | Ends this session | `POST /auth/logout` |
| Delete account | Explains what is deleted, asks for the password, then deletes everything | `DELETE /me` |
| About | Version, privacy policy, terms | – |

### Coaching (planned, M5)

Coaches, feedback threads, a Students section for coaches and practice goals:
[`../coach-access.md`](../coach-access.md) → "Screens".

Junior sign-up with a parent's consent, the parent's screens, and belts and exams for
juniors: [`../junior-belts.md`](../junior-belts.md) → "Screens". Exam results use the same
wording rules as below: a skill is *shown* or *not yet*, never *failed*.

## States every screen handles

| State | Behaviour |
|---|---|
| Loading | Skeleton placeholders, not spinners, for content screens |
| Empty | Explanation plus the action that fills it |
| Error | Plain-language message and Retry; validation errors next to their fields |
| Offline | "You're offline" banner; writes are disabled until the connection returns (v1 is online-only) |
| Low confidence | Scores shown with their confidence level and a short explanation |

## Wording

- **Pillar names:** Execution, Damage control, Consistency and Habits, the same on every platform.
- **"Next lever"** is internal; users see **"Your next focus"**.
- **Encouraging, never judgmental:** "3 strokes above your baseline", not "a bad round".
