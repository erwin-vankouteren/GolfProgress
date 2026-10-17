package com.golfprogress.app.ui.common

import kotlinx.coroutines.CancellationException

/** Something a screen loads from the API: still coming, failed, or here. */
sealed interface Load<out T> {
    data object Loading : Load<Nothing>

    data class Failed(
        val error: Throwable,
    ) : Load<Nothing>

    data class Loaded<T>(
        val value: T,
    ) : Load<T>
}

/** Runs [block], turning any failure (offline, an API error) into [Load.Failed]. */
suspend fun <T> load(block: suspend () -> T): Load<T> =
    try {
        Load.Loaded(block())
    } catch (e: CancellationException) {
        throw e
    } catch (e: Exception) {
        Load.Failed(e)
    }

val <T> Load<T>.valueOrNull: T? get() = (this as? Load.Loaded<T>)?.value
