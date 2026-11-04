import { fireEvent, render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { Header } from './Header'

describe('Header theme toggle', () => {
  beforeEach(() => {
    document.documentElement.dataset.theme = 'light'
    localStorage.clear()
  })

  afterEach(() => {
    document.documentElement.removeAttribute('data-theme')
    localStorage.clear()
    vi.restoreAllMocks()
  })

  it('switches the document theme, persists it, and notifies MUI', () => {
    const dispatchEvent = vi.spyOn(window, 'dispatchEvent')
    render(<Header />)

    fireEvent.click(screen.getByRole('button', { name: 'Cambiar a modo oscuro' }))

    expect(document.documentElement.dataset.theme).toBe('dark')
    expect(localStorage.getItem('stockma-theme')).toBe('dark')
    expect(dispatchEvent).toHaveBeenCalledWith(
      expect.objectContaining({ type: 'stockma-theme-change' }),
    )

    fireEvent.click(screen.getByRole('button', { name: 'Cambiar a modo claro' }))

    expect(document.documentElement.dataset.theme).toBe('light')
    expect(localStorage.getItem('stockma-theme')).toBe('light')
    expect(dispatchEvent).toHaveBeenCalledTimes(2)
  })

  it('opens and closes the mobile navigation with its menu button', () => {
    render(<Header />)

    const menuButton = screen.getByRole('button', { name: 'Abrir menú' })
    const mobileNavigation = document.getElementById('mobile-navigation') as HTMLElement

    expect(menuButton.getAttribute('aria-expanded')).toBe('false')
    expect(mobileNavigation.hidden).toBe(true)

    fireEvent.click(menuButton)

    expect(screen.getByRole('button', { name: 'Cerrar menú' }).getAttribute('aria-expanded')).toBe(
      'true',
    )
    expect(mobileNavigation.hidden).toBe(false)

    fireEvent.click(screen.getByRole('button', { name: 'Cerrar menú' }))

    expect(screen.getByRole('button', { name: 'Abrir menú' }).getAttribute('aria-expanded')).toBe(
      'false',
    )
    expect(mobileNavigation.hidden).toBe(true)

    fireEvent.click(screen.getByRole('button', { name: 'Abrir menú' }))
    fireEvent.click(mobileNavigation.querySelector('a')!)

    expect(screen.getByRole('button', { name: 'Abrir menú' }).getAttribute('aria-expanded')).toBe(
      'false',
    )
    expect(mobileNavigation.hidden).toBe(true)
  })
})
