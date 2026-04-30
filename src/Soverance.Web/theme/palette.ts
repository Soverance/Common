/**
 * Ember palette derived from a primary color, used by GlassBackground.
 * The primary's hue is preserved; saturation and lightness vary across the
 * gradient stops to produce a "white-hot center → mid → outer → falloff" ramp.
 */
export interface EmberPalette {
  hotCore: string   // 1.0 alpha
  bright: string    // 0.9 alpha
  mid: string       // 0.5 alpha
  outer: string     // 0.15 alpha
  falloff: string   // 0.0 alpha
}

interface Hsl {
  h: number
  s: number
  l: number
}

/**
 * Parse a CSS color (hex, rgb(), rgba(), or named) to HSL by leveraging the
 * browser's color parsing. Returns null in non-browser environments.
 */
function parseToHsl(color: string): Hsl | null {
  if (typeof document === 'undefined') return null
  const probe = document.createElement('canvas').getContext('2d')
  if (!probe) return null
  probe.fillStyle = '#000000'
  probe.fillStyle = color
  // After assigning, fillStyle is normalized to "#rrggbb" or "rgba(...)".
  const normalized = probe.fillStyle
  // Extract rgb(a) channels via regex (works for both #rrggbb after canvas normalization
  // and the rgba() form that canvas uses for colors with alpha).
  const hex = /^#([0-9a-f]{6})$/i.exec(normalized)
  let r: number, g: number, b: number
  if (hex) {
    const v = parseInt(hex[1], 16)
    r = (v >> 16) & 0xff
    g = (v >> 8) & 0xff
    b = v & 0xff
  } else {
    const m = /^rgba?\(([^)]+)\)/.exec(normalized)
    if (!m) return null
    const parts = m[1].split(',').map(s => parseFloat(s.trim()))
    if (parts.length < 3 || parts.slice(0, 3).some(Number.isNaN)) return null
    r = parts[0]; g = parts[1]; b = parts[2]
  }
  return rgbToHsl(r, g, b)
}

function rgbToHsl(r: number, g: number, b: number): Hsl {
  r /= 255; g /= 255; b /= 255
  const max = Math.max(r, g, b), min = Math.min(r, g, b)
  const l = (max + min) / 2
  let h = 0, s = 0
  if (max !== min) {
    const d = max - min
    s = l > 0.5 ? d / (2 - max - min) : d / (max + min)
    switch (max) {
      case r: h = ((g - b) / d + (g < b ? 6 : 0)); break
      case g: h = ((b - r) / d + 2); break
      case b: h = ((r - g) / d + 4); break
    }
    h *= 60
  }
  return { h, s: s * 100, l: l * 100 }
}

function hsla(h: number, s: number, l: number, a: number): string {
  return `hsla(${h.toFixed(1)}, ${s.toFixed(1)}%, ${l.toFixed(1)}%, ${a})`
}

/**
 * Compute an EmberPalette from a primary color string.
 * Falls back to a neutral warm palette in non-browser environments.
 */
export function derivePaletteFromPrimary(primary: string): EmberPalette {
  const hsl = parseToHsl(primary)
  // Fallback: RoboForge's original warm-orange palette
  if (!hsl) {
    return {
      hotCore: 'rgba(255, 255, 255, 1.0)',
      bright:  'rgba(255, 220, 150, 0.9)',
      mid:     'rgba(255, 140,  50, 0.5)',
      outer:   'rgba(200,  60,  10, 0.15)',
      falloff: 'rgba(100,  20,   0, 0.0)',
    }
  }
  const { h } = hsl
  return {
    hotCore: hsla(h, 25, 100, 1.0),
    bright:  hsla(h, 90,  60, 0.9),
    mid:     hsla(h, 95,  45, 0.5),
    outer:   hsla(h,100,  25, 0.15),
    falloff: hsla(h,100,  10, 0.0),
  }
}
