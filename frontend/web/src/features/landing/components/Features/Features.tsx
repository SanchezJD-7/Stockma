import shared from '../../landing.module.css'
import { LandingLead, LandingSectionHead, LandingSectionTitle } from '../../landing-texts'
import { MutedText } from '../../../../styles/components/texts'
import styles from './Features.module.css'

export function Features() {
  return (
    <section id='features' className={`${shared.section} ${shared.spaced} ${shared.deep}`}>
      <div className={shared.wrap}>
        <LandingSectionHead>
          <LandingSectionTitle>Funcionalidades principales</LandingSectionTitle>
          <LandingLead>
            Tu stock centralizado, con menos trabajo manual y mayor control sobre cada uno de tus
            productos.
          </LandingLead>
        </LandingSectionHead>
        <div className={`${shared.grid} ${styles.grid}`}>
          <article className={`${shared.card} ${styles.card}`}>
            <h3 className={shared.cardTitle}>Todo tu stock en un solo lugar</h3>
            <MutedText>
              Revisa existencias, ubicaciones y movimientos sin perder tiempo buscando en 10 sitios.
            </MutedText>
          </article>
          <article className={`${shared.card} ${styles.card}`}>
            <h3 className={shared.cardTitle}>Los vencimientos se marcan solos</h3>
            <MutedText>
              Te avisamos cuándo un lote está por expirar para que lo saques antes de perderlo.
            </MutedText>
          </article>
          <article className={`${shared.card} ${styles.card}`}>
            <h3 className={shared.cardTitle}>El producto aparece al pasar el lector</h3>
            <MutedText>
              La captura es rápida, clara y lista para registrar la operación sin errores.
            </MutedText>
          </article>
          <article className={`${shared.card} ${styles.card}`}>
            <h3 className={shared.cardTitle}>Todo tu equipo trabaja al mismo tiempo</h3>
            <MutedText>
              Tu equipo puede coordinar ventas, reabastecimiento y ajustes desde un solo lugar.
            </MutedText>
          </article>
          <article className={`${shared.card} ${styles.card}`}>
            <h3 className={shared.cardTitle}>Planes según lo que necesites</h3>
            <span className={styles.badge}>8 días de prueba gratuita</span>
            <MutedText>
              Empieza con el plan que mejor se adapte a tus necesidades y cámbialo cuando tu negocio
              crezca.
            </MutedText>
          </article>
          <article className={`${shared.card} ${styles.card}`}>
            <h3 className={shared.cardTitle}>Reportes de inventario a tu medida</h3>
            <span className={styles.badge}>Próximamente</span>
            <MutedText>
              Preparados para que el negocio entienda qué vender, cuánto queda y qué conviene mover.
            </MutedText>
          </article>
        </div>
      </div>
    </section>
  )
}
