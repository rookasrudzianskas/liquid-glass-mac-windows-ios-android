import org.jetbrains.kotlin.gradle.dsl.JvmTarget
import org.jetbrains.kotlin.gradle.tasks.KotlinCompile
import java.util.Properties
import java.util.zip.ZipFile

plugins {
    alias(libs.plugins.android.application)
    alias(libs.plugins.kotlin.android)
    alias(libs.plugins.kotlin.compose)
}

/**
 * Release signing comes from platforms/android/local-signing.properties
 * (storeFile, storePassword, keyAlias, keyPassword), which is never committed.
 * Without it the release build is signed with the debug key.
 */
val signingProperties = Properties().apply {
    val file = rootProject.file("local-signing.properties")
    if (file.isFile) file.inputStream().use(::load)
}

android {
    namespace = "io.github.rookasrudzianskas.liquidglass.studio"
    compileSdk = 35

    defaultConfig {
        applicationId = "io.github.rookasrudzianskas.liquidglass.studio"
        minSdk = 24
        targetSdk = 35
        versionCode = 2
        versionName = "1.0.1"
    }

    signingConfigs {
        if (signingProperties.isNotEmpty()) {
            create("release") {
                storeFile = rootProject.file(signingProperties.getProperty("storeFile"))
                storePassword = signingProperties.getProperty("storePassword")
                keyAlias = signingProperties.getProperty("keyAlias")
                keyPassword = signingProperties.getProperty("keyPassword")
            }
        }
    }

    buildTypes {
        release {
            isMinifyEnabled = true
            isShrinkResources = true
            proguardFiles(getDefaultProguardFile("proguard-android-optimize.txt"))
            signingConfig = signingConfigs.findByName("release") ?: signingConfigs.getByName("debug")
        }
    }

    buildFeatures {
        compose = true
        buildConfig = false
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    lint {
        abortOnError = true
        // targetSdk stays at the compileSdk this Android Gradle Plugin release supports (35).
        disable += setOf("GradleDependency", "NewerVersionAvailable", "AndroidGradlePluginVersion", "OldTargetApi")
    }
}

kotlin {
    compilerOptions {
        jvmTarget.set(JvmTarget.JVM_17)
    }
}

// The release classes are compiled from scratch, as the library's are (see liquidglass/build.gradle.kts): an
// incremental compile can emit different Compose stability code than a full one, and the APK would then depend
// on which files the last compile happened to recompile.
afterEvaluate {
    tasks.named<KotlinCompile>("compileReleaseKotlin") {
        incremental = false
        inputs.property("compiledFromScratch", true)
    }
}

dependencies {
    implementation(project(":liquidglass"))
    implementation(platform(libs.compose.bom))
    implementation(libs.compose.bom.ui)
    implementation(libs.compose.bom.foundation)
    implementation(libs.compose.bom.icons)
    implementation(libs.compose.bom.tooling.preview)
    implementation(libs.activity.compose)
    implementation(libs.core.ktx)
    debugImplementation(libs.compose.bom.tooling)
}

/** Fails when the release APK lacks the library's LICENSE and NOTICE.md (META-INF/liquid-glass-compose/). */
val verifyReleaseApkLicense by tasks.registering {
    group = "verification"
    description = "Checks that the release APK carries LICENSE and NOTICE.md."
    val apks = layout.buildDirectory.dir("outputs/apk/release")
    doLast {
        val apk = apks.get().asFile.listFiles { file -> file.extension == "apk" }?.singleOrNull()
            ?: throw GradleException("no single release APK in ${apks.get().asFile}")
        val names = ZipFile(apk).use { zip -> zip.entries().asSequence().map { it.name }.toSet() }
        val missing = listOf("LICENSE", "NOTICE.md").map { "META-INF/liquid-glass-compose/$it" }.filterNot(names::contains)
        if (missing.isNotEmpty()) throw GradleException("${apk.name} lacks ${missing.joinToString()}")
    }
}

tasks.matching { it.name == "assembleRelease" }.configureEach { finalizedBy(verifyReleaseApkLicense) }
