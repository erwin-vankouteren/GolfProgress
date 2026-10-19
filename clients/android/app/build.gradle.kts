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
        // The release workflow (#118) passes -PversionCode=<run number>: Play needs it to grow
        // with every upload. Local builds stay at 1.
        versionCode = (findProperty("versionCode") as String?)?.toInt() ?: 1
        versionName = (findProperty("versionName") as String?) ?: "1.0.0"

        // The API the app talks to. 10.0.2.2 is the development machine as seen from the
        // Android emulator, where the API runs locally; release builds override this.
        buildConfigField("String", "API_BASE_URL", "\"http://10.0.2.2:5068\"")
        // Profile → About links (#113); each is hidden while empty. None exist before M3.
        buildConfigField("String", "PRIVACY_POLICY_URL", "\"\"")
        buildConfigField("String", "TERMS_URL", "\"\"")

        // Plain HTTP only to reach a local API from the emulator; see the release build type.
        manifestPlaceholders["usesCleartextTraffic"] = "true"
    }

    // The upload key (#118), from the environment so it never lives in the repository. Without
    // it a release build is unsigned: fine for checking R8 locally, refused by Play.
    val keystore = System.getenv("ANDROID_KEYSTORE_PATH")
    if (keystore != null) {
        signingConfigs.create("upload") {
            storeFile = file(keystore)
            storePassword = System.getenv("ANDROID_KEYSTORE_PASSWORD")
            keyAlias = System.getenv("ANDROID_KEY_ALIAS")
            keyPassword = System.getenv("ANDROID_KEY_PASSWORD")
        }
    }

    buildTypes {
        release {
            // R8 shrinks and obfuscates; the libraries ship their own keep rules, and
            // proguard-rules.pro adds what they can't know about.
            isMinifyEnabled = true
            isShrinkResources = true
            proguardFiles(getDefaultProguardFile("proguard-android-optimize.txt"), "proguard-rules.pro")
            signingConfigs.findByName("upload")?.let { signingConfig = it }

            // The API and the legal pages for this build, e.g. -PapiBaseUrl=https://api.staging…
            // The release workflow always passes them; locally the emulator address remains.
            val apiBaseUrl = findProperty("apiBaseUrl") as String?
            buildConfigField("String", "API_BASE_URL", "\"${apiBaseUrl ?: "http://10.0.2.2:5068"}\"")
            buildConfigField("String", "PRIVACY_POLICY_URL", "\"${findProperty("privacyPolicyUrl") ?: ""}\"")
            buildConfigField("String", "TERMS_URL", "\"${findProperty("termsUrl") ?: ""}\"")

            // HTTPS only. -PallowCleartext=true exists solely to try a minified build against a
            // local API (docs/clients/android-release.md); the release workflow never sets it.
            manifestPlaceholders["usesCleartextTraffic"] = (findProperty("allowCleartext") == "true").toString()
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
