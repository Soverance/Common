import { useEffect, useMemo, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { Canvas, useFrame } from '@react-three/fiber'
import * as THREE from 'three'
import { derivePaletteFromPrimary, type EmberPalette } from '../theme/palette'

function useEmberTexture(palette: EmberPalette) {
  return useMemo(() => {
    const size = 64
    const canvas = document.createElement('canvas')
    canvas.width = size
    canvas.height = size
    const ctx = canvas.getContext('2d')!

    const gradient = ctx.createRadialGradient(
      size / 2, size / 2, 0,
      size / 2, size / 2, size / 2,
    )
    gradient.addColorStop(0,    palette.hotCore)
    gradient.addColorStop(0.15, palette.bright)
    gradient.addColorStop(0.4,  palette.mid)
    gradient.addColorStop(0.7,  palette.outer)
    gradient.addColorStop(1,    palette.falloff)

    ctx.fillStyle = gradient
    ctx.fillRect(0, 0, size, size)

    const tex = new THREE.CanvasTexture(canvas)
    tex.needsUpdate = true
    return tex
  }, [palette])
}

interface EmberFieldProps {
  count?: number
  palette: EmberPalette
}

function EmberField({ count = 220, palette }: EmberFieldProps) {
  const meshRef = useRef<THREE.Points>(null)
  const texture = useEmberTexture(palette)

  const positions = useMemo(() => {
    const arr = new Float32Array(count * 3)
    for (let i = 0; i < count; i++) {
      // Concentrate near bottom-left ("fire source") and let them rise/spread.
      const x = -8 + Math.random() * 6 + Math.random() * Math.random() * 8
      const y = -6 + Math.random() * 12
      const z = -2 + Math.random() * 4
      arr[i * 3] = x
      arr[i * 3 + 1] = y
      arr[i * 3 + 2] = z
    }
    return arr
  }, [count])

  useFrame((_, delta) => {
    const mesh = meshRef.current
    if (!mesh) return
    const positionsAttr = mesh.geometry.attributes.position as THREE.BufferAttribute
    const arr = positionsAttr.array as Float32Array
    for (let i = 0; i < arr.length; i += 3) {
      // Intentional per-frame randomness produces ember turbulence/flicker.
      // (Stored velocities give smoother, less alive-looking motion.)
      arr[i + 1] += delta * (0.4 + Math.random() * 0.3) // rise
      arr[i] += delta * (Math.random() - 0.5) * 0.2     // gentle drift
      if (arr[i + 1] > 8) {
        arr[i + 1] = -6
        arr[i] = -8 + Math.random() * 6 + Math.random() * Math.random() * 8
      }
    }
    positionsAttr.needsUpdate = true
  })

  return (
    <points ref={meshRef}>
      <bufferGeometry>
        <bufferAttribute
          attach="attributes-position"
          args={[positions, 3]}
        />
      </bufferGeometry>
      <pointsMaterial
        size={0.6}
        sizeAttenuation
        map={texture}
        transparent
        opacity={0.8}
        depthWrite={false}
        blending={THREE.AdditiveBlending}
      />
    </points>
  )
}

function readPrimary(): string {
  if (typeof document === 'undefined') return '#6366f1'
  const value = getComputedStyle(document.documentElement)
    .getPropertyValue('--primary')
    .trim()
  return value || '#6366f1'
}

function shouldReduceMotion(): boolean {
  if (typeof window === 'undefined') return false
  if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return true
  if ((navigator.hardwareConcurrency ?? 8) < 4) return true
  return false
}

export function GlassBackground() {
  const [palette, setPalette] = useState<EmberPalette>(() =>
    derivePaletteFromPrimary(readPrimary()),
  )
  const [reducedMotion] = useState<boolean>(() => shouldReduceMotion())

  // If --primary changes (e.g., theme switch), recompute palette.
  // We watch both data-theme (definite trigger) and style (catch-all for
  // dynamic CSS variable changes). To avoid redundant palette rebakes when
  // some other style attribute mutation fires (CSS-in-JS, devtools, extensions),
  // we cache the last-known primary string and only re-derive on actual change.
  const lastPrimary = useRef<string>(readPrimary())
  useEffect(() => {
    const observer = new MutationObserver(() => {
      const next = readPrimary()
      if (next === lastPrimary.current) return
      lastPrimary.current = next
      setPalette(derivePaletteFromPrimary(next))
    })
    observer.observe(document.documentElement, {
      attributes: true,
      attributeFilter: ['data-theme', 'style'],
    })
    return () => observer.disconnect()
  }, [])

  if (typeof document === 'undefined') return null

  const overlay = (
    <div
      style={{
        position: 'fixed',
        inset: 0,
        zIndex: -1,
        pointerEvents: 'none',
      }}
    >
      {!reducedMotion && (
        <Canvas
          orthographic
          camera={{ position: [0, 0, 10], zoom: 50 }}
          gl={{ alpha: true, antialias: true }}
          style={{ position: 'absolute', inset: 0 }}
        >
          <EmberField palette={palette} />
        </Canvas>
      )}
      <div
        style={{
          position: 'absolute',
          inset: 0,
          background: [
            `radial-gradient(ellipse 70% 55% at 15% 95%, ${palette.outer}, ${palette.falloff} 75%)`,
            `radial-gradient(ellipse 40% 30% at 5% 100%, ${palette.mid}, transparent 60%)`,
            `radial-gradient(ellipse 90% 50% at 40% 100%, ${palette.outer}, transparent 70%)`,
            `radial-gradient(ellipse 80% 60% at 50% 50%, transparent 0%, rgba(4, 3, 2, 0.5) 100%)`,
          ].join(', '),
          pointerEvents: 'none',
        }}
      />
    </div>
  )

  return createPortal(overlay, document.body)
}
