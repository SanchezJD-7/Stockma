import { fireEvent, render, screen } from '@testing-library/react'
import { vi } from 'vitest'
import { LandingPage } from './LandingPage'

describe('LandingPage', () => {
  it('renderiza las secciones requeridas en el orden de la landing', () => {
    render(<LandingPage />)

    const heroHeading = screen.getByRole('heading', { name: /automatiza tu inventario/i })
    const emphasizedText = heroHeading.querySelector('em')
    expect(emphasizedText?.textContent).toBe('deja las planillas atrás')
    expect(getComputedStyle(emphasizedText as HTMLElement).fontStyle).toBe('italic')

    const ids = [
      'landing-header',
      'hero',
      'audience',
      'how-it-works',
      'features',
      'comparison',
      'pricing',
      'faq',
      'demo',
      'waitlist',
      'landing-footer',
    ]

    ids.forEach((id) => {
      expect(document.getElementById(id)).toBeTruthy()
    })

    const main = document.querySelector('main')
    expect(main).toBeTruthy()
    expect((main?.lastElementChild as HTMLElement | null)?.id).toBe('waitlist')
    expect(
      Array.from(
        document.querySelectorAll(
          '#landing-page > header, #landing-page main > section, #landing-page > footer',
        ),
      ).map((element) => element.id),
    ).toEqual(ids)
    expect(screen.getAllByRole('img', { name: 'Stockma' })).toHaveLength(2)
  })

  it('desplaza a secciones internas sin agregar el hash a la URL', () => {
    window.history.replaceState({}, '', '/')
    render(<LandingPage />)

    const section = document.getElementById('how-it-works') as HTMLElement
    const scrollIntoView = vi.fn()
    Object.defineProperty(section, 'scrollIntoView', { configurable: true, value: scrollIntoView })

    fireEvent.click(screen.getByRole('link', { name: 'Cómo funciona' }))

    expect(scrollIntoView).toHaveBeenCalledWith({ behavior: 'smooth', block: 'start' })
    expect(window.location.pathname).toBe('/')
    expect(window.location.hash).toBe('')
  })

  it('apunta todos los enlaces internos a una sección existente', () => {
    render(<LandingPage />)

    const internalLinks = Array.from(document.querySelectorAll<HTMLAnchorElement>('a[href^="#"]'))

    expect(internalLinks.length).toBeGreaterThan(0)

    internalLinks.forEach((link) => {
      const targetId = link.getAttribute('href')!.slice(1)
      expect(
        document.getElementById(targetId),
        `el ancla "${link.textContent}" apunta a #${targetId}, que no existe`,
      ).toBeTruthy()
    })
  })

  it('mantiene los CTA de lista de espera habilitados y sin acción en esta versión', () => {
    window.history.replaceState({}, '', '/')
    render(<LandingPage />)

    const callsToAction = [
      screen.getByRole('button', { name: 'Únete a la lista de espera' }),
      ...screen.getAllByRole('button', { name: 'Empezar prueba' }),
      screen.getByRole('button', { name: 'Hablar con ventas' }),
      screen.getByRole('button', { name: 'Cotizar mi plan' }),
      screen.getByRole('button', { name: 'Mientras tanto, únete a la lista de espera' }),
      screen.getByRole('button', { name: 'Unirme a la lista de espera' }),
    ]

    callsToAction.forEach((button) => {
      expect(button.hasAttribute('disabled')).toBe(false)
      fireEvent.click(button)
    })

    expect(screen.queryByRole('dialog')).toBeNull()
    expect(window.location.pathname).toBe('/')
    expect(window.location.hash).toBe('')
  })
})
