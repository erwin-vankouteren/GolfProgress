package com.golfprogress.app.di

import com.golfprogress.app.data.ApiCoachingRepository
import com.golfprogress.app.data.ApiDashboardRepository
import com.golfprogress.app.data.ApiFeedbackRepository
import com.golfprogress.app.data.ApiGoalsRepository
import com.golfprogress.app.data.ApiPracticeRepository
import com.golfprogress.app.data.ApiProgressRepository
import com.golfprogress.app.data.ApiRoundsRepository
import com.golfprogress.app.data.ApiStudentRepository
import com.golfprogress.app.data.ApiUserRepository
import com.golfprogress.app.data.CoachingRepository
import com.golfprogress.app.data.DashboardRepository
import com.golfprogress.app.data.FeedbackRepository
import com.golfprogress.app.data.GoalsRepository
import com.golfprogress.app.data.PracticeRepository
import com.golfprogress.app.data.ProgressRepository
import com.golfprogress.app.data.RoundsRepository
import com.golfprogress.app.data.StudentRepository
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

    @Binds
    abstract fun coaching(repository: ApiCoachingRepository): CoachingRepository

    @Binds
    abstract fun feedback(repository: ApiFeedbackRepository): FeedbackRepository

    @Binds
    abstract fun goals(repository: ApiGoalsRepository): GoalsRepository

    @Binds
    abstract fun students(repository: ApiStudentRepository): StudentRepository
}
