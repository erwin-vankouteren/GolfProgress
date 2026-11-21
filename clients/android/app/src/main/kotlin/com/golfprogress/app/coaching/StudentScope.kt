package com.golfprogress.app.coaching

import androidx.lifecycle.SavedStateHandle
import java.util.UUID

/**
 * The navigation argument naming the student whose screens these are (#208). The golfer's
 * own round, practice and goal screens show a student's for their coach when it's there:
 * read-only, through the coach's routes, as on the web (studentScope.ts).
 */
const val GOLFER_ID = "golferId"

/** The student's id on a student's screens; null on the golfer's own. */
fun SavedStateHandle.studentId(): UUID? = get<String>(GOLFER_ID)?.let(UUID::fromString)
