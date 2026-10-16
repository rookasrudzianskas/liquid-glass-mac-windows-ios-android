package io.github.rookasrudzianskas.liquidglass.internal

import android.os.Build
import android.view.HapticFeedbackConstants
import android.view.View
import androidx.compose.runtime.withFrameNanos
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Job
import kotlinx.coroutines.launch

/**
 * Runs [step] once per frame while it reports motion, then stops: springs are
 * only driven by `withFrameNanos` while something animates. [step] receives
 * the frame time delta in seconds and returns true once everything is at rest.
 */
internal class FrameAnimator(private val scope: CoroutineScope, private val step: (dt: Float) -> Boolean) {
    private var job: Job? = null

    fun wake() {
        if (job?.isActive == true) return
        job = scope.launch {
            var last = -1L
            while (true) {
                val now = withFrameNanos { it }
                val dt = if (last < 0) 1f / 60f else ((now - last) / 1e9f).coerceIn(0f, 0.1f)
                last = now
                if (step(dt)) break
            }
        }
    }
}

/** Haptic feedback for glass controls, using the richest constant the platform offers. */
internal object GlassHaptics {
    fun toggle(view: View, on: Boolean) {
        val constant = when {
            Build.VERSION.SDK_INT >= 34 -> if (on) HapticFeedbackConstants.TOGGLE_ON else HapticFeedbackConstants.TOGGLE_OFF
            else -> HapticFeedbackConstants.CONTEXT_CLICK
        }
        view.performHapticFeedback(constant)
    }

    /** A detent: a slider step or a new tab under the finger. */
    fun tick(view: View) {
        val constant = if (Build.VERSION.SDK_INT >= 34) HapticFeedbackConstants.SEGMENT_TICK else HapticFeedbackConstants.CLOCK_TICK
        view.performHapticFeedback(constant)
    }

    /** A gesture's start: a thumb picked up. */
    fun press(view: View) {
        view.performHapticFeedback(HapticFeedbackConstants.VIRTUAL_KEY)
    }
}
