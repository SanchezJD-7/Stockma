import { PrimaryButton } from '../../../../styles/components/buttons'
import shared from '../../landing.module.css'
import styles from './DemoSection.module.css'
import { DemoBody, DemoTitle } from './DemoSection.styles'

export function DemoSection() {
  return (
    <section id='demo' className={`${shared.section} ${shared.spaced} ${styles.root}`}>
      <div className={shared.wrap}>
        <div className={styles.panel}>
          <span className={styles.badge}>Próximamente</span>
          <DemoTitle>Agenda tu demo</DemoTitle>
          <DemoBody>
            Estamos terminando de construir Stockma y todavía no podemos mostrarte el producto
            funcionando. Cuando esté listo vas a poder agendar 30 minutos por videollamada: vemos tu
            inventario real, cargamos un par de productos y revisamos el plan que mejor se ajusta a
            tu negocio.
          </DemoBody>
          <div className={styles.actions}>
            <PrimaryButton className={styles.cta} type='button' disabled>
              Agendar demo
            </PrimaryButton>
            <PrimaryButton className={styles.cta} type='button'>
              Mientras tanto, únete a la lista de espera
            </PrimaryButton>
          </div>
        </div>
      </div>
    </section>
  )
}
