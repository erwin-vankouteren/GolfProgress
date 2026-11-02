# Google Play store listing

The texts and settings for GolfProgress's Play Console listing (#125), in English (the
default) and Dutch. Play Console → **Grow users → Store presence → Main store listing**;
the Dutch goes in as a translation. Keep them in step with what the app does: no
planned features, no other apps by name (Play's metadata policy), and the Dutch in the
app's own terms (`clients/web/src/i18n/nl.ts`).

## Settings

| Field | Value |
|---|---|
| App name | GolfProgress |
| Price | Free. Paid features may come later as an in-app subscription (terms of use → "Price"); Play doesn't let a free app become a paid download |
| Contains ads | No |
| In-app purchases | No, until a subscription exists |
| Category | Sports |
| Tags | Golf, Sports tracker |
| Contact email | service@golfprogress.app |
| Website | https://golfprogress.app |
| Privacy policy | https://golfprogress.app/privacy |
| App icon | `design/app-icon/play-store-512.png` |
| Feature graphic | `design/store/play-feature-graphic.jpg` (1024 × 500), and `-nl.jpg` for the Dutch listing; `node design/build-store-graphics.mjs` |

## English

**Short description** (80 characters at most)

> Log a round in a minute. See your progress beyond the score, practice included.

**Full description**

> GolfProgress is the golf app for players who want to get better without turning every round into a data-entry job. Log a round in under a minute, after you've played, and see what went right, what to work on next, and how your practice adds up.
>
> A FITNESS SCORE, NOT JUST A SCORE
> Every round gets a fitness score out of 100, built from four pillars: Execution, Damage control, Consistency and Habits. Each is compared with your own recent rounds, so a tough day doesn't hide what you did well. Tap a pillar to see how it was worked out.
>
> MICRO-WINS AND ONE NEXT FOCUS
> GolfProgress picks up to three wins from every round, such as "Fewer penalties" or "Putting held steady", and gives you one clear thing to work on next time, with the reason.
>
> TRACK AS MUCH AS YOU LIKE
> Only strokes and holes are required. Choose Minimal, Balanced or Detailed, and the form asks just for those stats: putts, penalties, fairways, greens in regulation, three-putts and blow-up holes.
>
> PRACTICE COUNTS
> Log range, putting, short-game, simulator and lesson sessions with a goal and notes. Playing or practising about once a week earns points in your Habits pillar.
>
> REFLECT IN THIRTY SECONDS
> Rate your focus and mood, then note what worked and one change for next time.
>
> SEE YOUR TREND
> Charts of your fitness score and strokes over time, monthly averages, and a monthly review with highlights and a focus for the month ahead.
>
> PRIVATE BY DESIGN
> No ads and no tracking, and your data is never sold. It's stored in the EU, and you can delete your account at any time in the app.
>
> FREE DURING THE LAUNCH
> GolfProgress is free while we launch. If paid features come later, you'll hear about it in the app at least 30 days ahead, and you'll keep access to everything you've entered.
>
> In English and Dutch. Use the same account on Android and on the web at golfprogress.app.

## Nederlands

**Korte beschrijving**

> Voer een ronde in een minuut in en zie je voortgang verder dan de score.

**Volledige beschrijving**

> GolfProgress is de golfapp voor spelers die beter willen worden zonder van elke ronde een invoerklus te maken. Voer je ronde na afloop in een minuut in en zie wat goed ging, waar je nu aan werkt en wat je training oplevert.
>
> EEN FITNESSSCORE, NIET ALLEEN EEN SCORE
> Elke ronde krijgt een fitnessscore van 0 tot 100, opgebouwd uit vier onderdelen: Uitvoering, Schadebeperking, Consistentie en Gewoontes. Elk onderdeel vergelijkt je met je eigen recente rondes, zodat een lastige dag niet verbergt wat je goed deed. Tik op een onderdeel om te zien hoe het is berekend.
>
> MICRO-WINS EN ÉÉN VOLGENDE FOCUS
> GolfProgress haalt tot drie winstpunten uit elke ronde, zoals "Minder strafslagen" of "Putten stabiel", en geeft je één duidelijk punt om de volgende keer aan te werken, met de reden erbij.
>
> HOUD BIJ WAT JIJ WILT
> Alleen slagen en holes zijn verplicht. Kies Minimaal, Gemiddeld of Uitgebreid, en het formulier vraagt alleen die statistieken: putts, strafslagen, fairways, greens in regulation, drie-putts en uitschieters.
>
> TRAINING TELT MEE
> Leg sessies op de driving range, putten, korte spel, simulator en lessen vast, met een doel en notities. Ongeveer één keer per week spelen of trainen levert punten op voor je Gewoontes.
>
> REFLECTEREN IN DERTIG SECONDEN
> Geef je focus en stemming een cijfer, en noteer wat werkte en één ding om volgende keer anders te doen.
>
> ZIE JE TREND
> Grafieken van je fitnessscore en slagen door de tijd, gemiddelden per maand, en een maandoverzicht met hoogtepunten en een focus voor de komende maand.
>
> PRIVACY VOOROP
> Geen advertenties en geen tracking, en je gegevens worden nooit verkocht. Ze worden in de EU opgeslagen, en je kunt je account altijd in de app verwijderen.
>
> GRATIS TIJDENS DE LANCERING
> GolfProgress is gratis zolang we lanceren. Komen er later betaalde functies, dan hoor je dat minstens 30 dagen van tevoren in de app, en je houdt toegang tot alles wat je hebt ingevoerd.
>
> In het Nederlands en Engels. Gebruik hetzelfde account op Android en op het web via golfprogress.app.

## Screenshots

Phone screenshots in `design/store/screenshots/en` and `nl` (1080 × 1920, JPEG, as Play
refuses alpha), in this order: the dashboard, a round's score and micro-wins, Log round,
Progress (last 3 months, scrolled to the latest rounds) and Practice. They're of a release
build against staging, signed in as a demo golfer with four months of rounds, practice
and reflections, with Android's demo-mode status bar (9:41, full battery and signal).
Retake them when those screens change.
