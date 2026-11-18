import { fireEvent, render, screen } from '@testing-library/react'
import { useState } from 'react'
import { describe, expect, it, vi } from 'vitest'
import { WaitlistSuccessModal } from './WaitlistSuccessModal'

function CloseableSuccessModal() {
  const [open, setOpen] = useState(true)
  return <WaitlistSuccessModal open={open} onClose={() => setOpen(false)} />
}

describe('WaitlistSuccessModal', () => {
  it('muestra el mensaje de confirmación en un diálogo referenciado, sin campos ni formulario', () => {
    render(<WaitlistSuccessModal open onClose={vi.fn()} />)

    const dialog = screen.getByRole('dialog')
    expect(dialog.getAttribute('aria-modal')).toBe('true')

    const title = screen.getByRole('heading', { name: '¡Listo! Ya estás en la lista de espera' })
    expect(dialog.getAttribute('aria-labelledby')).toBe(title.id)
    expect(
      screen.getByText('Te avisaremos por correo muy pronto nuestra fecha de lanzamiento'),
    ).toBeTruthy()

    expect(screen.queryByRole('textbox')).toBeNull()
    expect(screen.queryByRole('form')).toBeNull()
  })

  it('se cierra con el botón', () => {
    render(<CloseableSuccessModal />)

    fireEvent.click(screen.getByRole('button', { name: 'Cerrar' }))

    expect(screen.queryByRole('dialog')).toBeNull()
  })

  it('se cierra con Escape', () => {
    render(<CloseableSuccessModal />)

    fireEvent.keyDown(document, { key: 'Escape' })

    expect(screen.queryByRole('dialog')).toBeNull()
  })

  it('se cierra con el clic fuera del diálogo', () => {
    render(<CloseableSuccessModal />)
    const backdrop = screen.getByRole('dialog').parentElement
    if (!backdrop) throw new Error('el diálogo debe vivir sobre el backdrop')

    fireEvent.click(backdrop)

    expect(screen.queryByRole('dialog')).toBeNull()
  })

  it('el clic dentro del diálogo no lo cierra', () => {
    render(<CloseableSuccessModal />)

    fireEvent.click(screen.getByRole('heading', { name: /ya estás en la lista de espera/i }))

    expect(screen.getByRole('dialog')).toBeTruthy()
  })
})
