package com.golfprogress.app.profile

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.golfprogress.api.ApiProblem
import com.golfprogress.api.generated.models.UserResponse
import com.golfprogress.app.data.UserRepository
import com.golfprogress.app.ui.common.Load
import com.golfprogress.app.ui.common.load
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

/** Why a save or an account action failed, when no field says it. */
sealed interface AccountProblem {
    data object Failed : AccountProblem

    data object CantConnect : AccountProblem

    data class TooManyAttempts(
        val seconds: Int?,
    ) : AccountProblem
}

data class ProfileState(
    val me: Load<UserResponse> = Load.Loading,
    // Your details
    val details: ProfileValues = ProfileValues(),
    val detailsSubmitted: Boolean = false,
    val detailsApiErrors: Map<ProfileField, String> = emptyMap(),
    val detailsSaving: Boolean = false,
    val detailsSaved: Boolean = false,
    val detailsProblem: AccountProblem? = null,
    // Preferences
    val preferences: Preferences = Preferences(),
    val preferencesSaving: Boolean = false,
    val preferencesSaved: Boolean = false,
    val preferencesProblem: AccountProblem? = null,
    // Change password
    val currentPassword: String = "",
    val newPassword: String = "",
    val passwordSubmitted: Boolean = false,
    val currentPasswordApiError: String? = null,
    val newPasswordApiError: String? = null,
    val passwordSaving: Boolean = false,
    val passwordChanged: Boolean = false,
    val passwordProblem: AccountProblem? = null,
    // Delete account
    val confirmDelete: Boolean = false,
    val deletePassword: String = "",
    /** The API's message, e.g. a wrong password. */
    val deleteError: String? = null,
    val deletePasswordMissing: Boolean = false,
    val deleteProblem: AccountProblem? = null,
    val deleting: Boolean = false,
    val deleted: Boolean = false,
) {
    val detailsErrors: Map<ProfileField, ProfileError> get() = validateProfile(details)

    val newPasswordError: PasswordError?
        get() = checkNewPassword(newPassword, currentPassword, (me as? Load.Loaded)?.value?.email)
}

/**
 * Profile (#113, docs/clients/ux-design.md → "Profile"): your details, preferences,
 * change password and delete account. Saving replaces the profile everywhere it's read,
 * so a new tracking mode changes Log round's fields at once.
 */
