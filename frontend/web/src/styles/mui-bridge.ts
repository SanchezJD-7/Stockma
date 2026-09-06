import { createTheme, type Theme } from '@mui/material/styles'

function requireToken(root: HTMLElement, name: string): string {
  const value = getComputedStyle(root).getPropertyValue(name).trim()

  if (value === '') {
    throw new Error(
      `El token ${name} no está definido. ¿Se importó 'styles.css' antes de construir el theme?`,
    )
  }

  return value
}

export function createMuiBridge(root: HTMLElement = document.documentElement): Theme {
  const token = (name: string) => requireToken(root, name)

  return createTheme({
    palette: {
      primary: {
        main: token('--color-primary'),
        dark: token('--color-primary-active'),
        light: token('--color-primary-bg'),
        contrastText: token('--color-primary-contrast'),
      },
      background: {
        default: token('--color-bg-page'),
        paper: token('--color-bg-surface'),
      },
      text: {
        primary: token('--color-text-primary'),
        secondary: token('--color-text-secondary'),
        disabled: token('--color-text-disabled'),
      },
      divider: token('--color-border'),
    },
    shape: {
      borderRadius: 8,
    },
    typography: {
      fontFamily: token('--font-family'),
    },
    components: {
      MuiButton: {
        defaultProps: {
          disableElevation: true,
        },
      },
    },
  })
}
