package com.golfprogress.app.di

import com.golfprogress.app.data.ApiDashboardRepository
import com.golfprogress.app.data.ApiPracticeRepository
import com.golfprogress.app.data.ApiProgressRepository
import com.golfprogress.app.data.ApiRoundsRepository
import com.golfprogress.app.data.ApiUserRepository
import com.golfprogress.app.data.DashboardRepository
import com.golfprogress.app.data.PracticeRepository
import com.golfprogress.app.data.ProgressRepository
import com.golfprogress.app.data.RoundsRepository
import com.golfprogress.app.data.UserRepository
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

    @Binds
    abstract fun rounds(repository: ApiRoundsRepository): RoundsRepository

    @Binds
    abstract fun user(repository: ApiUserRepository): UserRepository

    @Binds
    abstract fun practice(repository: ApiPracticeRepository): PracticeRepository

    @Binds
    abstract fun progress(repository: ApiProgressRepository): ProgressRepository
}
