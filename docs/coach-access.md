# Coach access and feedback

Golfers invite their coach or golf pro, who then follows their rounds, practice and
reflections, gives feedback on them, and sets practice goals. The work is planned for
milestone **M5 Coaching**, after the public launch. Nothing of it exists yet.

Background: the product overview (`docs/golfprogress-overview.pdf`, "Coming next").
The rest of the system is in [`architecture.md`](architecture.md), and the existing screens
in [`clients/ux-design.md`](clients/ux-design.md).

## Requirements

The overview promises four things. Each becomes a requirement, and the design adds what
they imply.

| # | Requirement | From the overview |
|---|---|---|
| R1 | A golfer invites a coach, and can remove them at any time | "You stay in control" |
| R2 | A coach sees only what the golfer shares | "They see only what you share" |
| R3 | A coach comments on a specific round, reflection or practice session; the golfer sees the feedback next to it | "Feedback where it belongs" |
| R4 | A coach suggests a goal for the next practice sessions; the practice log shows whether the golfer followed it, and the rounds show whether it worked | "Practice with a purpose" |
| R5 | Before a lesson, the coach sees the golfer's trend, next focus and reflections | "Better lessons" |

What they imply:

| # | Requirement |
|---|---|
| N1 | **Private by default.** Sharing personal data with another person needs the golfer's explicit choice, is covered by the privacy policy and store declarations, and ends cleanly on removal or account deletion. |
| N2 | **Every coach request is authorised** against an active link and what's shared, and tests prove a removed coach loses access immediately. |
| N3 | Golfer features work on web, iOS and Android, in English and Dutch, and pass the accessibility checklist. |
| N4 | People hear about new feedback without having to open the app to check. |
| N5 | Invitations can't be used to spam people. |

## Decisions

### One kind of account

A coach is an ordinary GolfProgress account that accepted an invitation. There's no
separate coach sign-up, coach role or directory of coaches: golfers invite the coach they
already have. A coach can also be a golfer with their own rounds.

*Why:* it reuses sign-in, password reset and account deletion as they are, and many club
pros play too. A directory or academy accounts can come later (see Open questions).

### Inviting a coach (R1, N5)

1. The golfer enters the coach's email address and chooses what to share (below).
2. The API emails the coach a link with a single-use token, valid for **7 days**. Like
   password reset links, only a hash of the token is stored.
3. The coach opens the link on the web, signs in or creates an account, and accepts or
   declines. **The account's email must match the invitation**, so a forwarded link can't
   be accepted by someone else.
4. Until then the golfer sees the invitation as *Pending*, and can cancel it.

Limits: at most **3 active coaches** per golfer, and **10 invitations a day** per golfer.
Inviting an address that already has a pending invitation from the same golfer sends the
email again rather than creating a second one.

### What a coach sees (R2, R5)

The golfer chooses per coach, and can change it at any time, with immediate effect:

| Share | Contents | Choice |
|---|---|---|
| **Rounds and progress** | Rounds and their stats, fitness scores with pillars, micro-wins, next focus, baseline, timeline, monthly reviews | Always shared: it's what coaching is about |
| **Reflections and notes** | Round reflections, and the free-text notes on rounds and practice sessions | On by default, can be switched off |
| **Practice** | Practice sessions, and practice goals | On by default, can be switched off |

Never shared: the golfer's email address (the coach sees the display name), password,
preferences, handicap and typical score, and their other coaches.

The golfer and the coach both see what's shared, on the coach's student overview and in the golfer's
Coaches screen.

### Ending access (R1)

Either side can end the link: the golfer removes the coach, or the coach stops coaching
the golfer. Access ends with the next request; there's no grace period. What the coach
wrote stays with the golfer: feedback is kept, and practice goals are kept, marked as
from a former coach. Inviting the same coach again creates a new link.

### Feedback (R3)

Each round, reflection and practice session can have one **feedback thread**. The coach
writes; the golfer can reply, so a question about the feedback has a place to go.

- Messages are plain text, 1–2,000 characters. Authors can edit or delete their own.
- A coach can only comment on what they can see: on a reflection only when reflections are
  shared, on a practice session only when practice is shared.
