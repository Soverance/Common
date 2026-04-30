import { useCallback, useLayoutEffect, useMemo, useState, type ReactNode } from 'react'
import { ALL_THEMES, STORAGE_KEY, THEME_ATTRIBUTE, ThemeContext, type Theme } from './ThemeContext'

interface ThemeProviderProps {
  defaultTheme?: Theme
  children: ReactNode
}

function readStoredTheme(fallback: Theme): Theme {
  if (typeof window === 'undefined') return fallback
  const stored = window.localStorage.getItem(STORAGE_KEY)
  if (stored === 'light' || stored === 'dark' || stored === 'glass') return stored
  return fallback
}

export function ThemeProvider({ defaultTheme = 'dark', children }: ThemeProviderProps) {
  const [theme, setThemeState] = useState<Theme>(() => readStoredTheme(defaultTheme))

  // Apply the active theme synchronously after render but before paint.
  // useLayoutEffect (vs useEffect) prevents flash-of-wrong-theme: the
  // data-theme attribute is set before the browser draws the first frame.
  // localStorage write is consolidated here so both setTheme and cycle stay
  // pure (no side effects inside React's state updaters).
  useLayoutEffect(() => {
    if (typeof document === 'undefined') return
    document.documentElement.setAttribute(THEME_ATTRIBUTE, theme)
    if (typeof window !== 'undefined') {
      window.localStorage.setItem(STORAGE_KEY, theme)
    }
  }, [theme])

  const setTheme = useCallback((next: Theme) => {
    setThemeState(next)
  }, [])

  const cycle = useCallback(() => {
    setThemeState(prev => {
      const idx = ALL_THEMES.indexOf(prev)
      return ALL_THEMES[(idx + 1) % ALL_THEMES.length]
    })
  }, [])

  const value = useMemo(() => ({ theme, setTheme, cycle }), [theme, setTheme, cycle])

  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>
}
