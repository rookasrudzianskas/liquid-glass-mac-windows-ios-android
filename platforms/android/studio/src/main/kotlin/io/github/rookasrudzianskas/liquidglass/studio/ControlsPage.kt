package io.github.rookasrudzianskas.liquidglass.studio

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.BoxScope
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.navigationBars
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.statusBars
import androidx.compose.foundation.layout.windowInsetsBottomHeight
import androidx.compose.foundation.layout.windowInsetsTopHeight
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material.icons.filled.Edit
import androidx.compose.material.icons.filled.Favorite
import androidx.compose.material.icons.filled.KeyboardArrowDown
import androidx.compose.material.icons.filled.PlayArrow
import androidx.compose.material.icons.filled.Share
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.dp
import io.github.rookasrudzianskas.liquidglass.GlassButton
import io.github.rookasrudzianskas.liquidglass.GlassButtonSize
import io.github.rookasrudzianskas.liquidglass.GlassIcon
import io.github.rookasrudzianskas.liquidglass.GlassMaterial
import io.github.rookasrudzianskas.liquidglass.GlassMenu
import io.github.rookasrudzianskas.liquidglass.GlassMenuItem
import io.github.rookasrudzianskas.liquidglass.GlassShape
import io.github.rookasrudzianskas.liquidglass.GlassSlider
import io.github.rookasrudzianskas.liquidglass.GlassSurface
import io.github.rookasrudzianskas.liquidglass.GlassSwitch
import io.github.rookasrudzianskas.liquidglass.GlassTab
import io.github.rookasrudzianskas.liquidglass.GlassTabBar
import io.github.rookasrudzianskas.liquidglass.GlassText
import io.github.rookasrudzianskas.liquidglass.LocalGlassContentColor
import kotlin.math.roundToInt

/** Controls: buttons in every size, tinted and icon-only, a slider, switches, a tab bar and a menu. */
@Composable
fun BoxScope.ControlsPage() {
    Column(
        Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(horizontal = 20.dp),
        verticalArrangement = Arrangement.spacedBy(14.dp),
    ) {
        Spacer(Modifier.windowInsetsTopHeight(WindowInsets.statusBars))
        GlassText("Controls", Modifier.padding(top = 12.dp), style = StudioType.Title)

        SectionLabel("Buttons")
        Row(horizontalArrangement = Arrangement.spacedBy(10.dp), verticalAlignment = Alignment.CenterVertically) {
            GlassButton("Small", onClick = {}, size = GlassButtonSize.Small)
            GlassButton("Regular", onClick = {})
            GlassButton("Large", onClick = {}, size = GlassButtonSize.Large)
        }
        Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
            GlassButton("Play", onClick = {}, icon = Icons.Filled.PlayArrow, tint = StudioColors.Blue)
            GlassButton("Done", onClick = {}, icon = Icons.Filled.Check, tint = StudioColors.Green)
            GlassButton("Delete", onClick = {}, tint = StudioColors.Red)
        }
        Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
            GlassButton(Icons.Filled.Add, "Add", onClick = {})
            GlassButton(Icons.Filled.Favorite, "Favorite", onClick = {}, tint = StudioColors.Pink)
            GlassButton(Icons.Filled.Share, "Share", onClick = {})
            GlassButton(Icons.Filled.Edit, "Edit", onClick = {}, material = GlassMaterial.Regular)
            GlassButton(Icons.Filled.Delete, "Delete", onClick = {}, enabled = false)
        }

        SectionLabel("Slider and switch")
        var volume by rememberSaveable { mutableFloatStateOf(0.62f) }
        var steps by rememberSaveable { mutableFloatStateOf(3f) }
        var motion by rememberSaveable { mutableStateOf(true) }
        var haptics by rememberSaveable { mutableStateOf(false) }
        GlassSurface(Modifier.fillMaxWidth(), shape = GlassShape(28.dp), material = GlassMaterial.Regular) {
            Column(Modifier.padding(horizontal = 18.dp, vertical = 14.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
                ValueRow("Volume", "${(volume * 100).roundToInt()}%")
                GlassSlider(volume, { volume = it }, tint = StudioColors.Blue)
                ValueRow("Detents", steps.roundToInt().toString())
                GlassSlider(steps, { steps = it }, valueRange = 0f..5f, steps = 4, tint = StudioColors.Orange)
                SwitchRow("Liquid motion", motion, tint = StudioColors.Green) { motion = it }
                SwitchRow("Haptics", haptics) { haptics = it }
            }
        }

        SectionLabel("Tab bar")
        var range by rememberSaveable { mutableIntStateOf(1) }
        val ranges = remember { listOf(GlassTab("Day"), GlassTab("Week"), GlassTab("Month"), GlassTab("Year")) }
        GlassTabBar(ranges, range, { range = it }, Modifier.fillMaxWidth())

        SectionLabel("Menu")
        var sortOpen by remember { mutableStateOf(false) }
        var sort by rememberSaveable { mutableIntStateOf(0) }
        val sorts = listOf("Name", "Date modified", "Size", "Kind")
        GlassMenu(
            expanded = sortOpen,
            onDismissRequest = { sortOpen = false },
            trigger = {
                GlassButton(onClick = { sortOpen = true }) {
                    GlassText("Sort by ${sorts[sort]}")
                    GlassIcon(Icons.Filled.KeyboardArrowDown, contentDescription = null)
                }
            },
        ) {
            sorts.forEachIndexed { index, label -> GlassMenuItem(label, onClick = { sort = index }, checked = index == sort) }
        }

        Spacer(Modifier.height(TabBarClearance))
        Spacer(Modifier.windowInsetsBottomHeight(WindowInsets.navigationBars))
    }
}

@Composable
private fun SectionLabel(text: String) {
    GlassText(text.uppercase(), Modifier.padding(top = 10.dp), style = StudioType.Section, color = LocalGlassContentColor.current.copy(alpha = 0.72f))
}

@Composable
private fun ValueRow(label: String, value: String) {
    Row(Modifier.fillMaxWidth().padding(top = 4.dp), verticalAlignment = Alignment.CenterVertically) {
        GlassText(label, Modifier.weight(1f), style = StudioType.Label)
        GlassText(value, style = StudioType.Label, color = LocalGlassContentColor.current.copy(alpha = 0.6f))
    }
}

@Composable
private fun SwitchRow(label: String, checked: Boolean, tint: Color = Color.Unspecified, onChange: (Boolean) -> Unit) {
    Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
        GlassText(label, Modifier.weight(1f), style = StudioType.Label)
        GlassSwitch(checked, onChange, tint = tint)
    }
}

/** Space the floating tab bar covers at the bottom of scrolling pages. */
val TabBarClearance = 96.dp

/** Accent colors used across the Studio. */
object StudioColors {
    val Blue = Color(0xFF0A84FF)
    val Green = Color(0xFF30D158)
    val Red = Color(0xFFFF453A)
    val Pink = Color(0xFFFF375F)
    val Orange = Color(0xFFFF9F0A)
    val Purple = Color(0xFFBF5AF2)
}
