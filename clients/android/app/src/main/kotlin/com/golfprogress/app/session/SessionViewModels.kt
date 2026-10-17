package com.golfprogress.app.session

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.golfprogress.app.R
import com.golfprogress.app.auth.AuthField
import com.golfprogress.app.auth.AuthForm
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.launch
import javax.inject.Inject

/** Restores the session at launch (#71), and signs out. */
@HiltViewModel
class SessionViewModel
    @Inject
    constructor(
        private val session: Session,
    ) : ViewModel() {
        val state: StateFlow<SessionState> = session.state

        init {
            if (session.state.value == SessionState.Restoring) retry()
        }

        fun retry() {
            viewModelScope.launch { session.restore() }
        }

        fun signOut() {
            viewModelScope.launch { session.signOut() }
        }

        fun onboarded() = session.onboarded()
    }

/** The Sign in and Create account forms (#71). */
@HiltViewModel
class AuthViewModel
    @Inject
    constructor(
        private val session: Session,
    ) : ViewModel() {
        val signInForm = AuthForm(listOf(AuthField.Email, AuthField.Password), R.string.auth_wrong_credentials)
        val createAccountForm =
            AuthForm(listOf(AuthField.DisplayName, AuthField.Email, AuthField.Password), R.string.auth_email_taken)

        fun signIn() {
            viewModelScope.launch { signInForm.submit { email, _, password -> session.signIn(email, password) } }
        }

        fun createAccount() {
            viewModelScope.launch {
                createAccountForm.submit { email, displayName, password -> session.register(email, displayName, password) }
            }
        }
    }
