export interface TenantBranding {
  primary: string
  primaryActive: string
  primaryBg: string
}

const CSS_VARIABLE: Record<keyof TenantBranding, string> = {
  primary: '--color-primary',
  primaryActive: '--color-primary-active',
  primaryBg: '--color-primary-bg',
}

const CONTRAST_VARIABLE = '--color-primary-contrast'

const HEX_COLOR = /^#(?:[0-9a-f]{3}|[0-9a-f]{6})$/i

const LUMINANCE_THRESHOLD = 0.179

const WHITE = '#ffffff'
const BLACK = '#000000'

function expand(hex: string): string {
  return hex.length === 4 ? `#${hex[1]}${hex[1]}${hex[2]}${hex[2]}${hex[3]}${hex[3]}` : hex
}

function channelLuminance(value: number): number {
  const channel = value / 255
  return channel <= 0.03928 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4
}

function relativeLuminance(hex: string): number {
  const full = expand(hex)
  const red = channelLuminance(Number.parseInt(full.slice(1, 3), 16))
  const green = channelLuminance(Number.parseInt(full.slice(3, 5), 16))
  const blue = channelLuminance(Number.parseInt(full.slice(5, 7), 16))
  return 0.2126 * red + 0.7152 * green + 0.0722 * blue
}

export function contrastTextFor(background: string): typeof WHITE | typeof BLACK {
  if (!HEX_COLOR.test(background)) {
    return WHITE
  }
  return relativeLuminance(background) > LUMINANCE_THRESHOLD ? BLACK : WHITE
}

export function applyTenantBranding(
  branding: Partial<TenantBranding> | null | undefined,
  root: HTMLElement = document.documentElement,
): void {
  let primary: string | null = null

  for (const [key, variable] of Object.entries(CSS_VARIABLE)) {
    const color = branding?.[key as keyof TenantBranding]

    if (color !== undefined && HEX_COLOR.test(color)) {
      root.style.setProperty(variable, color)
      if (key === 'primary') {
        primary = color
      }
    } else {
      root.style.removeProperty(variable)
    }
  }

  if (primary === null) {
    root.style.removeProperty(CONTRAST_VARIABLE)
  } else {
    root.style.setProperty(CONTRAST_VARIABLE, contrastTextFor(primary))
  }
}
