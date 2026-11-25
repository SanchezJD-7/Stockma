import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { useState } from 'react'
import { AxiosError } from 'axios'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { WaitlistModal } from './WaitlistModal'
import { api } from '../../../../shared/http'
import { json, mockApi, mockPending, restoreHttp } from '../../../../shared/http.testkit'

afterEach(() => {
  restoreHttp()
})

function fillValidForm() {
  fireEvent.change(screen.getByLabelText('Nombre completo'), {
    target: { value: 'Juan Pérez' },
  })
  fireEvent.change(screen.getByLabelText('Teléfono'), { target: { value: '3001234567' } })
  fireEvent.change(screen.getByLabelText('Correo electrónico'), {
    target: { value: 'juan@ejemplo.com' },
  })
}

function submitForm() {
  fireEvent.click(screen.getByRole('button', { name: 'Unirme a la lista de espera' }))
}

function CloseableModal() {
  const [open, setOpen] = useState(true)
  return <WaitlistModal open={open} onClose={() => setOpen(false)} onSuccess={vi.fn()} />
}

describe('WaitlistModal', () => {
  it('renderiza el diálogo con los tres campos, el título referenciado y el prefijo +57 visible', () => {
    render(<WaitlistModal open onClose={vi.fn()} onSuccess={vi.fn()} />)

    const dialog = screen.getByRole('dialog')
    expect(dialog.getAttribute('aria-modal')).toBe('true')

    const title = screen.getByRole('heading', { name: 'Únete a la lista de espera' })
    expect(dialog.getAttribute('aria-labelledby')).toBe(title.id)

    expect(screen.getByLabelText('Nombre completo')).toBeTruthy()
    expect(screen.getByLabelText('Teléfono')).toBeTruthy()
    expect(screen.getByLabelText('Correo electrónico')).toBeTruthy()
    expect(screen.getByLabelText('Indicativo Colombia +57')).toBeTruthy()
  })

  it('mientras envía muestra spinner, aria-busy, botón y campos deshabilitados, sin doble submit', async () => {
    const requests = mockPending()

    render(<WaitlistModal open onClose={vi.fn()} onSuccess={vi.fn()} />)
    fillValidForm()
    submitForm()

    const button = await screen.findByRole('button', { name: /registrando/i })
    expect(button.getAttribute('aria-busy')).toBe('true')
    expect((button as HTMLButtonElement).disabled).toBe(true)
    expect(screen.getByRole('progressbar')).toBeTruthy()

    expect((screen.getByLabelText('Nombre completo') as HTMLInputElement).disabled).toBe(true)
    expect((screen.getByLabelText('Teléfono') as HTMLInputElement).disabled).toBe(true)
    expect((screen.getByLabelText('Correo electrónico') as HTMLInputElement).disabled).toBe(true)

    const form = button.closest('form')
    if (!form) throw new Error('el botón debe vivir dentro del formulario')
    fireEvent.submit(form)

    expect(requests).toHaveLength(1)
  })

  it('con 201 dispara onSuccess y envía exactamente lo que escribió el usuario', async () => {
    const requests = mockApi(
      json(201, { id: '3b8d1f60-2c14-4a9e-8f77-0a5b3e2d9c41', email: 'juan@ejemplo.com' }),
    )
    const onSuccess = vi.fn()

    render(<WaitlistModal open onClose={vi.fn()} onSuccess={onSuccess} />)
    fillValidForm()
    submitForm()

    await waitFor(() => expect(onSuccess).toHaveBeenCalledTimes(1))
    expect(JSON.parse(String(requests[0].data))).toEqual({
      fullName: 'Juan Pérez',
      countryCode: '+57',
      phone: '3001234567',
      email: 'juan@ejemplo.com',
    })
  })

  it('con 400 marca el campo del mapa de errores y muestra el resumen en un alert', async () => {
    mockApi(
      json(400, {
        errorCode: 'VALIDATION_FAILED',
        errors: { email: ['El correo no tiene un formato válido'] },
      }),
    )

    render(<WaitlistModal open onClose={vi.fn()} onSuccess={vi.fn()} />)
    fillValidForm()
    submitForm()

    const alert = await screen.findByRole('alert')
    expect(alert.textContent).toContain('Revisa los campos marcados')
    expect(screen.getByText('El correo no tiene un formato válido')).toBeTruthy()
    expect(screen.getByRole('dialog')).toBeTruthy()
  })

  it('con 409 muestra el motivo puntual, mantiene la modal abierta y conserva los datos', async () => {
    mockApi(json(409, { errorCode: 'WAITLIST_EMAIL_DUPLICATE' }))

    render(<WaitlistModal open onClose={vi.fn()} onSuccess={vi.fn()} />)
    fillValidForm()
    submitForm()

    const alert = await screen.findByRole('alert')
    expect(alert.textContent).toContain('Ese correo ya está en la lista de espera')
    expect(screen.getByRole('dialog')).toBeTruthy()
    expect((screen.getByLabelText('Nombre completo') as HTMLInputElement).value).toBe('Juan Pérez')
    expect((screen.getByLabelText('Teléfono') as HTMLInputElement).value).toBe('3001234567')
    expect((screen.getByLabelText('Correo electrónico') as HTMLInputElement).value).toBe(
      'juan@ejemplo.com',
    )
  })

  it('con 500 muestra el mensaje genérico, mantiene la modal abierta y conserva los datos', async () => {
    mockApi(json(500, { errorCode: 'INTERNAL_ERROR' }))

    render(<WaitlistModal open onClose={vi.fn()} onSuccess={vi.fn()} />)
    fillValidForm()
    submitForm()

    const alert = await screen.findByRole('alert')
    expect(alert.textContent).toContain('No pudimos completar el registro; intenta de nuevo')
    expect(screen.getByRole('dialog')).toBeTruthy()
    expect((screen.getByLabelText('Nombre completo') as HTMLInputElement).value).toBe('Juan Pérez')
    expect((screen.getByLabelText('Correo electrónico') as HTMLInputElement).value).toBe(
      'juan@ejemplo.com',
    )
  })

  it('sin respuesta del servidor muestra el mensaje de conexión y no cierra', async () => {
    api.defaults.adapter = () => Promise.reject(new AxiosError('Network Error'))

    render(<WaitlistModal open onClose={vi.fn()} onSuccess={vi.fn()} />)
    fillValidForm()
    submitForm()

    const alert = await screen.findByRole('alert')
    expect(alert.textContent).toContain('No hay conexión con el servidor; verifica tu internet')
    expect(screen.getByRole('dialog')).toBeTruthy()
  })

  it('una falla sin errorCode se trata como problema de conexión', async () => {
    mockApi(json(502, null))

    render(<WaitlistModal open onClose={vi.fn()} onSuccess={vi.fn()} />)
    fillValidForm()
    submitForm()

    const alert = await screen.findByRole('alert')
    expect(alert.textContent).toContain('No hay conexión con el servidor; verifica tu internet')
    expect(screen.getByRole('dialog')).toBeTruthy()
  })

  it('Escape cierra la modal', () => {
    render(<CloseableModal />)
    expect(screen.getByRole('dialog')).toBeTruthy()

    fireEvent.keyDown(document, { key: 'Escape' })

    expect(screen.queryByRole('dialog')).toBeNull()
  })

  it('el clic fuera del diálogo cierra la modal', () => {
    render(<CloseableModal />)
    const backdrop = screen.getByRole('dialog').parentElement
    if (!backdrop) throw new Error('el diálogo debe vivir sobre el backdrop')

    fireEvent.click(backdrop)

    expect(screen.queryByRole('dialog')).toBeNull()
  })

  it('el clic dentro del diálogo no lo cierra', () => {
    render(<CloseableModal />)

    fireEvent.click(screen.getByRole('heading', { name: 'Únete a la lista de espera' }))

    expect(screen.getByRole('dialog')).toBeTruthy()
  })
})
