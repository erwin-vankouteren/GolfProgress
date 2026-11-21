package com.golfprogress.app.navigation

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.golfprogress.app.data.UserRepository
import com.golfprogress.app.ui.common.Load
import com.golfprogress.app.ui.common.load
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import javax.inject.Inject

/**
 * Whether the signed-in user coaches anyone (#208): then Students is in the top bar, as it's
 * in the web app's navigation once someone accepted. Checked at start and whenever the app
 * comes back into view; when it can't be checked, what was known stays.
 */
@HiltViewModel
class CoachEntryViewModel
    @Inject
    constructor(
        private val user: UserRepository,
    ) : ViewModel() {
        private val mutableHasStudents = MutableStateFlow(false)
        val hasStudents: StateFlow<Boolean> = mutableHasStudents.asStateFlow()

        fun refresh() {
            viewModelScope.launch {
                (load { user.me() } as? Load.Loaded)?.let { mutableHasStudents.value = it.value.hasStudents }
            }
        }
    }
