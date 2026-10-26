# Junior golfers and belts

Junior golfers (*jeugdspelers*) work towards **belts**, as in judo: white, yellow, orange,
green, blue, brown and black. Their coach sets an **exam** for the next belt, takes it with
the junior on the range or the course, and awards the belt when the junior shows every
skill it asks for. The work is planned for milestone **M5 Coaching**, after
[coach access](coach-access.md), which it builds on. Nothing of it exists yet.

The rest of the system is in [`architecture.md`](architecture.md), and the existing screens
in [`clients/ux-design.md`](clients/ux-design.md).

## Requirements

| # | Requirement |
|---|---|
| J1 | A coach enrols a junior student in the belt programme; the junior starts on the white belt |
| J2 | A coach sets an exam for a junior's next belt: a date, optionally a place, and the skills it covers |
| J3 | The junior and their parent see the coming exam and what it asks, so the junior can practise for it |
| J4 | The coach evaluates each skill during the exam; when all are shown, the junior gets the belt |
| J5 | The junior sees their belt, their belt history and what to work on after an exam that isn't passed yet |
| J6 | GolfProgress has a standard syllabus per belt; a coach can adjust it for their own students |

What they imply:

| # | Requirement |
|---|---|
| N1 | **A parent consents for juniors under 16** before the account can be used, and can see the junior's belts and coaches and delete the account (AVG article 8). |
| N2 | **Encouraging, never judgmental** (`clients/ux-design.md` → "Wording"): an exam is *passed* or *not yet*, never *failed*. |
| N3 | Junior and parent features work on web, iOS and Android, in English and Dutch, and pass the accessibility checklist. A belt is never shown by colour alone. |
| N4 | A belt can't be awarded by mistake or by someone who isn't the junior's coach, and every award can be traced to an exam or a coach. |
| N5 | An exam keeps the skills it was taken with, even after the syllabus changes. |

## Decisions

### Junior accounts and parent consent (N1)

A junior has an ordinary GolfProgress account, like coaches do
([coach access](coach-access.md) → "One kind of account"). Sign-up asks one extra
question: **"Are you 16 or older?"**

- **Yes:** sign-up is as it is today. 16- and 17-year-olds need no parent and can join the
  belt programme like any student.
- **No:** the junior also gives their birth month and year, and a parent's email address.
  The account is created but **waits for the parent**: the junior can sign in and sees
  only *Waiting for your parent*, with Resend and Change email.

The parent gets an email with a single-use link, valid for **7 days** (stored as a hash, as
for coach invitations). They open it on the web, sign in or create their own account, and
**confirm** or **decline**. Confirming links the parent to the junior and unlocks the
account. Declining, or no answer within **14 days**, deletes the junior's account.

The parent of an under-16 junior:

- sees the junior's belt, coming exams, exam results and coaches (not their rounds or
  reflections: that's the junior's own, as for any golfer);
- **invites and removes the junior's coaches.** A junior under 16 can't invite a coach
  themselves: sharing data with another adult is the parent's choice. The coach's
  invitation says *"Sanne's parent invites you to coach Sanne"*;
- can **delete the junior's account**, which also withdraws consent.

At most **2 parents** per junior; a confirmed parent can invite the second. On the junior's
16th birthday (from the birth month) the parent link stays, and the junior can end it and
manage their own coaches from then on.

*Why:* GolfProgress relies on consent for coaching (sharing with another user), and under
Dutch law a child under 16 can't give it on their own. The terms now set 16 as the minimum
age, so this replaces that rule rather than adding to it. Asking for the birth month only
from under-16s keeps adults' data as it is.

### The belt programme (J1)

| Belt | Dutch | Exam |
|---|---|---|
| White | Wit | None: given on enrolment |
| Yellow | Geel | Yes |
| Orange | Oranje | Yes |
| Green | Groen | Yes |
| Blue | Blauw | Yes |
| Brown | Bruin | Yes |
| Black | Zwart | Yes |

- **Enrolling:** on a student's overview, the coach chooses *Start the belt programme*.
  The junior gets the white belt. Any student can be enrolled; the programme is meant for
  juniors, and the screens say so, but a coach may use it for an adult beginner too.
- **Belts belong to the junior,** not to the coach: they keep them when they change coach
  or club, and every coach of theirs sees them. Belts are always shared with the junior's
  coaches, like rounds and progress.