- The golfer sees the thread under the item it's about, and **"New from your coach"** on
  the dashboard while anything is unread. The coach sees unread replies per student.
- At most 60 messages an hour per user, to stop accidental or deliberate floods.

### Practice goals (R4)

A coach sets a goal for a golfer: a title ("Lag putts inside 1 metre"), optional
details, an optional **focus area** (Putting, Penalties, Tee shots, Consistency: the
existing goal focus areas), an optional session type, a **target number of sessions**,
and a period of at most 8 weeks.

- **Followed?** When logging a practice session, the golfer can link it to an active goal;
  the app suggests the goal when the session type matches. The goal shows *3 of 6
  sessions*.
- **Did it work?** For a goal with a focus area, the app compares that area's stat in the
  rounds played during the goal and the 2 weeks after, with the 5 rounds before it
  started:

  | Focus area | Stat |
  |---|---|
  | Putting | Putts per 18 holes |
  | Penalties | Penalties per 18 holes |
  | Tee shots | Fairways hit (%) |
  | Consistency | Spread of scores (standard deviation) |

  It's shown with a confidence level, like every other number in the app: with two rounds
  and no putts tracked, it says it can't tell yet.
- The golfer can mark a goal done, or dismiss it. Goals need practice to be shared.
- Golfers can't create goals themselves in this milestone (see Open questions).

### Notifications (N4)

Email, because push notifications aren't built yet ([Push notifications](https://github.com/erwin-vankouteren/GolfProgress/issues/127)):

| Email | To | When | Can be switched off |
|---|---|---|---|
| Invitation | Coach | On invite | No: it's the invitation itself |
| Feedback digest | Golfer | Once a day, only when there's unread feedback | Yes, in Preferences |
| Activity digest | Coach | Once a week: each student's new rounds, sessions and replies | Yes, in Preferences |

Digests are sent by a background service in the API that runs once a day. The API runs as
a single App Service instance, and a row lock in the database makes sure each digest goes
out once even if that changes. When push notifications arrive, these become push
notifications with the same preferences.

### Where the coach works

Golfers use the coaching features on **web, iOS and Android**. The coach's own workspace
(students, overview, giving feedback, setting goals) comes to the **web first**, because
coaches mostly prepare lessons at a desk. The mobile coach workspace follows within the
milestone, as its own feature, so it can move to a later milestone without blocking the rest.

## API

All endpoints need a signed-in user. Coach endpoints answer **404**, not 403, when the
caller has no active link to that golfer or the item isn't shared, so they don't reveal
whether a golfer, round or session exists.

**Golfer**

| Endpoint | Purpose |
|---|---|
| `POST /me/coach-invitations` | Invite a coach: email and what to share |
| `DELETE /me/coach-invitations/{id}` | Cancel a pending invitation |
| `GET /me/coaches` | Active coaches and pending invitations, with what each sees |
| `PATCH /me/coaches/{linkId}` | Change what a coach sees |
| `DELETE /me/coaches/{linkId}` | Remove a coach |
| `GET /me/practice-goals` | Goals, with progress |
| `PATCH /me/practice-goals/{id}` | Mark done or dismiss |
| `POST`/`PUT /me/practice-sessions` | Gain an optional `practiceGoalId` |

**Invitation (the coach, signed in)**

| Endpoint | Purpose |
|---|---|
| `POST /coach-invitations/accept` | Accept, with the token from the email |
| `POST /coach-invitations/decline` | Decline |

**Coach**

| Endpoint | Purpose |
|---|---|
| `GET /coach/students` | Students: name, last activity, unread replies, what's shared |
| `DELETE /coach/students/{golferId}` | Stop coaching a student |
| `GET /coach/students/{golferId}/overview` | Trend, next focus, baseline, latest reflections, active goals |
| `GET /coach/students/{golferId}/rounds`, `/rounds/{roundId}` | Rounds, read-only, the same contracts as the golfer's |
| `GET /coach/students/{golferId}/practice-sessions`, `/timeline`, `/monthly-reviews` | As above |
| `POST`/`PATCH`/`DELETE /coach/students/{golferId}/practice-goals[/{id}]` | Set and change goals |

