// The GolfProgress API client for the Android app (#65): Retrofit interfaces and
// kotlinx.serialization models generated from api/openapi.json on every build, plus the
// hand-written token handling and error mapping in src/main/kotlin.
//
// A plain Kotlin/JVM library: it builds and tests without the Android SDK, and the app
// module (#77) depends on it.

import org.jetbrains.kotlin.gradle.tasks.KotlinCompile
import org.openapitools.generator.gradle.plugin.tasks.GenerateTask

plugins {
    alias(libs.plugins.kotlin.jvm)
    alias(libs.plugins.kotlin.serialization)
    alias(libs.plugins.openapi.generator)
}

kotlin {
    jvmToolchain(17)
}

val spec = rootProject.layout.projectDirectory.file("../../api/openapi.json")
val generatedDir = layout.buildDirectory.dir("generated/openapi")

val generateApi = tasks.named<GenerateTask>("openApiGenerate") {
    generatorName.set("kotlin")
    library.set("jvm-retrofit2")
    inputSpec.set(spec.asFile.path)
    outputDir.set(generatedDir.map { it.asFile.path })
    packageName.set("com.golfprogress.api.generated")
    apiPackage.set("com.golfprogress.api.generated.apis")
    modelPackage.set("com.golfprogress.api.generated.models")
    configOptions.set(
        mapOf(
            "serializationLibrary" to "kotlinx_serialization",
            "useCoroutines" to "true",
            "dateLibrary" to "java8",
            "omitGradleWrapper" to "true",
        ),
    )
    // The generator also writes a Gradle build, docs and a README; only sources are used.
    generateApiDocumentation.set(false)
    generateModelDocumentation.set(false)
    generateApiTests.set(false)
    generateModelTests.set(false)
}

sourceSets {
    main {
        kotlin.srcDir(generatedDir.map { it.dir("src/main/kotlin") })
    }
}

tasks.withType<KotlinCompile>().configureEach {
    dependsOn(generateApi)
}

dependencies {
    api(libs.retrofit)
    api(libs.okhttp)
    api(libs.kotlinx.serialization.json)
    implementation(libs.retrofit.converter.kotlinx.serialization)
    implementation(libs.kotlinx.coroutines.core)
    // Only for the generated infrastructure/ApiClient.kt, which must compile; the app
    // uses GolfProgressApi instead.
    implementation(libs.okhttp.logging.interceptor)
    implementation(libs.retrofit.converter.scalars)

    testImplementation(libs.junit.jupiter)
    testImplementation(libs.okhttp.mockwebserver)
    testRuntimeOnly(libs.junit.platform.launcher)
}

tasks.test {
    // KnownValuesTest reads the spec directly (#134), so a spec change must re-run it.
    inputs.file(spec)
    useJUnitPlatform()
}
