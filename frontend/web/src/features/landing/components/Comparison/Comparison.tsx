import shared from '../../landing.module.css'
import { LandingLead, LandingSectionTitle } from '../../landing-texts'
import { MutedText } from '../../../../styles/components/texts'
import styles from './Comparison.module.css'

export function Comparison() {
  return (
    <section id='comparison' className={`${shared.section} ${shared.spaced}`}>
      <div className={shared.wrap}>
        <LandingSectionTitle>
          De la hoja y el lápiz a un software que hace ese trabajo por ti
        </LandingSectionTitle>
        <LandingLead>
          Cansado de los procesos manuales, rutinarios y agotadores que te quitan tiempo para
          realizar tus actividades extralaborales? Con Stockma puedes tener un software automatizado
          que te evita esos dolores de cabeza y hace todos los trabajos manuales por ti.
        </LandingLead>
        <div className={`${shared.grid} ${styles.grid}`}>
          <article className={`${shared.card} ${styles.card}`}>
            <h3 className={shared.cardTitle}>
              Sin Stockma: Procesos manuales y pérdidas invisibles
            </h3>
            <MutedText>
              Terminas la jornada pero tu trabajo no acaba: te toca reacomodar inventario, adivinar
              qué lotes están por expirar y asumir pérdidas de productos que se vencieron sin que te
              dieras cuenta.
            </MutedText>
            <ul className={`${styles.list} ${styles.against}`}>
              <li>Descontrol de registros</li>
              <li>Revisiones tediosas</li>
              <li>Pérdida de inversión</li>
              <li>Rotación deficiente</li>
              <li>Cierres de caja propensos al error</li>
            </ul>
          </article>
          <article className={`${shared.card} ${styles.card}`}>
            <h3 className={shared.cardTitle}>
              Con Stockma: Cero sorpresas, tienes el control total de tu negocio
            </h3>
            <MutedText>
              Automatiza tu operación diaria, elimina los errores manuales y protege cada peso de tu
              inversión.
            </MutedText>
            <ul className={styles.list}>
              <li>Inventario en tiempo real</li>
              <li>Semaforización inteligente</li>
              <li>Trazabilidad FEFO</li>
              <li>Captura rápida con IA</li>
              <li>Auditorías a 1 clic</li>
              <li>Optimización de tiempo</li>
            </ul>
          </article>
        </div>
      </div>
    </section>
  )
}
