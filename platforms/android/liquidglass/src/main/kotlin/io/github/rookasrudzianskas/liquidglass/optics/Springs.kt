package io.github.rookasrudzianskas.liquidglass.optics

import kotlin.math.PI
import kotlin.math.abs
import kotlin.math.cos
import kotlin.math.exp
import kotlin.math.max
import kotlin.math.sin
import kotlin.math.sqrt

/** A damped spring described by its perceptual [duration] (seconds) and [bounce] in (-1, 1). */
internal data class SpringSpec(val duration: Float, val bounce: Float) {
    /** The same response without overshoot, used under reduced motion. */
    fun critical(): SpringSpec = if (bounce > 0f) copy(bounce = 0f) else this

    companion object {
        /** Finger down: the material catches it. */
        val Press: SpringSpec = SpringSpec(0.24f, 0.2f)

        /** Finger up: gel-like overshoot back to rest. */
        val Release: SpringSpec = SpringSpec(0.5f, 0.42f)

        /** Light settling in and out. */
        val Glow: SpringSpec = SpringSpec(0.42f, 0f)

        /** Glass growing out of another shape (menus). */
        val Morph: SpringSpec = SpringSpec(0.42f, 0.18f)

        /** Selection lenses and drag follow. */
        val Track: SpringSpec = SpringSpec(0.18f, 0f)

        /** The measured switch spring: response 0.47 s, damping ratio 0.7. */
        val Switch: SpringSpec = SpringSpec(0.47f, 0.3f)

        /** Track spring while a finger drags a surface of the given size. */
        fun trackWhileDragging(width: Float, height: Float): SpringSpec =
            SpringSpec(0.18f + 0.0005f * max(width, height), 0f)
    }
}

/** Stiffness and damping of a unit-mass spring. */
internal data class SpringCoefficients(val stiffness: Double, val damping: Double)

internal fun springCoefficients(spec: SpringSpec): SpringCoefficients {
    val duration = spec.duration.toDouble()
    val bounce = spec.bounce.toDouble()
    val omega = 2.0 * PI / duration
    val damping = if (bounce >= 0.0) 4.0 * PI * (1.0 - bounce) / duration else 4.0 * PI / (duration * (1.0 + bounce))
    return SpringCoefficients(omega * omega, damping)
}

/**
 * One spring's state. [step] advances it in closed form (port of `springStep` in
 * spec/reference.mjs) and snaps to the target once it is visually at rest.
 */
internal class SpringValue(initial: Float = 0f) {
    var value: Float = initial
        private set
    var velocity: Float = 0f
        private set
    var target: Float = initial

    val isSettled: Boolean get() = value == target && velocity == 0f

    /** Jumps to [to] with no motion. */
    fun snap(to: Float) {
        value = to
        velocity = 0f
        target = to
    }

    /** Advances by [dt] seconds toward [target] along [spec]. */
    fun step(spec: SpringSpec, dt: Float) {
        val next = springStep(value.toDouble(), velocity.toDouble(), target.toDouble(), spec, dt.toDouble())
        value = next[0].toFloat()
        velocity = next[1].toFloat()
    }
}

/**
 * Advances a damped spring by [dt] seconds in closed form. Returns value and velocity.
 */
internal fun springStep(value: Double, velocity: Double, target: Double, spec: SpringSpec, dt: Double): DoubleArray {
    if ((value == target && velocity == 0.0) || !(dt > 0.0)) return doubleArrayOf(value, velocity)
    val (stiffness, damping) = springCoefficients(spec)
    val omega = sqrt(stiffness)
    val zeta = damping / (2.0 * omega)
    val x0 = value - target
    val v0 = velocity
    val x: Double
    val v: Double
    if (zeta < 1.0) {
        val wd = omega * sqrt(1.0 - zeta * zeta)
        val decay = exp(-zeta * omega * dt)
        val a = x0
        val b = (v0 + zeta * omega * x0) / wd
        val c = cos(wd * dt)
        val s = sin(wd * dt)
        x = decay * (a * c + b * s)
        v = decay * ((b * wd - zeta * omega * a) * c - (a * wd + zeta * omega * b) * s)
    } else if (zeta == 1.0) {
        val decay = exp(-omega * dt)
        val b = v0 + omega * x0
        x = decay * (x0 + b * dt)
        v = decay * (b - omega * (x0 + b * dt))
    } else {
        val root = omega * sqrt(zeta * zeta - 1.0)
        val r1 = -zeta * omega + root
        val r2 = -zeta * omega - root
        val c2 = (v0 - r1 * x0) / (r2 - r1)
        val c1 = x0 - c2
        val e1 = exp(r1 * dt)
        val e2 = exp(r2 * dt)
        x = c1 * e1 + c2 * e2
        v = c1 * r1 * e1 + c2 * r2 * e2
    }
    val scale = maxOf(1.0, abs(target), abs(x0))
    if (abs(x) < 1e-4 * scale && abs(v) < 1e-3 * scale) return doubleArrayOf(target, 0.0)
    return doubleArrayOf(target + x, v)
}
