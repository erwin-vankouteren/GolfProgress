package com.golfprogress.app.di

import android.content.Context
import androidx.datastore.preferences.core.PreferenceDataStoreFactory
import androidx.datastore.preferences.preferencesDataStoreFile
import com.golfprogress.api.AccessTokenProvider
import com.golfprogress.api.GolfProgressApi
import com.golfprogress.api.generated.apis.AuthApi
import com.golfprogress.api.generated.apis.ClientConfigApi
import com.golfprogress.api.unwrap
import com.golfprogress.app.BuildConfig
import com.golfprogress.app.language.AppLocales
import com.golfprogress.app.session.EncryptedRefreshTokenStore
import com.golfprogress.app.session.KeystoreTokenCipher
import com.golfprogress.app.session.RefreshTokenStore
import com.golfprogress.app.session.Session
import com.golfprogress.app.session.TokenRefresher
import com.golfprogress.app.version.MinimumVersionSource
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.android.qualifiers.ApplicationContext
import dagger.hilt.components.SingletonComponent
import okhttp3.OkHttpClient
import java.util.concurrent.TimeUnit
import javax.inject.Named
import javax.inject.Singleton

/** App-wide dependencies (#77, #71). Tests replace them by constructing classes directly. */
@Module
@InstallIn(SingletonComponent::class)
object AppModule {
    /** The API client for everything after sign-in: its tokens come from the session. */
    @Provides
    @Singleton
    fun api(
        session: Session,
        http: OkHttpClient,
    ): GolfProgressApi = GolfProgressApi(BuildConfig.API_BASE_URL, session, http)

    /**
     * Every request asks for the language the app shows, so the API's own texts
     * (micro-wins, next focus, emails) come in it too.
     *
     * Reads may take up to 90 seconds rather than OkHttp's 10: staging's serverless
     * database can take a minute to resume after idling, and the first request after
     * that (usually the refresh at launch) waits for it. Screens explain the wait.
     */
    @Provides
    @Singleton
    fun http(
        @ApplicationContext context: Context,
    ): OkHttpClient =
        OkHttpClient
            .Builder()
            .readTimeout(90, TimeUnit.SECONDS)
            .callTimeout(90, TimeUnit.SECONDS)
            .addInterceptor { chain ->
                chain.proceed(
                    chain
                        .request()
                        .newBuilder()
                        .header("Accept-Language", AppLocales.shown(context).tag)
                        .build(),
                )
            }.build()

    @Provides
    @Singleton
    fun session(tokens: TokenRefresher): Session = Session(tokens)

    /** The auth endpoints through a client of their own, without tokens: those calls are anonymous. */
    @Provides
    @Singleton
    fun tokenRefresher(
        store: RefreshTokenStore,
        http: OkHttpClient,
    ): TokenRefresher = TokenRefresher(GolfProgressApi(BuildConfig.API_BASE_URL, SignedOut, http).service<AuthApi>(), store)

    /** The refresh token, encrypted with a Keystore key, in DataStore file "session". */
    @Provides
    @Singleton
    fun refreshTokenStore(
        @ApplicationContext context: Context,
    ): RefreshTokenStore =
        EncryptedRefreshTokenStore(
            PreferenceDataStoreFactory.create { context.preferencesDataStoreFile("session") },
            KeystoreTokenCipher(),
        )

    @Provides
    @Named("appVersion")
    fun appVersion(): String = BuildConfig.VERSION_NAME

    @Provides
    fun minimumVersion(api: GolfProgressApi): MinimumVersionSource =
        MinimumVersionSource {
            api
                .service<ClientConfigApi>()
                .getClientConfig()
                .unwrap()
                .minimumVersions.android
        }
}

/** For clients that only make anonymous calls: no token, nothing to refresh. */
object SignedOut : AccessTokenProvider {
    override fun accessToken(): String? = null

    override fun refreshAccessToken(): String? = null
}