- **Belts go up one at a time:** an exam is always for the next belt.
- **A belt earned elsewhere:** a coach can *record a belt* without an exam, with a short
  reason ("Earned at GC De Hoge Kleij, June 2026"). It's shown as recorded, not examined.
- **Correcting a mistake:** the coach who awarded or recorded the latest belt can undo it
  within **7 days**. After that, only support can.

### Exams (J2, J3, J4)

1. **Setting the exam.** On the student's overview the coach chooses *Set an exam*: the
   date, optionally the place ("Driving range, Hilversum"), and a note for the junior.
   The skills come from the coach's syllabus for that belt (below), and the coach can
   leave out or add skills for this one exam. A junior has at most **one open exam**.
2. **Before the exam,** the junior and their parent see it on the dashboard and on the
   Belts screen: the date and place and *What you'll show*, the list of skills. The coach
   can change the date, place and skills, or cancel the exam, until it's taken.
3. **Taking the exam.** On the day, the coach opens the exam on their phone (the web app
   works on a phone; the native apps follow with the mobile coach workspace) and marks
   each skill **Shown** or **Not yet**, with an optional note ("Great tempo!"). Results
   save as they're marked, so a dropped connection loses nothing.
4. **Finishing.** The coach chooses *Finish exam* and can add a closing word.
   - **Every skill shown:** the exam is **passed** and the junior gets the belt, dated the
     exam day. The next time the junior opens the app, they see the new belt, full screen.
   - **Any skill not yet:** the result is **not yet**. The junior sees what they showed,
     and what to work on, with the coach's notes.
5. **A retake** is a new exam for the same belt. Skills shown in an exam less than
   **3 months** earlier start as *Shown* (the coach can still test them again), so the
   junior only has to show what's left.

*Why only Shown or Not yet:* juniors are as young as 6. Scores per skill would invite
comparison between children; a coach who wants to track numbers can use the note.

### The syllabus (J6, N5)

GolfProgress comes with a **standard syllabus**: for each belt, 5 to 8 skills, in six
areas: Putting, Chipping and pitching, Full swing, Bunker, Rules and etiquette, and On the
course. A skill is one sentence a coach can check on the spot, with a measure where one
helps ("Holes 3 of 5 putts from 1 metre").

A coach adjusts it for their own students: per belt they can reword, add, remove and
reorder skills, or go back to the standard. Their syllabus applies to the exams they set
from then on.

**Each exam keeps a copy of its skills.** Changing the syllabus never changes an exam that
was set or taken, so a belt always shows what it was earned with.

Draft standard syllabus, to be reviewed with a golf professional before release (see
Open questions):

| Belt | Skills |
|---|---|
| Yellow | Holes 3 of 5 putts from 1 m · 3 of 5 chips from the fringe finish within 3 m of the hole · Gets 3 of 5 balls in the air with a short iron · Knows where to stand when someone swings, and waits until it's safe · Repairs a pitch mark |
| Orange | Holes 4 of 5 putts from 1 m · 3 of 5 putts from 6 m finish within 1 m · 3 of 5 chips finish within 2 m · 3 of 5 pitches from 20 m land on the green · Hits a driver or hybrid 3 of 5 times into the fairway area · Replaces a divot and rakes a bunker · Plays 3 holes and keeps their own score |
| Green | 4 of 5 putts from 6 m finish within 1 m · Gets out of a greenside bunker 3 of 5 times · 3 of 5 pitches from 30 m finish within 6 m · 7-iron: 3 of 5 balls within a 20 m wide lane · Knows out of bounds, penalty areas and a lost ball, and their penalties · Plays 6 holes with no more than double par on each |
| Blue | Holes 3 of 5 putts from 2 m · 3 of 5 bunker shots finish on the green · Chooses the club for a shot and explains why · Takes a correct drop: relief from a penalty area and an unplayable ball · Keeps up the pace of play · Plays 9 holes with a playing partner, keeping score for both |
| Brown | 4 of 5 putts from 10 m finish within 1 m · Plays a high and a low pitch on request · Hits a fade or draw shape on request, 2 of 3 times · Explains the rules of a stroke play competition and how to mark a scorecard · Plays a qualifying 9-hole round for a handicap |
| Black | Holes 4 of 5 putts from 2 m · Gets up and down from a greenside bunker 2 of 5 times · Plays 18 holes at their handicap or better · Knows the rules well enough to settle a common question on the course · Helps a younger junior practise for their yellow belt |

### Notifications

Email, as for the rest of coaching ([coach access](coach-access.md) → "Notifications"):

