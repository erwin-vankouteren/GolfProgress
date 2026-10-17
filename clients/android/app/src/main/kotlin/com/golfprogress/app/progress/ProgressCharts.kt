package com.golfprogress.app.progress

import androidx.compose.foundation.shape.CircleShape
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.unit.dp
import com.golfprogress.api.generated.models.MonthlyTimelinePointResponse
import com.golfprogress.app.ui.common.currentLocale
import com.golfprogress.app.ui.theme.LocalPalette
import com.patrykandpatrick.vico.compose.cartesian.CartesianChartHost
import com.patrykandpatrick.vico.compose.cartesian.axis.Axis
import com.patrykandpatrick.vico.compose.cartesian.axis.HorizontalAxis
import com.patrykandpatrick.vico.compose.cartesian.axis.VerticalAxis
import com.patrykandpatrick.vico.compose.cartesian.data.CartesianChartModelProducer
import com.patrykandpatrick.vico.compose.cartesian.data.CartesianLayerRangeProvider
import com.patrykandpatrick.vico.compose.cartesian.data.CartesianValueFormatter
import com.patrykandpatrick.vico.compose.cartesian.data.columnModel
import com.patrykandpatrick.vico.compose.cartesian.data.lineModel
import com.patrykandpatrick.vico.compose.cartesian.layer.ColumnCartesianLayer
import com.patrykandpatrick.vico.compose.cartesian.layer.LineCartesianLayer
import com.patrykandpatrick.vico.compose.cartesian.layer.rememberColumnCartesianLayer
import com.patrykandpatrick.vico.compose.cartesian.layer.rememberLine
import com.patrykandpatrick.vico.compose.cartesian.layer.rememberLineCartesianLayer
import com.patrykandpatrick.vico.compose.cartesian.rememberCartesianChart
import com.patrykandpatrick.vico.compose.common.Fill
import com.patrykandpatrick.vico.compose.common.ProvideVicoTheme
import com.patrykandpatrick.vico.compose.common.VicoTheme
import com.patrykandpatrick.vico.compose.common.component.rememberLineComponent
import com.patrykandpatrick.vico.compose.common.component.rememberShapeComponent
import java.time.LocalDate
import java.time.format.DateTimeFormatter
import java.time.format.FormatStyle

// The Progress charts (#109), drawn with Vico in the design tokens' colours. They are
// pictures for sighted users: TalkBack skips them, and hears the summary sentence beside
// each instead, with "Show as table" for the numbers.

/** Vico's axis lines and labels in the token colours, so dark mode follows. */
@Composable
private fun TokenChartTheme(content: @Composable () -> Unit) {
    val palette = LocalPalette.current
    val theme =
        VicoTheme(
            candlestickCartesianLayerColors =
                VicoTheme.CandlestickCartesianLayerColors(
                    palette.success,
                    palette.textSecondary,
                    palette.error,
                ),
            columnCartesianLayerColors = listOf(palette.primary, palette.info),
            lineColor = palette.border,
            textColor = palette.textSecondary,
        )
    ProvideVicoTheme(theme, content)
}

