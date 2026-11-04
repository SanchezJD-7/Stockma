import shared from '../../landing.module.css'
import { LandingLead, LandingSectionTitle } from '../../landing-texts'
import { PrimaryButton } from '../../../../styles/components/buttons'
import styles from './Pricing.module.css'

export function Pricing() {
  return (
    <section id='pricing' className={`${shared.section} ${shared.spaced}`}>
      <div className={shared.wrap}>
        <LandingSectionTitle>Elige tu plan</LandingSectionTitle>
        <LandingLead>
          Los primeros 8 días son gratis. Después eliges el plan que te sirva.
        </LandingLead>
        <div className={`${shared.grid} ${styles.grid}`}>
          <article className={`${shared.card} ${styles.plan}`} aria-pressed='false'>
            <h3 className={shared.cardTitle}>Básico</h3>
            <p className={styles.price}>
              $ 49.000 <span className={styles.priceUnit}>/ mes</span>
            </p>
            <p>Para dejar el cuaderno sin complicarte.</p>
            <ul>
              <li>Productos, lotes y vencimientos</li>
              <li>Semáforo de vencimientos</li>
              <li>1 usuario</li>
            </ul>
            <PrimaryButton type='button'>Empezar prueba</PrimaryButton>
          </article>
          <article className={`${shared.card} ${styles.plan}`} aria-pressed='true'>
            <h3 className={shared.cardTitle}>Profesional</h3>
            <p className={styles.price}>
              $ 89.000 <span className={styles.priceUnit}>/ mes</span>
            </p>
            <p>Para un equipo que trabaja sobre el mismo inventario.</p>
            <ul>
              <li>Todo lo del Básico</li>
              <li>Hasta 5 usuarios</li>
              <li>Lector de códigos de barras</li>
              <li>Reportes para el contador (próximamente)</li>
            </ul>
            <PrimaryButton type='button'>Empezar prueba</PrimaryButton>
          </article>
          <article className={`${shared.card} ${styles.plan}`} aria-pressed='false'>
            <h3 className={shared.cardTitle}>Negocio</h3>
            <p className={styles.price}>
              $ 149.000 <span className={styles.priceUnit}>/ mes</span>
            </p>
            <p>Para negocios con más movimiento y más gente.</p>
            <ul>
              <li>Todo lo del Profesional</li>
              <li>Usuarios sin límite</li>
              <li>Acompañamiento en la carga inicial</li>
            </ul>
            <PrimaryButton type='button'>Hablar con ventas</PrimaryButton>
          </article>
          <article className={`${shared.card} ${styles.plan}`} aria-pressed='false'>
            <h3 className={shared.cardTitle}>A tu medida</h3>
            <p className={styles.price}>Bajo cotización</p>
            <p>Si tu operación pide algo que no está en los planes, lo armamos contigo.</p>
            <ul>
              <li>Las funciones que necesites</li>
              <li>Módulos hechos para tu negocio</li>
              <li>Acompañamiento dedicado</li>
            </ul>
            <PrimaryButton type='button'>Cotizar mi plan</PrimaryButton>
          </article>
        </div>
      </div>
    </section>
  )
}
