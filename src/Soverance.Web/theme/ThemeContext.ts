import { createContext } from 'react'

export type Theme = 'light' | 'dark' | 'glass'

export interface ThemeContextValue {
  theme: Theme
  setTheme: (theme: Theme) => void
  cycle: () => void
}

export const ThemeContext = createContext<ThemeContextValue | null>(null)

export const STORAGE_KEY = '@soverance/theme'
export const THEME_ATTRIBUTE = 'data-theme'
export const ALL_THEMES: readonly Theme[] = ['light', 'dark', 'glass'] as const
