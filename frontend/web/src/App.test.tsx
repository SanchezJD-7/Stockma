import { render, screen } from '@testing-library/react'
import App from './App'

describe('App routing', () => {
  beforeEach(() => {
    window.history.pushState({}, '', '/')
  })

  it('muestra landing en la ruta raíz', () => {
    render(<App />)

    expect(screen.getByRole('heading', { name: /automatiza tu inventario/i })).toBeTruthy()
  })

  it('muestra login en /login', () => {
    window.history.pushState({}, '', '/login')

    render(<App />)

    expect(screen.getByRole('heading', { name: 'Iniciar sesión' })).toBeTruthy()
  })

  it('muestra landing en rutas desconocidas', () => {
    window.history.pushState({}, '', '/foo')

    render(<App />)

    expect(screen.getByRole('heading', { name: /automatiza tu inventario/i })).toBeTruthy()
  })
})
