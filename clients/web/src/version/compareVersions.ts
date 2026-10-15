/**
 * Compares two major.minor.patch versions numerically: negative when `a` is older,
 * zero when equal, positive when newer. "1.10.0" is newer than "1.9.0".
 * Unparseable parts count as 0, so a malformed version never blocks the app.
 */
export function compareVersions(a: string, b: string): number {
  const parts = (v: string) => v.split(".").map((p) => Number.parseInt(p, 10) || 0);
  const [x, y] = [parts(a), parts(b)];

  for (let i = 0; i < Math.max(x.length, y.length); i++) {
    const diff = (x[i] ?? 0) - (y[i] ?? 0);
    if (diff !== 0) return diff;
  }
  return 0;
}
