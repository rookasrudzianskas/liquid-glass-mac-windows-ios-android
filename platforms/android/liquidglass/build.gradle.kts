import org.jetbrains.kotlin.gradle.dsl.JvmTarget

plugins {
    alias(libs.plugins.android.library)
    alias(libs.plugins.kotlin.android)
    alias(libs.plugins.kotlin.compose)
    `maven-publish`
}

val libraryGroup = "io.github.rookasrudzianskas"
val libraryArtifact = "liquid-glass-compose"
val libraryVersion = "1.0.0"

/** Repository root (two levels above this Gradle project), holding spec/ and artifacts/. */
val repositoryRoot: File = rootProject.projectDir.resolve("../..").canonicalFile

android {
    namespace = "io.github.rookasrudzianskas.liquidglass"
    compileSdk = 35

    defaultConfig {
        minSdk = 24
        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
    }

    buildFeatures {
        compose = true
        buildConfig = false
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_11
        targetCompatibility = JavaVersion.VERSION_11
    }

    // The CPU reference of the shader, used by both test suites.
    sourceSets {
        getByName("test").kotlin.srcDir("src/sharedTest/kotlin")
        getByName("androidTest").kotlin.srcDir("src/sharedTest/kotlin")
    }

    testOptions {
        unitTests.all {
            it.systemProperty("liquidglass.vectors", repositoryRoot.resolve("spec/vectors.json").path)
        }
    }

    lint {
        abortOnError = true
        checkDependencies = false
        disable += setOf("GradleDependency", "NewerVersionAvailable", "AndroidGradlePluginVersion")
    }

    publishing {
        singleVariant("release") {
            withSourcesJar()
            withJavadocJar()
        }
    }
}

/** Copies spec/vectors.json into the instrumented tests' assets, where the GPU shader tests read it. */
abstract class SpecVectorsAssets : DefaultTask() {
    @get:InputFile
    @get:PathSensitive(PathSensitivity.NONE)
    abstract val vectors: RegularFileProperty

    @get:OutputDirectory
    abstract val outputDirectory: DirectoryProperty

    @TaskAction
    fun copy() {
        val target = outputDirectory.get().asFile
        target.mkdirs()
        vectors.get().asFile.copyTo(target.resolve("vectors.json"), overwrite = true)
    }
}

val specVectorsAssets = tasks.register<SpecVectorsAssets>("specVectorsAssets") {
    vectors.set(repositoryRoot.resolve("spec/vectors.json"))
}

androidComponents {
    onVariants { variant ->
        variant.androidTest?.sources?.assets?.addGeneratedSourceDirectory(specVectorsAssets, SpecVectorsAssets::outputDirectory)
    }
}

kotlin {
    explicitApi()
    compilerOptions {
        jvmTarget.set(JvmTarget.JVM_11)
    }
}

dependencies {
    api(libs.compose.ui)
    api(libs.compose.foundation)
    implementation(libs.activity.compose)

    testImplementation(libs.junit)
    testImplementation(libs.kotlinx.serialization.json)

    androidTestImplementation(libs.junit)
    androidTestImplementation(libs.androidx.test.runner)
    androidTestImplementation(libs.androidx.test.core)
    androidTestImplementation(libs.androidx.test.ext.junit)
    // Declares the empty ComponentActivity the rendering tests compose into.
    debugImplementation(libs.compose.ui.test.manifest)
}

// JitPack builds this module from the repository root (see /jitpack.yml) and
// passes the coordinates it serves under through GROUP / ARTIFACT / VERSION.
val onJitPack = System.getenv("JITPACK") == "true"

publishing {
    publications {
        register<MavenPublication>("release") {
            groupId = if (onJitPack) System.getenv("GROUP") else libraryGroup
            artifactId = if (onJitPack) System.getenv("ARTIFACT") else libraryArtifact
            version = if (onJitPack) System.getenv("VERSION") else libraryVersion
            afterEvaluate { from(components["release"]) }
            pom {
                name.set("Liquid Glass for Jetpack Compose")
                description.set(
                    "Native Liquid Glass for Jetpack Compose: refraction, Clear and Regular materials, " +
                        "rim light and elastic interaction rendered with AGSL, sharing one optical model " +
                        "with the SwiftUI and WPF ports.",
                )
                url.set("https://liquid-glass-engineering.vercel.app")
                inceptionYear.set("2026")
                licenses {
                    license {
                        name.set("MIT License")
                        url.set("https://opensource.org/licenses/MIT")
                        distribution.set("repo")
                    }
                }
                developers {
                    developer {
                        id.set("rookasrudzianskas")
                        name.set("Rokas Rudzianskas")
                        url.set("https://github.com/rookasrudzianskas")
                    }
                }
                scm {
                    url.set("https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android")
                    connection.set("scm:git:https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android.git")
                    developerConnection.set("scm:git:ssh://git@github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android.git")
                }
            }
        }
    }
    repositories {
        // Self-hosted Maven repository served by the website at /maven.
        maven {
            name = "artifacts"
            url = uri(repositoryRoot.resolve("artifacts/android/maven"))
        }
    }
}

/** Copies the release AAR next to the Maven repository for direct downloads. */
val exportAar by tasks.registering(Copy::class) {
    group = "distribution"
    description = "Copies the release AAR to artifacts/android."
    dependsOn("assembleRelease")
    from(layout.buildDirectory.file("outputs/aar/liquidglass-release.aar"))
    into(repositoryRoot.resolve("artifacts/android"))
    rename { "$libraryArtifact-$libraryVersion.aar" }
}
