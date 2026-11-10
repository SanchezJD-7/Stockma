import { fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { HeroMockTable } from './HeroMockTable'

describe('HeroMockTable', () => {
  it('switches inventory tabs and shows expiration status chips', () => {
    render(<HeroMockTable />)

    expect(screen.getByRole('tab', { name: 'Productos' }).getAttribute('aria-selected')).toBe(
      'true',
    )
    expect(screen.getByRole('tabpanel').textContent).toContain('Yogur griego natural · 1 L')

    fireEvent.click(screen.getByRole('tab', { name: 'Lotes' }))

    expect(screen.getByRole('tab', { name: 'Lotes' }).getAttribute('aria-selected')).toBe('true')
    expect(screen.getByRole('tabpanel').textContent).toContain('Lote YG-2408 · vence 12 nov')

    fireEvent.click(screen.getByRole('tab', { name: 'Por vencer' }))

    expect(screen.getByRole('tab', { name: 'Por vencer' }).getAttribute('aria-selected')).toBe(
      'true',
    )
    expect(screen.getByRole('tabpanel').textContent).toContain('21 días')
    expect(screen.getByRole('tabpanel').textContent).toContain('9 días')
    expect(screen.getByRole('tabpanel').textContent).toContain('3 días')
    expect(document.querySelector('[data-status="warn"]')?.textContent).toBe('9 días')
    expect(document.querySelector('[data-status="danger"]')?.textContent).toBe('3 días')
  })
})
