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
    const icon = dialog.querySelector('svg[aria-hidden="true"]')
    const body = screen.getByText(
      'Te avisaremos por correo muy pronto nuestra fecha de lanzamiento',
    )
    expect(title.nextElementSibling).toBe(icon)
    expect(icon?.nextElementSibling).toBe(body)

    expect(screen.queryByRole('textbox')).toBeNull()
    expect(screen.queryByRole('form')).toBeNull()
  })

  it('se cierra con el botón', () => {
    render(<CloseableSuccessModal />)

    fireEvent.click(screen.getByRole('button', { name: 'Cerrar' }))

    expect(screen.queryByRole('dialog')).toBeNull()
  })

  it('vuelve a montar el icono al reabrir el modal para reiniciar la animación', () => {
    const { rerender } = render(<WaitlistSuccessModal open onClose={vi.fn()} />)
    const firstIcon = screen.getByRole('dialog').querySelector('svg[aria-hidden="true"]')

    rerender(<WaitlistSuccessModal open={false} onClose={vi.fn()} />)
    expect(screen.queryByRole('dialog')).toBeNull()

    rerender(<WaitlistSuccessModal open onClose={vi.fn()} />)
    const reopenedIcon = screen.getByRole('dialog').querySelector('svg[aria-hidden="true"]')

    expect(reopenedIcon).not.toBe(firstIcon)
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
