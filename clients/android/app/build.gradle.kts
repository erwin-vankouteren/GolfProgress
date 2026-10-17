// The GolfProgress Android app (#77). AGP 9 compiles Kotlin itself (built-in Kotlin), so
// the kotlin-android plugin isn't applied; the Compose compiler plugin still is.

plugins {
    alias(libs.plugins.android.application)
    alias(libs.plugins.kotlin.compose)
    alias(libs.plugins.ksp)
    alias(libs.plugins.hilt)
    alias(libs.plugins.ktlint)
}

android {
    namespace = "com.golfprogress.app"
    // Current AndroidX libraries require compiling against API 37.
    compileSdk = 37

    defaultConfig {
        applicationId = "com.golfprogress.app"
        minSdk = 26
        targetSdk = 37
        versionCode = 1
        versionName = "1.0.0"

        // The API the app talks to. 10.0.2.2 is the development machine as seen from the
        // Android emulator, where the API runs locally; release builds override this.
        buildConfigField("String", "API_BASE_URL", "\"http://10.0.2.2:5068\"")
    }

    buildTypes {
        release {
            isMinifyEnabled = false
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    buildFeatures {
        compose = true
        buildConfig = true
    }

    testOptions {
        // Compose UI tests run on the JVM with Robolectric: no emulator needed in CI.
        unitTests.isIncludeAndroidResources = true
    }
}

ktlint {
    filter {
        // Generated from design/tokens.json; its formatting follows the generator.
        exclude { it.file.path.contains("ui/theme/DesignTokens.kt") }
    }
}

dependencies {
    implementation(project(":api-client"))

    implementation(platform(libs.compose.bom))
    implementation(libs.compose.ui)
    implementation(libs.compose.ui.tooling.preview)
    implementation(libs.compose.material3)
    implementation(libs.compose.material.icons.core)
    implementation(libs.activity.compose)
    implementation(libs.navigation.compose)
    implementation(libs.lifecycle.viewmodel.compose)
    implementation(libs.lifecycle.runtime.compose)
    implementation(libs.core.ktx)
    implementation(libs.datastore.preferences)
    // Charts on Progress (#109), as docs/architecture.md chose.
    implementation(libs.vico.compose.m3)

    implementation(libs.hilt.android)
    ksp(libs.hilt.compiler)
    implementation(libs.hilt.navigation.compose)

    debugImplementation(libs.compose.ui.tooling)
    debugImplementation(libs.compose.ui.test.manifest)

    testImplementation(libs.junit4)
    testImplementation(libs.robolectric)
    testImplementation(platform(libs.compose.bom))
    testImplementation(libs.compose.ui.test.junit4)
    testImplementation(libs.kotlinx.coroutines.test)
    testImplementation(libs.okhttp.mockwebserver)
    // Hilt in Robolectric tests: the real screens with fake repositories (M2).
    testImplementation(libs.hilt.android.testing)
    kspTest(libs.hilt.compiler)
}
