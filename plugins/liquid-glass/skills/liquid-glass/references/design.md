# Designing with Liquid Glass

Guidance for using Liquid Glass Engineering well. It applies to every platform; the platform skills show the code.

## What glass is for

Glass separates the **control layer** from the **content layer**. Content (photos, documents, maps, lists, video) is what people came for; glass controls float above it, let it show through, and get out of the way. When everything is glass, nothing is: the hierarchy collapses and the screen turns into noise.

Use glass for:

- Navigation: tab bars, sidebars that float over content, top toolbars.
- Actions over content: floating buttons, media transport controls, map controls, editing toolbars.
- Transient UI: menus, popovers, sheets peeking over content, toasts.
- Compact information over imagery: a now-playing card over album art, a weather chip over a photo.

Avoid glass for:

- Reading surfaces: long text, settings forms, tables. Use solid backgrounds.
- Repeated items: list rows, grid cells, every card in a feed.
- Decoration: glass without anything meaningful behind it reads as a gray box. If the backdrop is a flat color, a solid surface looks better.

## Hierarchy and layering

- One glass layer at a time. A menu opening from a glass button is the exception: it is transient, Regular, and sits above.
- Keep glass at the edges and corners of the content: top and bottom bars, floating corner buttons. A glass element in the middle of content competes with it.
- Group related controls into one glass cluster (a toolbar capsule with three buttons) rather than three separate glass buttons. Fewer, larger shapes read calmer and cost less.
- Separate clusters with space, not dividers.

## Clear or Regular

| | Clear | Regular |
| --- | --- | --- |
| Look | Transparent, vivid, the backdrop stays sharp and refracts at the rim | Frosted: blurred, lightened (light) or deepened (dark) backdrop |
| Use over | Rich imagery, media, colorful art | Text, busy UI, unknown content |
| Best for | Small controls, tab bars over photos, media buttons | Menus, sheets, panels, cards with text, anything large |
| Legibility | Depends on the backdrop: check it | Reliable |

When in doubt, use Regular for anything holding more than a short label.

## Appearance

- `automatic` follows the system. Override only when the backdrop is known: a dark photo viewer wants dark glass even in light mode.
- Foreground colors come from the appearance (`#111113` light, white dark). Don't color labels to "match" the backdrop.

## Shape

- Controls: capsule. Icon-only: circle.
- Panels and cards: continuous corners, 16 to 28 depending on size.
- Concentric nesting: a control inset by `i` inside a panel with radius `R` uses radius `R − i`, so the gaps stay even.
- Keep shapes simple. The rim light follows the outline; complex silhouettes look busy.

## Tint

- Tint marks the primary action or a state (recording, selected filter). One tinted control per view.
- Use saturated system-like hues (blue `#007AFF`, green `#34C759`, red `#FF3B30`, orange `#FF9500`). Pastels wash out over bright backdrops.
- Tint colors the glass; labels on tinted glass should be white.
- On sliders and switches the tint colors the fill and the track instead.

## Motion

- Interactive glass reacts to touch: it grows a little, stretches toward a drag, glows under the finger and settles with a soft overshoot. That feedback is the material's personality; keep it on controls.
- Static panels and cards don't react to touch.
- Morph rather than fade where there's a source: menus grow out of their trigger and collapse back into it.
- Reduced motion removes stretch and overshoot. `motion = none` removes transitions entirely, for tests and screenshots.

## Legibility checklist

1. Put the control over the busiest area of the backdrop it will ever sit on and check contrast (4.5:1 for text under 18pt, 3:1 for large text and icons).
2. If it fails: switch to Regular; or switch appearance; or move the control; or add a subtle gradient scrim to the content under the bar.
3. Check light and dark, and with reduce transparency on.

## Accessibility

- Reduce transparency: glass becomes an opaque system-like fill (rim light stays), so text contrast is guaranteed.
- Increase contrast: a thin border is added and Regular is preferred.
- Controls keep their platform roles (button, tab, slider, switch, menu), labels and focus behavior; icon-only buttons need an accessible label.
- Large text: glass bars grow with Dynamic Type / font scale; don't fix their height.

## Common mistakes

- Glass over a flat background: nothing to refract, looks like a gray pill. Use a solid surface.
- Glass in a scrolling list: every row repaints its glass while scrolling. Use one glass bar above the list instead.
- Several overlapping glass panels: muddy, slow, unreadable.
- Tinted toolbars: the tint fights the content and every button looks primary.
- Shrinking refraction to zero "to be safe": it removes what makes the material read as glass. Keep the default unless a specific control needs less.
