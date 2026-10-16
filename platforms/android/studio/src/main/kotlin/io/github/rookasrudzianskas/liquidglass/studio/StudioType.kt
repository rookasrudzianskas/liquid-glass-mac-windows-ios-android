package io.github.rookasrudzianskas.liquidglass.studio

import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.sp

/** Studio typography: large titles, section labels and body text. */
object StudioType {
    val Title = TextStyle(fontSize = 34.sp, fontWeight = FontWeight.Bold, letterSpacing = (-0.4).sp)
    val Heading = TextStyle(fontSize = 20.sp, fontWeight = FontWeight.SemiBold)
    val Section = TextStyle(fontSize = 13.sp, fontWeight = FontWeight.SemiBold, letterSpacing = 0.6.sp)
    val Body = TextStyle(fontSize = 15.sp, fontWeight = FontWeight.Normal, lineHeight = 21.sp)
    val Label = TextStyle(fontSize = 17.sp, fontWeight = FontWeight.Normal)
    val Caption = TextStyle(fontSize = 13.sp, fontWeight = FontWeight.Medium)
}
