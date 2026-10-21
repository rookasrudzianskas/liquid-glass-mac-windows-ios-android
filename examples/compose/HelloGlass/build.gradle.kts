// The same build classpath as platforms/android's (see its build.gradle.kts): the Kotlin Gradle plugin that takes
// the place of the one the Android Gradle Plugin depends on, and the plugin's own libraries raised past advisories.
buildscript {
    dependencies {
        classpath(libs.kotlin.gradle.plugin)
        constraints {
            classpath(libs.bouncycastle.bcprov)
            classpath(libs.bouncycastle.bcpkix)
            classpath(libs.bouncycastle.bcutil)
            classpath(libs.jose4j)
            classpath(libs.jdom2)
            classpath(libs.commons.lang3)
        }
    }
}

plugins {
    alias(libs.plugins.android.application) apply false
    alias(libs.plugins.kotlin.compose) apply false
}
