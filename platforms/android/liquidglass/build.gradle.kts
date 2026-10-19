import org.jetbrains.kotlin.gradle.dsl.JvmTarget
import java.util.zip.ZipFile
import java.util.zip.ZipInputStream

plugins {
    alias(libs.plugins.android.library)
    alias(libs.plugins.kotlin.android)
    alias(libs.plugins.kotlin.compose)
    `maven-publish`
}

val libraryGroup = "io.github.rookasrudzianskas"
val libraryArtifact = "liquid-glass-compose"
val libraryVersion = "1.0.1"

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

/**
 * The repository's LICENSE and NOTICE.md as Java resources, under META-INF/liquid-glass-compose/: they land in the
 * AAR's classes.jar (so in the Maven and JitPack publications) and in every app built with the library. The optical
 * model is ported from the MIT-licensed Glass HQ library, whose copyright and permission notice NOTICE.md carries;
 * its license asks for that notice in all copies.
 */
abstract class LicenseResources : DefaultTask() {
    @get:InputFiles
    @get:PathSensitive(PathSensitivity.NAME_ONLY)
    abstract val notices: ConfigurableFileCollection

    @get:OutputDirectory
    abstract val outputDirectory: DirectoryProperty

    @TaskAction
    fun copy() {
        val target = outputDirectory.get().asFile.resolve("META-INF/liquid-glass-compose")
        target.deleteRecursively()
        target.mkdirs()
        notices.files.forEach { it.copyTo(target.resolve(it.name), overwrite = true) }
    }
}

val licenseResources = tasks.register<LicenseResources>("licenseResources") {
    notices.from(repositoryRoot.resolve("LICENSE"), repositoryRoot.resolve("NOTICE.md"))
}

androidComponents {
    onVariants { variant ->
        variant.sources.resources?.addGeneratedSourceDirectory(licenseResources, LicenseResources::outputDirectory)
        variant.androidTest?.sources?.assets?.addGeneratedSourceDirectory(specVectorsAssets, SpecVectorsAssets::outputDirectory)
    }
}

/** Fails when the release AAR's classes.jar lacks LICENSE or NOTICE.md; the publication and exportAar depend on it. */
val verifyReleaseAarLicense by tasks.registering {
    group = "verification"
    description = "Checks that the release AAR carries LICENSE and NOTICE.md."
    dependsOn("bundleReleaseAar")
    val aar = layout.buildDirectory.file("outputs/aar/liquidglass-release.aar")
    inputs.file(aar)
    doLast {
        val file = aar.get().asFile
        val names = ZipFile(file).use { zip ->
            val classes = zip.getEntry("classes.jar") ?: throw GradleException("${file.name} has no classes.jar")
            ZipInputStream(zip.getInputStream(classes)).use { jar ->
                generateSequence { jar.nextEntry }.map { it.name }.toSet()
            }
        }
        val missing = listOf("LICENSE", "NOTICE.md").map { "META-INF/liquid-glass-compose/$it" }.filterNot(names::contains)
        if (missing.isNotEmpty()) throw GradleException("${file.name}: classes.jar lacks ${missing.joinToString()}")
    }
}

tasks.matching { it.name.startsWith("publishReleasePublication") }.configureEach { dependsOn(verifyReleaseAarLicense) }

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
    dependsOn("assembleRelease", verifyReleaseAarLicense)
    from(layout.buildDirectory.file("outputs/aar/liquidglass-release.aar"))
    into(repositoryRoot.resolve("artifacts/android"))
    rename { "$libraryArtifact-$libraryVersion.aar" }
}