The coach reads through its own routes, rather than through the golfer's routes with
widened ownership checks, so no existing golfer endpoint changes how it authorises.

**Feedback (golfer and coach)**

| Endpoint | Purpose |
|---|---|
| `GET /feedback?golferId&targetType&targetId` | A thread; `targetType` is `round`, `reflection` or `practiceSession` |
| `POST /feedback` | Write a message |
| `PATCH`/`DELETE /feedback/{id}` | Edit or delete your own message |
| `POST /feedback/read` | Mark a golfer's threads read up to a time |
| `GET /me/feedback/unread` | Unread count and the newest threads, for the dashboard |

One authorisation policy decides every feedback and coach request: the caller is the
golfer, or a coach with an active link whose sharing covers the item.

## Data model

| Table | Columns |
|---|---|
| `CoachInvitations` | Id, GolferId, Email, TokenHash, ShareReflections, SharePractice, CreatedAt, ExpiresAt, AcceptedAt, DeclinedAt, CancelledAt |
| `CoachLinks` | Id, GolferId, CoachId, ShareReflections, SharePractice, CreatedAt, EndedAt, EndedBy (golfer or coach) |
| `FeedbackMessages` | Id, GolferId, AuthorId (null for a deleted coach account), TargetType, TargetId, Text, CreatedAt, EditedAt |
| `FeedbackReads` | UserId, GolferId, ReadUpTo |
| `PracticeGoals` | Id, GolferId, CoachId (null once the coach's account is deleted), Title, Details, FocusArea, SessionType, TargetSessions, StartsOn, EndsOn, Status (Active, Done, Dismissed), CreatedAt |
| `PracticeSessions` | Gains PracticeGoalId |

A unique index allows one active link per golfer and coach.

## Privacy, deletion and audit (N1)

- **Golfer deletes their account:** their invitations, links, feedback threads and goals
  go with everything else in `DELETE /me`.
- **Coach deletes their account:** their links and invitations they accepted are deleted.
  Their messages and goals stay with the golfers they were written for, without the
  author: they show as from *a former coach*. The deletion test that checks no row
  references a deleted user is extended to these tables.
- **Events:** coach invited, invitation accepted or declined, link ended (and by whom),
  sharing changed, goal set. These go in `Events`, as the rest of the activity log does.
- **Privacy policy and store declarations:** coaching shares data with another user at the
  golfer's request. The policy, the terms of use and both store declarations say so, before
  the feature is released.

## Screens

**Golfer** (web, iOS, Android)

| Screen | Content |
|---|---|
| Profile → **Coaches** | Coaches and pending invitations, what each sees, Invite a coach, Remove |
| Invite a coach | Email, the three sharing choices with an explanation of each, Send invitation |
| Round, reflection and session detail | A **Feedback from your coach** thread under the content, with Reply |
| Dashboard | **New from your coach** while anything is unread |
| Practice | **Goals from your coach**, with progress, above the session list; Log session suggests a goal |
| Goal detail | The goal, linked sessions, and "Did it work?" |
| Preferences | The two digest emails |

**Coach** (web first, then iOS and Android)

| Screen | Content |
|---|---|
| **Students** (new section, shown once someone has accepted) | Students with last activity and unread replies |
| Accept invitation | Who invites, what they share; Accept or Decline |
| Student overview | Trend, next focus, baseline, latest reflections, active goals, unread threads |
| Student's rounds and sessions | Read-only lists and details, each with its feedback thread |
| Set a practice goal | Title, details, focus area, session type, target sessions, period |

## Phasing

Each phase is releasable on its own:

1. **Invite, share and give feedback:** invitations, sharing and removal, the coach's
   students and overview on the web, and feedback threads on every client.
2. **Practice goals:** goals, linking sessions, and "Did it work?".
3. **Notifications and the mobile coach workspace:** the digest emails, and the coach
   screens on iOS and Android.

## Open questions

- **Pricing:** is coaching free, part of a paid plan for golfers, or paid by coaches?
- **Golfers' own goals:** should golfers set practice goals without a coach?
- **Academies:** a club or academy with several coaches sharing students.
- **Attachments:** coaches often teach with video; out of scope for now.
