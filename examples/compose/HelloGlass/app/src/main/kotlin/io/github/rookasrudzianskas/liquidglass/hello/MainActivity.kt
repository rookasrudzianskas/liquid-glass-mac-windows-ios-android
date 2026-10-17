package io.github.rookasrudzianskas.liquidglass.hello

import android.content.Intent
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.SystemBarStyle
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.navigationBars
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.statusBars
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.layout.windowInsetsPadding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.AccountCircle
import androidx.compose.material.icons.filled.Home
import androidx.compose.material.icons.filled.Search
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material.icons.filled.Share
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalUriHandler
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.semantics.heading
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import io.github.rookasrudzianskas.liquidglass.GlassAppearance
import io.github.rookasrudzianskas.liquidglass.GlassButton
import io.github.rookasrudzianskas.liquidglass.GlassContent
import io.github.rookasrudzianskas.liquidglass.GlassMaterial
import io.github.rookasrudzianskas.liquidglass.GlassScene
import io.github.rookasrudzianskas.liquidglass.GlassShape
import io.github.rookasrudzianskas.liquidglass.GlassSurface
import io.github.rookasrudzianskas.liquidglass.GlassSwitch
import io.github.rookasrudzianskas.liquidglass.GlassTab
import io.github.rookasrudzianskas.liquidglass.GlassTabBar
import io.github.rookasrudzianskas.liquidglass.GlassText
import io.github.rookasrudzianskas.liquidglass.GlassToolbar
import io.github.rookasrudzianskas.liquidglass.GlassToolbarSpacer

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        // The wallpaper is dark under both system bars, so their icons stay light.
        val bars = SystemBarStyle.dark(android.graphics.Color.TRANSPARENT)
        enableEdgeToEdge(statusBarStyle = bars, navigationBarStyle = bars)
        super.onCreate(savedInstanceState)
        setContent { HelloGlass() }
    }
}

private const val GUIDE = "https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android/blob/main/docs/android.md"

private val Tabs = listOf(
    GlassTab("Home", Icons.Filled.Home),
    GlassTab("Explore", Icons.Filled.Search),
    GlassTab("Settings", Icons.Filled.Settings),
)

/** One glass screen over the Aurora wallpaper: a toolbar, a card with two buttons and a switch, and a tab bar. */
@Composable
fun HelloGlass() {
    var frosted by rememberSaveable { mutableStateOf(true) }
    var tab by rememberSaveable { mutableIntStateOf(0) }
    val uriHandler = LocalUriHandler.current
    val context = LocalContext.current

    GlassScene(Modifier.fillMaxSize()) {
        // What the glass refracts: anything Compose draws, here under the system bars too.
        GlassContent(Modifier.fillMaxSize()) {
            Image(
                painter = painterResource(R.drawable.aurora),
                contentDescription = null,
                modifier = Modifier.fillMaxSize(),
                contentScale = ContentScale.Crop,
            )
        }

        // The bars float over the wallpaper's dark top and bottom, so they are dark glass in either theme.
        GlassToolbar(
            Modifier
                .align(Alignment.TopCenter)
                .windowInsetsPadding(WindowInsets.statusBars)
                .padding(start = 20.dp, top = 8.dp, end = 20.dp)
                .fillMaxWidth(),
            appearance = GlassAppearance.Dark,
        ) {
            GlassButton(Icons.Filled.Share, contentDescription = "Share", onClick = { context.startActivity(shareIntent(GUIDE)) })
            GlassToolbarSpacer()
            GlassButton(Icons.Filled.AccountCircle, contentDescription = "Profile", onClick = { tab = 2 })
        }

        // The card follows the system theme: light glass with dark text, or dark glass with white text.
        GlassSurface(
            Modifier
                .align(Alignment.Center)
                .padding(horizontal = 20.dp)
                .widthIn(max = 408.dp)
                .fillMaxWidth(),
            shape = GlassShape(32.dp),
            material = if (frosted) GlassMaterial.Regular else GlassMaterial.Clear,
        ) {
            Column(Modifier.padding(24.dp), verticalArrangement = Arrangement.spacedBy(16.dp)) {
                Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
                    GlassText("Hello, Glass", Modifier.semantics { heading() }, style = TitleStyle)
                    GlassText("Native glass over a live backdrop.", style = SubtitleStyle, maxLines = 1)
                }
                Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                    GlassButton("Get started", onClick = { tab = 1 }, tint = Color(0xFF0A84FF))
                    GlassButton("Learn more", onClick = { uriHandler.openUri(GUIDE) })
                }
                Row(verticalAlignment = Alignment.CenterVertically) {
                    // TalkBack reads the switch's own label, so the visible one is hidden from it.
                    GlassText("Frosted", Modifier.weight(1f).clearAndSetSemantics {})
                    GlassSwitch(checked = frosted, onCheckedChange = { frosted = it }, label = "Frosted")
                }
            }
        }

        GlassTabBar(
            tabs = Tabs,
            selectedIndex = tab,
            onSelectedIndexChange = { tab = it },
            modifier = Modifier
                .align(Alignment.BottomCenter)
                .windowInsetsPadding(WindowInsets.navigationBars)
                .padding(start = 20.dp, end = 20.dp, bottom = 8.dp)
                .widthIn(max = 400.dp)
                .fillMaxWidth(),
            appearance = GlassAppearance.Dark,
        )
    }
}

private val TitleStyle = TextStyle(fontSize = 34.sp, fontWeight = FontWeight.Bold)
private val SubtitleStyle = TextStyle(fontSize = 16.sp)

private fun shareIntent(text: String): Intent =
    Intent.createChooser(Intent(Intent.ACTION_SEND).setType("text/plain").putExtra(Intent.EXTRA_TEXT, text), null)
