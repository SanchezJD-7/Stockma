export interface Plan {
  name: 'Básico' | 'Profesional' | 'Negocio' | 'A tu medida'
  price: string
  bullets: string[]
  featured?: boolean
  custom?: boolean
}
