# Design system

Design tokens shared by the web, iOS and Android clients: colours (light and dark,
including one per score pillar), typography, spacing and corner radii. Each client turns
them into its own native form, and screens use only tokens, never literal colours or
sizes, so the three apps look consistent.

**Status:** not started. `tokens.json` is defined in #73, which also documents how each
platform consumes it.

Background: [`docs/architecture.md`](../docs/architecture.md) → "Design system".
