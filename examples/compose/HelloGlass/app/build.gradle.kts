import org.jetbrains.kotlin.gradle.dsl.JvmTarget

plugins {
    alias(libs.plugins.android.application)
    alias(libs.plugins.kotlin.compose)
}

android {
    namespace = "io.github.rookasrudzianskas.liquidglass.hello"
    // Compose 1.12 (the BOM in the version catalog) needs compileSdk 37.
    compileSdk = 37

    defaultConfig {
        applicationId = "io.github.rookasrudzianskas.liquidglass.hello"
        minSdk = 24
        targetSdk = 35
        versionCode = 3
        versionName = "1.0.2"
    }

    buildFeatures {
        compose = true
        buildConfig = false
    }

    lint {
        abortOnError = true
        // targetSdk stays at 35, the level 1.0.1 shipped with, as in the Studio (platforms/android).
        disable += setOf("GradleDependency", "NewerVersionAvailable", "AndroidGradlePluginVersion", "OldTargetApi")
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
}

kotlin {
    compilerOptions {
        jvmTarget.set(JvmTarget.JVM_17)
    }
}

dependencies {
    // Liquid Glass, published as io.github.rookasrudzianskas:liquid-glass-compose:1.0.2.
    implementation(libs.liquid.glass.compose)
    implementation(platform(libs.compose.bom))
    implementation(libs.compose.foundation)
    implementation(libs.compose.icons)
    implementation(libs.activity.compose)
}
