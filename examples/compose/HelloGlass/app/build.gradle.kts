import org.jetbrains.kotlin.gradle.dsl.JvmTarget

plugins {
    alias(libs.plugins.android.application)
    alias(libs.plugins.kotlin.android)
    alias(libs.plugins.kotlin.compose)
}

android {
    namespace = "io.github.rookasrudzianskas.liquidglass.hello"
    compileSdk = 35

    defaultConfig {
        applicationId = "io.github.rookasrudzianskas.liquidglass.hello"
        minSdk = 24
        targetSdk = 35
        versionCode = 2
        versionName = "1.0.1"
    }

    buildFeatures {
        compose = true
        buildConfig = false
    }

    lint {
        abortOnError = true
        // The versions are pinned to platforms/android's, whose Android Gradle Plugin supports targetSdk 35.
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
    // Liquid Glass, published as io.github.rookasrudzianskas:liquid-glass-compose:1.0.1.
    implementation(libs.liquid.glass.compose)
    implementation(platform(libs.compose.bom))
    implementation(libs.compose.foundation)
    implementation(libs.compose.icons)
    implementation(libs.activity.compose)
}
