import { fireEvent, render, screen } from '@testing-library/react'
import { afterEach, beforeEach, vi } from 'vitest'
import { LandingPage } from './LandingPage'

class MockIntersectionObserver implements IntersectionObserver {
  readonly root = null
  readonly rootMargin = ''
  readonly scrollMargin = ''
  readonly thresholds: readonly number[] = []
  readonly observedTargets: Element[] = []
  readonly unobservedTargets: Element[] = []
  private readonly callback: IntersectionObserverCallback

  constructor(callback: IntersectionObserverCallback) {
    this.callback = callback
    intersectionObservers.push(this)
  }

  observe(target: Element) {
    this.observedTargets.push(target)
  }

  unobserve(target: Element) {
    this.unobservedTargets.push(target)
  }

  disconnect() {}

  takeRecords(): IntersectionObserverEntry[] {
    return []
  }

  trigger(target: Element) {
    const rect = target.getBoundingClientRect()
    this.callback(
      [
        {
          boundingClientRect: rect,
          intersectionRatio: 1,
          intersectionRect: rect,
          isIntersecting: true,
          rootBounds: null,
          target,
          time: performance.now(),
        },
      ],
      this,
    )
  }
}

const intersectionObservers: MockIntersectionObserver[] = []

beforeEach(() => {
  intersectionObservers.length = 0
  vi.stubGlobal('IntersectionObserver', MockIntersectionObserver)
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('LandingPage', () => {
  it('renderiza las secciones requeridas en el orden de la landing', () => {
    render(<LandingPage />)

    const heroHeading = screen.getByRole('heading', { name: /automatiza tu inventario/i })
    expect(heroHeading.querySelector('span')?.textContent).toMatch(
      /deja las planillas atrás|olvídate de los excel|dile adiós a las libretas/,
    )

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
          '#landing-page > header, #landing-page main > section, #landing-page main > aside, #landing-page > footer',
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

  it('revela cada sección una sola vez cuando entra en el viewport', () => {
    render(<LandingPage />)

    const section = document.getElementById('features')!
    const observer = intersectionObservers.find((candidate) =>
      candidate.observedTargets.includes(section),
    )

    expect(section.getAttribute('data-reveal')).toBe('true')
    expect(section.hasAttribute('data-revealed')).toBe(false)
    expect(observer).toBeDefined()

    observer!.trigger(section)

    expect(section.getAttribute('data-revealed')).toBe('true')
    expect(observer!.unobservedTargets).toContain(section)
  })

  it('mantiene visibles las secciones si IntersectionObserver no está disponible', () => {
    vi.stubGlobal('IntersectionObserver', undefined)
    render(<LandingPage />)

    expect(document.getElementById('landing-page')?.hasAttribute('data-reveal-enabled')).toBe(false)
    expect(document.getElementById('features')?.textContent).toContain(
      'Todo tu stock en orden y visible',
    )
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
      ...screen.getAllByRole('button', { name: 'Unirme a la lista de espera' }),
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
