import { fireEvent, render, renderHook, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { WaitlistModalProvider, useOpenWaitlist } from './WaitlistModalProvider'
import { json, mockApi, restoreHttp } from '../../../../shared/http.testkit'

afterEach(() => {
  restoreHttp()
})

function Cta() {
  const { open } = useOpenWaitlist()
  return <button onClick={open}>Abrir lista de espera</button>
}

function renderWithProvider() {
  return render(
    <WaitlistModalProvider>
      <Cta />
    </WaitlistModalProvider>,
  )
}

function openForm() {
  fireEvent.click(screen.getByRole('button', { name: 'Abrir lista de espera' }))
}

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

describe('WaitlistModalProvider', () => {
  it('expone useOpenWaitlist().open() y abre el formulario con sus tres campos', () => {
    renderWithProvider()
    expect(screen.queryByRole('dialog')).toBeNull()

    openForm()

    expect(screen.getByRole('dialog')).toBeTruthy()
    expect(screen.getByLabelText('Nombre completo')).toBeTruthy()
    expect(screen.getByLabelText('Teléfono')).toBeTruthy()
    expect(screen.getByLabelText('Correo electrónico')).toBeTruthy()
  })

  it('sólo con 201 pasa de form a success y deja una sola modal visible', async () => {
    mockApi(json(201, { id: '3b8d1f60-2c14-4a9e-8f77-0a5b3e2d9c41', email: 'juan@ejemplo.com' }))
    renderWithProvider()

    openForm()
    fillValidForm()
    submitForm()

    const successTitle = await screen.findByRole('heading', {
      name: '¡Listo! Ya estás en la lista de espera',
    })
    expect(screen.queryByLabelText('Nombre completo')).toBeNull()

    const dialogs = screen.getAllByRole('dialog')
    expect(dialogs).toHaveLength(1)
    expect(dialogs[0].contains(successTitle)).toBe(true)
  })

  it('un error 409 mantiene la fase form sin abrir la de confirmación', async () => {
    mockApi(json(409, { errorCode: 'WAITLIST_EMAIL_DUPLICATE' }))
    renderWithProvider()

    openForm()
    fillValidForm()
    submitForm()

    const alert = await screen.findByRole('alert')
    expect(alert.textContent).toContain('Ese correo ya está en la lista de espera')
    expect(screen.getAllByRole('dialog')).toHaveLength(1)
    expect(screen.getByLabelText('Nombre completo')).toBeTruthy()
    expect(screen.queryByRole('heading', { name: /ya estás en la lista de espera/i })).toBeNull()
  })

  it('close() cierra las modales y resetea el formulario para la próxima apertura', () => {
    renderWithProvider()

    openForm()
    fillValidForm()
    fireEvent.keyDown(document, { key: 'Escape' })
    expect(screen.queryByRole('dialog')).toBeNull()

    openForm()

    expect((screen.getByLabelText('Nombre completo') as HTMLInputElement).value).toBe('')
    expect((screen.getByLabelText('Teléfono') as HTMLInputElement).value).toBe('')
    expect((screen.getByLabelText('Correo electrónico') as HTMLInputElement).value).toBe('')
  })

  it('cerrar la confirmación no reabre el formulario', async () => {
    mockApi(json(201, { id: '3b8d1f60-2c14-4a9e-8f77-0a5b3e2d9c41', email: 'juan@ejemplo.com' }))
    renderWithProvider()

    openForm()
    fillValidForm()
    submitForm()
    await screen.findByRole('heading', { name: '¡Listo! Ya estás en la lista de espera' })

    fireEvent.click(screen.getByRole('button', { name: 'Cerrar' }))

    expect(screen.queryByRole('dialog')).toBeNull()
    expect(screen.queryByRole('heading', { name: 'Únete a la lista de espera' })).toBeNull()
  })

  it('el hook lanza un error claro fuera del provider', () => {
    expect(() => renderHook(() => useOpenWaitlist())).toThrow(/WaitlistModalProvider/)
  })
})
