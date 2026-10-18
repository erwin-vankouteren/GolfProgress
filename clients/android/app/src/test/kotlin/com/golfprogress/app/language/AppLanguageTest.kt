package com.golfprogress.app.language

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class AppLanguageTest {
    @Test
    fun theChoiceWins_OverTheDevice() {
        assertEquals(AppLanguage.Dutch, shownLanguage(AppLanguage.Dutch, listOf("en-GB")))
        assertEquals(AppLanguage.English, shownLanguage(AppLanguage.English, listOf("nl-NL")))
    }

    @Test
    fun withoutAChoice_theFirstDeviceLanguageTheAppHas_ElseEnglish() {
        assertEquals(AppLanguage.Dutch, shownLanguage(null, listOf("nl-NL", "en-US")))
        assertEquals(AppLanguage.Dutch, shownLanguage(null, listOf("de-DE", "nl-BE")))
        assertEquals(AppLanguage.English, shownLanguage(null, listOf("en-GB", "nl")))
        assertEquals(AppLanguage.English, shownLanguage(null, listOf("fr-FR")))
        assertEquals(AppLanguage.English, shownLanguage(null, emptyList()))
    }

    @Test
    fun theApisNames_AndOnesThisVersionDoesntKnow() {
        assertEquals(AppLanguage.Dutch, AppLanguage.fromApi("Dutch"))
        assertEquals(AppLanguage.English, AppLanguage.fromApi("English"))
        assertNull(AppLanguage.fromApi("German"))
        assertNull(AppLanguage.fromApi(null))
    }
}
