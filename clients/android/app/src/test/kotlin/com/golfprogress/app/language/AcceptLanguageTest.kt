package com.golfprogress.app.language

import android.app.LocaleManager
import android.os.LocaleList
import com.golfprogress.app.di.AppModule
import okhttp3.Request
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.RuntimeEnvironment
import org.robolectric.annotation.Config

/** Every request asks the API for the language the app shows, so its texts match the screen. */
@RunWith(RobolectricTestRunner::class)
@Config(sdk = [35], qualifiers = "en-rGB")
class AcceptLanguageTest {
    private val server = MockWebServer()
    private val context = RuntimeEnvironment.getApplication()

    @Before
    fun start() {
        server.start()
        server.enqueue(MockResponse())
    }

    @After
    fun stop() {
        context.getSystemService(LocaleManager::class.java).applicationLocales = LocaleList.getEmptyLocaleList()
        server.shutdown()
    }

    @Test
    fun withoutAChoice_itAsksForTheDevicesLanguage() {
        assertEquals("en", acceptLanguageSent())
    }

    @Test
    fun aChoice_isWhatItAsksFor() {
        AppLocales.apply(context, AppLanguage.Dutch)

        assertEquals(AppLanguage.Dutch, AppLocales.choice(context))
        assertEquals("nl", acceptLanguageSent())
    }

    private fun acceptLanguageSent(): String? {
        AppModule
            .http(context)
            .newCall(Request.Builder().url(server.url("/me")).build())
            .execute()
            .close()
        return server.takeRequest().getHeader("Accept-Language")
    }
}
