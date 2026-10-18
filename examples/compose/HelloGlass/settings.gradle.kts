pluginManagement {
    repositories {
        google()
        mavenCentral()
        gradlePluginPortal()
    }
}

dependencyResolutionManagement {
    repositoriesMode.set(RepositoriesMode.FAIL_ON_PROJECT_REPOS)
    repositories {
        google()
        mavenCentral()
    }
}

rootProject.name = "HelloGlass"

include(":app")

// The app depends on io.github.rookasrudzianskas:liquid-glass-compose:1.0.1. Inside this repository
// that coordinate is built from the library's source next to it, so the example builds offline and in
// CI against the current code. Elsewhere, delete this block and add
// maven("https://liquid-glass-engineering.vercel.app/maven") to the repositories above (see README.md).
includeBuild("../../../platforms/android") {
    dependencySubstitution {
        substitute(module("io.github.rookasrudzianskas:liquid-glass-compose")).using(project(":liquidglass"))
    }
}
