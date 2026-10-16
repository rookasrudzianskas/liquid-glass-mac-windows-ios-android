package io.github.rookasrudzianskas.liquidglass.studio

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge

/** Liquid Glass Studio: the Android demo of Liquid Glass Engineering. */
class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        enableEdgeToEdge()
        super.onCreate(savedInstanceState)
        val launch = StudioLaunch.from(intent)
        setContent { StudioApp(launch) }
    }
}
