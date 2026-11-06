import { PrimaryButton } from '../../../../styles/components/buttons'
import shared from '../../landing.module.css'
import styles from './WaitlistSection.module.css'
import { WaitlistBody, WaitlistTitle } from './WaitlistSection.styles'

export function WaitlistSection() {
  return (
    <section id='waitlist' className={styles.root}>
      <div className={`${shared.wrap} ${styles.inner}`}>
        <div>
          <WaitlistTitle>Únete a la lista de espera</WaitlistTitle>
          <WaitlistBody>
            Rellena el formulario y recibe en primicia información de nuestro lanzamiento
          </WaitlistBody>
        </div>
        <PrimaryButton className={styles.cta} type='button'>
          Unirme a la lista de espera
        </PrimaryButton>
      </div>
    </section>
  )
}
