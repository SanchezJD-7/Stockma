import shared from '../../landing.module.css'
import { PrimaryButton } from '../../../../styles/components/buttons.styles'
import styles from './DemoSection.module.css'
import { useOpenWaitlist } from '../WaitlistModalProvider/WaitlistModalProvider'

export function DemoSection() {
  const { open } = useOpenWaitlist()

  return (
    <section id='demo' className={`${shared.section} ${shared.spaced} ${styles.root}`}>
      <div className={shared.wrap}>
        <div className={styles.panel}>
          <h2 className={styles.title}>Sé de los primeros en probar Stockma</h2>
          <p className={styles.body}>
            Estamos afinando los últimos detalles para revolucionar la gestión de tu inventario.
            Únete a la lista de espera hoy y obtén acceso prioritario y beneficios exclusivos en tu
            lanzamiento.
          </p>
          <div className={styles.actions}>
            <PrimaryButton className={styles.cta} type='button' onClick={open}>
              Unirme a la lista de espera
            </PrimaryButton>
          </div>
        </div>
      </div>
    </section>
  )
}
