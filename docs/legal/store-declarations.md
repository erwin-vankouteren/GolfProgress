# Store data declarations

> **DRAFT for review (#121).** The answers both stores ask for before an app can go to
> testers outside your team. They follow from the [privacy policy](privacy-policy.md) and
> what the code collects; recheck them whenever either changes, and once crash reporting
> (#119) is configured. Items marked **Decide** need your call.

## What the apps collect

| Data | Where it comes from | Linked to the user | Purpose |
|---|---|---|---|
| Email address | Create account | Yes | Sign-in, password reset |
| Name (display name) | Create account, Profile | Yes | Shown in the app |
| Golf rounds, notes, reflections, practice sessions | Entered by the user | Yes | The service itself |
| Profile details (handicap, typical score, preferences) | Profile | Yes | The service itself |
| Activity log (round logged, score computed; ids and dates only) | The API | Yes | App functionality, security |
| Crash and error reports (app version, device model, OS, stack trace, request id) | Crash reporting (#119) | No user id is attached | Fixing crashes |

Not collected: location, contacts, photos, files, advertising ids, browsing history,
payment details, health records. No data is sold or shared with third parties; Azure and
the crash reporting provider act on our behalf, which neither store counts as sharing.
No advertising or analytics SDKs, and no tracking across apps or websites.

## Google Play: data safety form

**Data collection and security**
- Does the app collect or share any of the required user data types? **Yes, collected; not shared.**
- Is all user data encrypted in transit? **Yes** (HTTPS only; HSTS on the API).
- Do you provide a way for users to request that their data is deleted? **Yes**, in the
  app (Profile → Delete account). Play also asks for a **web link** where users can ask
  for deletion: use the web app, where signed-in users can delete their account in
  Profile, or a page on the website explaining how. **Decide** which URL.

**Data types**

| Category → type | Collected | Shared | Required or optional | Purposes |
|---|---|---|---|---|
| Personal info → Email address | Yes | No | Required | Account management, App functionality |
| Personal info → Name | Yes | No | Required | Account management, App functionality |
| App activity → Other user-generated content (rounds, notes, reflections, practice) | Yes | No | Required | App functionality |
| App activity → App interactions (activity log) | Yes | No | Required | App functionality, Fraud prevention, security and compliance |
| App info and performance → Crash logs | Yes | No | Required | App functionality (fixing crashes) |
| App info and performance → Diagnostics | Yes | No | Required | App functionality |
| Device or other IDs | **Decide** after #119: only if the crash reporter sends an installation id | No | | |
| Health and fitness → Fitness info | **Decide:** golf rounds are sports performance; if you treat them as "information about physical exercise", declare them here as well | No | Required | App functionality |

Data is processed ephemerally? **No** for all of the above: it's stored.

## App Store: App Privacy ("nutrition label")

**Do you or your third-party partners collect data from this app?** Yes.

**Tracking:** No data is used to track users.

| Data type | Linked to the user | Used for tracking | Purposes |
|---|---|---|---|
| Contact Info → Email Address | Yes | No | App Functionality |
| Contact Info → Name | Yes | No | App Functionality |
| User Content → Other User Content (rounds, notes, reflections, practice) | Yes | No | App Functionality |
| Usage Data → Product Interaction (activity log) | Yes | No | App Functionality |
| Diagnostics → Crash Data | No | No | App Functionality |
| Diagnostics → Performance Data | No | No | App Functionality |
| Health & Fitness → Fitness | **Decide**, as for Google Play | No | App Functionality |

Apple also requires in-app account deletion for apps with sign-up: done (#84,
Profile → Delete account).

## Before submitting

- [ ] The privacy policy is published, and its URL is set in both store listings and the
      apps' About screens.
- [ ] Crash reporting (#119) is configured as described: no user id, no email, no notes
      or reflections in reports.
- [ ] The **Decide** items above are settled.
