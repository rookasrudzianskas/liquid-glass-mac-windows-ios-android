buildscript {
    dependencies {
        // The Android Gradle Plugin compiles Kotlin itself (built-in Kotlin) with the Kotlin Gradle plugin it depends
        // on, 2.2.10 for AGP 9.4.1; this one takes its place. Kotlin Gradle plugins before 2.4.20 deserialize build
        // cache metadata unsafely (GHSA-r937-wjx7-w2jp).
        classpath(libs.kotlin.gradle.plugin)
        // Libraries the Android Gradle Plugin itself runs with, raised past the advisories that affect the versions
        // it brings. They are on the build's classpath only and never ship in the AAR or an app.
        constraints {
            classpath(libs.bouncycastle.bcprov) {
                because("AGP 9.4.1 brings 1.80.2: GHSA-9pwp-9qqc-pr26, GHSA-qp49-qgx5-5m26 (< 1.85), GHSA-c3fc-8qff-9hwx (< 1.84)")
            }
            classpath(libs.bouncycastle.bcpkix) {
                because("AGP 9.4.1 brings 1.80.2: GHSA-wg6q-6289-32hp (< 1.84); bcprov, bcpkix and bcutil move together")
            }
            classpath(libs.bouncycastle.bcutil) {
                because("AGP 9.4.1 brings 1.80.2; bcprov, bcpkix and bcutil move together")
            }
            classpath(libs.jose4j) {
                because("AGP 9.4.1 brings 0.9.5: GHSA-3677-xxcr-wjqv (< 0.9.6)")
            }
            classpath(libs.jdom2) {
                because("AGP 9.4.1 brings 2.0.6: GHSA-2363-cqg2-863c (< 2.0.6.1)")
            }
            classpath(libs.commons.lang3) {
                because("AGP 9.4.1 brings 3.16.0 (through commons-compress 1.27.1): GHSA-j288-q9x7-2f5v (< 3.18.0)")
            }
        }
    }
}

plugins {
    alias(libs.plugins.android.application) apply false
    alias(libs.plugins.android.library) apply false
    alias(libs.plugins.kotlin.compose) apply false
}
