package com.golfprogress.app.profile

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.golfprogress.api.ApiProblem
import com.golfprogress.app.data.UserRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import java.time.ZoneId
import javax.inject.Inject

data class OnboardingState(
    /** 0: units, 1: tracking mode, 2: goal focus. */
    val step: Int = 0,
    val preferences: Preferences = Preferences(),
    val saving: Boolean = false,
    val problem: AccountProblem? = null,
    val done: Boolean = false,
) {
    val last: Boolean get() = step == STEPS - 1

    companion object {
        const val STEPS = 3
    }
}

/**
 * Onboarding after Create account (#113, docs/clients/wireframes.md → "Onboarding"):
 * units, tracking mode and up to three goal focus areas, saved together with PATCH /me,
 * with the device's time zone.
 */
@HiltViewModel
class OnboardingViewModel
    @Inject
    constructor(
        private val user: UserRepository,
    ) : ViewModel() {
        private val mutableState = MutableStateFlow(OnboardingState())
        val state: StateFlow<OnboardingState> = mutableState.asStateFlow()

        fun setPreferences(preferences: Preferences) = mutableState.update { it.copy(preferences = preferences) }

        fun toggleGoal(area: GoalArea) = setPreferences(state.value.preferences.let { it.copy(goalFocus = toggleGoal(it.goalFocus, area)) })

        fun back() = mutableState.update { it.copy(step = maxOf(0, it.step - 1)) }

        /** Next, or on the last step, save. */
        fun next() {
            val current = state.value
            if (!current.last) {
                mutableState.update { it.copy(step = it.step + 1) }
                return
            }
            if (current.saving) return
            mutableState.update { it.copy(saving = true, problem = null) }
            viewModelScope.launch {
                val changes = current.preferences.toChanges()
                try {
                    try {
                        user.update(changes.copy(timezone = ZoneId.systemDefault().id))
                    } catch (e: ApiProblem) {
                        // A time zone the API doesn't know shouldn't block onboarding.
                        if (e.status == 400 && e.messagesFor("timezone").isNotEmpty()) user.update(changes) else throw e
                    }
                    mutableState.update { it.copy(saving = false, done = true) }
                } catch (e: CancellationException) {
                    throw e
                } catch (e: Exception) {
                    mutableState.update {
                        it.copy(saving = false, problem = if (e is ApiProblem) AccountProblem.Failed else AccountProblem.CantConnect)
                    }
                }
            }
        }
    }
