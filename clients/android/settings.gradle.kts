rootProject.name = "golfprogress-android"

pluginManagement {
    repositories {
        gradlePluginPortal()
        mavenCentral()
        google()
    }
}

dependencyResolutionManagement {
    repositoriesMode.set(RepositoriesMode.FAIL_ON_PROJECT_REPOS)
    repositories {
        mavenCentral()
        google()
    }
}

// The app (#77) and the API client it uses, generated from api/openapi.json (#65).
include(":app")
include(":api-client")
