package com.golfprogress.app.di

import com.golfprogress.app.data.ApiDashboardRepository
import com.golfprogress.app.data.DashboardRepository
import dagger.Binds
import dagger.Module
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent

/** The repositories the screens read through (M2). Hilt tests replace this module with fakes. */
@Module
@InstallIn(SingletonComponent::class)
abstract class RepositoryModule {
    @Binds
    abstract fun dashboard(repository: ApiDashboardRepository): DashboardRepository
}
