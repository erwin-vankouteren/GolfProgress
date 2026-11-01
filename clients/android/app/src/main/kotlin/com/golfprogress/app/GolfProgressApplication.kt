package com.golfprogress.app

import android.app.Application
import com.golfprogress.app.monitoring.CrashReporting
import dagger.hilt.android.HiltAndroidApp

@HiltAndroidApp
class GolfProgressApplication : Application() {
    override fun onCreate() {
        super.onCreate()
        CrashReporting.start(this, BuildConfig.SENTRY_DSN)
    }
}
