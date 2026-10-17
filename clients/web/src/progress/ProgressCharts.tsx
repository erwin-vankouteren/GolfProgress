import type { ReactNode } from "react";
import { useIntl } from "react-intl";
import { Bar, BarChart, CartesianGrid, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { useMediaQuery } from "../common/media";
import { messages } from "../i18n/messages";
import type { MonthPoint, TimelineRow } from "./progress";

/**
 * The Progress charts (#107), loaded only when Progress opens: Recharts is most of the
 * page's weight. They are pictures for sighted users; the summary sentence and the
 * table beside each chart carry the same information for everyone else, so the charts
 * are hidden from screen readers. Recharts' own keyboard layer is off too: a chart
 * screen readers skip mustn't take keyboard focus either (#120).
 *
 * Colours are tokens: each series sets `color` in CSS and draws with currentColor, so
 * dark mode follows the tokens without reading them here.
 */

const chartHeight = 280;

const utcDate = (iso: string) => new Date(`${iso}T00:00:00Z`);

function useReducedMotion() {
  return useMediaQuery("(prefers-reduced-motion: reduce)");
}

function ChartTooltip({ title, lines }: { title: string; lines: { key: string; text: ReactNode }[] }) {
  return (
    <div className="chart-tooltip">
      <p className="chart-tooltip-title">{title}</p>
      {lines.map((line) => (
        <p key={line.key}>{line.text}</p>
      ))}
    </div>
  );
}

type TooltipProps<T> = { active?: boolean; payload?: ReadonlyArray<{ payload?: T }> };

export function TimelineChart({ rows, strokesHoles }: { rows: TimelineRow[]; strokesHoles: 9 | 18 | null }) {
  const intl = useIntl();
  const reducedMotion = useReducedMotion();
  const shortDate = (iso: string) => intl.formatDate(utcDate(iso), { timeZone: "UTC", day: "numeric", month: "short" });
  const longDate = (iso: string) =>
    intl.formatDate(utcDate(iso), { timeZone: "UTC", day: "numeric", month: "short", year: "numeric" });
  const fitnessName = intl.formatMessage(messages.fitnessScore);
  const strokesName = strokesHoles ? intl.formatMessage(messages.strokesLine, { holes: strokesHoles }) : "";
  // One point per round, two rounds on one day included, so the x axis is the index.
  const data = rows.map((row, index) => ({ ...row, index }));

  return (
    <div className="progress-chart" aria-hidden="true">
      <ul className="chart-legend">
        <li className="chart-legend-item chart-series--fitness">
          <span className="chart-swatch chart-swatch--solid" />
          {fitnessName}
        </li>
        {strokesHoles && (
          <li className="chart-legend-item chart-series--strokes">
            <span className="chart-swatch chart-swatch--dashed" />
            {strokesName}
          </li>
        )}
      </ul>
      <ResponsiveContainer width="100%" height={chartHeight}>
        <LineChart data={data} margin={{ top: 8, right: 8, bottom: 0, left: 0 }} accessibilityLayer={false}>
          <CartesianGrid vertical={false} />
          <XAxis
            dataKey="index"
            type="number"
            domain={[0, Math.max(0, data.length - 1)]}
            allowDecimals={false}
            tickFormatter={(index: number) => (data[index] ? shortDate(data[index].playedAt) : "")}
            minTickGap={24}
          />
          <YAxis yAxisId="fitness" domain={[0, 100]} width={36} />
          {strokesHoles && (
            <YAxis
              yAxisId="strokes"
              orientation="right"
              domain={["dataMin - 3", "dataMax + 3"]}
              allowDecimals={false}
              width={36}
            />
          )}
          <Tooltip
            cursor={{ className: "chart-cursor" }}
            content={({ active, payload }: TooltipProps<(typeof data)[number]>) => {
              const row = active ? payload?.[0]?.payload : undefined;
              if (!row) return null;
              return (
                <ChartTooltip
                  title={longDate(row.playedAt)}
                  lines={[
                    {
                      key: "fitness",
                      text: `${fitnessName}: ${row.fitness ?? intl.formatMessage(messages.notScored)}`,
                    },
                    ...(row.strokes !== null ? [{ key: "strokes", text: `${strokesName}: ${row.strokes}` }] : []),
                  ]}
                />
              );
            }}
          />
          <Line
            yAxisId="fitness"
            dataKey="fitness"
            className="chart-series--fitness"
            stroke="currentColor"
            strokeWidth={2}
            // Dots are drawn outside the line's layer, so each takes the series class itself.
            dot={{ r: 4, className: "chart-dot chart-series--fitness" }}
            activeDot={{ r: 6, className: "chart-dot chart-series--fitness" }}
            connectNulls
            isAnimationActive={!reducedMotion}
          />
          {strokesHoles && (
            <Line
              yAxisId="strokes"
              dataKey="strokes"
              className="chart-series--strokes"
              stroke="currentColor"
              strokeWidth={2}
              strokeDasharray="6 4"
              // Hollow dots and a dashed line: the series differ by more than colour.
              dot={{ r: 4, className: "chart-dot chart-dot--hollow chart-series--strokes" }}
              activeDot={{ r: 6, className: "chart-dot chart-dot--hollow chart-series--strokes" }}
              connectNulls
              isAnimationActive={!reducedMotion}
            />
          )}
        </LineChart>
      </ResponsiveContainer>
    </div>
  );
}

export function MonthlyChart({ items }: { items: MonthPoint[] }) {
  const intl = useIntl();
  const reducedMotion = useReducedMotion();
  const shortMonth = (iso: string) => intl.formatDate(utcDate(iso), { timeZone: "UTC", month: "short" });
  const longMonth = (iso: string) => intl.formatDate(utcDate(iso), { timeZone: "UTC", month: "long", year: "numeric" });
  const data = items.map((item) => ({
    ...item,
    average: item.fitnessAvg === null ? null : Math.round(item.fitnessAvg),
  }));

  return (
    <div className="progress-chart" aria-hidden="true">
      <ResponsiveContainer width="100%" height={chartHeight}>
        <BarChart data={data} margin={{ top: 8, right: 8, bottom: 0, left: 0 }} accessibilityLayer={false}>
          <CartesianGrid vertical={false} />
          <XAxis dataKey="monthStart" tickFormatter={shortMonth} interval="preserveStartEnd" />
          <YAxis domain={[0, 100]} width={36} />
          <Tooltip
            cursor={{ className: "chart-cursor" }}
            content={({ active, payload }: TooltipProps<(typeof data)[number]>) => {
              const item = active ? payload?.[0]?.payload : undefined;
              if (!item) return null;
              return (
                <ChartTooltip
                  title={longMonth(item.monthStart)}
                  lines={[
                    {
                      key: "average",
                      text: `${intl.formatMessage(messages.monthAverage)}: ${
                        item.average ?? intl.formatMessage(messages.noScoredRounds)
                      }`,
                    },
                    {
                      key: "rounds",
                      text: intl.formatMessage(messages.roundsPlayed, { count: item.roundsLogged }),
                    },
                  ]}
                />
              );
            }}
          />
          <Bar
            dataKey="average"
            className="chart-series--fitness"
            fill="currentColor"
            radius={[4, 4, 0, 0]}
            isAnimationActive={!reducedMotion}
          />
        </BarChart>
      </ResponsiveContainer>
    </div>
  );
}
