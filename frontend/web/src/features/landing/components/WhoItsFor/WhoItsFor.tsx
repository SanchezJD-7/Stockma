import shared from '../../landing.module.css'
import { LandingSectionTitle } from '../../landing-texts'
import { MutedText } from '../../../../styles/components/texts'
import styles from './WhoItsFor.module.css'

export function WhoItsFor() {
  return (
    <section id='audience' className={`${shared.section} ${shared.spaced}`}>
      <div className={shared.wrap}>
        <LandingSectionTitle>Para quién es Stockma</LandingSectionTitle>
        <div className={`${shared.grid} ${styles.grid}`}>
          <article className={shared.card}>
            <h3 className={shared.cardTitle}>Droguerías</h3>
            <MutedText>Control de inventario y vencimientos para cada producto y lote.</MutedText>
          </article>
          <article className={shared.card}>
            <h3 className={shared.cardTitle}>Minimercados</h3>
            <MutedText>Más rapidez en surtido, stock y movimientos diarios.</MutedText>
          </article>
          <article className={shared.card}>
            <h3 className={shared.cardTitle}>Distribuidoras</h3>
            <MutedText>
              Visibilización del negocio, trazabilidad y mejor control operativo.
            </MutedText>
          </article>
        </div>
      </div>
    </section>
  )
}