| Email | To | When | Can be switched off |
|---|---|---|---|
| Parent consent | Parent | On junior sign-up, and on Resend | No: it's the consent request |
| Exam set or changed | Junior, parents | When the coach sets, moves or cancels an exam | Yes, in Preferences |
| Exam result | Junior, parents | When the coach finishes an exam | Yes, in Preferences |

The result email for *not yet* is worded like the screen: what was shown first, then what
to work on.

### Certificate

For a passed exam, the web app has a **printable certificate** (A4 landscape): the
junior's name, the belt, the date, the coach's name and the skills shown. It's a print
page, so a parent can save it as PDF from the browser; the API doesn't make PDFs.

## API

All endpoints need a signed-in user. As for coach access, coach and parent endpoints
answer **404** when the caller has no active link to that junior.

**Sign-up and consent**

| Endpoint | Purpose |
|---|---|
| `POST /auth/register` | Gains `under16`, `birthMonth` (`yyyy-MM`) and `parentEmail`, required when `under16` is true |
| `GET /me` | Gains `parentConsent`: `notNeeded`, `pending` or `given` |
| `POST /me/parent-consent` | The junior resends the email, optionally to a different address |
| `POST /parent-consents/confirm`, `/decline` | The parent, signed in, with the token from the email |

While consent is pending, every other endpoint except `GET /me`, `DELETE /me` and sign-out
answers **403** with the problem type `parent-consent-required`, and the clients show
*Waiting for your parent*.

**Parent**

| Endpoint | Purpose |
|---|---|
| `GET /parent/juniors` | The parent's juniors, with belt and coming exam |
| `GET /parent/juniors/{juniorId}` | Belt history, exams with results, coaches |
| `POST /parent/juniors/{juniorId}/parents` | Invite a second parent |
| `POST /parent/juniors/{juniorId}/coach-invitations`, `DELETE …/coach-invitations/{id}` | Invite a coach for the junior, cancel |
| `PATCH`/`DELETE /parent/juniors/{juniorId}/coaches/{linkId}` | Change what a coach sees, remove them |
| `DELETE /parent/juniors/{juniorId}` | Delete the junior's account |

For an under-16 junior, `POST /me/coach-invitations` and the other coach-managing `/me`
endpoints answer **403** (`parent-manages-coaches`).

**Junior (and any enrolled golfer)**

| Endpoint | Purpose |
|---|---|
| `GET /me/belts` | Current belt, history, the open exam with its skills, and the latest result |

**Coach**

| Endpoint | Purpose |
|---|---|
| `POST /coach/students/{golferId}/belts` | Enrol (white belt), or record a belt earned elsewhere, with a reason |
| `DELETE /coach/students/{golferId}/belts/{beltId}` | Undo the latest belt, within 7 days, by the coach who gave it |
| `GET /coach/students/{golferId}/belts` | As `GET /me/belts`, plus every exam |
| `POST /coach/students/{golferId}/belt-exams` | Set an exam: date, place, note, skills |
| `PATCH /coach/belt-exams/{examId}` | Change date, place, note or skills before it's taken |
| `PUT /coach/belt-exams/{examId}/skills/{skillId}` | Mark a skill Shown or Not yet, with a note |
| `POST /coach/belt-exams/{examId}/finish`, `/cancel` | Finish (passed or not yet follows from the skills), or cancel |
| `GET /coach/syllabus` | The coach's syllabus per belt, and which belts follow the standard |
| `PUT`/`DELETE /coach/syllabus/{belt}` | Replace a belt's skills, or go back to the standard |

The student overview (`GET /coach/students/{golferId}/overview`) gains the belt and the open
exam. The authorisation policy from coach access decides every coach request; it gains the
parent as a caller with access to a junior's belts, exams and coaches.

## Data model

