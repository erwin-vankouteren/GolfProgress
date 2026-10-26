# Wireframes

Low-fidelity wireframes for the MVP screens (#74). They fix **structure, content and
order**, not visual style; that comes from the design tokens (`design/tokens.json`). Each
platform renders them with its own native controls, following the
[UX design](ux-design.md).

**Legend:**

| Notation | Meaning |
|---|---|
| `[ Button ]` | button |
| `[_____]` | text field |
| `( ) / (•)` | radio |
| `[x]` | checkbox |
| `‹` | back |
| `›` | opens detail |
| `░░░` | loading skeleton |
| `▮` | chart mark |

Phone frames are 36 characters wide, desktop frames 92.

## Navigation

```
Phone (iOS / Android)                        Desktop (web)
┌──────────────────────────────────┐        ┌──────────────────────────────────────────────────────────────────────────────────────────┐
│ Title                            │        │ GolfProgress                                                       [ + Log round ]  (E) │
│                                  │        ├──────────────┬───────────────────────────────────────────────────────────────────────────┤
│                                  │        │ ▸ Home       │                                                                           │
│        screen content            │        │   Rounds     │                         screen content                                    │
│                                  │        │   Practice   │                                                                           │
│                                  │        │   Progress   │                                                                           │
│                                  │        │   Profile    │                                                                           │
├──────────────────────────────────┤        │              │                                                                           │
│ Home Rounds  (+)  Practice Progr.│        └──────────────┴───────────────────────────────────────────────────────────────────────────┘
└──────────────────────────────────┘
```

- **Phone:** the centre tab `(+)` is **Log round**, one tap from anywhere. Profile is the
  avatar in each screen's top bar, keeping the tab bar to five slots.
- **Desktop:** a sidebar with the five sections, and **Log round** in the header. Below
  about 900 px the sidebar collapses into a menu button.

## Authentication

```
Welcome                               Sign in                               Create account
┌──────────────────────────────────┐ ┌──────────────────────────────────┐ ┌──────────────────────────────────┐
│                                  │ │ ‹                                │ │ ‹                                │
│          GolfProgress            │ │ Sign in                          │ │ Create account                   │
│                                  │ │                                  │ │                                  │
│  See your progress beyond the    │ │ Email                            │ │ Name                             │
│  score: every round scored on    │ │ [______________________________] │ │ [______________________________] │
│  execution, damage control,      │ │ Password                         │ │ Email                            │
│  consistency and habits.         │ │ [______________________________] │ │ [______________________________] │
│                                  │ │                                  │ │ Password                         │
│                                  │ │ ⚠ Email or password is wrong.    │ │ [______________________________] │
│ [        Create account        ] │ │                                  │ │ At least 8 characters.           │
│ [           Sign in            ] │ │ [           Sign in            ] │ │                                  │
│                                  │ │ Forgot password?                 │ │ [        Create account        ] │
└──────────────────────────────────┘ └──────────────────────────────────┘ └──────────────────────────────────┘

Update required (below GET /client-config's minimum)     Onboarding (after Create account, 3 steps)
┌──────────────────────────────────┐                    ┌──────────────────────────────────┐
│                                  │                    │ Step 2 of 3                      │
│        Update required           │                    │ How much do you want to track?   │
│                                  │                    │                                  │
│  This version of GolfProgress is │                    │ (•) Minimal: just your score     │
│  no longer supported. Update to  │                    │ ( ) Balanced: + putts, penalties │
│  keep using the app.             │                    │ ( ) Detailed: + fairways, greens,│
│                                  │                    │     three-putts, blow-ups        │
│ [      Open the App Store      ] │                    │ You can change this any time.    │
│                                  │                    │ [            Next              ] │
└──────────────────────────────────┘                    └──────────────────────────────────┘
```

- **Onboarding steps:** 1, units (Metric / Imperial); 2, tracking mode; 3, up to three
  goal focus areas, then straight to **Log round**.
- **Sign-in errors:** shown in one place, never revealing whether the email exists. A
  `429` reads "Too many attempts. Try again in N seconds."

## Home: dashboard

```
Phone                                         Desktop
┌──────────────────────────────────┐         ┌──────────────────────────────────────────────────────────────────────────────────────────┐
│ Home                         (E) │         │ Home                                                                                     │
│                                  │         │ ┌─────────────────────────────────┐ ┌─────────────────────────────────┐ ┌──────────────┐ │
│ ┌──────────────────────────────┐ │         │ │ Last round · 3 May · 88         │ │ Your next focus                 │ │ Baseline     │ │
│ │ Last round · 3 May      88 › │ │         │ │ Fitness score   72  ●High       │ │ Cut down 3-putts                │ │ Avg 91.4     │ │
│ │ Fitness score   72  ● High   │ │         │ │ Exec ▮▮▮▮▮▮ Dmg ▮▮▮▮▮▮▮         │ │ On long putts, aim for a 1–2 m  │ │ ● Medium     │ │
│ │ Exec ▮▮▮▮▮▮  Dmg ▮▮▮▮▮▮▮      │ │         │ │ Cons ▮▮▮▮▮▮▮▮ Hab ▮▮▮▮          │ │ circle: pace first.             │ │ 5 rounds     │ │
│ │ Cons ▮▮▮▮▮▮▮▮ Hab ▮▮▮▮        │ │         │ └─────────────────────────────────┘ └─────────────────────────────────┘ └──────────────┘ │
│ └──────────────────────────────┘ │         │ ┌───────────────────────────────────────────────────────────────┐ ┌──────────────────────┐ │
│ Micro-wins                       │         │ │ Micro-wins                                                    │ │ This month           │ │
│ ✓ Fewer penalties                │         │ │ ✓ Fewer penalties   ✓ Learning captured   ✓ Consistency win   │ │ Avg score 70  ▲ +4   │ │
│ ✓ Learning captured              │         │ └───────────────────────────────────────────────────────────────┘ └──────────────────────┘ │
│ Your next focus                  │         └──────────────────────────────────────────────────────────────────────────────────────────┘
│ Cut down 3-putts               › │
│ Baseline  91.4 · ● Medium        │
│ This month  70  ▲ +4             │
├──────────────────────────────────┤
│ Home Rounds  (+)  Practice Progr.│
└──────────────────────────────────┘
```

| State | Shows |
|---|---|
| Loading | The same cards as `░░░` skeletons |
| Empty (no rounds) | "Log your first round to see your fitness score." `[ Log a round ]` |
| Low confidence | Next to the score: "● Low: based on 2 rounds, early days." |
| Error | "Couldn't load your dashboard." `[ Try again ]` |

## Rounds

```
Round list                            Log round: Minimal                    Log round: Detailed
┌──────────────────────────────────┐ ┌──────────────────────────────────┐ ┌──────────────────────────────────┐
│ Rounds                       (E) │ │ ✕  Log round              [Save] │ │ ✕  Log round              [Save] │
│                                  │ │ Date         [ 15 Oct 2026   ▾ ] │ │ Date         [ 15 Oct 2026   ▾ ] │
│ 3 May · Hilversum          88  72│ │ Holes        ( ) 9    (•) 18     │ │ Holes        ( ) 9    (•) 18     │
│ 28 Apr · De Pan            91  65│ │ Course       [_________________] │ │ Course       [_________________] │
│ 21 Apr · Hilversum         86  77│ │ Tee          [_________________] │ │ Strokes      [−]   88   [+]      │
│ 14 Apr · Utrecht (9)       44  70│ │ Strokes      [−]   88   [+]      │ │ Putts        [−]   33   [+]      │
│ ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░   │ │                                  │ │ Penalties    [−]    1   [+]      │
│ (more load on scroll)            │ │ Notes        [_________________] │ │ Fairways     [−]    7   [+] /14  │
│                                  │ │                                  │ │ Greens (GIR) [−]    5   [+] /18  │
│                                  │ │ More stats? Change tracking mode │ │ Three-putts  [−]    2   [+]      │
│                                  │ │ in Profile.                      │ │ Blow-up holes[−]    1   [+]      │
│                                  │ │                                  │ │ ⚠ Putts can't exceed strokes.    │
├──────────────────────────────────┤ └──────────────────────────────────┘ └──────────────────────────────────┘
│ Home Rounds  (+)  Practice Progr.│
└──────────────────────────────────┘
```

- **Fields by tracking mode:** **Minimal** shows strokes only. **Balanced** adds putts and
  penalties. **Detailed** also adds fairways, greens in regulation, three-putts and
  blow-up holes. The course, tee and notes fields are always offered.
- **Limits follow the hole count:** with 9 holes the maximums show as `/9` and the
  validation adjusts.
- **Validation** messages appear under their field, with the same rules as the API.
  **Save** stays enabled, and validates on press.
- **After Save:** the round detail opens with its new score.

```
Round detail                                                   Add reflection (sheet)
┌──────────────────────────────────┐                          ┌──────────────────────────────────┐
│ ‹ Rounds          3 May    [Edit]│                          │ ✕  Reflection             [Save] │
│ Hilversum · 18 holes · 88        │                          │ Focus      ○ ○ ○ ● ○   4 of 5    │
│                                  │                          │ Mood       ○ ○ ● ○ ○   3 of 5    │
│ Fitness score  72   ● High       │                          │ [x] Frustration carried over     │
│ ▸ Execution        18 / 25       │                          │ What worked                      │
│ ▾ Damage control   21 / 25       │                          │ [______________________________] │
│   1 penalty, 1 blow-up hole      │                          │ What to repeat                   │
│ ▸ Consistency      23 / 25       │                          │ [______________________________] │
│ ▸ Habits           10 / 25       │                          │ One change for next time         │
│                                  │                          │ [______________________________] │
│ Micro-wins                       │                          │ Reflecting counts towards your   │
│ ✓ Fewer penalties                │                          │ Habits score.                    │
│ Your next focus                  │                          └──────────────────────────────────┘
│ Cut down 3-putts                 │
│ Stats  Putts 33 · Pen 1 · FW 7   │
│ Reflection          [ + Add ]    │
└──────────────────────────────────┘
```

- **Pillar labels:** each pillar row shows its number and name, never only its colour.
- **Edit:** opens the Log round form, prefilled. **Delete** is at the bottom of that form,
  with a confirmation.
- **Desktop:** the round list and the detail sit side by side, and the detail has its own
  URL.

## Practice

```
Session list                          Log session
┌──────────────────────────────────┐ ┌──────────────────────────────────┐
│ Practice                     (E) │ │ ✕  Log practice           [Save] │
│ Practice counts towards your     │ │ Date       [ 15 Oct 2026     ▾ ] │
│ Habits score.                    │ │ Type       [ Putting         ▾ ] │
│                                  │ │ Duration   [ 45 ] min            │
│ 12 May · Putting       45 min  › │ │ Venue      [___________________] │
│ 9 May  · Range         60 min  › │ │ Goal       [___________________] │
│ 2 May  · Lesson        30 min  › │ │ Tags       (lag putting ✕) [+]   │
│                                  │ │ Notes      [___________________] │
├──────────────────────────────────┤ └──────────────────────────────────┘
│ Home Rounds  (+)  Practice Progr.│
└──────────────────────────────────┘
```

- **Unknown session types:** a type the app doesn't know (an open enum, #134) is shown as
  "Practice".
- **Empty state:** "No practice logged yet." `[ Log practice ]`

## Progress

```
Timeline                              Baseline                              Monthly review
┌──────────────────────────────────┐ ┌──────────────────────────────────┐ ┌──────────────────────────────────┐
│ Progress                     (E) │ │ ‹ Progress     Baseline          │ │ ‹ Progress     April 2026        │
│ [Timeline][Monthly][Baseline]    │ │ Based on your last 5 rounds      │ │ Rounds 6 · Avg fitness 71        │
│ [ Last 3 months             ▾ ]  │ │ Confidence ● Medium · 67% stats  │ │ Highlights                       │
│ Fitness score                    │ │ Score        91.4  ± 3.2         │ │ • Fewer blow-up holes than March │
│ 80 ┤        ▮     ▮              │ │ Putts        32.6                │ │ • 3 reflections logged           │
│ 70 ┤  ▮  ▮     ▮     ▮           │ │ Penalties     1.4                │ │ Focus for May                    │
│ 60 ┤▮                            │ │ Blow-ups      2.0                │ │ Damage control on par 5s         │
│    └──────────────────           │ │ Fairways      6.8                │ │                                  │
│ "Up 9 points over 3 months:      │ │ Greens        5.2                │ │ [ Generate May review ]          │
│ best round 3 May (77)."          │ │                                  │ │                                  │
│ [ Show as table ]                │ └──────────────────────────────────┘ └──────────────────────────────────┘
└──────────────────────────────────┘
```

- **Accessible charts:** every chart has a one-sentence summary, read by screen readers,
  and a **Show as table** toggle.
- **Low-data state:** "Log 3 more rounds to see a trend."
- **Desktop:** the three views are tabs, and the chart and its table can sit side by side.

## Profile

```
Profile                               Delete account (confirmation)
┌──────────────────────────────────┐ ┌──────────────────────────────────┐
│ Profile                          │ │ ‹  Delete account                │
│ Erwin · erwin@example.com        │ │ This permanently deletes your    │
│ Handicap 18.4 · Typical 18: 92 › │ │ account and all your rounds,     │
│                                  │ │ scores, reflections and practice │
│ Preferences                      │ │ sessions. It can't be undone.    │
│ Units              Metric      › │ │                                  │
│ Tracking mode      Balanced    › │ │ Password                         │
│ Goal focus  Putting, Penalties › │ │ [______________________________] │
│                                  │ │                                  │
│ Account                          │ │ [  Delete my account  ] (red)    │
│ Change password                › │ │ [        Cancel        ]         │
│ Sign out                         │ └──────────────────────────────────┘
│ Delete account                 › │
│ About · Privacy policy         › │
└──────────────────────────────────┘
```

- **Delete account** is styled as destructive and always asks for the password. It's
  required for App Store and Google Play approval (#84).
- **Sign out** needs no confirmation.

## Junior belts (planned, M5)

The design, and every other screen: [`../junior-belts.md`](../junior-belts.md) → "Screens".

```
Belts (junior)                       Take the exam (coach)                New belt (junior, once)
┌──────────────────────────────────┐ ┌──────────────────────────────────┐ ┌──────────────────────────────────┐
│ ‹ Profile      Belts             │ │ ‹ Sanne      Blue belt exam      │ │                                  │
│                                  │ │ Sat 21 Nov · Driving range       │ │                                  │
│       ┌────────────────┐         │ │ 4 of 6 shown                     │ │         Well done, Sanne!        │
│       │   GREEN BELT   │         │ │ Putting                          │ │                                  │
│       └────────────────┘         │ │ Hole 3 of 5 putts from 2 m       │ │       ┌────────────────┐         │
│   Since 14 Sep 2026 · Coach Mark │ │   [ ✓ Shown ]  [ Not yet ]       │ │       │   BLUE BELT    │         │
│                                  │ │   Note [_____________________]   │ │       └────────────────┘         │
│ Next: blue belt exam             │ │ Bunker                           │ │                                  │
│ Sat 21 Nov · 10:00               │ │ 3 of 5 bunker shots on green     │ │   You showed all 6 skills on     │
│ Driving range, Hilversum         │ │   [ Shown ]  [ • Not yet ]       │ │   21 November 2026.              │
│ What you'll show                 │ │   Note [Open the face more___]   │ │                                  │
│ • Hole 3 of 5 putts from 2 m     │ │ Rules and etiquette              │ │   "Lovely calm putting today.    │
│ • 3 of 5 bunker shots on green   │ │ Take a correct drop              │ │    On to brown!" · Coach Mark    │
│ • Take a correct drop            │ │   [ ✓ Shown ]  [ Not yet ]       │ │                                  │
│ • ... 3 more                   › │ │ ...                              │ │                                  │
│                                  │ │                                  │ │ [       See your belts        ]  │
│ Your belts                       │ │ Closing word                     │ │                                  │
│ Green   14 Sep 2026  Exam    ›   │ │ [______________________________] │ │                                  │
│ Orange  20 Jun 2026  Exam    ›   │ │ [         Finish exam          ] │ │                                  │
│ Yellow   2 May 2026  Exam    ›   │ │                                  │ │                                  │
│ White   15 Mar 2026  Start       │ │                                  │ │                                  │
└──────────────────────────────────┘ └──────────────────────────────────┘ └──────────────────────────────────┘
```

- **A belt is never colour alone:** the badge has the belt's colour and its name, and a
  white belt has an outline.
- **Shown / Not yet** save as the coach taps them, so a dropped connection on the range
  loses nothing. *Finish exam* asks for confirmation when a skill has no result yet.
- **Not yet** results lead with what was shown: "You showed 4 of 6 skills. Work on: …"
- **New belt** shows once, the next time the junior opens the app after a passed exam.
- **Desktop:** the Belts screen puts the belt and the coming exam side by side; the coach
  takes exams on a phone, where the web app works too.

## States, once for all screens

```
Loading                               Error                                 Offline (v1 is online-only)
┌──────────────────────────────────┐ ┌──────────────────────────────────┐ ┌──────────────────────────────────┐
│ Rounds                           │ │ Rounds                           │ │ ⚠ You're offline. Changes can't  │
│ ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░   │ │                                  │ │   be saved until you reconnect.  │
│ ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░   │ │  Couldn't load your rounds.      │ │ Rounds                           │
│ ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░   │ │  Check your connection and try   │ │ 3 May · Hilversum          88  72│
│ ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░   │ │  again.        [ Try again ]     │ │ ...                              │
└──────────────────────────────────┘ └──────────────────────────────────┘ └──────────────────────────────────┘
```

- **Skeletons, not spinners,** for content screens.
- **Error messages** are plain language with a retry. Debug builds also show the
  request's `traceId`.
- **Offline:** a banner, with writes disabled, until the connection returns.
