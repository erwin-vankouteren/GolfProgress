package com.golfprogress.app.di

import com.golfprogress.api.AccessTokenProvider
import com.golfprogress.api.GolfProgressApi
import com.golfprogress.api.generated.apis.ClientConfigApi
import com.golfprogress.api.unwrap
import com.golfprogress.app.BuildConfig
import com.golfprogress.app.version.MinimumVersionSource
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import javax.inject.Named
import javax.inject.Singleton

/** App-wide dependencies (#77). Tests replace them by constructing classes directly. */
@Module
@InstallIn(SingletonComponent::class)
object AppModule {
    @Provides
    @Singleton
    fun api(): GolfProgressApi = GolfProgressApi(BuildConfig.API_BASE_URL, SignedOut)

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

/** The token provider before anyone has signed in; the session layer replaces it in #71. */
private object SignedOut : AccessTokenProvider {
    override fun accessToken(): String? = null

    override fun refreshAccessToken(): String? = null
}
