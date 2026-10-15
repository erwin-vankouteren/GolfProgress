package com.golfprogress.app.session

import androidx.datastore.preferences.core.PreferenceDataStoreFactory
import com.golfprogress.api.GolfProgressApi
import com.golfprogress.api.generated.apis.AuthApi
import com.golfprogress.api.generated.apis.UsersApi
import com.golfprogress.app.di.SignedOut
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.async
import kotlinx.coroutines.awaitAll
import kotlinx.coroutines.runBlocking
import okhttp3.mockwebserver.Dispatcher
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import okhttp3.mockwebserver.RecordedRequest
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test
import org.junit.rules.TemporaryFolder
import java.io.IOException
import java.util.concurrent.CopyOnWriteArrayList

/** Sign-in, session restore and the shared token refresh (#71), against a local HTTP server. */
class SessionTest {
    private lateinit var server: MockWebServer
    private val requests = CopyOnWriteArrayList<RecordedRequest>()

    /** Answers by path; set per test. */
    private var respond: (RecordedRequest) -> MockResponse = { problem(404) }

    @Before
    fun start() {
        server = MockWebServer()
        server.dispatcher =
            object : Dispatcher() {
                override fun dispatch(request: RecordedRequest): MockResponse {
                    requests += request
                    return respond(request)
                }
            }
        server.start()
    }

    @After
    fun stop() {
        // Some tests shut it down early, to simulate being offline.
        runCatching { server.shutdown() }
    }

    private fun refresher(store: RefreshTokenStore) =
        TokenRefresher(GolfProgressApi(server.url("/").toString(), SignedOut).service<AuthApi>(), store)

    private fun callsTo(path: String) = requests.count { it.path == path }

