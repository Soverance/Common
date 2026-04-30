import { useTheme, type Theme } from '../theme/useTheme'

interface ThemeSwitcherProps {
  className?: string
  /** Override the default cycle behavior (light → dark → glass → light) */
  onClick?: () => void
}

const THEME_LABEL: Record<Theme, string> = {
  light: 'Switch to dark theme',
  dark: 'Switch to glass theme',
  glass: 'Switch to light theme',
}

/**
 * Theme cycle button. Renders a single icon button that cycles through
 * light → dark → glass → light on click. The icon itself is supplied by
 * the consumer via children — typically a Lucide `SunMoon` or
 * theme-conditional icon. Styling is consumer-controlled via className.
 *
 * Example:
 *   <ThemeSwitcher className="h-9 w-9 rounded hover:bg-accent">
 *     <SunMoon className="h-4 w-4" />
 *   </ThemeSwitcher>
 */
export function ThemeSwitcher({
  className,
  onClick,
  children,
}: ThemeSwitcherProps & { children: React.ReactNode }) {
  const { theme, cycle } = useTheme()
  return (
    <button
      type="button"
      aria-label={THEME_LABEL[theme]}
      title={THEME_LABEL[theme]}
      onClick={onClick ?? cycle}
      className={className}
    >
      {children}
    </button>
  )
}
