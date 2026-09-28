import { createTheme, darken, lighten, type Theme } from '@mui/material/styles'

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
  const fieldBg = token('--color-field-bg')
  const fieldAccent = token('--color-field-accent')

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
      error: {
        main: token('--status-red'),
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
      MuiAlert: {
        styleOverrides: {
          root: ({ ownerState, theme }) =>
            ownerState.severity === 'error' && (ownerState.variant ?? 'standard') === 'standard'
              ? {
                  color: theme.palette.error.main,
                  backgroundColor: lighten(theme.palette.error.main, 0.9),
                }
              : {},
        },
      },
      MuiFilledInput: {
        styleOverrides: {
          root: {
            backgroundColor: fieldBg,
            '&:hover': {
              backgroundColor: darken(fieldBg, 0.04),
            },
            '&.Mui-focused': {
              backgroundColor: fieldBg,
            },
            '&::after': {
              borderBottomColor: fieldAccent,
            },
          },
          input: ({ theme }) => ({
            '&:-webkit-autofill': {
              boxShadow: `0 0 0 100px ${fieldBg} inset`,
              WebkitTextFillColor: theme.palette.text.primary,
              caretColor: theme.palette.text.primary,
            },
          }),
        },
      },
      MuiInputLabel: {
        styleOverrides: {
          root: {
            fontSize: token('--fs-14'),
            '&.Mui-focused': {
              color: fieldAccent,
            },
          },
        },
      },
    },
  })
}