| Table | Columns |
|---|---|
| `Users` | Gains BirthMonth (only for under-16 sign-ups) and ParentConsent (NotNeeded, Pending, Given) |
| `ParentConsents` | Id, JuniorId, Email, TokenHash, CreatedAt, ExpiresAt, ConfirmedAt, DeclinedAt |
| `ParentLinks` | Id, JuniorId, ParentId, CreatedAt, EndedAt |
| `SyllabusSkills` | Id, CoachId (null for the standard syllabus), Belt, Area, Text, SortOrder |
| `Belts` | Id, GolferId, Belt, AwardedOn, AwardedById (null once that coach's account is deleted), Source (Enrolled, Exam, Recorded), ExamId, Reason, CreatedAt |
| `BeltExams` | Id, GolferId, CoachId (null once deleted), Belt, ScheduledOn, Place, Note, Status (Open, Passed, NotYet, Cancelled), ClosingNote, FinishedAt, CreatedAt |
| `BeltExamSkills` | Id, ExamId, Area, Text, SortOrder, Result (null, Shown, NotYet), Note, CarriedFromExamId |

The standard syllabus is seeded by a migration. Unique indexes allow one open exam per
golfer and one belt of each colour per golfer.

## Privacy, deletion and audit (N1, N4)

- **The junior deletes their account,** or a parent does: belts, exams, parent links and
  consents go with everything else in `DELETE /me`.
- **A parent deletes their own account:** their parent links go. For an under-16 junior
  with no other parent, the junior's account goes back to *waiting for your parent*, and
  the junior can name another parent within 14 days.
- **A coach deletes their account:** belts and exams stay with the junior, from *a former
  coach*; their syllabus goes.
- **Events:** junior signed up, parent consent given or declined, parent link ended, belt
  enrolled, recorded or undone, exam set, finished or cancelled.
- **Legal:** the privacy policy and terms change from "not for children under 16" to how
  under-16s use GolfProgress with a parent's consent, written so a child can follow it.
  Juniors under 13 make the apps fall under the **Google Play Families policy** and Apple's
  rules for apps used by children: no third-party analytics or advertising in those
  accounts, and the store declarations must list the age groups. Crash reports and logs
  for junior accounts are reviewed against that before release.

## Screens

**Junior** (web, iOS, Android)

| Screen | Content |
|---|---|
| Sign up | *Are you 16 or older?*; for No: birth month and year, parent's email |
| Waiting for your parent | Who the email went to, Resend, Change email, Sign out |
| Dashboard | A **belt card**: the belt, and the coming exam or *Ask your coach about your next belt* |
| Profile → **Belts** | The belt, large; the coming exam with *What you'll show*; the latest result with notes; belt history |
| New belt | Full screen, once, after a passed exam: the belt and the coach's closing word |
| Certificate (web) | Printable, per belt earned in an exam |
| Preferences | The two exam emails |

**Parent** (web; works on a phone browser)

| Screen | Content |
|---|---|
| Confirm consent | Who signed up, what GolfProgress keeps, Confirm or Decline |
| Profile → **My juniors** | Each junior with their belt and coming exam |
| Junior | Belt history, exams and results, coaches (Invite a coach, Remove), Invite another parent, Delete account |

**Coach** (web first, then iOS and Android with the mobile coach workspace)

| Screen | Content |
|---|---|
| Student overview | A **Belt** section: the belt, the open exam; *Start the belt programme*, *Set an exam*, *Record a belt* |
| Set an exam | Belt (the next one), date, place, note, the skills from the syllabus with Add and Remove |
| Take the exam | The skills by area, each with **Shown** / **Not yet** and a note; *Finish exam* |
| Exam result | Passed or not yet, every skill and note |
| Profile → **Syllabus** | Per belt: the skills, Edit, *Back to the standard* |

**Showing a belt (N3).** A belt is a badge with its colour **and** its name ("Green belt"),
and a white belt has an outline so it shows on a white background. The colours are design
tokens (`design/tokens.json`), checked for contrast against both themes.

## Phasing

Junior belts come after phase 1 of coach access (invitations and the coach workspace), and
are releasable on their own:

1. **Junior accounts:** sign-up with parent consent, the parent's screens, and the legal
   documents. Nothing about belts yet, but juniors can be coached.
2. **Belts and exams:** the standard syllabus, enrolling, setting and taking exams on the
   web, and the Belts screen and dashboard card on every client.
3. **Finishing touches:** the coach's own syllabus, exam emails, the certificate, and
   taking exams in the native coach apps.

## Open questions

- **Syllabus review:** the draft standard syllabus needs a golf professional's review,
  and possibly alignment with the NGF's junior programme or the *golfvaardigheidsbewijs*.
- **Club programmes:** a club or academy that shares one syllabus between its coaches
  (see coach access → "Academies").
- **Stripes:** many judo clubs give young children a stripe between belts; worth adding
  if coaches ask for smaller steps.
- **Juniors' rounds:** juniors often play fewer than 9 holes, or from forward tees; logging
  rounds may need a simpler form for them.
- **Coach-initiated invitations:** a club coach with 20 juniors may want to send a join
  link, rather than waiting for 20 parents to invite them.
