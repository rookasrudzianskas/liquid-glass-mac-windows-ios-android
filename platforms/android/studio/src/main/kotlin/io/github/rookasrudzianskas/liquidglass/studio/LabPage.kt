package io.github.rookasrudzianskas.liquidglass.studio

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.gestures.detectDragGestures
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.interaction.collectIsFocusedAsState
import androidx.compose.foundation.interaction.collectIsPressedAsState
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxScope
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.safeDrawing
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.layout.windowInsetsPadding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.selection.selectable
import androidx.compose.foundation.selection.selectableGroup
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
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
import androidx.compose.ui.draw.clip
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.semantics.stateDescription
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.dp
import io.github.rookasrudzianskas.liquidglass.GlassAppearance
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

private val PanelShape = GlassShape(30.dp)

/** Space between the top of the safe area and the lens, and between the lens and the panel. */
private val LensTopGap = 72.dp
private val LensPanelGap = 16.dp

/**
 * Materials lab: live material, appearance, refraction, tint and radius over
 * the wallpaper, on a draggable lens. In portrait the panel sits below the
 * lens; in landscape beside it. Either way the panel keeps clear of the lens,
 * the system bars and the tab bar, and scrolls when its controls do not fit
 * (large fonts, short screens).
 */
@Composable
fun BoxScope.LabPage(launch: StudioLaunch) {
    var material by rememberSaveable { mutableStateOf(launch.labMaterial) }
    var appearance by rememberSaveable { mutableStateOf<GlassAppearance?>(null) }
    var refraction by rememberSaveable { mutableFloatStateOf(60f) }
    var radius by rememberSaveable { mutableFloatStateOf(36f) }
    var tint by rememberSaveable { mutableIntStateOf(0) }
    var offset by remember { mutableStateOf(Offset.Zero) }

    val lens = @Composable { modifier: Modifier ->
        GlassSurface(
            modifier = modifier
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
    }
    val panel = @Composable { modifier: Modifier ->
        GlassSurface(modifier, shape = PanelShape, material = GlassMaterial.Regular) {
            // Scrolled controls stay inside the panel's outline.
            Column(Modifier.clip(PanelShape).verticalScroll(rememberScrollState()).padding(18.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                Choice("Material", listOf("Clear" to GlassMaterial.Clear, "Regular" to GlassMaterial.Regular), material) { material = it }
                Choice("Appearance", listOf("Scene" to null, "Light" to GlassAppearance.Light, "Dark" to GlassAppearance.Dark), appearance) { appearance = it }
                Column {
                    val shown = "${refraction.roundToInt()} dp"
                    LabValue("Refraction", shown)
                    GlassSlider(
                        refraction,
                        { refraction = it },
                        Modifier.semantics { stateDescription = shown },
                        valueRange = 0f..120f,
                        tint = StudioColors.Blue,
                        label = "Refraction",
                    )
                }
                Column {
                    val shown = if (radius >= CapsuleRadius) "Capsule" else "${radius.roundToInt()} dp"
                    LabValue("Corner radius", shown)
                    GlassSlider(
                        radius,
                        { radius = it },
                        Modifier.semantics { stateDescription = shown },
                        valueRange = 0f..CapsuleRadius,
                        tint = StudioColors.Blue,
                        label = "Corner radius",
                    )
                }
                TintChoice(tint) { tint = it }
            }
        }
    }

    // Inside the system bars and cutouts, and above the floating tab bar.
    BoxWithConstraints(
        Modifier
            .fillMaxSize()
            .windowInsetsPadding(WindowInsets.safeDrawing)
            .padding(start = 16.dp, end = 16.dp, bottom = TabBarClearance),
    ) {
        if (maxWidth > maxHeight) {
            Row(Modifier.fillMaxSize(), horizontalArrangement = Arrangement.spacedBy(LensPanelGap), verticalAlignment = Alignment.CenterVertically) {
                Box(Modifier.weight(1f).fillMaxHeight(), contentAlignment = Alignment.Center) { lens(Modifier) }
                panel(Modifier.weight(1f).widthIn(max = 460.dp))
            }
        } else {
            Column(Modifier.fillMaxSize(), horizontalAlignment = Alignment.CenterHorizontally) {
                Spacer(Modifier.height(LensTopGap))
                lens(Modifier)
                Spacer(Modifier.height(LensPanelGap))
                // The panel takes what is left below the lens and scrolls inside it.
                Box(Modifier.weight(1f).fillMaxWidth(), contentAlignment = Alignment.BottomCenter) { panel(Modifier.fillMaxWidth()) }
            }
        }
    }
}

/** A label and its options; the options move below the label when both do not fit on one line. */
@OptIn(ExperimentalLayoutApi::class)
@Composable
private fun <T> Choice(label: String, options: List<Pair<String, T>>, selected: T, onSelect: (T) -> Unit) {
    FlowRow(
        Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalArrangement = Arrangement.spacedBy(8.dp),
    ) {
        GlassText(label, Modifier.align(Alignment.CenterVertically), style = StudioType.Label)
        Segmented(options, selected, onSelect, Modifier.align(Alignment.CenterVertically))
    }
}

/**
 * A segmented control drawn as plain fills: controls sitting on a glass panel
 * stay vibrant instead of becoming glass of their own. Options wrap onto
 * another line rather than squeeze their names.
 */
@OptIn(ExperimentalLayoutApi::class)
@Composable
private fun <T> Segmented(options: List<Pair<String, T>>, selected: T, onSelect: (T) -> Unit, modifier: Modifier = Modifier) {
    val foreground = LocalGlassContentColor.current
    FlowRow(
        modifier
            .background(foreground.copy(alpha = 0.1f), SegmentedShape)
            .padding(3.dp)
            .selectableGroup(),
    ) {
        for ((name, value) in options) {
            val on = value == selected
            val source = remember { MutableInteractionSource() }
            val pressed by source.collectIsPressedAsState()
            val focused by source.collectIsFocusedAsState()
            val fill = when {
                on -> StudioColors.Blue
                pressed -> foreground.copy(alpha = 0.12f)
                else -> Color.Transparent
            }
            Box(
                Modifier
                    // Pressed and focused states follow the chip's capsule (no rectangular default indication).
                    .then(if (focused) Modifier.border(2.dp, FocusColor, CircleShape) else Modifier)
                    .clip(CircleShape)
                    .background(fill)
                    .selectable(selected = on, interactionSource = source, indication = null, role = Role.RadioButton) { onSelect(value) }
                    .padding(horizontal = 14.dp, vertical = 6.dp),
            ) {
                GlassText(name, style = StudioType.Caption, color = if (on) Color.White else foreground)
            }
        }
    }
}

/** Keyboard focus ring of the lab's own choices, matching the library's controls. */
private val FocusColor = StudioColors.Blue

/** The outline of a segmented control: a capsule on one line, rounded when its options wrap. */
private val SegmentedShape = RoundedCornerShape(18.dp)

/** A slider's name and value. The slider itself tells TalkBack both, so the row is hidden from it. */
@OptIn(ExperimentalLayoutApi::class)
@Composable
private fun LabValue(label: String, value: String) {
    FlowRow(Modifier.fillMaxWidth().clearAndSetSemantics {}, horizontalArrangement = Arrangement.SpaceBetween) {
        GlassText(label, style = StudioType.Label)
        GlassText(value, style = StudioType.Value, color = LocalGlassContentColor.current.copy(alpha = ValueAlpha))
    }
}

@OptIn(ExperimentalLayoutApi::class)
@Composable
private fun TintChoice(selected: Int, onSelect: (Int) -> Unit) {
    FlowRow(
        Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalArrangement = Arrangement.spacedBy(8.dp),
    ) {
        GlassText("Tint", Modifier.align(Alignment.CenterVertically).padding(end = 10.dp), style = StudioType.Label)
        Row(
            Modifier.align(Alignment.CenterVertically).selectableGroup(),
            horizontalArrangement = Arrangement.spacedBy(10.dp),
        ) {
            LabTints.forEachIndexed { index, (name, color) -> Swatch(name, color, selected = index == selected) { onSelect(index) } }
        }
    }
}

@Composable
private fun Swatch(name: String, color: Color, selected: Boolean, onClick: () -> Unit) {
    val foreground = LocalGlassContentColor.current
    val source = remember { MutableInteractionSource() }
    val pressed by source.collectIsPressedAsState()
    val focused by source.collectIsFocusedAsState()
    val ring = when {
        focused -> FocusColor
        selected -> foreground.copy(alpha = 0.9f)
        else -> foreground.copy(alpha = if (pressed) 0.6f else 0.25f)
    }
    Box(
        Modifier
            .size(26.dp)
            .clip(CircleShape)
            .selectable(selected = selected, interactionSource = source, indication = null, role = Role.RadioButton, onClick = onClick)
            .semantics { contentDescription = "$name tint" }
            .border(if (selected || focused) 2.dp else 1.dp, ring, CircleShape)
            .padding(if (pressed) 5.dp else 4.dp)
            .background(if (color == Color.Unspecified) foreground.copy(alpha = 0.12f) else color, CircleShape),
    )
}