    private fun refreshTokensSentTo(path: String) =
        requests.filter { it.path == path }.map { Regex(""""refreshToken":"([^"]*)"""").find(it.body.readUtf8())?.groupValues?.get(1) }

    // TokenRefresher

    @Test
    fun `concurrent refreshes share one call`() =
        runBlocking {
            val store = FakeStore("refresh-1")
            respond = {
                Thread.sleep(100)
                auth(token = "access-2", refreshToken = "refresh-2")
            }
            val tokens = refresher(store)

            val results = (1..5).map { async(Dispatchers.IO) { tokens.refresh()?.token } }.awaitAll()

            assertEquals(List(5) { "access-2" }, results)
            assertEquals(1, callsTo("/auth/refresh"))
            assertEquals("refresh-2", store.load())
        }

    @Test
    fun `sends the stored refresh token and keeps the rotated one`() =
        runBlocking {
            val store = FakeStore("refresh-1")
            respond = { auth(token = "access-2", refreshToken = "refresh-2") }
            val tokens = refresher(store)

            tokens.refresh()

            assertEquals("access-2", tokens.accessToken())
            assertEquals(listOf("refresh-1"), refreshTokensSentTo("/auth/refresh"))
            assertEquals("refresh-2", store.load())
        }

    @Test
    fun `a rejected refresh token ends the session`() =
        runBlocking {
            val store = FakeStore("revoked")
            respond = { problem(401) }

            assertNull(refresher(store).refresh())
            assertNull(store.load())
        }

    @Test
    fun `an unreachable API keeps the session`() =
        runBlocking {
            val store = FakeStore("refresh-1")
            val tokens = refresher(store)
            server.shutdown()

            val failed = runCatching { tokens.refresh() }

            assertTrue(failed.exceptionOrNull() is IOException)
            assertEquals("refresh-1", store.load())
        }

    @Test
    fun `without a stored token there is nothing to refresh`() =
        runBlocking {
            assertNull(refresher(FakeStore()).refresh())
            assertEquals(0, requests.size)
        }

    // Session

    @Test
    fun `restores from the stored refresh token`() =
        runBlocking {
            respond = { auth() }
            val session = Session(refresher(FakeStore("refresh-1")))

            session.restore()

            assertEquals(SessionState.SignedIn(USER_ID), session.state.value)
            assertEquals("access-1", session.accessToken())
        }

    @Test
    fun `with nothing stored it is signed out`() =
        runBlocking {
            val session = Session(refresher(FakeStore()))

            session.restore()

            assertEquals(SessionState.SignedOut, session.state.value)
        }

    @Test
    fun `offline at launch keeps the stored session to try again`() =
        runBlocking {
            val store = FakeStore("refresh-1")
            val session = Session(refresher(store))
            server.shutdown()

            session.restore()

            assertEquals(SessionState.RestoreFailed, session.state.value)
            assertEquals("refresh-1", store.load())
        }

    @Test
    fun `signing in keeps the access token in memory and the refresh token in the store`() =
        runBlocking {
            val store = FakeStore()
            respond = { auth(token = "access-1", refreshToken = "refresh-1") }
            val session = Session(refresher(store))

            session.signIn("erwin@example.test", "correct horse")

            assertEquals(SessionState.SignedIn(USER_ID), session.state.value)
            assertEquals("access-1", session.accessToken())
            assertEquals("refresh-1", store.load())
            val body = requests.single().body.readUtf8()
            assertTrue(body, body.contains("\"email\":\"erwin@example.test\""))
        }

    @Test
    fun `signing out revokes and forgets the session`() =
        runBlocking {
            val store = FakeStore()
            respond = { if (it.path == "/auth/logout") MockResponse().setResponseCode(204) else auth(refreshToken = "refresh-1") }
            val session = Session(refresher(store))
            session.signIn("erwin@example.test", "correct horse")

            session.signOut()

            assertEquals(SessionState.SignedOut, session.state.value)
            assertNull(session.accessToken())
            assertNull(store.load())
            assertEquals(listOf("refresh-1"), refreshTokensSentTo("/auth/logout"))
        }

    @Test
    fun `when the session can no longer be refreshed the app signs out`() =
        runBlocking {
            respond = { if (it.path == "/auth/login") auth() else problem(401) }
            val session = Session(refresher(FakeStore()))
            session.signIn("erwin@example.test", "correct horse")
            val api = GolfProgressApi(server.url("/").toString(), session)

            val response = api.service<UsersApi>().getMe()

            assertEquals(401, response.code())
            assertEquals(SessionState.SignedOut, session.state.value)
            assertEquals(1, callsTo("/auth/refresh"))
        }

    @Test
    fun `a throttled refresh keeps the session`() =
        runBlocking {
            respond = {
                when (it.path) {
                    "/auth/login" -> auth()
                    "/auth/refresh" -> problem(429).setHeader("Retry-After", "30")
                    else -> problem(401)
                }
            }
            val store = FakeStore()
            val session = Session(refresher(store))
            session.signIn("erwin@example.test", "correct horse")

            GolfProgressApi(server.url("/").toString(), session).service<UsersApi>().getMe()

            assertEquals(SessionState.SignedIn(USER_ID), session.state.value)
            assertEquals("refresh-1", store.load())
        }

    // Fixtures

    private class FakeStore(
        @Volatile private var token: String? = null,
    ) : RefreshTokenStore {
        override suspend fun load() = token

        override suspend fun save(token: String) {
            this.token = token
        }

        override suspend fun clear() {
            token = null
        }
    }

    private companion object {
        const val USER_ID = "3f2b8c1e-5d4a-4b6f-9e2d-1a7c8b9d0e1f"

        fun auth(
            token: String = "access-1",
            refreshToken: String = "refresh-1",
        ) = MockResponse()
            .setHeader("Content-Type", "application/json")
            .setBody("""{"userId":"$USER_ID","token":"$token","refreshToken":"$refreshToken","expiresAt":"2026-10-15T10:00:00Z"}""")

        fun problem(code: Int) =
            MockResponse()
                .setResponseCode(code)
                .setHeader("Content-Type", "application/problem+json")
                .setBody("""{"status":$code}""")
    }
}

/** The refresh token at rest (#71): encrypted, and dropped if it can't be decrypted. */
class EncryptedRefreshTokenStoreTest {
    @get:Rule
    val folder = TemporaryFolder()

    /** Stands in for the Keystore cipher, which Robolectric doesn't provide. */
    private class XorCipher(
        private val key: Byte = 0x5A,
    ) : TokenCipher {
        override fun encrypt(plaintext: ByteArray) = ByteArray(plaintext.size) { (plaintext[it].toInt() xor key.toInt()).toByte() }

        override fun decrypt(ciphertext: ByteArray): ByteArray {
            if (ciphertext.isEmpty()) throw java.security.GeneralSecurityException("empty")
            return encrypt(ciphertext)
        }
    }

    private fun store(cipher: TokenCipher = XorCipher()): Pair<EncryptedRefreshTokenStore, java.io.File> {
        val file = folder.newFolder().resolve("session.preferences_pb")
        return EncryptedRefreshTokenStore(PreferenceDataStoreFactory.create { file }, cipher) to file
    }

    @Test
    fun `saves, reads and clears the token`() =
        runBlocking {
            val (store, _) = store()

            assertNull(store.load())
            store.save("refresh-1")
            assertEquals("refresh-1", store.load())
            store.clear()
            assertNull(store.load())
        }

    @Test
    fun `never writes the token in plain text`() =
        runBlocking {
            val (store, file) = store()

            store.save("refresh-token-in-plain-text")

            assertFalse(String(file.readBytes(), Charsets.ISO_8859_1).contains("refresh-token-in-plain-text"))
        }

    @Test
    fun `a token that can't be decrypted is dropped`() =
        runBlocking {
            val (store, _) =
                store(
                    object : TokenCipher {
                        override fun encrypt(plaintext: ByteArray) = ByteArray(0)

                        override fun decrypt(ciphertext: ByteArray): ByteArray = throw javax.crypto.AEADBadTagException()
                    },
                )
            store.save("refresh-1")

            assertNull(store.load())
            assertNull(store.load())
        }
}
