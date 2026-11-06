import { fireEvent, render, screen } from '@testing-library/react'
import { Pricing } from './Pricing'

describe('Pricing plan selection', () => {
  it('marca is-selected y aria-pressed al hacer clic, y desmarca la tarjeta anterior', () => {
    render(<Pricing />)

    const plans = screen.getAllByRole('article')
    expect(plans).toHaveLength(4)

    fireEvent.click(plans[0])
    expect(plans[0].getAttribute('aria-pressed')).toBe('true')
    expect(plans[0].classList.contains('is-selected')).toBe(true)

    fireEvent.click(plans[1])
    expect(plans[0].getAttribute('aria-pressed')).toBe('false')
    expect(plans[0].classList.contains('is-selected')).toBe(false)
    expect(plans[1].getAttribute('aria-pressed')).toBe('true')
    expect(plans[1].classList.contains('is-selected')).toBe(true)
  })

  it('permite seleccionar con Enter y Espacio', () => {
    render(<Pricing />)

    const plans = screen.getAllByRole('article')

    fireEvent.keyDown(plans[2], { key: 'Enter' })
    expect(plans[2].getAttribute('aria-pressed')).toBe('true')
    expect(plans[2].classList.contains('is-selected')).toBe(true)

    fireEvent.keyDown(plans[3], { key: ' ' })
    expect(plans[2].getAttribute('aria-pressed')).toBe('false')
    expect(plans[3].getAttribute('aria-pressed')).toBe('true')
    expect(plans[3].classList.contains('is-selected')).toBe(true)
  })
})
