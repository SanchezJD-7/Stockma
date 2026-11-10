import { useState, type KeyboardEvent } from 'react'
import shared from '../../landing.module.css'
import { LandingLead, LandingSectionHead, LandingSectionTitle } from '../../landing-texts'
import { PrimaryButton, SecondaryButton } from '../../../../styles/components/buttons'
import styles from './Pricing.module.css'

interface PlanCard {
  id: string
  name: string
  price: string
  unit?: string
  description: string
  features: string[]
  cta: string
  custom?: boolean
  popular?: boolean
}

const plans: PlanCard[] = [
  {
    id: 'basico',
    name: 'Básico',
    price: '$ 49.000',
    unit: 'mes',
    description: 'Para dejar el cuaderno sin complicarte.',
    features: ['Productos, lotes y vencimientos', 'Semáforo de vencimientos', '1 usuario'],
    cta: 'Empezar prueba',
  },
  {
    id: 'profesional',
    name: 'Profesional',
    price: '$ 89.000',
    unit: 'mes',
    description: 'Para un equipo que trabaja sobre el mismo inventario.',
    features: [
      'Todo lo del Básico',
      'Hasta 5 usuarios',
      'Lector de códigos de barras',
      'Reportes para el contador (próximamente)',
    ],
    cta: 'Empezar prueba',
    popular: true,
  },
  {
    id: 'negocio',
    name: 'Negocio',
    price: '$ 149.000',
    unit: 'mes',
    description: 'Para negocios con más movimiento y más gente.',
    features: [
      'Todo lo del Profesional',
      'Usuarios sin límite',
      'Acompañamiento en la carga inicial',
    ],
    cta: 'Hablar con ventas',
  },
  {
    id: 'a-tu-medida',
    name: 'A tu medida',
    price: 'Bajo cotización',
    description: 'Si tu operación pide algo que no está en los planes, lo armamos contigo.',
    features: [
      'Las funciones que necesites',
      'Módulos hechos para tu negocio',
      'Acompañamiento dedicado',
    ],
    cta: 'Cotizar mi plan',
    custom: true,
  },
]

export function Pricing() {
  const [selectedPlanId, setSelectedPlanId] = useState<string | null>(null)

  function togglePlan(id: string) {
    setSelectedPlanId((current) => (current === id ? null : id))
  }

  function handlePlanKeyDown(event: KeyboardEvent<HTMLElement>, id: string) {
    if (event.key !== 'Enter' && event.key !== ' ') return
    event.preventDefault()
    togglePlan(id)
  }

  return (
    <section id='pricing' className={`${shared.section} ${shared.spaced}`}>
      <div className={shared.wrap}>
        <LandingSectionHead>
          <LandingSectionTitle>Elige tu plan</LandingSectionTitle>
          <LandingLead>
            Los primeros 8 días son gratis. Después eliges el plan que te sirva.
          </LandingLead>
        </LandingSectionHead>
        <div className={`${shared.grid} ${styles.grid}`}>
          {plans.map((plan) => (
            <article
              key={plan.id}
              className={`${shared.card} ${styles.plan} ${plan.custom ? styles.custom : ''} ${
                plan.popular ? styles.popular : ''
              } ${selectedPlanId === plan.id ? 'is-selected' : ''}`}
              aria-pressed={selectedPlanId === plan.id}
              tabIndex={0}
              onClick={(event) => {
                if ((event.target as Element).closest('button, a')) return
                togglePlan(plan.id)
              }}
              onKeyDown={(event) => handlePlanKeyDown(event, plan.id)}
            >
              {plan.popular && <span className={styles.badge}>Más popular</span>}
              <h3 className={shared.cardTitle}>{plan.name}</h3>
              <div className={styles.priceWrapper}>
                <p className={`${styles.price} ${plan.custom ? styles.priceSm : ''}`}>
                  {plan.price}
                </p>
                {plan.unit ? <span className={styles.priceUnit}>/ {plan.unit}</span> : null}
              </div>
              <p className={styles.desc}>{plan.description}</p>
              <ul>
                {plan.features.map((feature) => (
                  <li key={feature}>{feature}</li>
                ))}
              </ul>
              {plan.popular ? (
                <PrimaryButton type='button'>{plan.cta}</PrimaryButton>
              ) : (
                <SecondaryButton type='button'>{plan.cta}</SecondaryButton>
              )}
            </article>
          ))}
        </div>
      </div>
    </section>
  )
}
