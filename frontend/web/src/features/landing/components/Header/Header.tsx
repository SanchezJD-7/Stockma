import DarkModeOutlinedIcon from '@mui/icons-material/DarkModeOutlined'
import LightModeOutlinedIcon from '@mui/icons-material/LightModeOutlined'
import CloseOutlinedIcon from '@mui/icons-material/CloseOutlined'
import MenuOutlinedIcon from '@mui/icons-material/MenuOutlined'
import { useState } from 'react'
import logoUrl from '../../../../assets/Stockma_horizontal.png'
import { GhostButton } from '../../../../styles/components/buttons'
import styles from './Header.module.css'

const navigationLinks = [
  { label: 'Producto', href: '#features' },
  { label: 'Cómo funciona', href: '#how-it-works' },
  { label: 'Planes', href: '#pricing' },
  { label: 'Agendar demo', href: '#demo' },
]

export function Header() {
  const [isDark, setIsDark] = useState(() => document.documentElement.dataset.theme === 'dark')
  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false)

  function toggleTheme() {
    const nextTheme = isDark ? 'light' : 'dark'
    document.documentElement.dataset.theme = nextTheme

    try {
      localStorage.setItem('stockma-theme', nextTheme)
    } catch {}

    window.dispatchEvent(new Event('stockma-theme-change'))
    setIsDark(nextTheme === 'dark')
  }

  return (
    <header id='landing-header' className={styles.root}>
      <div className={styles.inner}>
        <div>
          <a className={styles.brand} href='/' aria-label='Stockma inicio'>
            <img className={styles.logo} src={logoUrl} alt='Stockma' />
          </a>
        </div>
        <nav className={styles.desktopNav} aria-label='Navegación principal'>
          {navigationLinks.map(({ href, label }) => (
            <a key={href} className={styles.navLink} href={href}>
              {label}
            </a>
          ))}
        </nav>
        <div className={styles.actions}>
          <GhostButton
            className={styles.iconButton}
            type='button'
            aria-label={isDark ? 'Cambiar a modo claro' : 'Cambiar a modo oscuro'}
            aria-pressed={isDark}
            onClick={toggleTheme}
          >
            {isDark ? (
              <LightModeOutlinedIcon fontSize='small' aria-hidden='true' />
            ) : (
              <DarkModeOutlinedIcon fontSize='small' aria-hidden='true' />
            )}
          </GhostButton>
          <GhostButton
            className={`${styles.iconButton} ${styles.menuToggle}`}
            type='button'
            aria-label={isMobileMenuOpen ? 'Cerrar menú' : 'Abrir menú'}
            aria-expanded={isMobileMenuOpen}
            aria-controls='mobile-navigation'
            onClick={() => setIsMobileMenuOpen((isOpen) => !isOpen)}
          >
            {isMobileMenuOpen ? (
              <CloseOutlinedIcon fontSize='small' aria-hidden='true' />
            ) : (
              <MenuOutlinedIcon fontSize='small' aria-hidden='true' />
            )}
          </GhostButton>
        </div>
      </div>
      <nav
        id='mobile-navigation'
        className={styles.mobileNav}
        aria-label='Navegación móvil'
        hidden={!isMobileMenuOpen}
      >
        {navigationLinks.map(({ href, label }) => (
          <a
            key={href}
            className={styles.navLink}
            href={href}
            onClick={() => setIsMobileMenuOpen(false)}
          >
            {label}
          </a>
        ))}
      </nav>
    </header>
  )
}
