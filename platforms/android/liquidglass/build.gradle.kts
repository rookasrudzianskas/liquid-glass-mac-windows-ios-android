import org.jetbrains.kotlin.gradle.dsl.JvmTarget
import org.jetbrains.kotlin.gradle.dsl.KotlinVersion
import org.jetbrains.kotlin.gradle.tasks.KotlinCompile
import java.util.zip.ZipFile
import java.util.zip.ZipInputStream

plugins {
    alias(libs.plugins.android.library)
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
        // The Android Gradle Plugin 9 writes the library's compileSdk (35) into the AAR's metadata as the lowest
        // compileSdk an app using it may have; the 1.0.1 AAR says 1 and leaves that floor to its dependencies (34).
        aarMetadata {
            minCompileSdk = 1
        }
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
        getByName("test").kotlin.directories += "src/sharedTest/kotlin"
        getByName("androidTest").kotlin.directories += "src/sharedTest/kotlin"
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
val verifyReleaseAarLicense = tasks.register("verifyReleaseAarLicense") {
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

// Kotlin 2.4 compiles the library at language and API version 2.0, and the publication depends on the 2.0.21
// standard library, as 1.0.1 did: the classes keep Kotlin metadata version 2.0.0 and use no newer standard library
// API, so apps on Kotlin 2.0 can still compile against the library. Kotlin 2.4 supports language version 2.0 and
// warns that it is deprecated.
kotlin {
    explicitApi()
    coreLibrariesVersion = libs.versions.kotlinStdlib.get()
    compilerOptions {
        jvmTarget.set(JvmTarget.JVM_11)
        languageVersion.set(KotlinVersion.KOTLIN_2_0)
        apiVersion.set(KotlinVersion.KOTLIN_2_0)
    }
}

// The release classes (the AAR's classes.jar, so the Maven and JitPack publications, and the Studio's release
// build) are compiled from scratch, never incrementally. An incremental compile recompiles only the changed files
// and reads the module's other classes from its previous output, and the Compose compiler, treating those classes
// as external, emits different stability code than a full compile. The released 1.0.1 AAR (sha1 68c31ce1) came
// from such a compile of GlassMenu.kt alone, which reads GlassOverlay.$stable at run time and passes 0 instead of
// 6 in four $changed arguments, so it differs from the clean build of the tag that JitPack serves (75d2bc2d). The
// input property gives the task a build cache key of its own, so no output of an earlier incremental compile is
// restored. The Kotlin plugin sets `incremental` when it configures the task, after any configureEach here, hence
// afterEvaluate. Debug compiles stay incremental.
afterEvaluate {
    tasks.named<KotlinCompile>("compileReleaseKotlin") {
        incremental = false
        inputs.property("compiledFromScratch", true)
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

// JitPack rewrites the Gradle module metadata it serves: every file of every variant becomes
// <artifact>-<version>.<extension>, dropping any classifier, so the sources and javadoc variants
// name <artifact>-<version>.jar, a file JitPack does not serve, and variant-aware lookups of the
// sources or javadoc fail; no file name survives that rewrite. On JitPack the build therefore writes
// no .module (and the POM no published-with-gradle-metadata marker): Gradle reads the POM, which
// carries the same api and runtime dependencies, and finds -sources.jar and -javadoc.jar by their
// classifiers. The project's own Maven repository keeps its module metadata.
tasks.withType<GenerateModuleMetadata>().configureEach { enabled = !onJitPack }

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
tasks.register<Copy>("exportAar") {
    group = "distribution"
    description = "Copies the release AAR to artifacts/android."
    dependsOn("assembleRelease", verifyReleaseAarLicense)
    from(layout.buildDirectory.file("outputs/aar/liquidglass-release.aar"))
    into(repositoryRoot.resolve("artifacts/android"))
    rename { "$libraryArtifact-$libraryVersion.aar" }
}
