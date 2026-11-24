import { act, fireEvent, render, screen, within } from '@testing-library/react'
import { beforeEach, describe, expect, it } from 'vitest'
import { notify, useNotify } from '../../shared/notify'
import { Toaster } from './Toaster'

beforeEach(() => {
  useNotify.setState({ toasts: [] })
})

describe('Toaster', () => {
  it('sin avisos no pinta nada', () => {
    const { container } = render(<Toaster />)

    expect(container.innerHTML).toBe('')
  })

  it('pinta el aviso invocado con notify desde cualquier lado', () => {
    render(<Toaster />)

    act(() => notify.error('No hay conexión con el servidor'))

    expect(screen.getByRole('alert').textContent).toContain('No hay conexión con el servidor')
  })

  it('apila varios avisos y respeta el severity de cada uno', () => {
    render(<Toaster />)

    act(() => {
      notify.error('Fallo')
      notify.success('Listo')
    })

    const alerts = screen.getAllByRole('alert')
    expect(alerts).toHaveLength(2)
    expect(alerts[0].classList.contains('MuiAlert-colorError')).toBe(true)
    expect(alerts[1].classList.contains('MuiAlert-colorSuccess')).toBe(true)
  })

  it('el botón de cierre descarta el aviso', () => {
    render(<Toaster />)

    act(() => notify.error('Fallo'))

    const [alert] = screen.getAllByRole('alert')
    fireEvent.click(within(alert).getByRole('button'))

    expect(screen.queryByRole('alert')).toBeNull()
  })
})