@HiltViewModel
class ProfileViewModel
    @Inject
    constructor(
        private val user: UserRepository,
    ) : ViewModel() {
        private val mutableState = MutableStateFlow(ProfileState())
        val state: StateFlow<ProfileState> = mutableState.asStateFlow()

        init {
            loadProfile()
        }

        fun retry() {
            mutableState.update { it.copy(me = Load.Loading) }
            loadProfile()
        }

        private fun loadProfile() {
            viewModelScope.launch {
                val me = load { user.me() }
                mutableState.update {
                    val loaded = (me as? Load.Loaded)?.value
                    it.copy(
                        me = me,
                        details = loaded?.let(::profileValuesOf) ?: it.details,
                        preferences = loaded?.let(::preferencesOf) ?: it.preferences,
                    )
                }
            }
        }

        // ------------------------------------------------------------ details

        fun setDetails(values: ProfileValues) =
            mutableState.update { it.copy(details = values, detailsSaved = false, detailsApiErrors = emptyMap()) }

        fun saveDetails() {
            val current = state.value
            if (current.detailsSaving) return
            mutableState.update { it.copy(detailsSubmitted = true, detailsProblem = null, detailsSaved = false) }
            if (current.detailsErrors.isNotEmpty()) return
            mutableState.update { it.copy(detailsSaving = true) }
            viewModelScope.launch {
                try {
                    val me = user.update(current.details.toChanges())
                    mutableState.update { it.copy(me = Load.Loaded(me), detailsSaving = false, detailsSaved = true) }
                } catch (e: CancellationException) {
                    throw e
                } catch (e: Exception) {
                    val fields =
                        if (e is ApiProblem && e.status == 400) {
                            ProfileField.entries.mapNotNull { f -> e.messagesFor(f.apiName).firstOrNull()?.let { f to it } }.toMap()
                        } else {
                            emptyMap()
                        }
                    mutableState.update {
                        it.copy(
                            detailsSaving = false,
                            detailsApiErrors = fields,
                            detailsProblem = if (fields.isEmpty()) problemOf(e) else null,
                        )
                    }
                }
            }
        }

        // ------------------------------------------------------------ preferences

        fun setPreferences(preferences: Preferences) = mutableState.update { it.copy(preferences = preferences, preferencesSaved = false) }

        fun toggleGoal(area: GoalArea) = setPreferences(state.value.preferences.let { it.copy(goalFocus = toggleGoal(it.goalFocus, area)) })

        fun savePreferences() {
            val current = state.value
            if (current.preferencesSaving) return
            mutableState.update { it.copy(preferencesSaving = true, preferencesProblem = null, preferencesSaved = false) }
            viewModelScope.launch {
                try {
                    val me = user.update(current.preferences.toChanges())
                    mutableState.update { it.copy(me = Load.Loaded(me), preferencesSaving = false, preferencesSaved = true) }
                } catch (e: CancellationException) {
                    throw e
                } catch (e: Exception) {
                    mutableState.update { it.copy(preferencesSaving = false, preferencesProblem = problemOf(e)) }
                }
            }
        }

        // ------------------------------------------------------------ password

        fun setCurrentPassword(text: String) =
            mutableState.update { it.copy(currentPassword = text, currentPasswordApiError = null, passwordChanged = false) }

        fun setNewPassword(text: String) =
            mutableState.update { it.copy(newPassword = text, newPasswordApiError = null, passwordChanged = false) }

        fun changePassword() {
            val current = state.value
            if (current.passwordSaving) return
            mutableState.update { it.copy(passwordSubmitted = true, passwordProblem = null, passwordChanged = false) }
            if (current.currentPassword.isEmpty() || current.newPasswordError != null) return
            mutableState.update { it.copy(passwordSaving = true) }
            viewModelScope.launch {
                try {
                    user.changePassword(current.currentPassword, current.newPassword)
                    mutableState.update {
                        it.copy(
                            passwordSaving = false,
                            passwordChanged = true,
                            currentPassword = "",
                            newPassword = "",
                            passwordSubmitted = false,
                        )
                    }
                } catch (e: CancellationException) {
                    throw e
                } catch (e: Exception) {
                    // A wrong current password is a 400 on its field: the user stays signed in.
                    val currentError = (e as? ApiProblem)?.takeIf { it.status == 400 }?.messagesFor("currentPassword")?.firstOrNull()
                    val newError = (e as? ApiProblem)?.takeIf { it.status == 400 }?.messagesFor("newPassword")?.firstOrNull()
                    mutableState.update {
                        it.copy(
                            passwordSaving = false,
                            currentPasswordApiError = currentError,
                            newPasswordApiError = newError,
                            passwordProblem = if (currentError == null && newError == null) problemOf(e) else null,
                        )
                    }
                }
            }
        }

        // ------------------------------------------------------------ delete account

        fun askToDelete() =
            mutableState.update { it.copy(confirmDelete = true, deletePassword = "", deleteError = null, deleteProblem = null) }

        fun cancelDelete() = mutableState.update { it.copy(confirmDelete = false, deletePassword = "") }

        fun setDeletePassword(text: String) =
            mutableState.update {
                it.copy(deletePassword = text, deleteError = null, deletePasswordMissing = false)
            }

        /** Deletes, then [deleted] tells the screen to sign out, which clears the session. */
        fun confirmDelete() {
            val current = state.value
            if (current.deleting) return
            if (current.deletePassword.isEmpty()) {
                mutableState.update { it.copy(deletePasswordMissing = true) }
                return
            }
            mutableState.update { it.copy(deleting = true, deleteError = null, deleteProblem = null) }
            viewModelScope.launch {
                try {
                    user.deleteAccount(current.deletePassword)
                    mutableState.update { it.copy(deleting = false, deleted = true) }
                } catch (e: CancellationException) {
                    throw e
                } catch (e: Exception) {
                    val wrong = (e as? ApiProblem)?.takeIf { it.status == 400 }?.messagesFor("password")?.firstOrNull()
                    mutableState.update {
                        it.copy(
                            deleting = false,
                            deleteError = wrong,
                            deleteProblem =
                                if (wrong ==
                                    null
                                ) {
                                    problemOf(e)
                                } else {
                                    null
                                },
                        )
                    }
                }
            }
        }

        private fun problemOf(e: Exception): AccountProblem =
            when {
                e is ApiProblem && e.status == 429 -> AccountProblem.TooManyAttempts(e.retryAfterSeconds)
                e is ApiProblem -> AccountProblem.Failed
                else -> AccountProblem.CantConnect
            }
    }