/** Fitness score per round (0–100, start axis), and strokes as a dashed line on the end axis. */
@Composable
fun TimelineChart(
    chart: TimelineChart,
    dates: List<String>,
    modifier: Modifier = Modifier,
) {
    val producer = remember { CartesianChartModelProducer() }
    LaunchedEffect(chart) {
        producer.runTransaction {
            lineModel { series(chart.fitness.map { it.first }, chart.fitness.map { it.second }) }
            if (chart.strokes.isNotEmpty()) lineModel { series(chart.strokes.map { it.first }, chart.strokes.map { it.second }) }
        }
    }
    val locale = currentLocale()
    val shortDate = remember(locale) { DateTimeFormatter.ofPattern("d MMM", locale) }
    val palette = LocalPalette.current

    TokenChartTheme {
        val fitness =
            rememberLineCartesianLayer(
                lineProvider =
                    LineCartesianLayer.LineProvider.series(
                        LineCartesianLayer.rememberLine(
                            fill = LineCartesianLayer.LineFill.single(Fill(palette.primary)),
                            pointProvider =
                                LineCartesianLayer.PointProvider.single(
                                    LineCartesianLayer.Point(rememberShapeComponent(Fill(palette.primary), CircleShape), 8.dp),
                                ),
                        ),
                    ),
                rangeProvider = remember { CartesianLayerRangeProvider.fixed(minY = 0.0, maxY = 100.0) },
                verticalAxisPosition = Axis.Position.Vertical.Start,
            )
        val strokes =
            rememberLineCartesianLayer(
                lineProvider =
                    LineCartesianLayer.LineProvider.series(
                        LineCartesianLayer.rememberLine(
                            fill = LineCartesianLayer.LineFill.single(Fill(palette.info)),
                            stroke = LineCartesianLayer.LineStroke.Dashed(),
                            pointProvider =
                                LineCartesianLayer.PointProvider.single(
                                    LineCartesianLayer.Point(
                                        rememberShapeComponent(
                                            Fill(palette.surface),
                                            CircleShape,
                                            strokeFill = Fill(palette.info),
                                            strokeThickness = 2.dp,
                                        ),
                                        8.dp,
                                    ),
                                ),
                        ),
                    ),
                verticalAxisPosition = Axis.Position.Vertical.End,
            )
        val dateFormatter =
            CartesianValueFormatter { _, x, _ ->
                dates.getOrNull(x.toInt())?.let { LocalDate.parse(it).format(shortDate) } ?: " "
            }
        val layers = if (chart.strokes.isEmpty()) arrayOf(fitness) else arrayOf(fitness, strokes)
        CartesianChartHost(
            chart =
                rememberCartesianChart(
                    *layers,
                    startAxis = VerticalAxis.rememberStart(),
                    endAxis = if (chart.strokes.isEmpty()) null else VerticalAxis.rememberEnd(),
                    bottomAxis = HorizontalAxis.rememberBottom(valueFormatter = dateFormatter),
                ),
            modelProducer = producer,
            modifier = modifier.clearAndSetSemantics {},
        )
    }
}

/** Average fitness score per month, as bars on 0–100; months without one have none. */
@Composable
fun MonthlyChart(
    items: List<MonthlyTimelinePointResponse>,
    modifier: Modifier = Modifier,
) {
    val averaged = items.mapIndexedNotNull { i, item -> item.fitnessAvg?.let { i to it } }
    val producer = remember { CartesianChartModelProducer() }
    LaunchedEffect(items) {
        producer.runTransaction { columnModel { series(averaged.map { it.first }, averaged.map { it.second }) } }
    }
    val locale = currentLocale()
    val month = remember(locale) { DateTimeFormatter.ofPattern("MMM", locale) }
    val palette = LocalPalette.current

    TokenChartTheme {
        val bars =
            rememberColumnCartesianLayer(
                columnProvider = ColumnCartesianLayer.ColumnProvider.series(rememberLineComponent(Fill(palette.primary), 16.dp)),
                rangeProvider =
                    remember {
                        CartesianLayerRangeProvider.fixed(
                            minX = 0.0,
                            maxX = (items.size - 1).toDouble(),
                            minY = 0.0,
                            maxY = 100.0,
                        )
                    },
            )
        CartesianChartHost(
            chart =
                rememberCartesianChart(
                    bars,
                    startAxis = VerticalAxis.rememberStart(),
                    bottomAxis =
                        HorizontalAxis.rememberBottom(
                            valueFormatter =
                                CartesianValueFormatter { _, x, _ ->
                                    items.getOrNull(x.toInt())?.let { LocalDate.parse(it.monthStart).format(month) } ?: " "
                                },
                        ),
                ),
            modelProducer = producer,
            modifier = modifier.clearAndSetSemantics {},
        )
    }
}

/** A month's full name, e.g. "October 2026". */
fun monthName(
    iso: String,
    locale: java.util.Locale,
): String = LocalDate.parse(iso).format(DateTimeFormatter.ofPattern("LLLL yyyy", locale))

/** A date as the rest of the app writes it. */
fun mediumDate(
    iso: String,
    locale: java.util.Locale,
): String = LocalDate.parse(iso).format(DateTimeFormatter.ofLocalizedDate(FormatStyle.MEDIUM).withLocale(locale))
