package io.github.rookasrudzianskas.liquidglass.studio

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.gestures.detectDragGestures
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxScope
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.selected
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.dp
import io.github.rookasrudzianskas.liquidglass.GlassAppearance
import io.github.rookasrudzianskas.liquidglass.GlassButton
import io.github.rookasrudzianskas.liquidglass.GlassButtonSize
import io.github.rookasrudzianskas.liquidglass.GlassMaterial
import io.github.rookasrudzianskas.liquidglass.GlassShape
import io.github.rookasrudzianskas.liquidglass.GlassSlider
import io.github.rookasrudzianskas.liquidglass.GlassSurface
import io.github.rookasrudzianskas.liquidglass.GlassText
import io.github.rookasrudzianskas.liquidglass.LocalGlassContentColor
import kotlin.math.roundToInt

/** Corner radius at which the lab lens becomes a capsule. */
private const val CapsuleRadius = 80f

private val LabTints = listOf(
    "None" to Color.Unspecified,
    "Blue" to StudioColors.Blue,
    "Pink" to StudioColors.Pink,
    "Green" to StudioColors.Green,
    "Orange" to StudioColors.Orange,
    "Purple" to StudioColors.Purple,
)

/** Materials lab: live material, appearance, refraction, tint and radius over the wallpaper, on a draggable lens. */
@Composable
fun BoxScope.LabPage(launch: StudioLaunch) {
    var material by rememberSaveable { mutableStateOf(launch.labMaterial) }
    var appearance by rememberSaveable { mutableStateOf<GlassAppearance?>(null) }
    var refraction by rememberSaveable { mutableFloatStateOf(60f) }
    var radius by rememberSaveable { mutableFloatStateOf(36f) }
    var tint by rememberSaveable { mutableStateOf(0) }
    var offset by remember { mutableStateOf(Offset.Zero) }

    GlassSurface(
        modifier = Modifier
            .align(Alignment.TopCenter)
            .statusBarsPadding()
            .padding(top = 72.dp)
            .offset { IntOffset(offset.x.roundToInt(), offset.y.roundToInt()) }
            .pointerInput(Unit) {
                detectDragGestures { change, drag ->
                    change.consume()
                    offset += drag
                }
            }
            .size(270.dp, 180.dp),
        shape = if (radius >= CapsuleRadius) GlassShape.Capsule else GlassShape(radius.dp),
        material = material,
        appearance = appearance,
        tint = LabTints[tint].second,
        refraction = refraction.dp,
        interactive = true,
        contentAlignment = Alignment.Center,
    ) {
        GlassText("Lens", style = StudioType.Heading)
    }

    GlassSurface(
        Modifier
            .align(Alignment.BottomCenter)
            .navigationBarsPadding()
            .padding(start = 16.dp, end = 16.dp, bottom = TabBarClearance)
            .fillMaxWidth(),
        shape = GlassShape(30.dp),
        material = GlassMaterial.Regular,
    ) {
        Column(Modifier.padding(18.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
            Choice("Material", listOf("Clear" to GlassMaterial.Clear, "Regular" to GlassMaterial.Regular), material) { material = it }
            Choice("Appearance", listOf("Scene" to null, "Light" to GlassAppearance.Light, "Dark" to GlassAppearance.Dark), appearance) { appearance = it }
            Column {
                LabValue("Refraction", "${refraction.roundToInt()} dp")
                GlassSlider(refraction, { refraction = it }, valueRange = 0f..120f)
            }
            Column {
                LabValue("Corner radius", if (radius >= CapsuleRadius) "Capsule" else "${radius.roundToInt()} dp")
                GlassSlider(radius, { radius = it }, valueRange = 0f..CapsuleRadius)
            }
            Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                GlassText("Tint", Modifier.weight(1f), style = StudioType.Label)
                LabTints.forEachIndexed { index, (name, color) -> Swatch(name, color, selected = index == tint) { tint = index } }
            }
        }
    }
}

@Composable
private fun <T> Choice(label: String, options: List<Pair<String, T>>, selected: T, onSelect: (T) -> Unit) {
    Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
        GlassText(label, Modifier.weight(1f), style = StudioType.Label)
        for ((name, value) in options) {
            GlassButton(
                text = name,
                onClick = { onSelect(value) },
                size = GlassButtonSize.Small,
                tint = if (value == selected) StudioColors.Blue else Color.Unspecified,
                modifier = Modifier.semantics { this.selected = value == selected },
            )
        }
    }
}

@Composable
private fun LabValue(label: String, value: String) {
    Row(Modifier.fillMaxWidth()) {
        GlassText(label, Modifier.weight(1f), style = StudioType.Label)
        GlassText(value, style = StudioType.Label, color = LocalGlassContentColor.current.copy(alpha = 0.6f))
    }
}

@Composable
private fun Swatch(name: String, color: Color, selected: Boolean, onClick: () -> Unit) {
    val foreground = LocalGlassContentColor.current
    Box(
        Modifier
            .size(26.dp)
            .border(if (selected) 2.dp else 1.dp, foreground.copy(alpha = if (selected) 0.9f else 0.25f), CircleShape)
            .padding(4.dp)
            .background(if (color == Color.Unspecified) foreground.copy(alpha = 0.12f) else color, CircleShape)
            .semantics(mergeDescendants = true) {
                contentDescription = "$name tint"
                this.selected = selected
            }
            .clickable(role = Role.RadioButton, onClick = onClick),
    )
}
