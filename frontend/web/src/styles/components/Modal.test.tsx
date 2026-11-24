import { fireEvent, render, screen } from '@testing-library/react'
import { useState } from 'react'
import { describe, expect, it, vi } from 'vitest'
import { Modal } from './Modal'

interface HarnessProps {
  closeButton?: boolean
}

function Harness({ closeButton = false }: HarnessProps) {
  const [open, setOpen] = useState(true)

  return (
    <Modal
      open={open}
      onClose={() => setOpen(false)}
      title='Título de prueba'
      closeButton={closeButton}
    >
      <button type='button'>Acción</button>
    </Modal>
  )
}

describe('Modal', () => {
  it('con open=false no renderiza nada', () => {
    render(
      <Modal open={false} onClose={vi.fn()} title='Título de prueba'>
        <p>Contenido</p>
      </Modal>,
    )

    expect(screen.queryByRole('dialog')).toBeNull()
  })

  it('expone el título como heading referenciado por el diálogo y le da el foco', () => {
    render(<Harness />)

    const dialog = screen.getByRole('dialog')
    const title = screen.getByRole('heading', { name: 'Título de prueba' })

    expect(dialog.getAttribute('aria-modal')).toBe('true')
    expect(dialog.getAttribute('aria-labelledby')).toBe(title.id)
  })

  it('sin la prop closeButton no ofrece botón de cerrar', () => {
    render(<Harness />)

    expect(screen.queryByRole('button', { name: 'Cerrar' })).toBeNull()
    expect(screen.getByRole('button', { name: 'Acción' })).toBeTruthy()
  })

  it('con la prop closeButton ofrece cerrar el diálogo', () => {
    render(<Harness closeButton />)

    fireEvent.click(screen.getByRole('button', { name: 'Cerrar' }))

    expect(screen.queryByRole('dialog')).toBeNull()
  })

  it('Escape cierra el diálogo', () => {
    render(<Harness closeButton />)

    fireEvent.keyDown(document, { key: 'Escape' })

    expect(screen.queryByRole('dialog')).toBeNull()
  })

  it('el clic en el backdrop cierra y el clic dentro no', () => {
    render(<Harness closeButton />)

    const dialog = screen.getByRole('dialog')
    const backdrop = dialog.parentElement
    if (!backdrop) throw new Error('el diálogo debe vivir sobre el backdrop')

    fireEvent.click(screen.getByRole('heading', { name: 'Título de prueba' }))
    expect(screen.getByRole('dialog')).toBeTruthy()

    fireEvent.click(backdrop)
    expect(screen.queryByRole('dialog')).toBeNull()
  })
})
