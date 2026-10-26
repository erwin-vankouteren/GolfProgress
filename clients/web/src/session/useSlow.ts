import { useEffect, useState } from "react";

/** How long a wait lasts before the screen explains it. */
export const slowAfterMs = 5_000;

/**
 * True once `waiting` has lasted `afterMs`, so a screen can say why it's taking long
 * (staging's database resuming after idling, say) instead of looking stuck.
 */
export function useSlow(waiting: boolean, afterMs = slowAfterMs): boolean {
  const [slow, setSlow] = useState(false);

  useEffect(() => {
    if (!waiting) return;
    const timer = setTimeout(() => setSlow(true), afterMs);
    return () => {
      clearTimeout(timer);
      setSlow(false);
    };
  }, [waiting, afterMs]);

  return waiting && slow;
}
